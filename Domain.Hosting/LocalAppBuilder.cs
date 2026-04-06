using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// 跨平台控制台或桌面应用专用构建器
/// 负责非 Web 环境下的服务注册、本地加密会话配置及领域主机初始化。
/// </summary>
/// <typeparam name="TUserInfo">用户信息类型</typeparam>
/// <typeparam name="TInitializer">领域初始化器类型</typeparam>
public class LocalAppBuilder<TUserInfo, TInitializer>(
    HostApplicationBuilderAdapter builder, DomainOptions options)
    : DomainAppBuilderBase<LocalAppBuilder<TUserInfo, TInitializer>, DomainOptions>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    /// <summary>
    /// 完成配置并执行初始化，返回领域主机实例。
    /// </summary>
    /// <returns>初始化完成的 DomainHost 实例</returns>
    /// <exception cref="DomainException">当初始化失败或容器构建异常时抛出</exception>
    public DomainHost<TUserInfo> Build()
    {
        // 1. 注册核心领域主机逻辑到 Autofac 容器
        ConfigureContainer((cb, _) =>
        {
            DomainHost<TUserInfo>.Initialize<TInitializer>(cb, builder.Configuration, Options);
        });

        // 2. 触发外部宿主构建逻辑
        // 由于 Adapter 封装了不同的宿主（如 HostApplicationBuilder 或其他）
        // 此处通过动态调用触发其 Build 行为以冻结服务容器
        builder.Build();

        // 3. 返回静态单例入口
        return DomainHost<TUserInfo>.Root ?? throw new DomainException("DomainHost 初始化失败，未能正确创建静态实例。");
    }

    /// <summary>
    /// 显式声明不使用任何会话管理（纯净/无状态模式）
    /// 将自动注入 <see cref="StatelessSessionManager{TUserInfo}"/> 以阻断任何磁盘持久化行为。
    /// </summary>
    public LocalAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        // 调用基类保护方法注册无状态管理器
        UseSessionManagerInternal<TUserInfo, StatelessSessionManager<TUserInfo>>();
        return this;
    }

    /// <summary>
    /// 开启本地加密会话（适用于桌面应用/控制台工具）
    /// 自动配置 DataProtection 并将密钥存储在本地用户目录。
    /// </summary>
    /// <param name="applicationName">应用唯一名称，用于隔离不同应用的加密密钥</param>
    public LocalAppBuilder<TUserInfo, TInitializer> UseLocalSession(string applicationName)
    {
        // 1. 设置参数，确保核心层后续逻辑（如日志前缀）一致
        Options.ApplicationName = applicationName;

        // 2. 配置本地数据保护（DataProtection）
        // 确保跨平台（Windows/Linux/macOS）下的密钥持有与加密能力
        this.RegisterServices((services, options) =>
        {
            var keyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                applicationName,
                "Keys");

            services.AddDataProtection()
                .SetApplicationName(applicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        });

        // 3. 注入 LocalSessionManager 占据坑位
        // 内部会利用注入的 IDataProtectionProvider 执行加密持久化
        UseSessionManagerInternal<TUserInfo, LocalSessionManager<TUserInfo>>();
        return this;
    }

    /// <summary>使用指定的会话管理器（定制完整的会话管理器）</summary>
    /// <typeparam name="TUserInfo"></typeparam>
    /// <typeparam name="TSessionManager"></typeparam>
    public LocalAppBuilder<TUserInfo, TInitializer> UseSessionManager<TSessionManager>()
        where TSessionManager : ISessionManager<TUserInfo>
    {
        UseSessionManagerInternal<TUserInfo, TSessionManager>();
        return this;
    }

    /// <summary>使用指定的会话管理器（定制完整的会话管理器）</summary>
    public LocalAppBuilder<TUserInfo, TInitializer> UseSessionManager(ISessionManager<TUserInfo> instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        UseSessionManagerInternal(instance);
        return this;
    }
}