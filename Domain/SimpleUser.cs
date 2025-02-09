using System;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain
{
    public class SimpleUser : IUser
    {
        public SimpleUser(string userIdString, string userName) : this(userName)
        {
            userIdString.EnsureHasValue();
            UserIdString = userIdString;
        }
        public SimpleUser(int userId, string userName) : this(userName)
        {
            UserIdString = userId.ToString();
        }
        public SimpleUser(Guid userGuid, string userName) : this(userName)
        {
            UserIdString = userGuid.ToString();
        }

        private SimpleUser(string userName)
        {
            userName.EnsureHasValue();
            UserName = userName;
        }

        #region Implementation of IUser

        public string UserIdString { get; }

        public string UserName { get; }

        #endregion
    }
}