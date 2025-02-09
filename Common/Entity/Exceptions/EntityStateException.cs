using System;

namespace TKW.Framework.Common.Entity.Exceptions {
    /// <summary>
    /// 实体状态异常
    /// </summary>
    public class EntityStateException : Exception
    {
        public string EntityName { get; }
        public EntityStateExceptionType Type { get; }

        /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
        public EntityStateException(string entityName, EntityStateExceptionType type)
            : base($"Entity '{entityName}' Is {type}")
        {
            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException(
                    "Value cannot be null or whitespace.",
                    nameof(entityName));
            Type = type;
            EntityName = entityName;
        }
    }
}