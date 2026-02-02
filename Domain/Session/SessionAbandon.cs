using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

public delegate void SessionAbandon<TUserInfo>(string sessionKey, SessionInfo<TUserInfo> session) 
    where TUserInfo : class, IUserInfo, new();