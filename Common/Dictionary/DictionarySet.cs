using System.Collections.Generic;

namespace TKW.Framework.Common.Dictionary
{
    public class DictionarySet
    {
        public IReadOnlyList<Dictionary> Dictionaries { get; }

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public DictionarySet()
        {
            Dictionaries = new List<Dictionary>();
        }
    }
}
