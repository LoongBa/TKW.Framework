using System;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session
{
    public class SessionUser<T>
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
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(username));
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(roleName));
            if (string.IsNullOrWhiteSpace(sessionKeyName))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(sessionKeyName));
            if (string.IsNullOrWhiteSpace(sessionKey))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(sessionKey));

            Username = username;
            UserId = userId;
            RoleName = roleName;
            SessionKeyName = sessionKeyName;
            SessionKey = sessionKey;
        }

        public SessionUser(string sessionKey, IUserHelper<T> userHelper) : this(userHelper.RetrieveAndActiveUserSession(sessionKey), userHelper.SessionKey_KeyName) { }
        public SessionUser(DomainUserSession<T> domainUserSession, IUserHelper<T> userHelper) : this(domainUserSession, userHelper.SessionKey_KeyName) { }

        public SessionUser(DomainUserSession<T> domainUserSession, string sessionKeyName)
        {
            domainUserSession.AssertNotNull(name: nameof(domainUserSession));
            Username = domainUserSession.User.Identity.Name;
            UserId = domainUserSession.User.UserIdString;
            RoleName = string.Join(",", domainUserSession.User.RoleNameStringArray);
            SessionKey = domainUserSession.Key;
            sessionKeyName.EnsureHasValue();
            SessionKeyName = sessionKeyName;
        }
    }
}