using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Hosting;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Maui.Hosting;

/// <summary>
/// 领域驱动的 MAUI 应用基类，自动接管异步领域自举与异常拦截
/// </summary>
public abstract class DomainMauiApplication<TUserInfo, TOptions>(IServiceProvider serviceProvider) : Application
    where TUserInfo : class, IUserInfo, new()
    where TOptions : DomainOptions, new()
{
    protected readonly IServiceProvider ServiceProvider = serviceProvider;

    /// <summary>
    /// 在域引导期间提供初始页面。
    /// 默认为一个简单的加载页面。
    /// </summary>
    protected virtual Page CreateLoadingPage()
    {
        // 创建并返回一个新的内容页面
        return new ContentPage
        {
            // 将活动指示器设置为页面的内容
            Content = new ActivityIndicator
            {
                IsRunning = true, // 设置指示器处于运行（加载中）状态
                HorizontalOptions = LayoutOptions.Center, // 水平方向居中对齐
                VerticalOptions = LayoutOptions.Center // 垂直方向居中对齐
            }
        };
    }


    /// <summary>
    /// 重写创建窗口的方法，在应用启动时提供初始窗口。
    /// </summary>
    /// <param name="activationState">应用的激活状态，可能为 null。</param>
    /// <returns>返回一个包含加载页面的窗口实例。</returns>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // 立即返回一个带有加载页面的窗口。
        // 当域名（或核心业务逻辑）准备就绪后，我们稍后会将其替换掉。
        return new Window(CreateLoadingPage());
    }


    protected override async void OnStart()
    {
        base.OnStart();

        try
        {
            // 框架代为执行异步自举，完全不会卡死 UI
            await ServiceProvider.UseTKWDomainAsync<TUserInfo, TOptions>();

            // 领域就绪，通知子类可以跳转到主页了
            OnDomainReady();
        }
        catch (SystemSetupRequiredException ex)
        {
            // 拦截到需要业务初始化的异常，通知子类跳转到 Setup 页面
            OnSystemSetupRequired(ex);
        }
        catch (Exception ex)
        {
            // 拦截基础设施崩溃（如本地 SQLite 无法创建）
            OnInfrastructureFailed(ex);
        }
    }

    /// <summary>
    /// 安全地交换主窗口根页面的辅助方法。
    /// 该方法确保页面切换操作在主线程上执行，以避免多线程操作UI引发异常。
    /// </summary>
    protected void SetRootPage(Page newPage)
    {
        // 在主线程上执行操作，以确保UI线程安全
        Dispatcher.Dispatch(() =>
        {
            // 获取应用程序当前打开的窗口集合中的第一个窗口（通常为主窗口）
            var window = this.Windows.FirstOrDefault();

            // 检查窗口是否为空，防止在窗口未初始化或已关闭时引发空引用异常
            if (window != null)
            {
                // 将窗口的当前页面设置为新传入的页面，完成根页面切换
                window.Page = newPage;
            }
        });
    }

    /// <summary> 领域环境已就绪（正常流）。子类应在此处调用 SetRootPage()。 </summary>
    protected abstract void OnDomainReady();

    /// <summary> 领域环境需要初始化业务参数（引导流）。子类应在此处调用 SetRootPage()。 </summary>
    protected abstract void OnSystemSetupRequired(SystemSetupRequiredException exception);

    /// <summary> 基础设施引发致命错误（崩溃流） </summary>
    protected virtual void OnInfrastructureFailed(Exception exception)
    {
        // 提供默认实现，使用新的 SetRootPage 机制
        SetRootPage(new ContentPage
        {
            Content = new Label
            {
                Text = $"启动失败: {exception.Message}",
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        });
    }
}