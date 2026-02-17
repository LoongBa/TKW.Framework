using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

public class DomainUser<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    // 支撑 AOP 作用域联动的异步上下文
    internal static readonly AsyncLocal<ILifetimeScope?> _ActiveScope = new();

    [JsonConstructor]
    public DomainUser() { }

    public string SessionKey { get; set; } = string.Empty;
    public bool IsAuthenticated { get; internal set; }

    /// <summary>
    /// 内聚性优化：通过属性接收所属 Host，避免全局静态搜索
    /// </summary>
    [JsonIgnore]
    internal DomainHost<TUserInfo>? AttachedHost { get; set; }

    [JsonIgnore]
    private DomainHost<TUserInfo> Host => AttachedHost
        ?? DomainHost<TUserInfo>.Root
        ?? throw new InvalidOperationException("DomainUser 尚未关联到有效的 DomainHost 实例。");

    /// <summary>
    /// 解析作用域优先级逻辑：
    /// 1. 优先使用 AOP 拦截器创建的子作用域（保证事务与对象一致性）
    /// 2. 其次使用 Host 全局容器（作为保底）
    /// </summary>
    [JsonIgnore]
    private ILifetimeScope CurrentScope => _ActiveScope.Value ?? Host.Container
        ?? throw new InvalidOperationException("无法获取解析作用域，请确保容器已初始化。");

    [JsonInclude]
    public TUserInfo UserInfo { get; set; } = null!;

    #region 服务解析

    public TDomainService Use<TDomainService>() where TDomainService : DomainServiceBase<TUserInfo>
    {
        // 解析时自动将当前 User 注入到 Service 的构造函数中
        return CurrentScope.Resolve<TDomainService>(TypedParameter.From(this));
    }

    public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
    {
        return CurrentScope.Resolve<TAopContract>(TypedParameter.From(this));
    }

    #endregion

    #region 角色判断、登录、Claims 转换
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
        // 关键点：将领域角色映射到标准 ClaimTypes.Role
        claims.AddRange(UserInfo.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(role => new Claim(ClaimTypes.Role, role)));
        // 构造 Identity 时必须指定 RoleClaimType，这样 Principal.IsInRole() 才能正确工作
        var identity = new ClaimsIdentity(claims, "TKW.Domain", ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    public async Task LoginAsUserAsync(string userName, string passwordHashed, EnumLoginFrom loginFrom)
    {
        var userInfo = await Host.UserHelper.UserLoginAsync(this, userName, passwordHashed, loginFrom);
        UserInfo = userInfo;
        IsAuthenticated = true;
        await UpdateAndActiveSessionAsync();
    }

    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync()
    {
        var session = await Host.SessionManager!.GetSessionAsync(SessionKey);
        var newSession = session.BindUser(this);
        await Host.SessionManager.UpdateAndActiveSessionAsync(newSession.Key, _ => newSession);
        return newSession;
    }

    #endregion
}