using Microsoft.Extensions.Caching.Memory;

namespace TKW.Framework.Domain.Session;

public delegate void SessionRemoved(string sessionKey, CommonSession session, EvictionReason reason, object state) /*ICopyValues<T>*/;