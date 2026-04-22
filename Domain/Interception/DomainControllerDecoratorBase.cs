using System;
using System.Threading.Tasks;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

public abstract class DomainControllerDecoratorBase<TService, TUserInfo>(TService inner, DomainHost<TUserInfo> host)
    where TService : class, IDomainService, IAopContract, new()
    where TUserInfo : class, IUserInfo, new()
{
    protected readonly TService Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    protected readonly DomainHost<TUserInfo> Host = host ?? throw new ArgumentNullException(nameof(host)); // 或泛型 TUserInfo，视实际生成器上下文而定

    // 辅助：在装饰器里调用 inner 前后可以复用的方法
    protected async Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> func, string methodName)
    {
        // 1. 创建 DomainContext（可由 Host 提供工厂方法）
        // 2. 触发 pre-filters（Host.GlobalFilters / Controller / Method），这部分需要在生成器里填充 Method 对应的过滤器 metadata
        try
        {
            return await func().ConfigureAwait(false);
        }
        // ReSharper disable once RedundantCatchClause
        catch (Exception)
        {
            // 统一异常处理/日志记录
            throw;
        }
    }

    protected async Task InvokeAsync(Func<Task> func, string methodName)
    {
        await InvokeAsync<object>(async () => { await func(); return null; }, methodName);
    }
}