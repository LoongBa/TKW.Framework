using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Session;

public delegate void SessionCreated<TUserInfo>(string sessionKey, SessionInfo<TUserInfo> session)
    where TUserInfo: class, IUserInfo, new();