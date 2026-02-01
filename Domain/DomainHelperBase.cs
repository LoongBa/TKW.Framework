#nullable enable
using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域帮助基类（抽象）
/// </summary>
public abstract class DomainHelperBase
{
    /// <summary>
    /// 初始化领域帮助类的新实例
    /// </summary>
    /// <param name="hostFactory">DomainHost 工厂，如果为 null 则使用默认工厂</param>
    /// <remarks>
    /// MARK: 构造函数被调用时 DomainHost 可能还未创建实例，
    /// 因此需要传入工厂委托或使用默认静态工厂。
    /// </remarks>
    protected DomainHelperBase(Func<DomainHost>? hostFactory = null)
    {
        DomainHostFactory = hostFactory ?? DomainHost.Factory;
    }

    /// <summary>
    /// 获取 DomainHost 工厂委托
    /// </summary>
    protected Func<DomainHost> DomainHostFactory { get; }

    /// <summary>
    /// 创建一个新的 DomainUser 实例
    /// </summary>
    /// <returns>初始化后的 DomainUser 对象</returns>
    protected internal DomainUser CreateUserInstance()
    {
        return new DomainUser(DomainHostFactory);
    }

    /// <summary>
    /// 当新的 Guest 会话创建时调用的抽象方法
    /// 领域层应根据需要重载该方法以执行特定的业务逻辑（如初始化 Guest 信息）。
    /// </summary>
    /// <param name="session">当前会话信息</param>
    /// <returns>异步返回用户信息</returns>
    protected abstract Task<SimpleUserInfo> OnNewGuestSessionCreatedAsync(SessionInfo session);

    /// <summary>
    /// 验证用户登录信息并返回用户信息的抽象方法
    /// 领域层应根据需要重载该方法以实现具体的登录验证逻辑。
    /// </summary>
    /// <param name="user">当前领域用户</param>
    /// <param name="userName">用户名</param>
    /// <param name="passwordHashed">哈希后的密码</param>
    /// <param name="authType">登录来源枚举</param>
    /// <returns>异步返回验证通过后的用户信息</returns>
    protected abstract Task<SimpleUserInfo> OnUserLoginAsync(
        DomainUser user,
        string userName,
        string passwordHashed,
        LoginFromEnum authType);

    #region 内部实现

    /// <summary>
    /// 创建新的 Guest 会话
    /// </summary>
    /// <param name="session">初始会话信息</param>
    /// <returns>异步返回绑定用户后的会话信息</returns>
    internal async Task<SessionInfo> CreateNewGuestSessionAsync(SessionInfo session)
    {
        // 1. 将新创建的 User 实例绑定到 Session
        var newSession = session.BindUser(CreateUserInstance());

        // 2. 调用领域层重载的方法，获取或初始化 Guest 的 UserInfo
        var userInfo = await OnNewGuestSessionCreatedAsync(newSession).ConfigureAwait(false);

        // 3. 更新 Session 中的 User 状态
        newSession.User!.UserInfo = userInfo;
        newSession.User.IsAuthenticated = false;

        // 4. 统一通过 SessionManager 持久化并激活会话（关键步骤）
        await DomainHostFactory().SessionManager
            .UpdateAndActiveSessionAsync(
                newSession.Key!,
                _ => newSession) // 使用 updater 模式确保原子性更新
            .ConfigureAwait(false);

        return newSession;
    }

    /// <summary>
    /// 处理用户登录逻辑
    /// </summary>
    /// <param name="user">当前领域用户</param>
    /// <param name="userName">用户名</param>
    /// <param name="passwordHashed">哈希后的密码</param>
    /// <param name="loginFrom">登录来源</param>
    /// <returns>异步返回登录成功的用户信息</returns>
    /// <exception cref="ArgumentNullException">当参数为 null 或空时抛出</exception>
    internal async Task<SimpleUserInfo> UserLoginAsync(
        DomainUser user,
        string userName,
        string passwordHashed,
        LoginFromEnum loginFrom)
    {
        // 调用业务方法：验证用户名、密码，并确保参数有效性
        return await OnUserLoginAsync(
                user.EnsureNotNull(),
                userName.EnsureHasValue(),
                passwordHashed.EnsureHasValue(),
                loginFrom)
            .ConfigureAwait(false);
    }

    #endregion
}
