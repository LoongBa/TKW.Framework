# TKWF.Domain V3.1 使用说明1.0

## 1. 介绍（Introduction）

### 1.1 什么是 TKWF.Domain

TKWF.Domain 是 TKW 系列框架的领域层核心基础框架，始于 .NET 2.0 时代，在 .NET 4.x 时代趋于成熟。目前采用 **.NET 10+** 重新构建，为应用提供一套轻量、现代、可扩展且高度领域自治的基础设施。

### 1.2 核心特性（Features）

- **领域自治**：业务逻辑完全封装在领域层。表现层（Web API、Blazor、MAUI 等）仅充当会话提供者和调用入口。

- **强类型上下文驱动**：所有领域服务必须通过唯一入口 `DomainUser<TUserInfo>` 获取，确保请求链路中的身份与权限绝对安全。

- **现代化 AOP 拦截体系**：基于 Castle DynamicProxy 实现异步优先的拦截器架构，提供方法级、控制器级、全局级的强类型拦截（Filter 与 Flag）；基于双 `AsyncLocal` 模式消除单例拦截器高并发状态竞争，支持事务、日志、权限等切面，彻底解决并发状态竞争问题。

- **异常不吞没原则**：领域层仅记录日志和填充异常上下文，真实异常抛给宿主层做协议映射；拦截器仅记录异常日志，原样抛出异常到宿主层，保证异常链路完整。

- **Entity 直连 ORM**：CodeFirst 设计，Entity 直接联动 FreeSql，去除 Model 到 Entity 无意义中间层；Entity 直接作为领域实体与 FreeSql 联动，砍掉无意义的 Model 到 Entity 中间层。

- **异步上下文隔离**：通过 `AsyncLocal<T>` 实现拦截器上下文的异步链路隔离，避免多线程 / 异步并发下的上下文覆盖问题；每个异步链路独立存储上下文，无并发覆盖风险。

## 2. 快速开始（Getting Started）

### 2.1 环境准备

- **运行时**：.NET 10+

- **IDE**：Visual Studio 2025+ 或 VS Code（搭配 C# Dev Kit）

- **核心依赖**：Autofac、FreeSql、Castle.Core（需确保 Castle.Core 版本兼容 DynamicProxy 异步拦截）

### 2.2 领域初始化（以 Web 项目为例）

在 Web 项目的 `Program.cs` 中，通过 `ConfigTkwDomain` 扩展方法注入领域能力：

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg =>
{
    cfg.UseDomainExceptionMiddleware = true;   // 开启异常映射（原文档标注待裁决项统一为该配置）
    cfg.EnableDomainLogging = true;         // 开启 AOP 日志 Filter/领域层日志（由拦截器统一记录）
    cfg.IsDevelopment = builder.Environment.IsDevelopment();
    cfg.ConnectionString = builder.Configuration.GetConnectionString("PostgreSQL") ?? string.Empty;
})
.RegisterServices((services, _) =>
{
    services.AddControllers();
    services.AddCors(corsOptions => { /* 配置CORS策略 */ });
})
.UseDomainSession<DmpUserInfo>(cfg =>
{
    cfg.SessionKeyName = "DmpSession";
    cfg.ExpiredTimeSpan = TimeSpan.FromMinutes(15);
})
.BeforeRouting((app, options) =>
{
    if (options.IsDevelopment) app.UseDeveloperExceptionPage();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseCors(options.IsDevelopment ? "DevelopmentPolicy" : "ProductionPolicy");
})
.UseAspNetCoreRouting()
.AfterRouting((app, _) =>
{
    app.MapControllers();
});

builder.Build().Run();
```

## 3. 核心概念（Core Concepts）

### 3.1 领域初始化器（DomainHostInitializerBase）

每个领域项目需实现 `DomainHostInitializerBase<TUserInfo>` 完成服务注册与基础设施配置，是领域层的核心入口。

```csharp
public class DmpDomainInitializer : DomainHostInitializerBase<DmpUserInfo>
{
    // 领域层守门员：强制修正表现层传入的配置
    protected override void OnPreInitialize(DomainOptions options, IConfiguration? configuration)
    {
        if (!options.IsDevelopment && string.IsNullOrEmpty(options.ConnectionString))
            throw new InvalidOperationException("生产环境必须配置数据库连接字符串");
    }

    // 注册数据库、缓存、MQ 等基础设施
    protected override void OnRegisterInfrastructureServices(
        ContainerBuilder containerBuilder,
        IServiceCollection services,
        IConfiguration? configuration,
        DomainOptions options)
    {
        // 注册 FreeSql、Redis 等基础设施
    }

    // 注册业务领域服务（返回 UserHelper）
    protected override DomainUserHelperBase<DmpUserInfo> OnRegisterDomainServices(
        ContainerBuilder cb,
        IServiceCollection services,
        IConfiguration? configuration)
    {
        cb.RegisterDomainService<IMerchantService, MerchantService>();
        return new DmpUserHelper();
    }

    // 容器构建后配置（全局 Filter、异常工厂等）
    protected override void OnContainerBuilt(IContainer? container, IConfiguration? configuration, bool isExternalContainer = false)
    {
        EnableDomainLogging(EnumDomainLogLevel.Normal);
        // 配置全局过滤器
        AddGlobalFilter(new MyCustomGlobalFilter());
        // 批量添加全局过滤器
        host.AddGlobalFilters(new List<DomainFilterAttribute<DmpUserInfo>> { /* 批量添加 */ });
    }
}
```

**初始化顺序**：

```Plain
OnPreInitialize() → OnRegisterInfrastructureServices() → OnRegisterDomainServices() → OnContainerBuilt()
```

### 3.2 实体、DTO 与服务：基于 xCodeGen 的双轨制体系

三者通过 `xCodeGen` 工具联动，遵循 **"Generated（自动生成）+ Partial（手动扩展）"** 模式：

| 组件          | 组成方式                       | 核心职责与逻辑                                                                                        |
| ----------- | -------------------------- | ---------------------------------------------------------------------------------------------- |
| **Entity**  | `*.cs`+`*.g.cs`+`*.biz.cs` | 数据锚点 + 硬核验证，持久化前触发`Validate()`，`*.biz.cs`处理跨字段领域规则；对应数据库表，通过生成的`Validate()`执行物理阻断校验            |
| **DTO**     | `*.g.cs`+`*.cs`            | 表现层契约，`record`保证不可变，支持`EnumSceneFlags`分场景验证；负责表现层输入 / 输出，利用`record`保证不可变性                      |
| **Service** | `*.g.cs`+`*.cs`            | 业务流程编排，继承`DomainServiceBase<TUserInfo>`，标注 AOP Filter；`*.g.cs`提供`Internal`系列方法操作仓储，`*.cs`编排业务流 |

#### 3.2.1 实战：物理阻断验证

```csharp
// MerchantInfo.biz.cs
partial void OnBusinessValidate(EnumSceneFlags scene, List<ValidationResult> results)
{
    if (this.Status == MerchantStatusEnum.Disabled && this.EnableAutoReconciliation)
        results.Add(new ValidationResult("禁用状态下不能开启自动对账", [nameof(Status)]));
}
```

#### 3.2.2 实战：高性能 DTO 映射

弃用 AutoMapper，采用生成的硬编码赋值，实现微秒级转换：

```csharp
createDto.Validate(EnumSceneFlags.Create); // 场景化验证
var entity = createDto.ToEntity();         // DTO转实体
updateDto.ApplyToEntity(existingEntity);   // 更新实体
```

### 3.3 领域用户与服务获取（DomainUser）

`DomainUser<TUserInfo>` 是访问领域逻辑的唯一入口，封装当前用户身份，V3.1 强化了异步链路的作用域锚定：

| 方法                        | 说明                      | 适用场景    |
| ------------------------- | ----------------------- | ------- |
| `User.Use<TService>()`    | 获取原始服务实例，不触发 AOP 拦截     | 领域内部协作  |
| `User.UseAop<TService>()` | 获取 AOP 代理实例，自动触发 Filter | 主流程业务调用 |

**作用域锚定**：`DomainHost.NewDomainContext()` 通过 `AsyncLocal` 将子容器与调用上下文绑定，避免并发下的 DbContext 释放 / 共享问题；V3.1 中确保在复杂的 `await` 链路中，`User.Use<T>` 始终定位到当前调用的子容器。

### 3.4 AOP 拦截 Filter（V3.1 核心更新）

#### 3.4.1 拦截器核心特性

| **特性**        | **说明**                                                                                                               |
| ------------- | -------------------------------------------------------------------------------------------------------------------- |
| **异步上下文隔离**   | 通过 `AsyncLocal<DomainContext<TUserInfo>>` 存储上下文，确保每个异步链路拥有独立的逻辑沙箱，互不干扰。                                              |
| **同步 / 异步统一** | 同时实现 `IAsyncInterceptor` + `IInterceptor`，利用适配器模式自动处理所有方法类型的拦截。                                                      |
| **按需创建适配器**   | 每次拦截时创建轻量级 `AsyncDeterminationInterceptor`，消除单例适配器在多线程下的状态竞争隐患。                                                      |
| **强制资源清理**    | **清理逻辑严格位于 `finally` 块中**。这保证了即使业务逻辑或过滤器发生崩溃，当前线程的上下文也会被强制归还或清空。这在物理上阻断了对线程池中后续任务的上下文污染，并确保 `ILifetimeScope` 得到物理释放。 |
| **异常不吞没**     | 仅记录结构化异常日志并填充 `InterceptorExceptionContext`，随后原样抛出，保证宿主层能感知真实异常。                                                     |

#### 3.4.2 三级 Filter 注册

```csharp
// 1. 全局级（OnContainerBuilt中）
AddGlobalFilter(new MyCustomGlobalFilter());

// 2. 控制器级（服务接口上）
[MyControllerFilter]
public interface IMerchantService : IDomainService
{
    // 3. 方法级（接口方法上）
    [TransactionFilter]
    Task CreateAsync(MerchantCreateDto dto);
}
```

#### 3.4.3 执行顺序

- 前置（PreProceed）：Global → Controller → Method

- 后置（PostProceed）：Method → Controller → Global

- 去重机制：同类型 Filter 同时出现在控制器 / 方法级时，仅执行方法级。

#### 3.4.4 自定义 Filter 实现

```csharp
public class MyAuditFilter : DomainFilterAttribute<DmpUserInfo>
{
    public override bool CanWeGo(DomainInvocationWhereType whereType, DomainContext<DmpUserInfo> context)
        => true;

    public override async Task PreProceedAsync(DomainInvocationWhereType whereType, DomainContext<DmpUserInfo> context)
    {
        // 方法执行前逻辑（如审计记录开始）
        await Task.CompletedTask;
    }

    public override async Task PostProceedAsync(DomainInvocationWhereType whereType, DomainContext<DmpUserInfo> context)
    {
        // 方法执行后逻辑（如审计记录提交）
        await Task.CompletedTask;
    }
}
```

#### 3.4.5 拦截器生命周期（开发者仅需实现钩子）

`DomainInterceptor<TUserInfo>` 继承 `BaseInterceptor<TUserInfo>` 后，仅需实现 / 重写以下核心钩子：

```csharp
// 初始化：创建 Autofac 子容器、绑定 DomainContext
protected override void InitialSync(IInvocation invocation) => InitializeScope(invocation);
protected override Task InitialAsync(IInvocation invocation) { InitializeScope(invocation); return Task.CompletedTask; }
// 清理：释放子容器
protected override void CleanUpSync() { Context?.LifetimeScope?.Dispose(); }
protected override async Task CleanUpAsync() { if (Context?.LifetimeScope != null) await Context.LifetimeScope.DisposeAsync(); }
// 过滤器执行：全局/控制器/方法级 Filter 前置/后置处理
protected override async Task PreProceedAsync(IInvocation invocation) { /* 执行前置过滤器 */ }
protected override async Task PostProceedAsync(IInvocation invocation) { /* 执行后置过滤器 */ }
```

### 3.5 异常处理策略

遵循**记录但不吞没**原则，领域层仅记录日志、填充上下文，异常抛给宿主层处理：

#### 3.5.1 异常日志与上下文填充

```csharp
protected override void LogException(InterceptorExceptionContext context)
{
    // 记录结构化日志
    Context?.Logger?.LogError(context.Exception,
        "领域方法异常 | 方法: {MethodName} | 用户: {UserName}",
        context.Invocation.Method.Name,
        Context.DomainUser.UserInfo.UserName);

    // 填充错误码
    context.IsAuthenticationError = context.Exception is AuthenticationException;
    context.ErrorCode = context.IsAuthenticationError ? "AUTH_001" : "BUS_001";

    // 不吞没异常
    context.ExceptionHandled = false;
}
```

#### 3.5.2 业务异常类型

```csharp
throw new DomainException("商户余额不足"); // 业务逻辑异常
throw new ValidationResultsException(validationResults); // 验证失败
throw new AuthenticationException("会话过期"); // 认证失败
throw new UnauthorizedAccessException("权限不足"); // 授权失败
```

#### 3.5.3 宿主联动

宿主层的 `WebExceptionMiddleware` 截获异常并转换为 400/500 JSON 响应，开发者无需在 Service 中编写 `try-catch`。

### 3.6 仓储与工作单元（Repositories   UoW）

- **仓储**：直接使用 FreeSql 的 `IBaseRepository<T>`，通过 `User.Use<IBaseRepository<T>>()` 获取当前作用域实例；

- **事务控制**：方法上标记 `[TransactionFilter]`，拦截器自动开启 / 回滚事务，支持 SavePoint 嵌套；V3.1 中拦截器会在异步 / 同步方法中统一处理事务：进入方法时开启事务，异常时回滚，正常执行后提交。

```csharp
[TransactionFilter]
Task<MerchantInfo> CreateMerchantAsync(MerchantCreateDto dto);
```

### 3.7 Session 管理

- 默认实现：`NoSessionManager<TUserInfo>`（无会话场景）；

- Web 场景：通过 `UseDomainSession` 注册基于 Cookie/Header 的会话实现：
  
  ```csharp
  .UseDomainSession<DmpUserInfo>(cfg =>
  {
      cfg.SessionKeyName = "DmpSession";
      cfg.ExpiredTimeSpan = TimeSpan.FromMinutes(15);
  })
  ```

- 游客会话：`DomainHost.Root!.NewGuestSessionAsync()` 创建匿名会话。

## 4. 高级指南（Advanced）

### 4.1 自定义拦截器（扩展 BaseInterceptor）

继承 `BaseInterceptor<TUserInfo>` 定制 AOP 行为（核心逻辑不可重写 `Intercept` 方法）：

```csharp
public class MyInterceptor : BaseInterceptor<MyUserInfo>
{
    protected override void InitialSync(IInvocation invocation) => Initialize(invocation);
    protected override Task InitialAsync(IInvocation invocation) { Initialize(invocation); return Task.CompletedTask; }

    private void Initialize(IInvocation invocation)
    {
        Context = BuildContext(invocation); // 绑定AsyncLocal上下文
    }

    protected override void LogException(InterceptorExceptionContext context)
    {
        // 记录日志，保持ExceptionHandled = false
        context.ExceptionHandled = false;
    }

    // 清理资源
    protected override void CleanUpSync() { Context?.LifetimeScope?.Dispose(); }
    protected override async Task CleanUpAsync() { if (Context?.LifetimeScope != null) await Context.LifetimeScope.DisposeAsync(); }
}
```

### 4.2 依赖注入策略

| 策略    | 注册方式                          | 适用场景         |
| ----- | ----------------------------- | ------------ |
| 强制注册  | `.PreserveExistingDefaults()` | 核心业务服务，防止被覆盖 |
| 可替换注册 | 普通注册（最后注册胜出）                  | 基础设施（日志、缓存）  |

### 4.3 审计与钩子（Hooks）

通过 `partial method` 自动处理审计字段：

```csharp
// MerchantInfoService.cs
partial void OnBeforeCreate(MerchantInfo entity)
{
    entity.CreateTime = DateTime.Now;
    entity.LastOperatorUid = User.UserInfo.UserIdString;
}

partial void OnBeforeUpdate(MerchantInfo entity)
{
    entity.UpdateTime = DateTime.Now;
    entity.LastOperatorUid = User.UserInfo.UserIdString;
}
```

### 4.4 静态上下文访问

同一 AOP 链路内，通过 `AsyncLocal` 安全访问当前上下文：

```csharp
var ctx = BaseInterceptor<DmpUserInfo>.CurrentContext;
if (ctx != null)
{
    var methodName = ctx.Invocation.Method.Name;
    var userName = ctx.DomainContext.DomainUser.UserInfo.UserName;
}
```

## 5. 核心配置与最佳实践

### 5.1 DomainHost 初始化（V3.1 关键注意点）

```csharp
// 初始化 DomainHost（由 ConfigTkwDomain 内部调用，开发者无需手动调用）
var host = DomainHost<TUserInfo>.Initialize<MyDomainInitializer>(options, upLevelContainer, configuration);
// 核心规则：
// 1. 禁止重复初始化：Root 实例只能初始化一次
// 2. 外部容器兼容：支持传入现有 Autofac 容器，避免重复构建
// 3. 日志工厂绑定：优先使用宿主层的 ILoggerFactory，无则从容器解析
```

### 5.2 开发避坑指南（V3.1 新增）

1. **禁止手动持有 DomainContext**：必须通过 `BaseInterceptor` 的 `Context` 属性访问。严禁定义静态或实例字段来存储 `DomainContext`，否则在高并发环境下会导致严重的请求数据串扰。

2. **异步上下文与 ConfigureAwait (false)**：
   
   - **框架处理**：拦截器底层在 `await` 业务方法返回的 `Task` 时，已经显式处理了 `ConfigureAwait(false)` 以优化内核性能。
   
   - **开发者注意**：在重写 `InitialAsync`、`PreProceedAsync` 或 `CleanUpAsync` 等**自定义钩子**时，若涉及异步 I/O 操作（如查询数据库或远程配置），开发者应同样注意异步上下文的切回。建议保持使用 `ConfigureAwait(false)`，以确保整个拦截链路的执行效率，并彻底规避在高同步上下文环境（如早期 ASP.NET 或某些特定的 UI 线程）下的潜在死锁风险。

3. **过滤器避免重度耗时操作**：全局/控制器/方法过滤器会按序同步编排。虽然支持异步执行，但过重的业务逻辑（如复杂的外部 API 调用）应尽量移至 Service 内部，以免阻塞 AOP 管道。

4. **子容器自动释放**：业务层无需手动释放从 `User.Use<T>` 拿到的 `ILifetimeScope`。`DomainInterceptor` 内部已通过 `CleanUp` 钩子在 `finally` 块中实现了自动异步释放（`DisposeAsync`）。

5. **异常处理规范**：在 Service 内部若需抛出异常，请直接抛出 `DomainException`。拦截器会自动捕获并记录日志，由宿主层中间件负责将其映射为协议响应，切勿在 Service 方法内过度使用 `try-catch` 导致异常被意外“吞没”。

## 6. 版本变更说明（V3.1 vs V1.1）

| 变更点     | V1.1 状态             | V3.1 状态                                               |
| ------- | ------------------- | ----------------------------------------------------- |
| 拦截器底层实现 | 未明确异步处理逻辑           | 基于 `AsyncLocal` 重构，统一同步 / 异步拦截，解决并发状态竞争               |
| 上下文管理   | 简单绑定 ILifetimeScope | 强制通过 `AsyncLocal` 隔离上下文，`finally` 块强制清理               |
| 资源释放    | 未明确释放逻辑             | 同步 / 异步分别调用 `Dispose`/`DisposeAsync`，释放 Autofac 子容器   |
| 过滤器执行   | 仅基础遍历               | 区分全局 / 控制器 / 方法级过滤器，反向遍历后置过滤器，避免执行顺序错误                |
| 异常上下文   | 仅记录异常信息             | 封装 `InterceptorExceptionContext`，包含调用信息 + 异常实例，便于日志溯源 |
| 文档覆盖度   | 仅基础使用说明             | 补充拦截器核心特性、生命周期、开发避坑指南，对齐最新代码实现                        |

## 7. 附录：扩展生态与子项目

| 项目                 | 状态   | 说明                            |
| ------------------ | ---- | ----------------------------- |
| TKWF.Domain.Web    | 稳定   | WebAPI / Minimal API 宿主集成     |
| TKWF.Domain.Blazor | 规划中  | Blazor WASM / Server 状态与缓存支持  |
| TKWF.Domain.Maui   | 规划中  | 桌面 / 移动端本地安全存储封装              |
| TKWF.EntityORM     | 暂缓   | 目前直接使用 FreeSql                |
| TKWF.xCodeGen      | 独立发布 | 跨平台代码生成工具（Entity/DTO/Service） |

> （注：文档部分内容可能由 AI 生成）
