using System;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Session
{
    public class CommonSession<T>
    {
        /// <summary>
        /// 初始化 <see cref="T:ISessionValue"/> 类的新实例。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public CommonSession(string key, T value)
        {
            Key = key.HasValue() ? key : Guid.NewGuid().ToString();
            Value = value;
            TimeLastActived = TimeCreated = DateTime.Now;
        }

        public CommonSession(T value) : this(null, value) { }

        /// <summary>
        /// 缓存项的 Key
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 缓存的值
        /// </summary>
        public T Value { get; internal set; }

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

        internal CommonSession<T> Active()
        {
            TimeLastActived = DateTime.Now;
            return this;
        }

        internal void UpdateValue(T value)
        {
            value.AssertNotNull(name: nameof(value));
            Value = value;
        }

        /// <exception cref="InvalidCastException"></exception>
        public DomainUserSession<TUserBase> ToUserSession<TUserBase>()
            where TUserBase : DomainUser
        {
            Value.AssertNotNull(name: nameof(Value));
            var user = Value as DomainUser;
            if (user == null)
                throw new InvalidCastException($"指定的类型并非 DomainUser 派生类：{nameof(TUserBase)}");
            user.SessionKey = Key;
            return new DomainUserSession<TUserBase>(Key, user as TUserBase);
        }

        public static CommonSession<T> New(T value)
        {
            return new CommonSession<T>(value);
        }
    }
}