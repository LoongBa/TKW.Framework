using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域用户帮助基类：负责管理用户实例的生命周期、登录逻辑以及游客会话初始化。
/// </summary>
/// <typeparam name="TUserInfo">用户信息类型</typeparam>
public abstract class DomainUserHelperBase<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    protected DomainUserHelperBase() { }

    /// <summary>
    /// 当前关联的领域主机实例。通过 AttachHost 方法由框架在初始化阶段注入。
    /// </summary>
    protected DomainHost<TUserInfo>? Host { get; private set; }

    /// <summary>
    /// 建立与 DomainHost 的反向关联，确保 Helper 内部逻辑不依赖全局静态 Root。
    /// </summary>
    internal void AttachHost(DomainHost<TUserInfo> host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
    }

    /// <summary>
    /// 实例创建工厂：创建一个新的 DomainUser 实例，并自动建立其与 Host 的关联。
    /// </summary>
    /// <remarks>
    /// 这一步非常关键，它确保了新创建的 User 能够通过其内部的 Host 属性访问到容器和 SessionManager。
    /// </remarks>
    protected internal DomainUser<TUserInfo> CreateUserInstance()
    {
        // 核心优化：在 new 的同时完成宿主关联
        return new DomainUser<TUserInfo> { AttachedHost = Host };
    }

    #region 核心业务逻辑 (由 DomainHost 调用)

    /// <summary>
    /// 创建新的游客会话：初始化用户实例、绑定默认信息并同步至会话管理器。
    /// </summary>
    internal async Task<SessionInfo<TUserInfo>> CreateNewGuestSessionAsync(SessionInfo<TUserInfo> session)
    {
        // 1. 创建关联了宿主的用户实例
        var newUser = CreateUserInstance();

        // 2. 将用户绑定到会话对象中
        var newSession = session.BindUser(newUser);

        // 3. 调用业务层实现：允许具体项目为游客分配默认权限或标识
        var userInfo = await OnNewGuestSessionCreatedAsync(newSession).ConfigureAwait(false);

        // 4. 填充用户信息并设置认证状态为 false
        newSession.User!.UserInfo = userInfo;
        newSession.User.IsAuthenticated = false;

        // 5. 将完整的游客会话同步并激活
        // 注意：此处必须确保 Host 已被 Attach，否则会抛出空引用异常
        await Host!.SessionManager!
            .UpdateAndActiveSessionAsync(newSession.Key, _ => newSession)
            .ConfigureAwait(false);

        return newSession;
    }

    /// <summary>
    /// 执行用户登录逻辑。
    /// </summary>
    internal async Task<TUserInfo> UserLoginAsync(
        DomainUser<TUserInfo> user,
        string userName,
        string passwordHashed,
        EnumLoginFrom loginFrom)
    {
        // 验证输入并调用业务层具体的登录验证实现
        return await OnUserLoginAsync(
                user.EnsureNotNull(),
                userName.EnsureHasValue(),
                passwordHashed.EnsureHasValue(),
                loginFrom)
            .ConfigureAwait(false);
    }

    #endregion

    #region 业务子类必须实现的抽象方法

    /// <summary>
    /// 当新游客会话创建时触发。子类应在此返回游客的初始 UserInfo 对象。
    /// </summary>
    protected abstract Task<TUserInfo> OnNewGuestSessionCreatedAsync(SessionInfo<TUserInfo> session);

    /// <summary>
    /// 执行具体的登录验证逻辑（如数据库查询、密码核对）。
    /// </summary>
    protected abstract Task<TUserInfo> OnUserLoginAsync(
        DomainUser<TUserInfo> user,
        string userName,
        string passwordHashed,
        EnumLoginFrom loginFrom);

    #endregion
}