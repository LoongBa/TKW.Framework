using System;

namespace TKW.Framework.Common.Dictionary
{
    public class DictionaryItem
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Memo { get; set; }
        public int DisplayOrder { get; set; }
        public Guid Uid { get; set; }

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public DictionaryItem()
        {
            Name = DisplayName = Memo = string.Empty;// default(string);
            Uid = Guid.Empty;
        }
    }
}