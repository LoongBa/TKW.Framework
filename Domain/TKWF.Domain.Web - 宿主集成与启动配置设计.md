# TKWF.Domain.Web - 宿主集成与启动配置设计

**模块名称**：TKWF.Domain.Web  
**目标宿主**：ASP.NET Core（Web API、Minimal API、Blazor Server、Razor Pages 等）  
**依赖项目**：TKWF.Domain（核心框架）  
**版本**：1.3（2026-02-13）  
**状态**：已实现，可编译通过

## 设计目标

- 通过一行配置 + 分阶段链式回调，极大简化 Program.cs 中的管道搭建代码。
- 严格保护领域自治：表现层配置通过 DomainOptions 传递，领域层可进行“守门员”修正。
- 路由阶段作为显式、可选的分界点，支持标准路由（自动插入 UseRouting）或自定义路由（手动控制）。
- 未显式选择路由阶段时，通过警告日志提醒开发者（可关闭）。
- 服务注册（IServiceCollection）与管道配置（IApplicationBuilder）完全分离，避免混杂。
- 提供清晰的三段管道结构，确保异常捕获最前、路由后逻辑可控。

## 核心设计原则

1. **管道三段结构**  
   
   - **BeforeRouting**：路由之前（异常捕获、开发者异常页、CORS、静态文件等）  
   - **Routing阶段**：路由分水岭（显式调用 UseAspNetCoreRouting 或 UseCustomRouting）  
   - **AfterRouting**：路由之后（授权、端点映射、Fallback 等）

2. **路由阶段互斥与显式原则**  
   
   - 必须显式调用 `UseAspNetCoreRouting()` 或 `UseCustomRouting()` 中的一个。  
   - 同时调用 → 抛 `InvalidOperationException`。  
   - 都不调用 → 输出警告日志（可通过 `SuppressRoutingWarning = true` 关闭）。  
   - `UseAspNetCoreRouting()`：框架自动插入 `app.UseRouting()`。  
   - `UseCustomRouting()`：开发者在回调中手动写入 `app.UseRouting()` 或其他自定义路由逻辑（回调可为空，仅用于消除警告）。

3. **服务注册独立**  
   
   - 使用 `.RegisterServices(...)` 分段，专门处理 IServiceCollection 注册（CORS、GraphQL、FastEndpoints 等）。  
   - 与管道配置分离，确保服务注册在容器构建阶段完成。

4. **领域层守门员机制**  
   
   - 表现层通过 `DomainOptions` 传递配置。  
   - 领域层在 `OnPreInitialize` 中可强制修正或拒绝不安全配置。

## 启动配置入口

### ConfigTkwDomain – 统一配置入口

位置：TKWF.Domain.Web 项目

签名：

```csharp
public static DomainPipelineBuilder ConfigTkwDomain<TUserInfo, TInitializer>(
    this WebApplicationBuilder builder,
    Action<DomainWebConfigurationOptions>? configure = null)
    where TUserInfo : class, IUserInfo, new()
    where TInitializer : DomainHostInitializerBase<TUserInfo>, new()
```

**典型使用（推荐写法）**

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.EnableDomainLogging = true;
    cfg.UseSessionUserMiddleware = true;
    cfg.UseDomainExceptionMiddleware = true;
})
.RegisterServices(services =>
{
    // 服务注册（独立阶段）
    services.AddCors(opt =>
    {
        opt.AddPolicy("DevelopmentPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    });
    services.AddGraphQLServer()
        .AddQueryType<MerchantQuery>()
        .AddFiltering().AddSorting().AddProjections();
})
.BeforeRouting(app =>
{
    // 路由之前：异常页、CORS 等
    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

    app.UseCors("DevelopmentPolicy");
})
.UseAspNetCoreRouting()           // 自动插入 app.UseRouting()
.AfterRouting(app =>
{
    // 路由之后：授权、端点映射等
    app.UseAuthorization();
    app.MapGraphQL();
    // app.UseFastEndpoints();
});
```

**自定义路由示例**

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg => { ... })
.RegisterServices(services => { ... })
.BeforeRouting(app => { ... })
.UseCustomRouting(app =>
{
    app.UseRouting();           // 手动
    // 可插入自定义路由中间件或特殊顺序
    app.UseAuthorization();
})
.AfterRouting(app =>
{
    app.MapGraphQL();
});
```

**不使用路由（极少场景）**

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg => { ... })
.RegisterServices(services => { ... })
.BeforeRouting(app => { ... })
.AfterRouting(app => { ... });
// 未调用任何路由阶段 → 触发警告（可关闭）
```

## DomainPipelineBuilder – 分阶段构建器

```csharp
public class DomainPipelineBuilder
{
    public DomainPipelineBuilder BeforeRouting(Action<WebApplication> action) { ... }
    public DomainPipelineBuilder UseAspNetCoreRouting() { ... }           // 自动 UseRouting
    public DomainPipelineBuilder UseCustomRouting(Action<WebApplication> action) { ... }
    public DomainPipelineBuilder AfterRouting(Action<WebApplication> action) { ... }
    public DomainPipelineBuilder RegisterServices(Action<IServiceCollection> action) { ... }
}
```

## DomainWebConfigurationOptions – 配置选项

```csharp
public class DomainWebConfigurationOptions : DomainOptions
{
    public bool UseDomainExceptionMiddleware { get; set; } = true;
    public bool UseSessionUserMiddleware { get; set; } = true;
    public bool EnableDomainLogging { get; set; } = false;

    public bool SuppressRoutingWarning { get; set; } = false;   // 关闭路由警告
    internal bool HasRoutingPhase { get; set; } = false;        // 内部标记是否显式选择了路由阶段
}
```

## 路由阶段互斥与警告机制

- **同时调用** `UseAspNetCoreRouting` 和 `UseCustomRouting` → 抛 `InvalidOperationException`
- **都不调用** → `RoutingWarningHostedService` 输出警告（可通过 `SuppressRoutingWarning = true` 关闭）
- **调用其中一个** → 视为已确认，消除警告（`HasRoutingPhase = true`）

## 中间件注册顺序建议（推荐模板）

```csharp
var app = builder.Build();

// 1. BeforeRouting 自动执行（异常捕获最前）

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

// 2. 路由阶段（显式调用 UseAspNetCoreRouting 或 UseCustomRouting）

// 3. AfterRouting 自动执行（SessionUserMiddleware 等）

app.UseCors(...);
app.UseAuthorization();
app.MapGraphQL();

app.Run();
```

## 后续扩展方向

- Blazor Server：扩展 `UseBlazorRouting()` 等专用阶段
- MAUI / Console：提供对应宿主适配器
- 更多基础设施开关（CORS 策略、Authentication 等）

此文档为独立模块文件，专注于 Web 宿主集成机制，与核心框架设计文档互补。

---

**修正与补充说明**：

- 已完整补充 `.RegisterServices(...)` 分段，并将其作为服务注册的独立阶段。
- `RoutingWarningHostedService` 已使用 `_Options.HasRoutingPhase` 判断（通过 `DomainPipelineBuilder` 中的 `_HasRoutingPhase` 同步到 `_Options`）。
- 文档中明确了三段结构、路由互斥规则、警告机制，并以最新代码为准。

如果需要进一步完善某个部分（例如增加更多开关、调整警告文案、或补充 Blazor 示例），请告诉我。
