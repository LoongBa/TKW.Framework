#nullable enable
using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 实现了 IDomainServer 的抽象基类
/// </summary>
public abstract class DomainHelperBase
{
    /// <summary>
    /// 初始化领域帮助类的新实例
    /// </summary>
    protected DomainHelperBase(Func<DomainHost>? hostFactory = null)
    {
        // MARK: 构造函数被调用时 DomainHost 还未创建实例，
        DomainHostFactory = hostFactory ?? DomainHost.Factory;
    }

    protected Func<DomainHost> DomainHostFactory { get; }

    protected internal virtual DomainUser CreateUserInstance()
    {
        var user = new DomainUser();
        user.SetDomainHostFactory(DomainHostFactory);
        return user;
    }
    protected virtual DomainUser CreateUserInstance(string userName, UserAuthenticationType authenticationType = UserAuthenticationType.Unset)
    {
        var user = CreateUserInstance();
        user.AddUserIdentity(userName, authenticationType);
        return user;
    }

    /// <summary>
    /// 返回新的 Guest 用户：领域层根据需要重载该方法
    /// </summary>
    protected virtual DomainUser OnGuestUserLogin()
    {
        var random = new Random((int)DateTime.Now.Ticks).Next(1000000);
        return CreateUserInstance($"Guest_{random}");
    }

    /// <summary>
    /// 验证用户登录信息并返回用户
    /// </summary>
    protected abstract Task<IUserInfo> OnUserLoginAsync(string userName, string passWordHashed, UserAuthenticationType authType);

    #region Implementation of IDomainServer<T>
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <exception cref="SessionException">Condition.</exception>
    public async Task<SessionInfo> UserLoginAsync(DomainUser user, string userName,
        string passWordHashed, UserAuthenticationType authType)
    {
        ArgumentNullException.ThrowIfNull(user);

        //调用业务方法：验证用户名、密码
        var userInfo = await OnUserLoginAsync(userName.EnsureHasValue(), passWordHashed.EnsureHasValue(), authType);

        //更新 User 信息
        user.UserInfo = userInfo;
        return await DomainHost.Root.SessionManager.GetAndActiveSessionAsync(user.SessionKey);
    }

    #endregion
}