using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Domain.Exceptions;

/// <summary>
/// 实体状态异常
/// </summary>
public class EntityStateException : DomainException
{
    public string EntityName { get; }
    public EntityStateExceptionType Type { get; }

    /// <summary>初始化 <see cref="T:System.Object" /> 类的新实例。</summary>
    public EntityStateException(string entityName, EntityStateExceptionType type)
        : base($"Entity '{entityName}' Is {type}.")
    {
        Type = type;
        EntityName = entityName.EnsureNotEmptyOrNull(nameof(entityName)); ;
    }
}