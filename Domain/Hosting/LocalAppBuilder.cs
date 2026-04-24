using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.DataProtection;
using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;
using TKW.Framework.Domain.Session;

namespace TKW.Framework.Domain.Hosting;

/// <summary>
/// 跨平台控制台或桌面应用专用构建器 (V4 标准 DI 版)
/// </summary>
public class LocalAppBuilder<TUserInfo, TInitializer>(
    IDomainAppBuilderAdapter builder, DomainOptions options)
    : DomainAppBuilderBase<LocalAppBuilder<TUserInfo, TInitializer>, DomainOptions, TUserInfo>(builder, options)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    public DomainHost<TUserInfo> Build()
    {
        // 1. V4 架构下，初始化已在 AddDomain 阶段完成，此处主要负责触发宿主构建逻辑
        // 如果 Adapter 包装了 IHostApplicationBuilder，则调用其 Build
        if (Builder is IHostApplicationBuilderAdapter hostAdapter)
        {
            hostAdapter.Build();
        }

        return DomainHost<TUserInfo>.Root ?? throw new DomainException("DomainHost 初始化失败。");
    }

    public LocalAppBuilder<TUserInfo, TInitializer> NoSession()
    {
        UseSessionManager<StatelessSessionManager<TUserInfo>>();
        return this;
    }

    public LocalAppBuilder<TUserInfo, TInitializer> UseLocalSession(string applicationName)
    {
        Options.ApplicationName = applicationName;

        // 配置 DataProtection
        RegisterServices((services, options) =>
        {
            var keyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                applicationName,
                "Keys");

            var dpBuilder = services.AddDataProtection()
                .SetApplicationName(applicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath));

            // 关键修复：仅在 Windows 上使用 DPAPI 加密静态密钥
            if (OperatingSystem.IsWindows())
            {
                dpBuilder.ProtectKeysWithDpapi();
            }
            // 在非 Windows 平台上，你可以选择：
            // 1. 不调用任何保护（依赖文件系统权限）
            // 2. 或者使用 X.509 证书进行保护（跨平台方案）
            // .ProtectKeysWithCertificate("thumbprint");

            // 注册本地会话管理器
            services.Replace(ServiceDescriptor.Singleton<ISessionManager<TUserInfo>>(sp =>
                ActivatorUtilities.CreateInstance<LocalSessionManager<TUserInfo>>(sp, applicationName)));
        });

        return this;
    }
}