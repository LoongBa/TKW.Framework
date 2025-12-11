using System;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain
{
    public class SimpleUserInfo : IUser
    {
        public SimpleUserInfo(string userIdString, string userName) : this(userName)
        {
            userIdString.EnsureHasValue();
            UserIdString = userIdString;
        }
        public SimpleUserInfo(int userId, string userName) : this(userName)
        {
            UserIdString = userId.ToString();
        }
        public SimpleUserInfo(Guid userGuid, string userName) : this(userName)
        {
            UserIdString = userGuid.ToString();
        }

        private SimpleUserInfo(string userName)
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