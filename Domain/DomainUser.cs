using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域用户基础类（泛型版本，支持具体 UserInfo 类型）
/// </summary>
/// <typeparam name="TUserInfo">具体的 UserInfo 类型，必须实现 IUserInfo 接口</typeparam>
public class DomainUser<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    [JsonConstructor]
    internal DomainUser() { }

    protected internal DomainUser(Func<DomainHost<TUserInfo>> domainHostFactory)
    {
        DomainHostFactory = domainHostFactory.EnsureNotNull(nameof(domainHostFactory));
    }

    /// <summary>
    /// 会话键
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// 是否已认证
    /// </summary>
    public bool IsAuthenticated { get; internal set; }

    [JsonIgnore]
    private IContainer Container => Host.Container.EnsureNotNull("Host.Container")!;
    [JsonIgnore]
    private DomainHost<TUserInfo> Host => DomainHostFactory().EnsureNotNull("DomainHostFactory()");
    [JsonIgnore]
    private ISessionManager<TUserInfo> SessionManager => Host.SessionManager.EnsureNotNull("Host.SessionManager")!;

    /// <summary>
    /// 用户信息（具体类型，序列化安全）
    /// </summary>
    [JsonInclude]
    public TUserInfo UserInfo { get; protected internal set; } = null!;

    /// <summary>
    /// DomainHost 工厂（运行时依赖，不参与序列化）
    /// </summary>
    [JsonIgnore]
    public Func<DomainHost<TUserInfo>> DomainHostFactory { get; } = null!;

    #region 角色判断

    /// <summary>
    /// 检查用户是否拥有指定角色（泛型枚举版本）
    /// </summary>
    public bool IsInRole<T>(T role) where T : struct, Enum
        => UserInfo.IsInRole(role);

    /// <summary>
    /// 检查用户是否拥有指定角色（字符串版本）
    /// </summary>
    public bool IsInRole(string role)
        => UserInfo.IsInRole(role);

    #endregion

    #region 领域服务调用（AOP 友好）

    /// <summary>
    /// 使用领域服务（运行时解析）
    /// </summary>
    public TDomainService Use<TDomainService>() where TDomainService : DomainServiceBase<TUserInfo>
    {
        return Container.Resolve<TDomainService>(TypedParameter.From(this));
    }

    /// <summary>
    /// 使用 AOP 领域服务
    /// </summary>
    public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
    {
        return Container.Resolve<TAopContract>(TypedParameter.From(this));
    }

    #endregion

    #region 登录与会话相关

    /// <summary>
    /// 将 DomainUser 转换为 ClaimsPrincipal（用于 HttpContext.User、Blazor AuthenticationState 等）
    /// </summary>
    /// <remarks>
    /// 1. 遵循 ASP.NET Core 标准 ClaimTypes
    /// 2. 支持多角色（Roles）
    /// 3. 包含常用自定义 Claim
    /// 4. AuthenticationType 使用 "TKW.Domain" 便于区分
    /// </remarks>
    public ClaimsPrincipal ToClaimsPrincipal()
    {
        var claims = new List<Claim>
        {
            // 标准 Claim
            new(ClaimTypes.NameIdentifier, UserInfo.UserIdString),   // 用户ID
            new(ClaimTypes.Name, UserInfo.UserName),                   // 用户名
            new(ClaimTypes.GivenName, UserInfo.DisplayName ?? throw new InvalidOperationException()),     // 显示名称

            // 自定义 Claim（强烈推荐保留）
            new("SessionKey", SessionKey),                           // 会话密钥
            new("LoginFrom", UserInfo.LoginFrom.ToString()),                         // 登录来源
            new("IsAuthenticated", IsAuthenticated.ToString().ToLowerInvariant()),   // 是否已认证
        };
        claims.AddRange(
            from role in UserInfo.Roles where !string.IsNullOrWhiteSpace(role) 
            select new Claim(ClaimTypes.Role, role)
        );

        // 添加所有角色（支持多角色）

        // 创建 Identity 并包装成 ClaimsPrincipal
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "TKW.Domain",      // 自定义认证类型，便于区分
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Guest 登录为指定用户
    /// </summary>
    public async Task LoginAsUserAsync(
        string userName,
        string passwordHashed,
        EnumLoginFrom loginFrom)
    {
        var userInfo = await DomainHostFactory().UserHelper
            .UserLoginAsync(this, userName, passwordHashed, loginFrom)
            .ConfigureAwait(false);

        UserInfo = userInfo;
        IsAuthenticated = true;

        await UpdateAndActiveSessionAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 异步获取并激活指定会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync()
    {
        return await DomainHostFactory().SessionManager!
            .GetAndActiveSessionAsync(SessionKey)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 游客或用户注销（注销会话）
    /// </summary>
    internal async Task GuestOrUserLogoutAsync()
    {
        await DomainHostFactory().SessionManager!
            .AbandonSessionAsync(SessionKey)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 异步更新并激活指定会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync()
    {
        var session = await SessionManager
            .GetSessionAsync(SessionKey)
            .ConfigureAwait(false);

        var newSession = session.BindUser(this);

        await SessionManager.UpdateAndActiveSessionAsync(
            newSession.Key,
            _ => newSession)
            .ConfigureAwait(false);

        return newSession;
    }

    #endregion
}