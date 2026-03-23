namespace TKW.Framework.Domain.Exceptions;

public enum EntityStateExceptionType
{
    /// <summary>
    /// 实体已被删除
    /// </summary>
    EntityIsDeleted,
    /// <summary>
    /// 实体已被禁用
    /// </summary>
    EntityIsDisabled,
}