namespace TKW.Framework.Domain.Exceptions;

/// <summary>
/// 无法找到相应的实体异常
/// </summary> 
public class EntityNotFoundException(string entityName, string description = "") : DomainException($"Entity Not found [{entityName}]：{description}.")
{
    public string EntityName { get; private set; } = entityName;
    public string Description { get; private set; } = description;
}