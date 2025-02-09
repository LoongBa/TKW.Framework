using System;

namespace TKW.Framework.Common.DataType
{
    public struct Operator
    {
        /// <summary>
        /// 初始化 <see cref="T:System.Object"/> 类的新实例。
        /// </summary>
        public Operator(string name, int id, Guid uid = default(Guid))
        {
            Name = name ?? string.Empty;
            Id = id;
            Uid = uid;
        }

        public string Name { get; set; }
        public int Id { get; set; }
        public Guid Uid { get; set; }
    }
}