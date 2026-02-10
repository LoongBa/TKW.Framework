# TKWF.Domain 领域框架设计文档 V1.1

**版本**：V1.1  
**更新日期**：2026.02.10  
**状态**：核心框架稳定，扩展项目规划中

### 修改历史

| 日期         | 版本   | 主要变更                                                                                                                             |
| ---------- | ---- | -------------------------------------------------------------------------------------------------------------------------------- |
| 2026.02.09 | V1.0 | 完成框架基本功能（DomainHost、AOP、Filter、Session 等）                                                                                        |
| 2026.02.10 | V1.1 | 重构全局异常处理机制（仅记录日志，不吞异常）；异步拦截器稳定（基于 AsyncInterceptorBase + 同步转发兼容）；新增 DomainUser.ToClaimsPrincipal()；规划扩展项目结构；重点标注 Castle 异步拦截已知问题 |

---

## 1. 概述

`TKWF.Domain` 是 TKW 系列框架的**领域层核心基础框架**，目标是为 .NET 应用（Web、MAUI、WPF、Console、Blazor 等）提供一套**轻量、现代、可扩展、领域自治**的领域服务基础设施。

核心设计目标：

- **领域自治**：业务逻辑完全封装在领域服务中，表现层仅负责会话管理和入口调用
- 统一强类型用户上下文（`DomainUser<TUserInfo>`）
- 强大的**方法级 + 控制器级 + 全局级** 属性驱动 AOP（异步优先）
- 内置会话管理（HybridCache）、结构化日志、异常诊断等基础设施
- 充分利用 .NET 10 现代特性（`record`、`required`、`JsonConstructor`、`async/await` 优先、`null-safety`）

框架底层依赖 **Autofac + Castle DynamicProxy**，通过 `DomainHost<TUserInfo>` 作为统一初始化和管理入口。

---

## 2. 核心设计思路

1. **泛型用户上下文驱动**：所有领域服务围绕 `DomainUser<TUserInfo>` 构建，实现类型安全的用户上下文传递。
2. **属性驱动的轻量 AOP**：使用 `DomainFilterAttribute<TUserInfo>` 和 `DomainFlagAttribute`，支持三级过滤器（Global → Controller → Method）。
3. **Host + Initializer 模式**：单点初始化入口，类似 .NET Host 构建器。
4. **强单例基础设施**：核心组件默认 `SingleInstance()` 并提供 `*Forced` 扩展，防止意外覆盖。
5. **异步优先 + 不可变设计**：会话使用 `record`，拦截器全面异步（基于 `AsyncInterceptorBase`）。
6. **异常处理原则**：领域层仅记录日志 + 填充上下文，不吞异常、不包装抛出，让原始异常正常传播给表现层处理。

---

## 3. 领域自治与使用模式

**设计的核心是领域自治**：业务行为必须封装在**领域服务**（`DomainServiceBase<TUserInfo>`）和**领域控制器**（`DomainController<TUserInfo>`，带 AOP 约束的领域服务）中。表现层/接入层**不得直接实现业务逻辑**，只能充当“会话提供者”和“调用入口”。

### 3.1 核心实现思路

- **表现层职责**：
  
  1. 通过 `DomainHost` 用 `SessionKey` 获取（或自动创建）`DomainUser<TUserInfo>`。
  2. 通过 `user.Use<TDomainService>()` 或 `user.UseAop<IAopContract>()` 获取服务实例。
  3. 调用服务方法，AOP 自动生效。

- **框架自动注入机制**：
  
  - `DomainServiceBase<TUserInfo>` 构造函数要求注入 `DomainUser<TUserInfo>`。
  - `DomainInterceptor<TUserInfo>` 在每次方法调用前自动创建 `DomainContext<TUserInfo>` 并注入当前 `DomainUser`。
  - 服务内部可直接使用 `this.User`、`this.User.IsInRole(...)`、`Use<T>()` 链式调用其他服务，或通过 `DomainContext` 获取更完整的上下文。

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
var host = DomainHost<UserInfo>.Build<...>();

var session = await host.NewGuestSessionAsync();
var user = session.User!;

var merchantService = user.UseAop<IMerchantService>();
await merchantService.CreateMerchantAsync("新商户");
```

#### 3.3.2 WPF / MAUI 桌面应用

```csharp
string? sessionKey = await LoadSessionKeyAsync();

if (string.IsNullOrEmpty(sessionKey))
{
    var newSession = await DomainHost<UserInfo>.Root!.NewGuestSessionAsync();
    sessionKey = newSession.Key;
    await SaveSessionKeyAsync(sessionKey);
}

var user = await DomainHost<UserInfo>.Root!.GetDomainUserAsync(sessionKey);
var service = user.UseAop<IMerchantService>();
await service.UpdateMerchantAsync(...);
```

#### 3.3.3 WebAPI / Blazor Server / Minimal API

```csharp
app.Use(async (context, next) =>
{
    var sessionKey = context.Request.Cookies["SessionKey"] 
                  ?? context.Request.Headers["X-Session-Key"];

    if (!string.IsNullOrEmpty(sessionKey))
    {
        var user = await DomainHost<UserInfo>.Root!.GetDomainUserAsync(sessionKey);
        context.Items["DomainUser"] = user;
    }
    await next();
});
```

---

## 4. 主要功能特性（现状）

| 特性                                   | 状态  | 实现方式                                        | 备注                                       |
| ------------------------------------ | --- | ------------------------------------------- | ---------------------------------------- |
| 泛型 DomainUser<TUserInfo>             | 已完成 | `DomainUser<TUserInfo>`                     | 支持序列化 + 运行时解析服务                          |
| 领域服务基类                               | 已完成 | `DomainServiceBase<TUserInfo>`              | 提供 `Use<T>()` 链式调用                       |
| AOP 拦截器                              | 已完成 | `DomainInterceptor<TUserInfo>` + Castle     | 支持异步拦截（基于 AsyncInterceptorBase + 同步转发兼容） |
| 多层级 Filter（Global/Controller/Method） | 已完成 | `DomainFilterAttribute`                     | 执行顺序：Global → Controller → Method        |
| 全局异常统一处理                             | 已完成 | `DefaultGlobalExceptionLoggerFactory`       | 仅记录日志，不吞异常、不抛新异常                         |
| 会话管理（Session）                        | 已完成 | `SessionManager<TUserInfo>` + `HybridCache` | 支持内存 + Redis 分布式                         |

**已知问题（重点标注）**：

- Castle 异步拦截接口兼容性：当前 `BaseInterceptor` 继承 `AsyncInterceptorBase` 并实现 `IInterceptor` 接口，同步方法通过 `GetAwaiter().GetResult()` 转发到异步实现。**存在潜在隐患**：
  - 同步调用可能导致线程阻塞或死锁（在有 SynchronizationContext 的场景）
  - 转发过程中异常传播可能被扭曲（已通过测试缓解，但非完美方案）
  - 建议未来完全禁用同步拦截，强制所有领域服务使用异步方法

---

## 5. 核心模块与类结构

- **入口与宿主**  
  `DomainHost<TUserInfo>` —— 整个框架的根单例，持有 `IContainer`、`UserHelper`、`SessionManager`、`LoggerFactory`、`GlobalFilters` 等  
  `DomainHostInitializerBase<TUserInfo, TDomainHelper>` —— 抽象初始化器，由应用层继承实现注册逻辑

- **用户与会话**  
  `DomainUser<TUserInfo>` —— 当前用户上下文（核心）  
  `SessionInfo<TUserInfo>` —— 不可变会话 record  
  `ISessionManager<TUserInfo>` + `SessionManager<TUserInfo>`（基于 `HybridCache`）

- **领域服务**  
  `IDomainService`  
  `DomainServiceBase<TUserInfo>`  
  `DomainController<TUserInfo>`（可选，带 AOP）

- **AOP 拦截体系**  
  `BaseInterceptor<TUserInfo>`（抽象基类）  
  `DomainInterceptor<TUserInfo>`（核心实现）  
  `DomainFilterAttribute<TUserInfo>`（抽象 Filter）  
  `DomainFlagAttribute`（标记型属性）  
  `DomainContext<TUserInfo>`（拦截上下文）  
  `DomainContracts<TUserInfo>`（缓存 Filter + Flag）

- **异常处理**  
  `DefaultGlobalExceptionLoggerFactory`（仅记录日志）  
  `InterceptorExceptionContext`（扩展字段：ErrorMessage、IsAuthenticationError 等）

---

## 6. 依赖注入与 Autofac 策略（现状）

框架提供了丰富的扩展方法（`DomainDiExtensions`）：

- `AddAopService<TContract, TImpl, TUserInfo>()` —— 启用异步拦截器（`EnableInterfaceInterceptors` + `AsyncInterceptorBase`）
- `AddService<T>()` —— 普通领域服务（无拦截）
- `RegisterInstanceForced<T>()` / `RegisterTypeForced<T>()` —— 强制单例，不可被后续覆盖
- `UseSessionManager<TSessionManager, TUserInfo>()` —— 强制单例
- `IfNotRegistered`、`PreserveExistingDefaults` 等常规 Autofac 能力均可使用

**关键点**：

- 领域核心服务默认采用 `SingleInstance()` 并倾向不可覆盖
- `RegisterInstance()` 优先级最高，可用于最终覆盖（如测试 Mock）

---

## 7. 当前实现状态评估

**优势**：

- 领域自治设计彻底，表现层与领域层解耦极佳
- 多平台复用性强（同一领域服务可在 Console、WPF、WebAPI 中使用）
- Filter 三级体系 + DomainContext 提供强大扩展能力
- 会话管理成熟（`HybridCache` 支持内存 + Redis）
- 异常处理机制优化为“仅记录日志 + 上下文增强”，不吞异常，原始异常可正常传播
- 异步拦截器稳定（基于 `AsyncInterceptorBase` + 同步转发兼容）

**已知问题（重点标注）**：

- Castle 异步拦截接口兼容性：当前 `BaseInterceptor` 继承 `AsyncInterceptorBase` 并实现 `IInterceptor` 接口，同步方法通过 `GetAwaiter().GetResult()` 转发到异步实现。**存在潜在隐患**：
  - 同步调用可能导致线程阻塞或死锁（在有 SynchronizationContext 的场景，如 ASP.NET、WPF、MAUI）
  - 转发过程中异常传播可能被扭曲（已通过测试缓解，但非完美方案）
  - **建议未来完全禁用同步拦截**，强制所有领域服务使用异步方法

**待完善**：

- 全局 Filter 仍为静态列表，已改为 `ConfigureGlobalFilterInstances` 虚方法（待更多 Filter 示例）
- 角色权限 Filter（`RequireRoleFlagAttribute`）实现待完成
- 事务、日志、缓存等常用 Filter 示例需补充完整
- 使用文档和 Starter Template 不足
- 扩展项目（Web、Blazor、Maui）的具体实现

---

## 8. 扩展项目架构

为保持 `TKWF.Domain` 核心框架的**纯净性和轻量性**（不引入任何表现层依赖），我们将不同宿主环境的适配代码分离到独立的扩展项目中。

### 8.1 扩展项目规划

| 项目名称                    | 目标平台                             | 主要职责                                                    | 依赖关系           | 当前状态    |
| ----------------------- | -------------------------------- | ------------------------------------------------------- | -------------- | ------- |
| **TKWF.Domain.Web**     | WebAPI、Minimal API、Blazor Server | Middleware、异常处理、ClaimsPrincipal 适配                      | 依赖 TKWF.Domain | 规划中（优先） |
| **TKWF.Domain.Blazor**  | Blazor WASM + Server 通用扩展        | AuthenticationStateProvider、ProtectedLocalStorage 封装    | 依赖 TKWF.Domain | 规划中     |
| **TKWF.Domain.Maui**    | MAUI、WPF、WinUI 桌面/移动应用           | SecureStorage、DomainUserService（INotifyPropertyChanged） | 依赖 TKWF.Domain | 规划中     |
| **TKWF.Domain.Console** | Console、Worker Service           | 控制台会话管理、命令行参数支持                                         | 依赖 TKWF.Domain | 后续      |
| **TKWF.Domain.Testing** | 单元测试支持                           | Mock DomainHost、Mock SessionManager                     | 依赖 TKWF.Domain | 后续      |

**设计原则**：

- 每个扩展项目**仅依赖 TKWF.Domain**，不反向依赖
- 公共逻辑（如 `ToClaimsPrincipal()`、错误响应模型）可提取到共享项目（如 `TKWF.Domain.Web.Common`）
- 后续每个扩展项目将**独立维护自己的文档**

**当前状态**：

- `TKWF.Domain.Web`：已完成 `SessionUserMiddleware` 和 `DomainExceptionMiddleware` 的设计
- `TKWF.Domain.Blazor`：已完成 `DomainAuthenticationStateProvider` 和 `ProtectedSessionStorage` 的设计
- `TKWF.Domain.Maui`：已完成 `MauiProtectedSessionStorage` 的设计

---

## 9. 当前实现状态评估与 TODO

**已完成**：

- 异步拦截器稳定（`AsyncInterceptorBase` + 同步转发兼容）
- 全局异常处理优化为“仅记录日志 + 上下文增强”
- `DomainUser.ToClaimsPrincipal()` 支持 Claims 转换
- `DomainHostInitializerBase` 支持 `ConfigureGlobalFilterInstances`

**TODO（优先级排序）**：

**高优先级（本周完成）**：

- [ ] 完成 `TKWF.Domain.Web` 项目（Middleware + 扩展方法）
- [ ] 完成 `TKWF.Domain.Blazor` 项目（AuthenticationStateProvider）
- [ ] 完成 `TKWF.Domain.Maui` 项目（SecureStorage 封装）

**中优先级（下周完成）**：

- [ ] 补充常用 Filter 示例（`LoggingFilterAttribute`、`TransactionFilterAttribute`）
- [ ] 完善 `RequireRoleFlagAttribute` + `AuthorityFilterAttribute`
- [ ] 提供官方 Starter Templates（WebAPI + Blazor Server）

**长期方向**：

- [ ] 插件系统（`IDomainModule` 接口）
- [ ] 领域事件总线
- [ ] OpenTelemetry 集成
- [ ] 文档与示例完善

---

## 10. 总结与讨论点

`TKWF.Domain` 已形成一套**以领域自治为核心**、**跨平台友好**的现代领域框架。通过 `DomainUser` + `Use/UseAop` + AOP Filter + 扩展项目分层设计，实现了业务逻辑的彻底封装和高度可测试性。

**当前重点讨论点**：

- `TKWF.Domain.Web`、`TKWF.Domain.Blazor`、`TKWF.Domain.Maui` 的项目结构是否合理？
- 是否需要增加 `TKWF.Domain.Web.Common` 共享项目？
- 异常处理机制是否符合预期（仅记录、不吞异常）？
- 下一步是否优先完成 `TKWF.Domain.Web` 的 Middleware 集成？
