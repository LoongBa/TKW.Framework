using System;
using System.Collections.Generic;
using TKW.Framework.Common.Enumerations;

namespace TKW.Framework.Domain.Interfaces;

public interface IUserInfo
{
    string UserIdString { get; set; }
    string UserName { get; set; }
    string DisplayName { get; set; }
    EnumLoginFrom LoginFrom { get; set; }
    List<string> Roles { get; set; }
    public bool IsInRole<T>(T role) where T : Enum
        => Roles?.Contains(role.ToString()) ?? false;
    public bool IsInRole(string role) => Roles?.Contains(role) ?? false;
}
