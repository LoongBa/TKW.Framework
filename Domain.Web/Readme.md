# TKWF.Domain.Web 配置教程

**一行配置 + 分阶段管道构建**（推荐模板）

### 1. 设计目标

这个模板的目标是让 **Program.cs** 保持**极简、可读、可维护**，同时满足以下要求：

- 领域层与表现层清晰分离
- 路由阶段显式、可控（自动或手动）
- 服务注册（IServiceCollection）与管道配置（IApplicationBuilder）彻底分离
- 异常捕获最前、路由后逻辑可控
- 未来扩展方便（Blazor、MAUI、Worker 等）

---

### 2. 配置选项详解（DomainWebConfigurationOptions）

| 参数                             | 类型   | 默认值   | 说明                                            | 推荐场景            |
| ------------------------------ | ---- | ----- | --------------------------------------------- | --------------- |
| `UseDomainExceptionMiddleware` | bool | true  | 是否启用框架统一的 JSON 异常响应中间件                        | 生产环境强烈推荐        |
| `UseSessionUserMiddleware`     | bool | true  | 是否启用 SessionUserMiddleware（注入 DomainUser）     | 几乎所有项目都必须开启     |
| `EnableDomainLogging`          | bool | false | 是否启用领域层 AOP 日志过滤器                             | 开发/调试时开启        |
| `SuppressRoutingWarning`       | bool | false | 是否关闭“未选择路由阶段”的启动警告                            | 特殊无路由项目可设为 true |
| `~~EnableAspNetCoreRouting~~`  | bool | false | **已废弃**不再使用 → 改用显式调用 `UseAspNetCoreRouting()` | —               |

---

### 3. 管道阶段详解（最重要）

| 方法                        | 执行时机        | 典型用途                                 | 是否必须 | 备注         |
| ------------------------- | ----------- | ------------------------------------ | ---- | ---------- |
| `.RegisterServices(...)`  | 立即执行（容器构建期） | 注册 CORS、GraphQL、FastEndpoints 等服务    | 推荐   | 服务注册必须在此阶段 |
| `.BeforeRouting(...)`     | 管道最前        | 异常页、CORS、静态文件等                       | 推荐   | 异常捕获必须最前   |
| `.UseAspNetCoreRouting()` | 路由分水岭       | 自动插入 `app.UseRouting()`              | 二选一  | 最常用        |
| `.UseCustomRouting(...)`  | 路由分水岭       | 手动控制路由顺序                             | 二选一  | 需要特殊顺序时使用  |
| `.AfterRouting(...)`      | 路由之后        | 授权、端点映射（MapGraphQL、UseFastEndpoints） | 推荐   | 端点映射放这里    |

---

### 4. 完整推荐范本

```csharp
using DMP_Lite.Domain;
using DMP_Lite.WebApi.GraphQL;
using FastEndpoints;
using TKW.Framework.Domain.Web;

var builder = WebApplication.CreateBuilder(args);

// ───────────────────────────────────────────────────────────────
// 1. 配置加载（严格优先级顺序）
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// ───────────────────────────────────────────────────────────────
// 2. 领域 + Web 管道配置（核心部分）
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.EnableDomainLogging = true;
    cfg.UseSessionUserMiddleware = true;      // 必须
    cfg.UseDomainExceptionMiddleware = true;  // 强烈推荐
})
.RegisterServices(services =>
{
    // 服务注册（必须在此阶段完成）
    services.AddCors(options =>
    {
        options.AddPolicy("DevelopmentPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        options.AddPolicy("ProductionPolicy", p => p
            .WithOrigins("https://your-frontend-domain.com", "https://*.yourdomain.com")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });

    services.AddGraphQLServer()
        .AddQueryType<MerchantQuery>()
        .AddFiltering().AddSorting().AddProjections()
        .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

    // services.AddFastEndpoints();
})
.BeforeRouting(app =>
{
    // 路由之前（最前）
    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

    var corsPolicy = app.Environment.IsDevelopment() ? "DevelopmentPolicy" : "ProductionPolicy";
    app.UseCors(corsPolicy);
})
.UseAspNetCoreRouting()                    // 推荐：自动 UseRouting
.AfterRouting(app =>
{
    // 路由之后
    app.UseAuthorization();
    app.MapGraphQL();
    // app.UseFastEndpoints();
});

var app = builder.Build();
app.Run();
```

---

### 5. 各种场景示例

#### 场景 1：标准 WebAPI + GraphQL（最常见）

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg => { ... })
    .RegisterServices(services => { ... })
    .BeforeRouting(app => { ... })
    .UseAspNetCoreRouting()
    .AfterRouting(app =>
    {
        app.UseAuthorization();
        app.MapGraphQL();
    });
```

#### 场景 2：需要自定义路由顺序（混合 FastEndpoints + GraphQL）

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg => { ... })
    .RegisterServices(services => { ... })
    .BeforeRouting(app => { ... })
    .UseCustomRouting(app =>
    {
        app.UseRouting();
        app.UseAuthorization();
        // 自定义中间件
    })
    .AfterRouting(app =>
    {
        app.MapGraphQL();
        app.UseFastEndpoints();
    });
```

#### 场景 3：完全不使用 ASP.NET Core 路由（极少）

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg => { ... })
    .RegisterServices(services => { ... })
    .BeforeRouting(app => { ... })
    // 不调用 UseAspNetCoreRouting 也不调用 UseCustomRouting
    .AfterRouting(app => { ... });
```

#### 场景 4：生产环境最小配置

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.UseSessionUserMiddleware = true;
    cfg.UseDomainExceptionMiddleware = true;
    cfg.SuppressRoutingWarning = true;   // 关闭警告
})
.RegisterServices(services => { ... })
.BeforeRouting(app => { app.UseCors("ProductionPolicy"); })
.UseAspNetCoreRouting()
.AfterRouting(app =>
{
    app.UseAuthorization();
    app.MapGraphQL();
});
```

---

### 6. 完整注释范本

```csharp
using DMP_Lite.Domain;
using DMP_Lite.WebApi.GraphQL;
using TKW.Framework.Domain.Web;

var builder = WebApplication.CreateBuilder(args);

// ───────────────────────────────────────────────────────────────
// 1. 配置加载（优先级顺序严格遵守官方推荐）
// ───────────────────────────────────────────────────────────────
// 核心原则：基础配置 → 环境覆盖 → 用户秘密 → 环境变量（最高优先级）
// 注意：appsettings.{Env}.json 是可选的，不要强制要求存在
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()           // 仅开发环境生效，生产环境忽略
    .AddEnvironmentVariables();           // 生产环境变量覆盖一切

// ───────────────────────────────────────────────────────────────
// 2. 领域层初始化 + Web 管道分阶段配置（核心一行）
// ───────────────────────────────────────────────────────────────
// 设计要点：
//   - ConfigTkwDomain 是整个框架的统一入口
//   - 通过 cfg 传递必要信息给领域层（IsDevelopment、ConnectionString 等）
//   - 领域层会在 OnPreInitialize 中进行守门员检查与修正
//   - 后续分阶段回调全部延迟到 app.Build() 后执行（通过 IStartupFilter 机制）
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    // 开发/生产环境标识（领域层需要此信息来决定日志级别、异常工厂等）
    cfg.IsDevelopment = builder.Environment.IsDevelopment();

    // 连接字符串（领域层必须使用此值初始化 FreeSql 或其他 ORM）
    // 注意：这里直接从 Configuration 获取，未来可改为注入 IOptions<DbSettings>
    cfg.ConnectionString = builder.Configuration
        .GetConnectionString("DefaultConnection") ?? string.Empty;

    // 领域层可观测性开关（默认关闭，显式开启）
    cfg.EnableDomainLogging = true;

    // Web 层核心中间件（默认开启，建议始终开启）
    cfg.UseSessionUserMiddleware = true;      // 必须开启，用于注入 DomainUser
    cfg.UseDomainExceptionMiddleware = true;  // 生产环境统一异常响应
})
// 服务注册阶段（独立于管道，立即执行）
// 设计要点：所有 IServiceCollection 注册必须在此阶段完成
// 原因：服务注册必须在容器构建阶段完成，不能延迟到管道
.RegisterServices(services =>
{
    // CORS 策略（开发/生产分离，推荐始终配置）
    services.AddCors(options =>
    {
        options.AddPolicy("DevelopmentPolicy", p => p
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());

        options.AddPolicy("ProductionPolicy", p => p
            .WithOrigins("https://your-frontend-domain.com", "https://*.yourdomain.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    });

    // GraphQL 配置（HotChocolate）
    services.AddGraphQLServer()
        .AddQueryType<MerchantQuery>()
        .AddFiltering()
        .AddSorting()
        .AddProjections()
        .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

    // FastEndpoints（按需启用，建议在 AfterRouting 中注册 UseFastEndpoints）
    // services.AddFastEndpoints();
})
// ───────────────────────────────────────────────────────────────
// 3. 管道分阶段配置（延迟到 app.Build() 后执行）
// ───────────────────────────────────────────────────────────────
// 阶段顺序：BeforeRouting → UseAspNetCoreRouting → AfterRouting
// 所有中间件注册都在此阶段完成，确保顺序可控
.BeforeRouting(app =>
{
    // 最前：开发异常页面（必须捕获所有后续异常）
    // 位置要点：放在 BeforeRouting 最前面，甚至可以独立成 Phase0
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    // CORS 必须在路由之前（处理 OPTIONS 预检请求）
    var corsPolicy = app.Environment.IsDevelopment() ? "DevelopmentPolicy" : "ProductionPolicy";
    app.UseCors(corsPolicy);

    // 可选：其他路由前中间件（如静态文件、认证等）
    // app.UseStaticFiles();
    // app.UseAuthentication();
})
.UseAspNetCoreRouting()  // 显式调用 → 自动插入 app.UseRouting()
.AfterRouting(app =>
{
    // 路由之后：授权、端点映射等
    app.UseAuthorization();

    // 端点映射（GraphQL、FastEndpoints 等）
    app.MapGraphQL();

    // FastEndpoints（如果启用，应放在这里）
    // app.UseFastEndpoints();
});

// ───────────────────────────────────────────────────────────────
// 4. 构建并启动应用
// ───────────────────────────────────────────────────────────────
// 注意：这里不再需要手动写 UseRouting、MapGraphQL 等，
// 因为它们已被 DomainPipelineBuilder 通过 IStartupFilter 注入
var app = builder.Build();

// 5. 启动
app.Run();

// ───────────────────────────────────────────────────────────────
// 未来扩展要点总结（写在文件末尾，作为长期记忆）
// ───────────────────────────────────────────────────────────────
// 1. 新增服务注册 → 放在 .RegisterServices() 中（立即执行）
// 2. 新增路由前中间件 → 放在 .BeforeRouting() 中
// 3. 需要自定义路由顺序 → 使用 .UseCustomRouting(app => { ... })
// 4. 端点映射（GraphQL/FastEndpoints） → 放在 .AfterRouting() 中
// 5. 异常处理策略调整 → 通过 cfg.UseDomainExceptionMiddleware 控制
// 6. 关闭路由警告 → cfg.SuppressRoutingWarning = true
// 7. 跨宿主扩展（Blazor/MAUI）→ 新建对应适配项目，复用类似链式结构
```

### 7. 最佳实践与注意事项

- **路由阶段必须显式选择**：不调用 `UseAspNetCoreRouting()` 或 `UseCustomRouting()` 会触发警告。
- **CORS 必须在 BeforeRouting**：否则 OPTIONS 预检请求可能失败。
- **UseAuthorization() 必须在 AfterRouting**：因为它依赖路由信息。
- **服务注册只能在 RegisterServices**：不能放在管道回调中（时机不对）。
- **开发环境**：强烈建议开启 `UseDeveloperExceptionPage()`（放在 BeforeRouting 最前）。
- **生产环境**：关闭 `EnableDomainLogging`，开启 `UseDomainExceptionMiddleware`。
