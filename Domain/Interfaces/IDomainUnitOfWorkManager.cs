using System;

namespace TKW.Framework.Domain.Interfaces;

/// <summary>
/// 工作单元管理器接口（ORM 无关）
/// </summary>
public interface IDomainUnitOfWorkManager : IDisposable
{
    /// <summary>
    /// 获取或创建当前作用域内的工作单元
    /// </summary>
    IDomainUnitOfWork GetUnitOfWork();
}