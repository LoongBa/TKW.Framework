using System;

namespace TKW.Framework.Domain.WebApi.Attributes
{
    [Serializable]
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method)]
    public class NonDynamicWebApiAttribute:Attribute
    {
        
    }
}