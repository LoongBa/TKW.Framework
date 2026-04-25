using FreeSql;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.FreeSql;

public class FreeSqlDomainUnitOfWork(IUnitOfWork uow) : IDomainUnitOfWork
{
    public void Commit() => uow.Commit();
    public void Rollback() => uow.Rollback();
    public object OriginalUow => uow;

    public void Dispose() => uow.Dispose();
}