using System.Collections.Generic;
using TKW.Framework.Domain.Interception;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain;

public class DomainContracts<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
    public DomainContracts()
    {
        MethodFilters = [];
        ControllerFilters = [];
        MethodFlags = [];
        ControllerFlags = [];
    }
    public List<DomainFilterAttribute<TUserInfo>> MethodFilters { get; }
    public List<DomainFilterAttribute<TUserInfo>> ControllerFilters { get; }
    public List<DomainFlagAttribute> MethodFlags { get; }
    public List<DomainFlagAttribute> ControllerFlags { get; }
}