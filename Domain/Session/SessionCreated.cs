namespace TKW.Framework.Domain.Session;

public delegate void SessionCreated<T>(string sessionKey, CommonSession<T> session) where T : class /*ICopyValues<T>*/;