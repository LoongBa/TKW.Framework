using System;

namespace TKW.Framework.Domain.Interfaces
{
    public interface ICopyValues<TValue> : ICloneable
    {
        TValue CopyValuesFrom(TValue fromObject);
    }
}