using System;
using System.Collections.Generic;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public class SimpleUserInfo(string userIdString, string userName) : IUserInfo
{
    // 无参构造，供 Json 反序列化使用
    public SimpleUserInfo() : this(string.Empty, string.Empty)
    {
        UserIdString = string.Empty;
        UserName = string.Empty;
        DisplayName = string.Empty;
        LoginFrom = LoginFromEnum.Unset;
        Roles = [];
    }

    public SimpleUserInfo(int userId, string userName) : this(userId.ToString(), userName)
    {
    }

    public SimpleUserInfo(Guid userGuid, string userName) : this(userGuid.ToString(), userName)
    {
    }

    #region Implementation of IUser

    public string UserIdString { get; set; } = userIdString;
    public string UserName { get; set; } = userName;
    public string DisplayName { get; set; }
    public LoginFromEnum LoginFrom { get; set; } = LoginFromEnum.Unset;
    public List<string> Roles { get; set; } = [];
    public bool IsInRole<T>(T role) where T : Enum
        => Roles?.Contains(role.ToString()) ?? false;
    public bool IsInRole(string role) => Roles?.Contains(role) ?? false;
    #endregion
}