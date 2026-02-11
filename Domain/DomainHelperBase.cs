using System;
using System.Threading.Tasks;
using TKW.Framework.Common.Enumerations;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain;

/// <summary>
/// 领域帮助基类（抽象）
/// </summary>
public abstract class DomainUserHelperBase<TUserInfo>(Func<DomainHost<TUserInfo>>? hostFactory = null)
    where TUserInfo : class, IUserInfo, new()
{
    protected Func<DomainHost<TUserInfo>> DomainHostFactory { get; } = hostFactory ?? DomainHost<TUserInfo>.Factory;

    protected internal DomainUser<TUserInfo> CreateUserInstance()
    {
        return new DomainUser<TUserInfo>(DomainHostFactory);
    }

    protected abstract Task<TUserInfo> OnNewGuestSessionCreatedAsync(SessionInfo<TUserInfo> session);

    protected abstract Task<TUserInfo> OnUserLoginAsync(
        DomainUser<TUserInfo> user,
        string userName,
        string passwordHashed,
        EnumLoginFrom loginFrom);

    internal async Task<SessionInfo<TUserInfo>> CreateNewGuestSessionAsync(SessionInfo<TUserInfo> session)
    {
        var newSession = session.BindUser(CreateUserInstance());

        var userInfo = await OnNewGuestSessionCreatedAsync(newSession).ConfigureAwait(false);

        newSession.User!.UserInfo = userInfo;
        newSession.User.IsAuthenticated = false;

        await DomainHostFactory().SessionManager!
            .UpdateAndActiveSessionAsync(
                newSession.Key!,
                _ => newSession)
            .ConfigureAwait(false);

        return newSession;
    }

    internal async Task<TUserInfo> UserLoginAsync(
        DomainUser<TUserInfo> user,
        string userName,
        string passwordHashed,
        EnumLoginFrom loginFrom)
    {
        return await OnUserLoginAsync(
                user.EnsureNotNull(),
                userName.EnsureHasValue(),
                passwordHashed.EnsureHasValue(),
                loginFrom)
            .ConfigureAwait(false);
    }
}