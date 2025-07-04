using System;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Session
{
    public class CommonSession
    {
        /// <summary>
        /// 初始化 <see cref="T:ISessionValue"/> 类的新实例。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public CommonSession(string key, DomainUser value)
        {
            Key = key.HasValue() ? key : Guid.NewGuid().ToString();
            Value = value;
            TimeLastActived = TimeCreated = DateTime.Now;
        }

        public CommonSession(DomainUser value) : this(null, value) { }

        /// <summary>
        /// 缓存项的 Key
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 缓存的值
        /// </summary>
        public DomainUser Value { get; internal set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime TimeCreated { get; set; }

        /// <summary>
        /// 最后一次激活时间
        /// </summary>
        public DateTime TimeLastActived { get; private set; }

        /// <summary>
        /// 发呆时间
        /// </summary>
        public TimeSpan Idle => DateTime.Now - TimeLastActived;

        internal CommonSession Active()
        {
            TimeLastActived = DateTime.Now;
            return this;
        }

        internal void UpdateValue(DomainUser value)
        {
            value.AssertNotNull(name: nameof(value));
            Value = value;
        }

        /// <exception cref="InvalidCastException"></exception>
        public DomainUserSession ToUserSession()
        {
            Value.AssertNotNull(name: nameof(Value));
            var user = Value as DomainUser;
            if (user == null)
                throw new InvalidCastException($"指定的类型并非 DomainUser 派生类：{nameof(DomainUser)}");
            user.SessionKey = Key;
            return new DomainUserSession(Key, user as DomainUser);
        }

        public static CommonSession New(DomainUser value)
        {
            return new CommonSession(value);
        }
    }
}