using System;

namespace TKW.Framework.Common.Entity.Exceptions
{
    /// <summary>
    /// 实体验证异常
    /// </summary>
    public class EntityDuplicatedException : Exception
    {
        public string EntityName { get; }

        /// <summary>用指定的错误消息初始化 <see cref="T:System.Exception" /> 类的新实例。</summary>
        /// <param name="entityName"></param>
        public EntityDuplicatedException(string entityName) : base($"实体 '{entityName}' 有重复项。")
        {
            EntityName = entityName;
        }

        /// <summary>用指定的错误消息初始化 <see cref="T:System.Exception" /> 类的新实例。</summary>
        /// <param name="entityName"></param>
        /// <param name="message">描述错误的消息。</param>
        public EntityDuplicatedException(string entityName, string message) : base($"实体 '{entityName}' 有重复项：{message}")
        {
            EntityName = entityName;
        }
    }
}