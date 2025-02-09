using System;

namespace TKW.Framework.Domain
{
    public class DomainUserSession<T>
        where T : DomainUser
    {
        public string Key { get; }
        public T User { get; }

        /// <summary>
        /// 初始化 <see cref="T:System.Object"/> 类的新实例。
        /// </summary>
        public DomainUserSession(string key, T user)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Argument is null or whitespace", nameof(key));
            Key = key;
            User = user ?? throw new ArgumentNullException(nameof(user));
        }

        public DomainUserSession<DomainUser> ToUserSession()
        {
            return new DomainUserSession<DomainUser>(Key, User);
        }
    }
}