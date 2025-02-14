using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Session
{
    public class SessionManager<T> : ISessionCache where T : DomainUser /*ICopyValues<T>*/
    {
        public event SessionCreated<T> SessionCreated;
        public event SessionAbandon<T> SessionAbandon;
        public event SessionRemoved<T> SessionTimeout;
        private readonly IMemoryCache _MemoryCache;
        private readonly string _CacheKeySalt;
        public const uint _SecondsCheckingIntervalDefault_ = 3;
        public const uint _SecondsTimeoutDefault_ = 10 * 60;

        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OverflowException">
        /// <paramref name="secondsTimeOut" /> 是小于 <see cref="F:System.TimeSpan.MinValue" /> 或大于 <see cref="F:System.TimeSpan.MaxValue" />。
        /// - 或 -<paramref name="secondsTimeOut" /> 为 <see cref="F:System.Double.PositiveInfinity" />。- 或 -<paramref name="secondsTimeOut" /> 为 <see cref="F:System.Double.NegativeInfinity" />。</exception>
        public SessionManager(
            uint secondsTimeOut = _SecondsTimeoutDefault_,
            uint secondsCheckingInterval = _SecondsCheckingIntervalDefault_,
            string sessionKeySalt = null,
            string sessionKeyKeyName = "SessionKey")
        {
            _MemoryCache = new MemoryCache(new MemoryCacheOptions
            {
                ExpirationScanFrequency = TimeSpan.FromSeconds((int)secondsCheckingInterval),
            });
            _CacheKeySalt = sessionKeySalt;
            SessionExpiredTimeSpan = TimeSpan.FromSeconds((int)secondsTimeOut);
            SessionKey_KeyName = sessionKeyKeyName;
        }

        public TimeSpan SessionExpiredTimeSpan { get; }
        public string SessionKey_KeyName { get; }

        /// <exception cref="SessionException"></exception>
        public CommonSession<T> AbandonSession(string sessionKey)
        {
            if (!sessionKey.HasValue())
                throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

            if (!_MemoryCache.TryGetValue(sessionKey, out var sessionValue))
                throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

            _MemoryCache.Remove(sessionKey);

            var session = sessionValue as CommonSession<T>;
            OnSessionAbandon(sessionKey, session);
            return session;
        }

        /// <summary>
        /// 激活会话
        /// </summary>
        /// <param name="sessionKey"></param>
        /// <exception></exception>
        public CommonSession<T> ActiveSession(string sessionKey)
        {
            if (!sessionKey.HasValue())
                throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

            if (!_MemoryCache.TryGetValue(sessionKey, out var sessionValue))
                throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

            var session = (CommonSession<T>)sessionValue;
            return session?.Active();
        }

        /// <exception cref="ArgumentException">Value cannot be null or whitespace.</exception>
        public bool ContainsSession(string sessionKey)
        {
            if (!sessionKey.HasValue())
                throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);
            return _MemoryCache.TryGetValue(sessionKey, out _);
        }

        /// <exception cref="SessionException"></exception>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public CommonSession<T> CreateSession(T value)
        {
            return CreateSession(value, null);
        }

        private CommonSession<T> CreateSession(T value, string sessionKey)
        {
            sessionKey ??= GenerateNewCacheKey(Guid.NewGuid().ToString());
            var session = new CommonSession<T>(sessionKey, value);
            if (_MemoryCache.TryGetValue(sessionKey, out _))
                throw new SessionException(sessionKey, SessionExceptionType.DuplicatedSessionKey);
            OnSessionCreated(session.Key, session);
            return _MemoryCache.Set(
                session.Key, session,
                new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(SessionExpiredTimeSpan)
                    .RegisterPostEvictionCallback(PostEvictionCallback)//订阅从缓存中移除事件
            );
        }

        /// <exception cref="SessionException"></exception>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Condition.</exception>
        public CommonSession<T> GetSession(string sessionKey)
        {
            if (!sessionKey.HasValue())
                throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

            if (_MemoryCache.TryGetValue(sessionKey, out var sessionValue))
                return sessionValue as CommonSession<T>;

            throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);
        }

        /// <exception cref="SessionException"></exception>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public CommonSession<T> GetOrCreateSession(string sessionKey, T value)
        {
            if (!sessionKey.HasValue())
                throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

            //TODO: 尝试从二级缓存获取会话
            if (_MemoryCache.TryGetValue(sessionKey, out var sessionValue))
                return sessionValue as CommonSession<T>;

            return CreateSession(value, sessionKey);
        }

        /// <exception cref="SessionException">Condition.</exception>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public CommonSession<T> GetAndActiveSession(string sessionKey)
        {
            return ActiveSession(sessionKey);
        }

        /// <summary>
        /// 更新会话值
        /// </summary>
        /// <remarks>注意：仅复制传入 value 的属性值（调用 ICacheValue.CopyValuesFrom() 方法）</remarks>
        /// <exception cref="SessionException"></exception>
        public CommonSession<T> UpdateSessionValue(string sessionKey, T value)
        {
            if (!sessionKey.HasValue())
                throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionKey);

            if (value == null)
                throw new SessionException(sessionKey, SessionExceptionType.InvalidSessionValue);

            if (!_MemoryCache.TryGetValue(sessionKey, out var sessionValue))
                throw new SessionException(sessionKey, SessionExceptionType.SessionNotFound);

            var session = (CommonSession<T>)sessionValue;
            session?.UpdateValue(value);
            session?.Active();
            return session;
        }

        private void PostEvictionCallback(object key, object value, EvictionReason reason, object state)
        {
            OnSessionTimeout((string)key, (CommonSession<T>)value, reason, state);
        }

        protected virtual void OnSessionCreated(string sessionKey, CommonSession<T> session)
        {
            //TODO: 加上二级、分布式会话机制
            SessionCreated?.Invoke(sessionKey, session);
        }
        protected virtual void OnSessionAbandon(string sessionKey, CommonSession<T> session)
        {
            //TODO: 加上二级、分布式会话机制
            SessionAbandon?.Invoke(sessionKey, session);
        }

        protected virtual void OnSessionTimeout(string sessionKey, CommonSession<T> session, EvictionReason reason, object state)
        {
            //TODO: 加上二级、分布式会话机制
            SessionTimeout?.Invoke(sessionKey, session, reason, state);
        }

        /// <summary>
        /// 执行与释放或重置非托管资源相关的应用程序定义的任务。
        /// </summary>
        public void Dispose()
        {
            _MemoryCache.Dispose();
        }

        protected virtual string GenerateNewCacheKey(string key)
        {
            //TODO:改进加密算法
            return string.IsNullOrWhiteSpace(_CacheKeySalt)
                       ? GetSha1HashData(key)
                       : GetSha1HashData(_CacheKeySalt + key);
        }

        private static string GetSha1HashData(string data)
        {
            //create new instance of md5
            var sha1 = Cryptography.HashAlgorithmFactory.Create(Cryptography.HashAlgorithmType.Sha1);

            //convert the input text to array of bytes
            var hashData = sha1.ComputeHash(Encoding.Default.GetBytes(data));

            // 之前的结果太长，缩短一点
            var result = string.Empty;
            return hashData.Aggregate(result, (current, item) => current + item.ToString("x2"));
        }
    }
}