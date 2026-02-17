using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using TKW.Framework.Common.Extensions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Interception;

/// <summary>
/// 领域拦截器：实现物理隔离的“按需开启作用域”模式。
/// 确保在高并发环境下，每一个方法调用都拥有独立的数据库连接、事务和缓存上下文。
/// </summary>
public class DomainInterceptor<TUserInfo> : BaseInterceptor<TUserInfo>, IDisposable
    where TUserInfo : class, IUserInfo, new()
{
    private readonly DomainHost<TUserInfo> _DomainHost;
    private readonly DefaultExceptionLoggerFactory? _GlobalExceptionLoggerFactory;

    public DomainInterceptor(DomainHost<TUserInfo> domainHost)
    {
        ArgumentNullException.ThrowIfNull(domainHost);
        _DomainHost = domainHost;
        _GlobalExceptionLoggerFactory = domainHost.ExceptionLoggerFactory;
    }

    /// <summary>
    /// 初始化：为当前方法调用开启专属的“逻辑沙箱”。
    /// </summary>
    protected override async Task InitialAsync(IInvocation invocation)
    {
        if (_DomainHost.Container == null)
            throw new InvalidOperationException("领域主机尚未完成初始化，无法开启拦截作用域。");

        // 【重构核心】：从根容器开启一个全新的生命周期作用域
        // 这确保了 Context 解析出的所有 DomainService 都是该方法独占的。
        var perCallScope = _DomainHost.Container.BeginLifetimeScope();

        // 将此作用域绑定到领域上下文中
        Context = _DomainHost.NewDomainContext(invocation, perCallScope);
        Context.EnsureNotNull();

        await Task.CompletedTask;
    }

    /// <summary>
    /// 清理：方法执行完毕后立即释放子容器，回收内存和数据库连接。
    /// </summary>
    protected override async Task CleanUpAsync()
    {
        // 只有在 Context 及其关联的作用域存在时才进行释放
        if (Context?.LifetimeScope != null)
        {
            // 使用异步释放，防止在处理数据库连接等 I/O 资源时阻塞线程
            await Context.LifetimeScope.DisposeAsync();
        }
    }

    #region 过滤器执行逻辑 (保持原有的多级顺序)

    protected override async Task PreProceedAsync(IInvocation invocation)
    {
        if (Context == null) return;

        // 全局 -> 控制器 -> 方法
        foreach (var filter in _DomainHost.GlobalFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Global, Context);
        }

        var controllerFilters = Context.ControllerFilters
            .Where(cf => Context.MethodFilters.All(mf => mf.TypeId != cf.TypeId));

        foreach (var filter in controllerFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Controller, Context);
        }

        foreach (var filter in Context.MethodFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                await filter.PreProceedAsync(DomainInvocationWhereType.Method, Context);
        }
    }

    protected override async Task PostProceedAsync(IInvocation invocation)
    {
        if (Context == null) return;

        // 方法 -> 控制器 -> 全局 (逆序)
        foreach (var filter in Context.MethodFilters.AsEnumerable().Reverse())
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Method, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Method, Context);
        }

        var controllerFilters = Context.ControllerFilters
            .Where(cf => Context.MethodFilters.All(mf => mf.TypeId != cf.TypeId))
            .AsEnumerable().Reverse();

        foreach (var filter in controllerFilters)
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Controller, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Controller, Context);
        }

        foreach (var filter in _DomainHost.GlobalFilters.AsEnumerable().Reverse())
        {
            if (filter.CanWeGo(DomainInvocationWhereType.Global, Context))
                await filter.PostProceedAsync(DomainInvocationWhereType.Global, Context);
        }
    }

    #endregion

    protected override void LogException(InterceptorExceptionContext context)
    {
        Context?.Logger?.LogError(context.Exception,
            "领域方法执行异常 | 方法: {MethodName} | 用户: {UserName}",
            context.Invocation.Method.Name,
            Context.DomainUser.UserInfo.UserName);

        var ex = context.Exception;
        context.ErrorMessage = ex.Message;
        context.IsAuthenticationError = ex is AuthenticationException;
        context.IsAuthorizationError = ex is UnauthorizedAccessException;
        context.Method = context.Invocation.Method.Name;
        context.UserName = Context?.DomainUser?.UserInfo?.UserName ?? "Unknown";
        context.TargetType = context.Invocation.InvocationTarget.GetType().Name;

        if (context.IsAuthenticationError) context.ErrorCode = "AUTH_001";
        else if (context.IsAuthorizationError) context.ErrorCode = "AUTH_002";

        _GlobalExceptionLoggerFactory?.LogException(context);
        context.ExceptionHandled = false;
    }

    public void Dispose()
    {
        // 拦截器实例本身的清理
        GC.SuppressFinalize(this);
    }
}