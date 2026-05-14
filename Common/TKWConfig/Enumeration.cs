using System;
using System.Collections.Generic;
using System.Linq;

namespace TKW.Framework.TKWConfig
{
    public class Enumeration
    {
        public Enumeration()
        {
            Items = new List<EnumerationItem>();
        }
        public Enumeration(Enumeration enumeration) : this()
        {
            foreach (var item in enumeration.Items)
                Items.Add(new EnumerationItem(item));
        }

        public Enumeration(string name)
            : this()
        {
            Name = name;
        }

        public string Name { get; set; }

        public IList<EnumerationItem> Items { get; }

        public EnumerationItem this[string name]
        {
            get
            {
                foreach (var item in Items.Where(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    return item;

                throw new ArgumentOutOfRangeException($"枚举'{Name}'中无法找到名为'{name}'的项。");
            }
        }

        public bool Contains(string name)
        {
            return Items.Any(enumerationItem => enumerationItem.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}