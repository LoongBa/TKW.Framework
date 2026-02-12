

# TkwDomain 框架简要使用指南和设计说明

## 第一部分：快速上手指南（面向应用开发者）

该部分侧重于如何在 `Program.cs` 中以最简洁的方式集成领域框架。

### 1. 核心配置入口

通过一行代码开启领域初始化，并利用**流式 API** 编排整个 Web 管道。

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 一行配置：初始化领域容器 + 编排 Web 中间件管道
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg => 
{
    // 配置 Web 特定开关
    cfg.UseDomainExceptionMiddleware = true;
    cfg.EnableDomainLogging = true;
})
.RegisterServices(services => 
{
    // 阶段 A：注册表现层/基础设施服务（如 GraphQL、FastEndpoints）
    services.AddGraphQLServer().AddQueryType<MerchantQuery>();
    services.AddFastEndpoints();
})
.BeforeRouting(app => 
{
    // 阶段 B：路由匹配前的中间件（如 CORS）
    app.UseCors();
})
.UseAspNetCoreRouting() // 阶段 C：启用标准路由处理
.AfterRouting(app => 
{
    // 阶段 D：终结点映射（路由匹配后的逻辑）
    app.MapGraphQL();
    app.UseFastEndpoints();
});

var app = builder.Build();
app.Run();
```

### 2. 领域服务调用

在业务代码中，严禁直接从容器获取服务，必须通过 `DomainUser` 这一唯一入口。

```csharp
// 获取用户上下文（通常在中间件或 Controller 中已准备好）
var user = context.Items["DomainUser"] as DomainUser<DmpUserInfo>;

// 获取受 AOP 拦截的服务
var aopService = user.UseAop<IUserServiceAop>();

// 获取普通领域服务
var commonService = user.Use<MerchantInfoService>();
```

---

## 第二部分：深度设计文档（面向框架维护者）

该部分详尽介绍框架各模块的设计思路、约束机制与底层实现原理。

### 模块一：初始化模型——表现层主导，领域层守门

为了平衡宿主环境的灵活性与领域层的自治性，框架采用了**双重同步初始化**流程。

- **配置分层**：
  
  - **DomainOptions (核心层)**：定义环境 (IsDevelopment)、连接字符串 (ConnectionString) 等核心参数，不依赖 Web 库。
  
  - **DomainWebConfigurationOptions (表现层)**：继承自基类，包含 Web 中间件开关和特定的实现类型。

- **守门员逻辑 (The Gatekeeper)**：
  
  1. **表现层赋值**：宿主根据 `IWebHostEnvironment` 设置 `IsDevelopment` 的初始值。
  
  2. **领域层修正**：在 `InitializeDiContainer` 中，执行 `OnPreInitialize` 钩子。领域层可以在此检查连接字符串，如果指向生产库，则强制将 `IsDevelopment` 设为 `false`，无论表现层如何设置。
  
  3. **二次同步**：修正后的参数会再次同步回基类属性，确保后续 DI 注册的一致性。

### 模块二：依赖注入 (DI) 策略——确定性覆盖

利用 Autofac 的特性，框架定义了确定的注册优先级，解决了 `Populate` 导致的乱序问题。

- **RegisterTypeForced (强自治)**：用于核心业务服务。它使用 `.PreserveExistingDefaults()`，确保即使表现层后续注册了相同接口，领域层的实现依然是默认解析首选。

- **RegisterTypeReplaceable (可扩展)**：用于日志、缓存等基础设施。遵循“最后注册胜出”原则，允许表现层通过标准扩展（如 `services.AddLogging`）进行覆盖。

- **手动 New 优化**：在创建 `DomainContext` 时，废弃 `Resolve` 解析，改为直接手动 `new`。这消除了高频 AOP 调用时的 DI 查找开销，大幅提升性能。

### 模块三：AOP 拦截体系——作用域锚定与性能

框架通过 `DomainInterceptor` 实现复杂的拦截逻辑，并保证了异步调用的上下文安全。

- **Metadata 缓存**：使用 `ConcurrentDictionary` 缓存接口和方法的 Filter/Flag 配置，避免每次调用都进行昂贵的反射操作。

- **作用域关联 (Scope Anchoring)**：
  
  - 拦截器在每次调用时开启一个新的子作用域 (`ILifetimeScope`)。
  
  - 通过 `AsyncLocal<ILifetimeScope?>` 将该作用域透传给当前异步流。
  
  - 当 `User.Use<T>` 被执行时，解析逻辑会自动在该子作用域内进行。这保证了同一个请求内的服务共享同一个事务或数据库上下文。

### 模块四：Web 管道编排——顺序确定性

原生 ASP.NET Core 的 `IStartupFilter` 是 LIFO（后进先出）顺序，这使得注册多个 Filter 时难以控制执行位置。

- **DomainPipelineBuilder**：内部维护一个有序的 `List<Action<WebApplication>>`。

- **统一 Filter**：整个框架只注册一个 `DomainPipelineFilter`。在构建管道时，它按 `List` 的物理顺序依次执行 Action。

- **状态锁定**：通过 `RoutingMode` 枚举防止 `UseAspNetCoreRouting` 与 `UseCustomRouting` 被重复调用或冲突调用。

### 模块五：异常处理与日志——不吞异常原则

框架坚持“领域层仅诊断，不决策”的原则。

- **Log Without Swallow**：拦截器捕获异常后，由 `DefaultExceptionLoggerFactory` 记录结构化日志并填充错误模型（如 `ErrorCode`），但**绝不吞掉异常**。

- **异常传播**：原始异常会继续向上传播到 Web 层的 `DomainExceptionMiddleware`，由表现层决定是返回标准的 JSON 错误还是重定向到错误页。
