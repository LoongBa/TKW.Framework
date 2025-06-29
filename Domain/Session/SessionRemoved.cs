using Microsoft.Extensions.Caching.Memory;

namespace TKW.Framework.Domain.Session;

public delegate void SessionRemoved<T>(string sessionKey, CommonSession<T> session, EvictionReason reason, object state) where T : class /*ICopyValues<T>*/;