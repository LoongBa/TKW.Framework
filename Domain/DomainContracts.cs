using System.Collections.Generic;
using TKW.Framework.Domain.Interception;

namespace TKW.Framework.Domain;

public class DomainContracts
{
    /// <summary>Initializes a new instance of the <see cref="T:System.Object"></see> class.</summary>
    public DomainContracts()
    {
        MethodFilters = [];
        ControllerFilters = [];
        MethodFlags = [];
        ControllerFlags = [];
    }
    public List<DomainActionFilterAttribute> MethodFilters { get; }
    public List<DomainActionFilterAttribute> ControllerFilters { get; }
    public List<DomainFlagAttribute> MethodFlags { get; }
    public List<DomainFlagAttribute> ControllerFlags { get; }
}