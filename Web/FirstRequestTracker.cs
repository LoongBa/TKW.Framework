using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Builder.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace TKW.Framework.Web;

/// <summary>
/// 首次请求跟踪工具，用于捕获应用接收的第一个请求信息并支持扩展操作
/// </summary>
public static class FirstRequestTracker
{
    #region 私有成员
    private static string _initialUrl;
    private static string _serverRoot;
    private static bool _captured = false;
    private static Func<HttpContext, Task> _customActionAsync;
    private static readonly object _lockObject = new object();
    private static IApplicationBuilder _appBuilder;
    private static readonly Dictionary<string, object> _firstRequestData = new Dictionary<string, object>();
    #endregion

    #region 公共属性
    /// <summary>
    /// 首次请求的完整URL（包含协议、主机、路径和查询参数）
    /// </summary>
    public static string InitialUrl => _initialUrl;

    /// <summary>
    /// 服务器根路径（协议+主机，如 https://localhost:5001）
    /// </summary>
    public static string ServerRoot => _serverRoot;

    /// <summary>
    /// 首次请求捕获完成时触发的事件
    /// </summary>
    public static event EventHandler<FirstRequestCapturedEventArgs> FirstRequestCaptured;
    #endregion

    #region 公共方法
    /// <summary>
    /// 注册首次请求跟踪中间件（同步版本）
    /// </summary>
    /// <param name="app">应用构建器实例</param>
    /// <param name="customAction">首次请求时执行的同步自定义操作</param>
    /// <param name="removeAfterCapture">捕获后是否从管道移除中间件</param>
    /// <returns>应用构建器实例（支持链式调用）</returns>
    public static IApplicationBuilder UseFirstRequestTracker(
        this IApplicationBuilder app,
        Action<HttpContext> customAction = null,
        bool removeAfterCapture = false)
    {
        // 将同步委托转换为异步委托，统一处理逻辑
        Func<HttpContext, Task> asyncAction = customAction != null
            ? ctx => { customAction(ctx); return Task.CompletedTask; }
            : null;

        return UseFirstRequestTrackerAsync(app, asyncAction, removeAfterCapture);
    }

    /// <summary>
    /// 注册首次请求跟踪中间件（异步版本）
    /// </summary>
    /// <param name="app">应用构建器实例</param>
    /// <param name="customActionAsync">首次请求时执行的异步自定义操作</param>
    /// <param name="removeAfterCapture">捕获后是否从管道移除中间件</param>
    /// <returns>应用构建器实例（支持链式调用）</returns>
    public static IApplicationBuilder UseFirstRequestTrackerAsync(
        this IApplicationBuilder app,
        Func<HttpContext, Task> customActionAsync = null,
        bool removeAfterCapture = false)
    {
        _customActionAsync = customActionAsync;
        _appBuilder = app;

        // 注册中间件到请求管道
        return app.Use(async (context, next) =>
        {
            // 仅在未捕获过首次请求时执行逻辑
            if (!_captured)
            {
                bool shouldProcess = false;

                // 第一阶段：在锁内判断是否需要处理（仅执行同步操作）
                lock (_lockObject)
                {
                    if (!_captured)
                    {
                        shouldProcess = true;
                        // 捕获基础URL信息（同步操作，快速完成）
                        _initialUrl = context.Request.GetDisplayUrl();
                        _serverRoot = $"{context.Request.Scheme}://{context.Request.Host}";
                    }
                }

                // 第二阶段：执行异步操作（锁外执行，避免阻塞）
                if (shouldProcess)
                {
                    // 执行用户自定义异步操作
                    if (_customActionAsync != null)
                    {
                        await _customActionAsync(context);
                    }

                    // 触发首次请求捕获完成事件
                    FirstRequestCaptured?.Invoke(null, new FirstRequestCapturedEventArgs
                    {
                        InitialUrl = _initialUrl,
                        ServerRoot = _serverRoot,
                        HttpContext = context
                    });

                    // 第三阶段：标记完成状态（再次加锁确保线程安全）
                    lock (_lockObject)
                    {
                        _captured = true;

                        // 如需移除中间件，执行移除逻辑
                        if (removeAfterCapture)
                        {
                            RemoveSelfFromPipeline();
                        }
                    }
                }
            }

            // 继续执行管道中的下一个中间件
            await next();
        });
    }

    /// <summary>
    /// 向首次请求数据字典中存储键值对
    /// </summary>
    /// <param name="key">存储的键（不区分大小写）</param>
    /// <param name="value">存储的值（支持任意类型）</param>
    public static void SetData(string key, object value)
    {
        // 仅在首次请求处理期间允许存储数据
        if (!_captured)
        {
            lock (_lockObject)
            {
                if (!_captured)
                {
                    _firstRequestData[key] = value;
                }
            }
        }
    }

    /// <summary>
    /// 从首次请求数据字典中获取指定键的值
    /// </summary>
    /// <typeparam name="T">返回值的类型</typeparam>
    /// <param name="key">要获取的键</param>
    /// <returns>键对应的的值（不存在或类型不匹配时返回默认值）</returns>
    public static T GetData<T>(string key)
    {
        if (_firstRequestData.TryGetValue(key, out var value) && value is T tValue)
        {
            return tValue;
        }
        return default;
    }

    /// <summary>
    /// 检查首次请求数据字典中是否包含指定键
    /// </summary>
    /// <param name="key">要检查的键</param>
    /// <returns>包含指定键返回 true，否则返回 false</returns>
    public static bool ContainsKey(string key) => _firstRequestData.ContainsKey(key);
    #endregion

    #region 私有方法
    /// <summary>
    /// 从请求管道中移除当前中间件
    /// </summary>
    private static void RemoveSelfFromPipeline()
    {
        // 转换为具体实现类型（依赖ASP.NET Core内部实现）
        if (_appBuilder is not ApplicationBuilder appBuilder) return;

        // 获取中间件组件列表
        var components = appBuilder.Properties["components"] as IList<Func<RequestDelegate, RequestDelegate>>;
        if (components == null) return;

        // 查找并移除当前中间件
        for (int i = components.Count - 1; i >= 0; i--)
        {
            var component = components[i];
            if (component.Target?.GetType().FullName?.Contains(nameof(FirstRequestTracker)) == true)
            {
                components.RemoveAt(i);
                break;
            }
        }
    }
    #endregion
}