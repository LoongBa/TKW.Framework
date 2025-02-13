using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

public delegate void SessionAbandon<T>(string sessionKey, CommonSession<T> session) where T : class /*ICopyValues<T>*/;