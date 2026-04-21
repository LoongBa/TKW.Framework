# TKWF.Domain.Web 配置教程

**常见 Web 项目示范**（WebAPI + Blazor Server）

本教程以 AdminUI 项目为实际范本，分为 **WebAPI 项目** 和 **Blazor Server 项目** 两大部分。

核心理念：**一行配置 + 分阶段管道**，让 Program.cs 极简、可读、可维护，同时严格保护领域自治。

---

### 1. WebAPI 项目配置范本（采用 GraphQL + FastEndpoints）

**适用场景**：纯 REST + GraphQL 的 WebAPI 项目

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
// 2. 领域层 + Web 管道配置（一行完成）
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.EnableDomainLogging = true;
    cfg.UseSessionUserMiddleware = true;      // 必须：注入 DomainUser
    cfg.UseDomainExceptionMiddleware = true;  // 生产环境统一 JSON 异常响应
    cfg.IsDevelopment = builder.Environment.IsDevelopment();
    cfg.ConnectionString = builder.Configuration
        .GetConnectionString("DefaultConnection") ?? string.Empty;
})
.RegisterServices(services =>
{
    // CORS 策略（开发/生产分离）
    services.AddCors(options =>
    {
        options.AddPolicy("DevelopmentPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        options.AddPolicy("ProductionPolicy", p => p
            .WithOrigins("https://your-frontend-domain.com", "https://*.yourdomain.com")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });

    // GraphQL（HotChocolate）
    services.AddGraphQLServer()
        .AddQueryType<MerchantQuery>()
        .AddFiltering().AddSorting().AddProjections()
        .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

    // FastEndpoints（按需开启）
    services.AddFastEndpoints();
})
.BeforeRouting(app =>
{
    // 路由之前（最前）
    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();

    var corsPolicy = app.Environment.IsDevelopment() ? "DevelopmentPolicy" : "ProductionPolicy";
    app.UseCors(corsPolicy);
})
.UseAspNetCoreRouting()                    // 自动 UseRouting
.AfterRouting(app =>
{
    // 路由之后
    app.UseAuthorization();
    app.MapGraphQL();
    app.UseFastEndpoints();                // FastEndpoints 端点映射
});

var app = builder.Build();
app.Run();
```

**配置项作用与扩展建议**

- `EnableDomainLogging`：开启领域 AOP 日志（开发时推荐开启）
- `UseSessionUserMiddleware`：必须开启（注入 DomainUser）
- `UseDomainExceptionMiddleware`：生产环境统一异常响应（推荐开启）
- `RegisterServices`：所有服务注册必须在此（CORS、GraphQL、FastEndpoints 等）
- `BeforeRouting`：路由前中间件（异常页、CORS）
- `UseAspNetCoreRouting`：自动插入 UseRouting（推荐）
- `AfterRouting`：路由后中间件（授权、端点映射）

**扩展建议**：

- 新增 OpenTelemetry：加到 `.RegisterServices`
- 自定义认证：加到 `.AfterRouting` 开头

---

### 2. Blazor Server 项目配置范本（不采用 GraphQL、FastEndpoints）

**适用场景**：AdminUI 项目（Blazor Server + Ant Design ProLayout）

```csharp
using DMP_Lite.Domain;
using TKW.Framework.Domain.Web;
using AntDesign.ProLayout;
using DMP_Lite.AdminUI.Services;
using Utils;

var builder = WebApplication.CreateBuilder(args);

// ───────────────────────────────────────────────────────────────
// 1. 配置加载（严格优先级顺序）
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// ───────────────────────────────────────────────────────────────
// 2. 领域层 + Web 管道配置（一行完成所有核心配置）
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.EnableDomainLogging = true;
    cfg.UseSessionUserMiddleware = true;      // 必须开启：注入 DomainUser
    cfg.UseDomainExceptionMiddleware = true;  // 生产环境统一 JSON 异常响应
    cfg.IsDevelopment = builder.Environment.IsDevelopment();
    cfg.ConnectionString = builder.Configuration
        .GetConnectionString("DefaultConnection") ?? string.Empty;
})
.RegisterServices(services =>
{
    // Blazor Server + Ant Design + Razor Pages
    services.AddRazorPages();
    services.AddServerSideBlazor();
    services.AddAntDesign();

    // ProLayout 配置
    services.Configure<ProSettings>(builder.Configuration.GetSection("ProSettings"));

    // 本地化 + 交互字符串
    services.AddLocalization();
    services.AddInteractiveStringLocalizer();

    // 业务服务（Scoped，仅用于演示数据）
    services.AddScoped<IChartService, ChartService>();
    services.AddScoped<IProjectService, ProjectService>();
    services.AddScoped<IUserService, UserService>();
    services.AddScoped<IAccountService, AccountService>();
    services.AddScoped<IProfileService, ProfileService>();

    // CORS 策略（开发/生产分离）
    services.AddCors(options =>
    {
        options.AddPolicy("DevelopmentPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        options.AddPolicy("ProductionPolicy", p => p
            .WithOrigins("https://your-frontend-domain.com", "https://*.yourdomain.com")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });
})
.BeforeRouting(app =>
{
    // ── BeforeRouting：路由之前执行（最前阶段） ──
    // 开发环境异常页面（必须最前，捕获所有后续异常）
    if (app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage();
    else // 生产环境异常处理（页面重定向）
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts(); // 默认 30 天 HSTS
    }

    // HTTPS 重定向（放在路由前更安全）
    app.UseHttpsRedirection();

    // 静态文件（放在路由前，避免路由拦截静态资源）
    app.UseStaticFiles();

    // CORS（必须在路由之前，处理 OPTIONS 预检请求）
    var corsPolicy = app.Environment.IsDevelopment() ? "DevelopmentPolicy" : "ProductionPolicy";
    app.UseCors(corsPolicy);
})
.UseAspNetCoreRouting() // 自动插入 app.UseRouting()
.AfterRouting(app =>
{
    // ── AfterRouting：路由之后执行 ──
    // 授权（必须在端点映射之前）
    app.UseAuthorization();

    // Blazor Server 路由（固定写法，放在最后）
    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    // 首次请求追踪（放在最后，确保捕获所有请求）
    app.UseFirstRequestTrackerAsync(removeAfterCapture: true);
});

// ───────────────────────────────────────────────────────────────
// 3. 构建并启动应用
// ───────────────────────────────────────────────────────────────
// 注意：这里不再需要任何手动中间件注册，所有逻辑已迁移到分阶段回调中
var app = builder.Build();
app.Run();
```

### 3. 配置项作用与实用扩展建议

| 配置项                            | 作用                          | 实用扩展建议                |
| ------------------------------ | --------------------------- | --------------------- |
| `EnableDomainLogging`          | 开启领域 AOP 日志过滤器              | 开发环境 true，生产环境 false  |
| `UseSessionUserMiddleware`     | 注入 DomainUser 到 HttpContext | 几乎所有项目必须 true         |
| `UseDomainExceptionMiddleware` | 生产环境统一 JSON 异常响应            | 生产环境必须 true           |
| `IsDevelopment`                | 传递开发环境标识给领域层                | 必须设置，用于领域层守门员逻辑       |
| `ConnectionString`             | 传递数据库连接字符串给领域层              | 必须设置，领域层初始化 FreeSql 等 |
| `SuppressRoutingWarning`       | 关闭路由阶段未选择的警告                | 纯静态文件项目可设为 true       |

**RegisterServices**：所有服务注册必须在此阶段（CORS、Blazor、AntDesign、业务服务等）

**BeforeRouting**：路由之前（异常页、CORS、HTTPS、静态文件）

**UseAspNetCoreRouting**：自动插入 UseRouting（推荐）

**AfterRouting**：路由之后（授权、Blazor Hub、Fallback、首次请求追踪）

### 4. 不同场景示范

**场景 1：完整 Blazor Server（推荐）**

使用上面的完整范本即可。

**场景 2：纯 WebAPI 项目（不含 Blazor）**

```csharp
.BeforeRouting(app => { ... })
.UseAspNetCoreRouting()
.AfterRouting(app =>
{
    app.UseAuthorization();
    app.MapGraphQL();
    app.UseFastEndpoints();
});
```

**场景 3：关闭框架异常中间件（使用官方异常页）**

```csharp
cfg.UseDomainExceptionMiddleware = false;
```

然后在 BeforeRouting 中手动添加：

```csharp
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");
```

**场景 4：生产环境最小配置**

```csharp
cfg.EnableDomainLogging = false;
cfg.SuppressRoutingWarning = true;
```

**场景 5：自定义路由顺序**

```csharp
.UseCustomRouting(app =>
{
    app.UseRouting();
    app.UseSomeCustomMiddleware();
    app.UseAuthorization();
})
```
