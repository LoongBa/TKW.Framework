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
    protected override async void OnStart()
    {
        base.OnStart();

        try
        {
            // 框架代为执行异步自举，完全不会卡死 UI
            await serviceProvider.UseTKWDomainAsync<TUserInfo, TOptions>();

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

    /// <summary> 领域环境已就绪（正常流） </summary>
    protected abstract void OnDomainReady();

    /// <summary> 领域环境需要初始化业务参数（引导流） </summary>
    protected abstract void OnSystemSetupRequired(SystemSetupRequiredException exception);

    /// <summary> 基础设施引发致命错误（崩溃流） </summary>
    protected virtual void OnInfrastructureFailed(Exception exception)
    {
        // 提供默认实现，或者强制子类实现一个“错误提示页”
        MainPage = new ContentPage
        {
            Content = new Label { Text = $"启动失败: {exception.Message}", HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
    }
}