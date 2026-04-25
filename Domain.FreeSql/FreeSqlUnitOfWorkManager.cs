using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.FreeSql;

public class FreeSqlUnitOfWorkManager(IFreeSql fsql) : IDomainUnitOfWorkManager
{
    private IDomainUnitOfWork? _currentUow;

    public IDomainUnitOfWork GetUnitOfWork()
    {
        if (_currentUow != null) return _currentUow;

        // 真正创建原生事务
        var nativeUow = fsql.CreateUnitOfWork();
        _currentUow = new FreeSqlDomainUnitOfWork(nativeUow);
        return _currentUow;
    }

    public void Dispose()
    {
        // 关键：当 DI 作用域（Scope）销毁时，自动释放事务
        _currentUow?.Dispose();
    }
}