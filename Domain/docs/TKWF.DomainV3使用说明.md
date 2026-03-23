# TKWF.Domain V3 使用说明 V1.1

## 1. 介绍 (Introduction)

### 1.1 什么是 TKWF.Domain

TKWF.Domain 是 TKW 系列框架的领域层核心基础框架，始于 .NET 2.0 时代，在 .NET 4.x 时代趋于成熟。目前采用 **.NET 10+** 重新构建，为应用提供一套轻量、现代、可扩展且高度领域自治的基础设施。

### 1.2 核心特性 (Features)

- **领域自治**：业务逻辑完全封装在领域层。表现层（Web API、Blazor、MAUI 等）仅充当会话提供者和调用入口。

- **强类型上下文驱动**：所有领域服务必须通过唯一入口 `DomainUser<TUserInfo>` 获取，确保请求链路中的身份与权限绝对安全。

- **现代化 AOP 拦截体系**：提供方法级、控制器级、全局级的强类型拦截（Filter 与 Flag），支持事务、日志、权限等切面。

- **异常不吞没原则**：领域层针对异常仅进行日志记录和上下文填充，真实异常将抛给宿主层进行协议映射。

- **Entity 直连 ORM**：基于 CodeFirst 设计，Entity 直接作为领域实体与 FreeSql 联动，砍掉无意义的 Model 到 Entity 中间层。

---

## 2. 快速开始 (Getting Started)

### 2.1 环境准备

- **运行时**：.NET 10+

- **IDE**：Visual Studio 2025+ 或 VS Code (搭配 C# Dev Kit)

- **核心依赖**：Autofac, FreeSql, Castle.Core

### 2.2 领域初始化 (以 Web 项目为例)

在 Web 项目的 `Program.cs` 中，通过 `ConfigTkwDomain` 扩展方法注入领域能力：

C#

```
// 1. 初始化领域容器与 Web 管道编排
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(cfg => 
{
    cfg.UseDomainExceptionMiddleware = true; // 开启异常映射
    cfg.EnableDomainLogging = true;
})
.RegisterServices(services => {
    // 注册 Web 层特定服务
})
.UseAspNetCoreRouting()
.AfterRouting(app => {
    app.MapControllers(); 
});
```

---

## 3. 核心概念 (Core Concepts)

### 3.1 实体、DTO 与服务：基于 xCodeGen 的双轨制体系

在 V3.1 中，三者通过 `xCodeGen` 工具紧密联动，遵循 **"Generated (自动生成) + Partial (手动扩展)"** 的协作模式。

| **组件**           | **组成方式**                                        | **核心职责与逻辑**                                                                                    |
| ---------------- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| **Entity (实体)**  | `*.cs` (定义)<br>`*.g.cs` (验证)<br>`*.biz.cs` (业务) | **数据锚点与硬核验证**。对应数据库表。通过生成的 `Validate()` 方法在持久化前执行**物理阻断校验**。在 `*.biz.cs` 中处理跨字段的状态机等领域规则。      |
| **DTO (传输对象)**   | `*.g.cs` (生成)<br>`*.cs` (手写验证)                  | **契约守门员**。负责表现层输入/输出，利用 `record` 保证不可变性。支持 `EnumSceneFlags` 实现分场景（如：创建、更新）的差异化验证。              |
| **Service (服务)** | `*.g.cs` (基础)<br>`*.cs` (扩展)                    | **流程指挥官**。继承 `DomainServiceBase`。`*.g.cs` 提供 `Internal` 系列方法操作仓储；开发者在 `*.cs` 中编排业务流并实现 AOP 拦截。 |

#### 3.1.1 实战：物理阻断验证 (Physical Blocking)

`xCodeGen` 会在 `*.g.cs` 中生成基础长度、必填验证。开发者通过 `*.biz.cs` 扩展领域规则。该验证在 `Repo.Insert/Update` 前会被强制触发：

C#

```
// MerchantInfo.biz.cs
partial void OnBusinessValidate(EnumSceneFlags scene, List<ValidationResult> results)
{
    if (this.Status == MerchantStatusEnum.Disabled && this.EnableAutoReconciliation)
        results.Add(new ValidationResult("禁用状态下不能开启自动对账", [nameof(Status)]));
}
```

#### 3.1.2 实战：高性能 DTO 映射

框架弃用 AutoMapper，改为生成的硬编码赋值，实现微秒级转换：

- **场景化验证**：`createDto.Validate(EnumSceneFlags.Create)`。

- **赋值映射**：`createDto.ToEntity()` 或 `updateDto.ApplyToEntity(targetEntity)`。

### 3.2 领域用户与服务获取 (DomainUser)

`DomainUser<TUserInfo>` 是访问领域逻辑的唯一合法入口，封装了当前登录用户的身份 (`UserInfo`)。

- **内部服务获取**：`User.Use<TService>()`。获取原始实例，适用于领域内部协作。

- **受控服务获取**：`User.UseAop<IMerchantService>()`。获取受拦截器封装的代理实例，自动触发事务、日志等 Filter。

- **作用域锚定**：利用 `AsyncLocal` 确保在复杂的 `await` 链路中，子容器 (`ILifetimeScope`) 始终与当前调用上下文绑定。

### 3.3 异常处理策略

领域层遵循“**记录但不吞没**”原则。

- **业务逻辑异常**：直接抛出 `DomainException("消息")` 或 `ValidationResultsException`。

- **宿主联动**：宿主层的 `WebExceptionMiddleware` 会截获异常并转换为 400/500 JSON 响应，开发者无需在 Service 中编写 `try-catch`。

### 3.4 仓储与工作单元 (Repositories & UoW)

- **仓储**：目前推荐直接集成 FreeSql 的 `IBaseRepository<T>`。

- **事务控制**：在接口方法上标记 `[TransactionFilter]`。拦截器会在方法进入前开启事务，并在抛出异常时自动回滚，支持 SavePoint 嵌套。

---

## 4. 高级指南 (Advanced)

### 4.1 AOP 拦截器深度定制

框架支持三级 Filter：

1. **方法级**：标记在接口方法上。

2. **控制器/接口级**：标记在接口类上。

3. **全局级**：在 `DomainHostInitializer` 中通过 `ConfigureGlobalFilterInstances` 注册。

### 4.2 依赖注入 (DI) 策略

- **RegisterTypeForced**：用于核心业务服务，确保领域实现不被表现层意外覆盖。

- **RegisterTypeReplaceable**：用于基础设施（如缓存、日志），允许表现层根据宿主环境进行替换。

### 4.3 审计与钩子 (Hooks)

利用 Service 中的 `partial method` 钩子自动处理审计字段：

C#

```
// MerchantInfoService.cs
partial void OnBeforeCreate(MerchantInfo entity)
{
    entity.CreateTime = DateTime.Now;
    entity.LastOperatorUid = User.UserInfo.UserIdString; 
}
```

---

## 5. 附录：Web 项目集成参考

在基于 `TKWF.Domain.Web` 的项目中，典型的 `Program.cs` 管道结构如下：

C#

```
var builder = WebApplication.CreateBuilder(args);
builder.ConfigTkwDomain<DmpUserInfo, DmpDomainInitializer>(...)
       .RegisterServices(s => ...)
       .BeforeRouting(app => { app.UseStaticFiles(); }) // 第一段
       .UseAspNetCoreRouting()                          // 第二段：路由分水岭
       .AfterRouting(app => { app.MapControllers(); }); // 第三段
```

---

## 6. 扩展生态与子项目

为保持核心框架纯净，特定平台的适配逻辑被分离到独立的扩展项目中：

- TKWF.Domain.Web：WebAPI / Minimal API 宿主集成
- TKWF.Domain.Blazor：Blazor WASM / Server 的状态与缓存支持（规划中）
- TKWF.Domain.Maui：桌面/移动端的本地安全存储封装（规划中）
- TKWF.EntityORM：暂未升级，目前先使用 FreeSql
- TKWF.xCodeGen：独立的跨平台代码生成工具

---

# 
