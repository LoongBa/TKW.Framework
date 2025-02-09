using System.ComponentModel;

namespace TKW.Framework.Common.Entity.Attributes
{
    public class EntityPropertyDescriptionAttribute : DescriptionAttribute
    {
        public string Name { get; }
        public string UnitName { get; }

        public EntityPropertyDescriptionAttribute(string name, string unitName = "", string description = "") : base(description)
        {
            Name = name;
            UnitName = unitName;
        }
    }
}