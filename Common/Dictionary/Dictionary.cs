using System.Collections.Generic;

namespace TKW.Framework.Common.Dictionary {
    public class Dictionary
    {
        public IReadOnlyList<DictionaryItem> Items { get; }

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public Dictionary()
        {
            Items = new List<DictionaryItem>();
        }
    }
}