# TKWF.Domain.Web - 宿主集成与启动配置设计

**模块名称**：TKWF.Domain.Web  
**目标宿主**：ASP.NET Core（Web API、Minimal API、Blazor Server、Razor Pages 等）  
**依赖项目**：TKWF.Domain（核心框架）  
**版本**：1.2（2026-02）  
**状态**：已实现，待运行验证

## 设计目标

- 实现**一行配置**完成领域初始化 + Web 中间件注册，极大简化 Program.cs。
- 严格保护领域自治：用户身份逻辑（DMPUserHelper）、核心过滤器、领域服务注册完全由领域层控制。
- 表现层仅负责声明用户上下文类型（TUserInfo）和少量 Web 适配配置。
- 通过 `IStartupFilter` 在正确时机自动注册 Web 中间件，避免手动分散配置。
- 支持 SessionManager 等基础设施的有限覆盖（带警告日志）。

## 核心设计原则

1. **领域层完全自决**  
   
   - `DMPUserHelper`（用户身份创建、登录验证）由 `DmpDomainInitializer` 指定。  
   - 核心过滤器（AuthorityFilter 等）在 `DomainHostInitializerBase` 中强制启用。  
   - `SessionManager` 类型由初始化器内部决定（`UseDefaultSessionManager`）。

2. **表现层最小知识**  
   
   - Program.cs 只需看到 `TUserInfo` 和初始化器类型。  
   - 用户帮助类（`DMPUserHelper`）和 `SessionManager` 实现对表现层透明。

3. **一行配置原则**  
   
   - `ConfigTKWDomain` 同时完成 Autofac 切换、领域容器注册、Web 中间件自动注册。  
   - 使用 `IStartupFilter` 在管道构建阶段拿到 `IApplicationBuilder` 执行中间件。

4. **约束机制**  
   
   - 核心领域逻辑不允许表现层覆盖。  
   - 基础设施覆盖时记录 Warning 日志。

## 启动配置入口

### ConfigTKWDomain – 统一配置入口（一行完成）

**位置**：TKWF.Domain.Web 项目

**职责**：切换 Autofac 工厂、注册领域容器、通过 `IStartupFilter` 自动注册 Web 中间件。

```csharp
public static IHostApplicationBuilder ConfigTKWDomain<TUserInfo, TInitializer>(
    this IHostApplicationBuilder builder,
    Action<DomainWebConfigurationOptions<TUserInfo>>? configureWeb = null)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
{
    // 1. 准备 Web 配置选项
    var webOptions = new DomainWebConfigurationOptions<TUserInfo>();
    configureWeb?.Invoke(webOptions);

    // 2. 切换 Autofac 工厂 + 注册领域容器（必须连续执行）
    builder.Host
        .UseServiceProviderFactory(new AutofacServiceProviderFactory())
        .ConfigureContainer<ContainerBuilder>(cb =>
        {
            DomainHost<TUserInfo>.Build<TInitializer>(
                upLevelContainer: cb,
                configuration: builder.Configuration);
        });

    // 3. 注册 IStartupFilter，在管道构建时执行 Web 中间件
    builder.Services.AddSingleton<IStartupFilter>(
        sp => new DomainWebStartupFilter<TUserInfo>(webOptions));

    return builder;
}
```

**典型使用（推荐写法）**

```csharp
// Program.cs 一行完成全部领域 + Web 配置
builder.ConfigTKWDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.UseSessionUserMiddleware = true;
    cfg.UseDomainExceptionMiddleware = true;
    cfg.EnableDomainLogging = true;
    // cfg.SessionManagerType = typeof(RedisSessionManager<DmpUserInfo>); // 覆盖示例
});
```

### 内部实现：DomainWebStartupFilter

```csharp
private sealed class DomainWebStartupFilter<TUserInfo> : IStartupFilter
    where TUserInfo : class, IUserInfo, new()
{
    private readonly DomainWebConfigurationOptions<TUserInfo> _options;

    public DomainWebStartupFilter(DomainWebConfigurationOptions<TUserInfo> options)
    {
        _options = options;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            if (_options.UseSessionUserMiddleware)
                app.UseMiddleware<SessionUserMiddleware<TUserInfo>>();

            if (_options.UseDomainExceptionMiddleware)
                app.UseMiddleware<DomainExceptionMiddleware>();

            // 其他中间件可继续添加（顺序敏感的配置建议留在 Program.cs）

            next(app);
        };
    }
}
```

## 配置选项类

```csharp
public class DomainWebConfigurationOptions<TUserInfo> where TUserInfo : class, IUserInfo, new()
{
    public bool UseSessionUserMiddleware { get; set; } = true;
    public bool UseDomainExceptionMiddleware { get; set; } = true;

    public bool EnableDomainLogging { get; set; }
    public EnumDomainLogLevel LoggingLevel { get; set; } = EnumDomainLogLevel.Minimal;

    public Type? SessionManagerType { get; set; }               // 允许覆盖，带警告
    public Type? ExceptionLoggerFactoryType { get; set; }       // 允许覆盖，带警告

    internal bool AttemptedToOverrideUserHelper { get; set; } = false;
}
```

## 约束与警告机制

| 场景                        | 处理方式                        | 日志级别    |
| ------------------------- | --------------------------- | ------- |
| 尝试覆盖用户帮助类                 | 抛 InvalidOperationException | —       |
| 覆盖 SessionManager         | 允许 + 记录 Warning 日志          | Warning |
| 未配置 SessionUserMiddleware | 默认开启                        | —       |
| 核心 Filter 被尝试关闭           | 领域层强制启用，不可覆盖                | —       |

## Program.cs 完整示例

```csharp
var builder = WebApplication.CreateBuilder(args);

// 一行完成领域初始化 + Web 中间件注册
builder.ConfigTKWDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.EnableDomainLogging = true;
    cfg.UseSessionUserMiddleware = true;
    cfg.UseDomainExceptionMiddleware = true;
});

// 其他服务注册
builder.Services.AddGraphQLServer()...;
builder.Services.AddCors(...);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseCors(...);
app.MapGraphQL();

app.Run();
```

## 后续扩展方向

- 支持更多宿主（Blazor、MAUI、Console）类似一行配置入口
- 在 `DomainWebConfigurationOptions` 中增加更多 Web 专属配置（如 CORS 策略选择、ClaimsPrincipal 映射等）
- 完善 SessionManager 覆盖的完整实现（容器替换逻辑）

此文档为独立模块文件，专注于 Web 宿主集成机制。
