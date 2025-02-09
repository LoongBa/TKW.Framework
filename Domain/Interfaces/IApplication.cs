using System;

namespace TKW.Framework.Domain.Interfaces
{
    public interface IApplication
    {
        Guid Uid { get; }
        string Name { get; }
        string Version { get; }
        string Description { get; }
    }
}