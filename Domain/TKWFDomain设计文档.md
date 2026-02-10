# TKWF.Domain 领域框架设计文档 V1.0

修改历史：

2026.02.09    V1.0 完成框架基本功能





## 1. 概述

`TKWF.Domain` 是 TKW 系列框架的**领域层核心基础框架**，目标是为 .NET 应用（Web、MAUI、WPF、Console、Blazor 等）提供一套**轻量、现代、可扩展、领域自治**的领域服务基础设施。

核心设计目标：

- **领域自治**：业务逻辑完全封装在领域服务中，表现层仅负责会话管理和入口调用
- 统一强类型用户上下文（`DomainUser<TUserInfo>`）
- 强大的**方法级 + 控制器级 + 全局级** 属性驱动 AOP
- 内置会话管理、统一异常处理、结构化日志等基础设施
- 充分利用 .NET 10 现代特性（`record`、`required`、`JsonConstructor`、`async/await` 优先、`null-safety`）

框架底层依赖 **Autofac + Castle DynamicProxy**，通过 `DomainHost<TUserInfo>` 统一初始化和管理。

## 2. 核心设计思路

1. **泛型用户上下文驱动**：所有领域服务围绕 `DomainUser<TUserInfo>` 构建，实现类型安全的用户上下文传递。
2. **属性驱动的轻量 AOP**：使用 `DomainFilterAttribute<TUserInfo>` 和 `DomainFlagAttribute`，支持三级过滤器（Global → Controller → Method）。
3. **Host + Initializer 模式**：单点初始化入口，类似 .NET Host 构建器。
4. **强单例基础设施**：核心组件（如 `SessionManager`）默认 `SingleInstance()` 并提供 `*Forced` 扩展，防止意外覆盖。
5. **异步优先 + 不可变设计**：会话使用 `record`，拦截器全面异步。

## 3. 领域自治与使用模式（新增）

**设计的核心是领域自治**：业务行为必须封装在**领域服务**（`DomainServiceBase<TUserInfo>`）和**领域控制器**（`DomainController<TUserInfo>`，带 AOP 约束的领域服务）中。表现层/接入层**不得直接实现业务逻辑**，只能充当“会话提供者”和“调用入口”。

### 3.1 核心实现思路

- **表现层职责**：
  
  1. 通过 `DomainHost` 用 `SessionKey` 获取（或自动创建）`DomainUser<TUserInfo>`。
  2. 通过 `user.Use<TDomainService>()` 或 `user.UseAop<IAopContract>()` 获取服务实例。
  3. 调用服务方法，AOP 自动生效。

- **框架自动注入机制**：
  
  - `DomainServiceBase<TUserInfo>` 构造函数要求注入 `DomainUser<TUserInfo>`。
  - `DomainInterceptor<TUserInfo>` 在每次方法调用前自动创建 `DomainContext<TUserInfo>` 并注入当前 `DomainUser`。
  - 服务内部可直接使用 `this.User`、`this.User.IsInRole(...)`、`Use<T>()` 链式调用其他服务，或通过 `DomainContext.Current` 获取更完整的上下文（Invocation、Filters、Logger 等）。

- **权限与过滤**：
  
  - 类/方法上标记 `[RequireRoleFlag("admin")]`、`[TransactionFilter]` 等 Filter 自动执行。
  - 代码中可手动 `if (!User.IsInRole("admin")) throw ...` 或通过 `DomainContext` 做更复杂判断。

这种设计确保**领域层完全独立于表现层技术**（Web、桌面、控制台均可复用同一套领域服务）。

### 3.2 标准使用流程

1. **应用启动时初始化**（一次）：
   
   ```csharp
   DomainHost<UserInfo>.Build<MyDomainInitializer, MyDomainHelper, SessionManager<UserInfo>>(containerBuilder);
   ```

2. **每次请求/操作时**：
   
   - 获取 `SessionKey`（Cookie、Header、LocalStorage、文件等）
   - `var user = await DomainHost<UserInfo>.Root!.GetDomainUserAsync(sessionKey);`
   - `var service = user.UseAop<IMerchantService>();`   // 或 Use<T>()
   - `await service.UpdateMerchantAsync(...);`

3. **首次访问（无 SessionKey）**：
   
   - 调用 `await DomainHost<UserInfo>.Root!.NewGuestSessionAsync();` 生成新 SessionKey 并返回。

### 3.3 多场景使用示例

#### 3.3.1 控制台程序（最简场景）

```csharp
// Program.cs
var host = DomainHost<UserInfo>.Build<...>();

// 模拟登录或游客
var session = await host.NewGuestSessionAsync();
var user = session.User!;

// 调用领域服务
var merchantService = user.UseAop<IMerchantService>();
await merchantService.CreateMerchantAsync("新商户");

// 服务内部自动拥有 User 上下文
// MerchantService.cs
public class MerchantService : DomainController<UserInfo>, IMerchantService
{
    public async Task CreateMerchantAsync(string name)
    {
        if (!User.IsInRole("admin")) throw new UnauthorizedAccessException();
        // ... 业务逻辑
        await Use<IMerchantRepository>().SaveAsync(...);
    }
}
```

#### 3.3.2 WPF / MAUI 桌面应用

```csharp
// App.xaml.cs 或启动页
private string? _sessionKey;

protected override async void OnStartup(StartupEventArgs e)
{
    // 初始化 DomainHost
    DomainHost<UserInfo>.Build<...>();

    // 从本地文件 / SecureStorage 读取 SessionKey
    _sessionKey = await LoadSessionKeyAsync();

    if (string.IsNullOrEmpty(_sessionKey))
    {
        var newSession = await DomainHost<UserInfo>.Root!.NewGuestSessionAsync();
        _sessionKey = newSession.Key;
        await SaveSessionKeyAsync(_sessionKey);
    }
}

// 任意页面/命令中使用
private async Task OnSaveMerchant()
{
    var user = await DomainHost<UserInfo>.Root!.GetDomainUserAsync(_sessionKey!);
    var service = user.UseAop<IMerchantService>();
    await service.UpdateMerchantAsync(...);
}
```

#### 3.3.3 WebAPI / Blazor Server / Minimal API

```csharp
// Program.cs 或 Startup
app.Use(async (context, next) =>
{
    var sessionKey = context.Request.Cookies["SessionKey"] 
                  ?? context.Request.Headers["X-Session-Key"];

    if (!string.IsNullOrEmpty(sessionKey))
    {
        var user = await DomainHost<UserInfo>.Root!.GetDomainUserAsync(sessionKey!);
        // 可放入 HttpContext.Items 或 Scoped 服务中
        context.Items["DomainUser"] = user;
    }
    await next();
});

// Controller / Endpoint 中
[HttpPost("merchants")]
public async Task<IActionResult> Create([FromServices] IHttpContextAccessor accessor)
{
    var user = (DomainUser<UserInfo>)accessor.HttpContext!.Items["DomainUser"]!;
    var service = user.UseAop<IMerchantService>();
    await service.CreateMerchantAsync(...);
    return Ok();
}
```

#### 3.3.4 Blazor WebAssembly（客户端）

与 MAUI 类似，使用 `ProtectedLocalStorage` 或 `IndexedDB` 存储 `SessionKey`，在 `AuthenticationStateProvider` 中加载 `DomainUser`，再通过 `User.UseAop<...>()` 调用服务（服务可部署为 WebAPI 或直接在 WASM 中运行）。

## 4. 主要功能特性（现状）

（保持原表，略）

## 5. 核心模块与类结构

（保持原内容，略）

## 6. 依赖注入与 Autofac 策略（现状）

（保持原内容，略）

## 7. 当前实现状态评估

**优势**：

- 领域自治设计彻底，表现层与领域层解耦极佳
- 多平台复用性强（同一领域服务可在 Console、WPF、WebAPI 中使用）
- Filter 三级体系 + DomainContext 提供强大扩展能力
- 会话管理成熟（`HybridCache` 支持内存 + Redis）

**待完善**：

- 全局 Filter 仍为静态列表，建议改为可注册模块
- 角色权限 Filter（`RequireRoleFlagAttribute`）实现待完成
- 事务、日志、缓存等常用 Filter 示例需补充完整
- 使用文档和 Starter Template 不足

## 8. 未来考虑与扩展方向（优先级建议）

**高优先级（阶段 1）**：

1. 完成 `RequireRoleFlagAttribute` + `AuthorityFilterAttribute`
2. 实现 `LoggingFilterAttribute`、`TransactionFilterAttribute`
3. 完善 `IDomainGlobalExceptionFactory` 的 Web/Desktop 具体实现

**中优先级（阶段 2）**：

- `CachingFilterAttribute`、`ValidationFilterAttribute`、`AuditLogFilterAttribute`
- 模块化全局 Filter 注册（`IDomainModule` 接口）

**长期方向**：

- 插件系统（运行时替换服务）
- 领域事件总线
- OpenTelemetry + 分布式追踪集成
- 基于 `DomainContext` 的更灵活的动态 Filter 注册

## 9. 总结与讨论点

`TKWF.Domain` 已形成一套**以领域自治为核心**、**跨平台友好**的现代领域框架。通过 `DomainUser` + `Use/UseAop` + AOP Filter 的组合，实现了业务逻辑的彻底封装和高度可测试性。

**建议下一步讨论**：

- 是否增加 `IDomainModule` 接口支持插件化 Filter 注册？
- Web 场景下 SessionKey 的最佳传递方式（Cookie vs Header vs Token）？
- 是否需要提供官方的 `TKWF.Domain.Web` / `TKWF.Domain.Maui` 辅助包？

---

后续根据实际设计进度需要继续增加**架构图**（Mermaid）、**详细 Filter 开发指南**、**Autofac 覆盖策略专节**等部分。
