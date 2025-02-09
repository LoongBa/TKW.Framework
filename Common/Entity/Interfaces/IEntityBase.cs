using System;

namespace TKW.Framework.Common.Entity.Interfaces
{
    public interface IEntityBase
    {
        Guid EntityGuid { get; }
        int EntityId { get; }
    }
}