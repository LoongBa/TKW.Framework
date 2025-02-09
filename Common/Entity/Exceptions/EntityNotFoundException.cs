using System;

namespace TKW.Framework.Common.Entity.Exceptions
{
    /// <summary>
    /// 无法找到相应的实体异常
    /// </summary> 
    public class EntityNotFoundException : Exception
    {
        public string EntityName { get; private set; }
        public string Condition { get; private set; }

        public EntityNotFoundException(string entityName)
            : base($"Not found [{entityName}].")
        {
            EntityName = entityName;
        }

        public EntityNotFoundException(string entityName, string condition)
            : base($"Not found [{entityName}]=\"{condition}\".")
        {
            EntityName = entityName;
            Condition = condition;
        }
    }
}
