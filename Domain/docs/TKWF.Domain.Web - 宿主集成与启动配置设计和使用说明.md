# TKWF.Domain.Web - 宿主集成与启动配置设计和使用说明 V1.3

## 1. 设计目标

TKWF.Domain.Web 是 TKWF.Domain 框架的 Web 宿主扩展，负责处理与 HTTP 协议相关的一切逻辑，保持核心框架的绝对纯净。

---

## 2. 核心设计原则

**协议层与领域层完全解耦**：Web 层不包含任何业务逻辑，仅作为会话提供者和调用入口。

**三段式管道编排**：明确分离 BeforeRouting、Routing 和 AfterRouting，保证中间件执行顺序的绝对确定性。

**异常映射职责**：Web 层负责将领域异常转换为 HTTP 协议语义（状态码、JSON 响应等）。

---

## 3. 启动配置入口

### 3.1 ConfigTkwDomain 统一配置入口

应用启动时，通过统一的扩展入口 ConfigTkwDomain 即可完成领域容器的初始化与宿主管道的有序编排。

```csharp
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.UseDomainExceptionMiddleware = true;
    cfg.EnableDomainLogging = true;
})
```

### 3.2 DomainPipelineBuilder 分阶段构建器

框架提供分阶段的管道构建器，严格保护三段式结构：

```csharp
.RegisterServices(services => { /* 注册服务 */ })
.BeforeRouting(app => { /* 路由前逻辑 */ })
.UseAspNetCoreRouting()
.AfterRouting(app => { /* 路由后逻辑 */ })
```

#### 1）典型的 Blazor Web 配置

```csharp
using DMP_Lite.Domain;
using TKW.Framework.Domain.Web

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
    cfg.UseWebExceptionMiddleware = true;  // 生产环境统一 JSON 异常响应
    cfg.IsDevelopment = builder.Environment.IsDevelopment();
    cfg.ConnectionString = builder.Configuration
        .GetConnectionString("PostgreSQL") ?? string.Empty;
    /*cfg.BindOptions(builder).DomainOptions("TKWDomain");
    cfg.BindOptions(builder).BusinessOptions<DmpOptions>("DmpOptions");*/
}).RegisterServices((services, _) =>
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

    // 领域自治：业务服务在领域层注册，不应在表现层注册。

    // CORS 策略（开发/生产分离）
    services.AddCors(options =>
    {
        options.AddPolicy("DevelopmentPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        options.AddPolicy("ProductionPolicy", p => p
            .WithOrigins("https://your-frontend-domain.com", "https://*.yourdomain.com")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });
})
.BeforeRouting((app, options) =>
{
    // ── BeforeRouting：路由之前执行（最前阶段） ──
    // 位置要点：这里是管道最前端，适合捕获所有异常、预处理请求

    // 开发环境异常页面（必须最前，捕获所有后续异常）
    if (options.IsDevelopment) app.UseDeveloperExceptionPage();
    else {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }
    app.UseHttpsRedirection(); // 自动插入 app.UseRouting()
    app.UseStaticFiles(); // 必须在路由前，确保静态资源快速响应
    app.UseCors(options.IsDevelopment ? "Dev" : "Prod");
})
.UseAspNetCoreRouting()
.AfterRouting((app, _) =>
{
    // ── AfterRouting：路由之后执行 ──
    // 位置要点：这里可以访问路由信息、认证状态等

    // Blazor Server 路由（固定写法，放在最后）
    app.MapBlazorHub();    // Blazor 长连接中心
    app.MapFallbackToPage("/_Host"); // 路由回退页
});
```

#### 2）典型的 WebApi 配置（GraghQL）

```csharp
using DMP_Lite.Domain;
using DMP_Lite.WebApi.GraphQL;
using TKW.Framework.Domain.Web;

var builder = WebApplication.CreateBuilder(args);
// ───────────────────────────────────────────────────────────────
// 1. 配置加载（严格优先级顺序）
// 本例中放到了配置回调中，也可以在这里统一加载配置文件和环境变量
// ───────────────────────────────────────────────────────────────
// 2. 领域层 + Web 管道配置（一行完成所有核心配置）
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    // 可以直接访问 builder.Configuration 或者使用专门的配置类
    cfg.IsDevelopment = builder.Environment.IsDevelopment();
    var json = cfg.IsDevelopment ? "appsettings.json" : "appsettings.Production.json";
    builder.Configuration.AddJsonFile(json, optional: true, reloadOnChange: true);
    // 获取必要的信息，如数据库连接字符串等
    cfg.ConnectionString = builder.Configuration
        .GetConnectionString("PostgreSQL") ?? string.Empty;
/*
    // 绑定配置项到领域选项
    cfg.BindOptions(builder).DomainOptions("DmpOptions")
        .Validate(config => config.ConnectionString.StartsWith("Host="),
            "连接字符串格式不正确");
    cfg.BindOptions(builder).BusinessOptions<DmpOptions>("TKWDomain");
*/
    // 根据需要覆盖绑定的配置项
    cfg.EnableDomainLogging = true;
    cfg.UseWebExceptionMiddleware = true;
})
.RegisterServices((services, options) =>
{
    services.AddCors(corsOptions =>
    {
        corsOptions.AddPolicy("DevelopmentPolicy", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        corsOptions.AddPolicy("ProductionPolicy", p => p.WithOrigins("https://your-frontend-domain.com")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
    });
    // 注入 HttpContextAccessor 以便在 GraphQL 查询中访问 HTTP 上下文
    //services.AddHttpContextAccessor();
    // 注入 GraphQL 查询类型
    services.AddScoped<RootQuery>();
    services.AddGraphQLServer()
        .AddQueryType<RootQuery>()
        .AddTypeExtension<PaymentLogQuery>()
        .AddTypeExtension<CouponReconLogQuery>()
        .AddTypeExtension<CouponReconRecordQuery>()
        .AddTypeExtension<ImportBatchQuery>()
        .AddFiltering().AddSorting().AddProjections()
        .ModifyRequestOptions(opt =>
            opt.IncludeExceptionDetails = options.IsDevelopment);
})
.UseDomainSession<DmpUserInfo>(cfg =>
{
    cfg.SessionKeyName = "DmpSession";
    cfg.ExpiredTimeSpan = TimeSpan.FromMinutes(15);
})
.BeforeRouting((app, options) =>
{
    // 必须最前：开发异常页面
    if (options.IsDevelopment)
        app.UseDeveloperExceptionPage();

    // CORS（必须在路由之前，处理 OPTIONS 预检请求）
    app.UseCors(options.IsDevelopment ? "DevelopmentPolicy" : "ProductionPolicy");
})
.UseAspNetCoreRouting()  // 自动 UseRouting
.AfterRouting((app, _) =>
{
    // 默认已启动授权中间件 app.UseAuthorization()，记得添加授权策略
    app.MapGraphQL();
    // app.UseFastEndpoints();
});
// ───────────────────────────────────────────────────────────────
// 3. 构建并启动应用
// ───────────────────────────────────────────────────────────────
// 注意：这里不再需要任何手动中间件注册，所有逻辑已迁移到分阶段回调中
builder.Build().Run();  // 运行 WebApplication
```

### 3.3 DomainWebConfigurationOptions 配置选项

可通过配置选项自定义 Web 宿主行为，如是否启用异常中间件、日志级别等。

---

## 4. 中间件详解

### 4.1 WebExceptionMiddleware

**职责**：捕获 DomainException 并转换为标准 JSON 响应。

**处理逻辑**：

1. 捕获 DomainException，返回业务错误码和消息
2. 捕获其他系统级异常，返回 500 状态码
3. 记录详细异常日志

**示例响应**：

```json
{
    "code": 4001,
    "msg": "商户余额不足"
}
```

### 4.2 SessionUserMiddleware

**职责**：解析 Cookie/Header 会话，创建 DomainUser 并注入上下文。

**处理逻辑**：

1. 从请求头或 Cookie 中提取会话标识
2. 验证会话有效性
3. 创建 DomainUser 实例
4. 注入到 HttpContext.Items 中

---

## 5. 路由阶段互斥与警告机制

框架严格保护路由阶段的互斥性：

- 必须显式调用 UseAspNetCoreRouting() 或 UseCustomRouting()
- 路由阶段不可重复声明
- 违规配置触发系统警告

---

## 6. 后续扩展方向

- Blazor Server 专用路由阶段
- MAUI/Console 宿主适配器
- 更多协议层扩展（gRPC、WebSocket 等）
