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
    /// <summary>方法级过滤器</summary>
    public List<DomainFilterAttribute<TUserInfo>> MethodFilters { get; set; }

    /// <summary>控制器级过滤器</summary>
    public List<DomainFilterAttribute<TUserInfo>> ControllerFilters { get; set; }

    /// <summary>方法级标志特性</summary>
    public List<DomainFlagAttribute> MethodFlags { get; set; }

    /// <summary>控制器级标志特性</summary>
    public List<DomainFlagAttribute> ControllerFlags { get; set; }
}