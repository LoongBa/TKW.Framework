using System;

namespace TKW.Framework.Domain.Interfaces;

/// <summary>
/// 领域级工作单元契约
/// </summary>
public interface IDomainUnitOfWork : IDisposable
{
    /// <summary>
    /// 提交事务
    /// </summary>
    void Commit();

    /// <summary>
    /// 回滚事务
    /// </summary>
    void Rollback();

    /// <summary>
    /// 获取原始的 ORM 事务对象 (如 FreeSql.IUnitOfWork)
    /// </summary>
    object OriginalUow { get; }
}