using System;
using TKW.Framework.Common.Extensions;

namespace DomainTest.Managers
{
    public class User
    {
        public User(string userName, string passwordHashed, string realName)
        {
            UserName = userName.EnsureHasValue();
            PasswordHashed = passwordHashed.EnsureHasValue();
            RealName = realName.EnsureHasValue();
            Uid = Guid.NewGuid();
        }
        public User(User user)
        {
            UserName = user.AssertNotNull().UserName;
            PasswordHashed = user.PasswordHashed.EnsureHasValue();
            RealName = user.RealName.EnsureHasValue();
            Uid = user.Uid;
        }

        public string UserName { get; set; }
        public string PasswordHashed { get; set; }
        public Guid Uid { get; set; }
        public string RealName { get; set; }
    }
}
