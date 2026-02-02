#nullable enable
using Autofac;
using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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

    #region 领域服务调用（AOP 友好）

    /// <summary>
    /// 使用领域服务（运行时解析）
    /// </summary>
    public TDomainService Use<TDomainService>() where TDomainService : DomainServiceBase<TUserInfo>
    {
        return DomainHostFactory().EnsureNotNull(nameof(DomainHostFactory))
            .Container.Resolve<TDomainService>(TypedParameter.From(this));
    }

    /// <summary>
    /// 使用 AOP 领域服务
    /// </summary>
    public TAopContract UseAop<TAopContract>() where TAopContract : IAopContract, IDomainService
    {
        return DomainHostFactory().EnsureNotNull(nameof(DomainHostFactory))
            .Container.Resolve<TAopContract>(TypedParameter.From(this));
    }

    #endregion

    #region 登录与会话相关

    /// <summary>
    /// Guest 登录为指定用户
    /// </summary>
    public async Task<DomainUser<TUserInfo>> LoginAsUserAsync(
        string userName,
        string passwordHashed,
        LoginFromEnum loginFrom)
    {
        var userInfo = await DomainHostFactory().UserHelper
            .UserLoginAsync(this, userName, passwordHashed, loginFrom)
            .ConfigureAwait(false);

        UserInfo = (TUserInfo)userInfo;  // 类型转换，确保兼容
        IsAuthenticated = true;

        await UpdateAndActiveSessionAsync().ConfigureAwait(false);

        return this;
    }

    /// <summary>
    /// 异步获取并激活指定会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> GetAndActiveSessionAsync()
    {
        return await DomainHostFactory().SessionManager
            .GetAndActiveSessionAsync(SessionKey)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 游客或用户注销（注销会话）
    /// </summary>
    internal async Task GuestOrUserLogoutAsync()
    {
        await DomainHostFactory().SessionManager
            .AbandonSessionAsync(SessionKey)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 异步更新并激活指定会话
    /// </summary>
    public async Task<SessionInfo<TUserInfo>> UpdateAndActiveSessionAsync()
    {
        var host = DomainHostFactory();
        var session = await host.SessionManager
            .GetSessionAsync(SessionKey)
            .ConfigureAwait(false);

        var newSession = session.BindUser(this);

        await host.SessionManager.UpdateAndActiveSessionAsync(
            newSession.Key!,
            _ => newSession)
            .ConfigureAwait(false);

        return newSession;
    }

    #endregion
}