using System;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

/*public class SessionUser<T>
    where T : DomainUser, ICopyValues<T>
{
    public string Username { get; }
    public string UserId { get; }
    public string RoleName { get; }
    public string SessionKeyName { get; }
    public string SessionKey { get; }

    public SessionUser(string username, string userId, string roleName, string sessionKeyName, string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("User cannot be null or whitespace.", nameof(username));
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("User cannot be null or whitespace.", nameof(roleName));
        if (string.IsNullOrWhiteSpace(sessionKeyName))
            throw new ArgumentException(
                "User cannot be null or whitespace.",
                nameof(sessionKeyName));
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new ArgumentException(
                "User cannot be null or whitespace.",
                nameof(sessionKey));

        Username = username;
        UserId = userId;
        RoleName = roleName;
        SessionKeyName = sessionKeyName;
        SessionKey = sessionKey;
    }

    public SessionUser(string sessionKey, IUserHelper userHelper) : this(userHelper.RetrieveAndActiveUserSessionAsync(sessionKey), userHelper.SessionKey_KeyName) { }
    public SessionUser(Session Session, IUserHelper userHelper) : this(Session, userHelper.SessionKey_KeyName) { }

    public SessionUser(Session Session, string sessionKeyName)
    {
        Session.AssertNotNull(name: nameof(Session));
        Username = Session.User.Identity?.Name ?? string.Empty;
        UserId = Session.User.UserIdString;
        RoleName = string.Join(",", Session.User.RoleNames);
        SessionKey = Session.Key;
        sessionKeyName.EnsureHasValue();
        SessionKeyName = sessionKeyName;
    }
}*/