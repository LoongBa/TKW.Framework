using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading; // 必须引用，用于 AsyncLocal
using System.Threading.Tasks;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域用户基础类（泛型版本，支持具体 UserInfo 类型）
/// </summary>
public class DomainUser<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    // 用于在 AOP 拦截期间存储当前作用域
    internal static readonly AsyncLocal<ILifetimeScope?> _ActiveScope = new();

    [JsonConstructor]
    internal DomainUser() { }

    protected internal DomainUser(Func<DomainHost<TUserInfo>> domainHostFactory)
    {
        DomainHostFactory = domainHostFactory.EnsureNotNull(nameof(domainHostFactory));
    }

    public string SessionKey { get; set; } = string.Empty;
    public bool IsAuthenticated { get; internal set; }

    [JsonIgnore]
    private IContainer Container => Host.Container.EnsureNotNull("Host.Container")!;

    [JsonIgnore]
    private DomainHost<TUserInfo> Host => DomainHostFactory().EnsureNotNull("DomainHostFactory()");

    [JsonIgnore]
    private ISessionManager<TUserInfo> SessionManager => Host.SessionManager.EnsureNotNull("Host.SessionManager")!;

    /// <summary>
    /// 当前解析所使用的作用域：优先使用 AOP 活跃作用域，否则使用全局容器
    /// </summary>
    [JsonIgnore]
    private ILifetimeScope CurrentScope => _ActiveScope.Value ?? Container;

    [JsonInclude]
    public TUserInfo UserInfo { get; protected internal set; } = null!;

    [JsonIgnore]
    public Func<DomainHost<TUserInfo>> DomainHostFactory { get; } = null!;

    #region 领域服务调用（解析时自动注入当前 User）

    /// <summary>
    /// 使用领域服务（从当前作用域解析）
    /// </summary>
    public TDomainService Use<TDomainService>() where TDomainService : DomainServiceBase<TUserInfo>
    {
        // 从当前作用域（可能是 AOP 子作用域）解析，并注入当前 User 实例
        return CurrentScope.Resolve<TDomainService>(TypedParameter.From(this));
    }

    /// <summary>
    /// 使用 AOP 领域服务（从当前作用域解析）
    /// </summary>
    public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
    {
        return CurrentScope.Resolve<TAopContract>(TypedParameter.From(this));
    }

    #endregion

    #region 角色判断、登录、Claims 转换 (保持不变)
    public bool IsInRole<T>(T role) where T : struct, Enum => UserInfo.IsInRole(role);
    public bool IsInRole(string role) => UserInfo.IsInRole(role);

    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserInfo.UserIdString),
            new(ClaimTypes.Name, UserInfo.UserName),
            new(ClaimTypes.GivenName, UserInfo.DisplayName),
            new("SessionKey", SessionKey),
            new("LoginFrom", UserInfo.LoginFrom.ToString()),
            new("IsAuthenticated", IsAuthenticated.ToString().ToLowerInvariant()),
        };
        claims.AddRange(from role in UserInfo.Roles where !string.IsNullOrWhiteSpace(role) select new Claim(ClaimTypes.Role, role));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TKW.Domain", ClaimTypes.Name, ClaimTypes.Role));
    }

    public async Task LoginAsUserAsync(string userName, string passwordHashed, EnumLoginFrom loginFrom)
    {
        var userInfo = await DomainHostFactory().UserHelper.UserLoginAsync(this, userName, passwordHashed, loginFrom);
        UserInfo = userInfo;
        IsAuthenticated = true;
        await UpdateAndActiveSessionAsync();
    }

    public async Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync() => await DomainHostFactory().SessionManager!.GetAndActiveSessionAsync(SessionKey);
    internal async Task GuestOrUserLogoutAsync() => await DomainHostFactory().SessionManager!.AbandonSessionAsync(SessionKey);

    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync()
    {
        var session = await SessionManager.GetSessionAsync(SessionKey);
        var newSession = session.BindUser(this);
        await SessionManager.UpdateAndActiveSessionAsync(newSession.Key, _ => newSession);
        return newSession;
    }
    #endregion
}