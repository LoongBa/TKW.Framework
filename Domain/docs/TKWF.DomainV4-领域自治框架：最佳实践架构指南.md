# TKWF.Domain V4 领域自治框架：最佳实践架构指南

> v1.0 初版 2026.04.26

## 前言：版本演进

> 历史演进说明：
> 
> + `V1` 基于 `.NET 2.x`： 早期采用继承复用，后期改为聚合复用来解决`自动生成与手写部分的分离`问题；
>   
>   + 延续`ASP/JSP`时代的设计思路实现的基础`ORM`，采用`ASP`作为模板引擎生成代码；
>   
>   + 数据库访问部分，早期沿用`VC/ATL`编写的`数据库自动映射COM组件`，后期改为基于`EntityFramework`设计的`ORM`
>   
>   + `DataService`雏形：06年设计了`基于Url的查询指令`，自动对接业务方法完成查询并返回`Json`结果供表现层调用（类似后来`OData`），初步实现`AOP`。
>   
>   + 分布式框架：同期设计实现了`缓存层`（支持本地、分布式，以及Dump和加载）、调度层（自动负载均衡）、数据注册和配置中心（升级的活动目录）。
> 
> + `V2` 基于 `.NET 3-4.x`：采用新出的`partial`很好的解决了`自动生成与手写部分的分离`，基于`CodeSmith`模板实现自动生成；
>   
>   + `ORM`采用配套工具提取数据结构`Schema`产生`XML配置文件`，简单配置后，用`CodeSmith`生成代码
>     
>     + 遗憾：`半自动衔接`，未能完成自动化，未能实现`CodeFirst`方式`ORM`。
>   
>   + `ORM`设计架构：通过`IEntityDAC<Entity>`并实现`MSSqlDAC`支持`热插换`和`跨库事务`；
>   
>   + `ORM`设计架构增强：基于模板为每个`Entity`生成对应`Model`和`EntityDaHelper`静态类，提升性能；
>   
>   + `DataService`框架：完整的**领域框架雏形**，基于动态加载和反射，自动将配置的`DataService<Entity>`类序列化后返回。
>     支持多种查询和响应级的格式化器，如`Json/Xml`，支持基于配置的`AOP`。
>     
>     + 遗憾：未能完成全自动配置，第一次需要手工配置。
>     
>     + 遗憾：计划，但未能完成动态注入——距离容器只差一步。
>   
>   > 这一阶段，逐渐脱离开发工作，主要精力在商务协调和产品设计。
> 
> + `V3` 基于 `.NET Core`重写：完整实现领域自动配置、依赖注入、代码自动生成、`AOP框架`。
>   
>   + 前期，基于`T4`实现代码生成、基于`Autofac Dynamic Proxy`实现`AOP框架`、基于`FreeSql`代替原`ORM`，`领域控制器`可在`控制台/桌面`、`Web/Web Api`、`测试环境`中独立应用；
>   
>   + 后期，基于`.NET 10`重写代生成器`xCodeGen`：通过`源生成器 SourceGenerator`提取元数据，再通过`xCodeGen Cli`调用模板生成代码。
>   
>   > 这一阶段，已经离开软件开发行业，纯粹是兴趣爱好。
> 
> + `V4` 基于`.NET 10`升级：基于`SourceGenerator` + `装饰器模式`实现`静态 AOP 框架`，完全不再依赖`Autofac`
>   
>   + `ORM 框架层`：重新引入`IEntityDAC<Entity>`、`DataService<Entity>`，基于`FreeSql`封装`FreeSqlEntityDAC`，后续可扩展；
>   
>   + `AOP 框架层`：自动注册`DataService<Entity>`、手写的`DomainControllerBase<Entity>`+`IAopContrace`契约，自动提供服务。
>   
>   > 为了让多 AI Agent 更好的工作，重新升级、优化。

---

## 一、框架核心价值：从“手工组装”到“领域自治”

**领域自治**的核心目标是让开发者从“写代码满足框架”变为“写代码只关注业务”。
通过物理级的执行流隔离，V4 架构带来了以下直观收益：

### 1. 逻辑零扩散：表现层与业务的“物理脱耦”

在传统架构中，业务逻辑经常由于对 `HttpContext`、`Session` 或特定 DI 容器的依赖而散落在 Web 层。

* **自治优势**：逻辑被严丝合缝地锁定在领域层内，表现层（Web/Blazor/WASM）仅充当“流量入口”。

* **适配友好**：无论宿主是 Web API、Blazor Server、MAUI 还是控制台，业务逻辑代码无需一行修改即可平滑迁移。

### 2. 测试友好：摆脱宿主依赖的“纯净”验证

由于业务不依赖于 Web 环境，单元测试和集成测试变得异常简单：

* **快速验证**：无需启动 Web 宿主或模拟复杂的 HTTP 上下文，通过 `DomainXunitTestBase` 即可秒级开启领域作用域进行业务验证。

* **确定性控制**：测试代码可以精准控制 `DomainUser` 的身份与权限，模拟各种复杂业务场景。

### 3. 调用之美：极简的入口

在传统架构中，解析服务往往需要处理复杂的 DI 容器或单例。在 V4 中，所有操作都回归到“用户（User）”这一核心实体：

* **业务编排**：`User.Use<OrderService>().Create(...)` —— 像调用本地方法一样调用原子服务，框架自动处理所有权注入与连接插电。

* **契约访问**：`User.UseAop<IPaymentController>().Process(...)` —— 无论在哪种宿主环境，一行代码即可享受完整的权限校验、事务控制与审计日志。

> **架构提示**：无论在哪种表现层配置下，开发者都应优先通过 `DomainUser.UseAop<IContract>()` 调用业务，这能确保所有的 **AOP 过滤器（权限、事务、日志）** 能够正常触发。对于领域内部的原子调用，则直接使用 `User.Use<Service>()` 以获得零反射的极致性能。

### 4. 编写之美：逻辑只需“拼积木”

框架通过 **SourceGenerator (SG)** 和 **xCodeGen CLI** 彻底消灭了重复劳动：

* **零成本数据层**：只需定义 `Entity` 实体，`ORM` 映射、`Dto` 转换、基础 `CRUDQ` 的 **DataService** 全自动生成。

* **原子化组装**：在控制器中，开发者无需手写复杂的数据库访问逻辑，只需调用自动生成的原子级方法进行业务拼装。

* **契约自动提取**：编写控制器逻辑时，SG 自动提取契约接口并生成 `AOP 装饰器`，实现“**写类即得接口，写代码即得代理**”。

### 5. 运维之美：全自动装配

彻底告别数千行且难以维护的 `Program.cs`：

* **自动注册**：领域框架根据 **MetaType** 自动决策注册路径（AsSelf 或 Interface+Proxy），无需手动配置 DI。

* **多宿主自适应**：同一套业务代码，通过“双层初始化器”自动适配 Web API、Blazor Server 或控制台，框架自动处理连接池管理与异步流隔离。

**解决的痛点**：

* ❌ 繁琐的 `services.AddScoped<...>` 手动注册。

* ❌ 手写 Dto 转换和 ORM 样板代码。

* ❌ 跨环境运行（如测试与 Web）时复杂的初始化差异。

* ❌ 多线程/长连接环境下容易出现的数据库连接泄漏或“用户串号”风险。

---

## 二、领域内部分层与注册策略

在 `TKWF.Domain` 体系下，一个标准的功能模块分为三个概念层级。V4 版本针对不同的层级引入了基于 `MetaType` 的差异化依赖注入策略：

| **层级**     | **组件名称**        | **核心职责**                                                          | **维护方式**                                            | **基类与特性**              | 存储位置           |
| ---------- | --------------- | ----------------------------------------------------------------- | --------------------------------------------------- | ---------------------- | -------------- |
| **元数据层**   | **Entity/View** | 定义数据模型、DB 映射与验证契约。                                                | **CodeFirst**方式手写、标注<br/>**SourceGenerator**自动提取元数据 | `IDomainEntity`        | /Entities      |
| **数据服务层**  | **Dto**         | **Dto**用于外部访问，自动与**Entity**验证、转换。                                 | `xCodeGen.CLI`根据元数据调用模板生成                           | `IDomainDto`           | /Entities/Dtos |
|            | **DataService** | 内部访问的基于 `IEntityDAC` 实现的 **CRUDQ** 方法<br/>公开访问的基于数据库约束等关系的扩展查询方法。 | `xCodeGen.CLI`根据元数据调用模板生成                           | `DomainDataService`    | /Services      |
| **业务控制器层** | **Controller**  | 业务编排、跨服务协作、**AOP 拦截控制**。                                          | 手工编写、维护（**AOP契约**）                                  | `DomainControllerBase` | /Controllers   |

> **核心原则**：
> 
> 领域框架的源生成器会自动发现 `DomainDataService`、`DomainControllerBase` 的子类；
> 
> 随后生成 `RegisterGeneratedDomainServices()`自动注册方法，由框架初始化器自动调用，完成自动注册。
> 
> 注册时会自动判断：
> 
> * 如果类型是 `Controller` 且具备`契约接口`和`代理类`，则注册为基于`契约接口`的 `AOP 拦截模式`；
> 
> * 如果属于领域框架自动生成的 `Service` 或 `DataService`，则直接作为类本身 `AsSelf` 注册。

---

## 三、开发流程：从数据到执行流

与正常软件开发一致，分为四步，每一步都很简单，仅在平时工作的基础上稍微注意即可。

### 第一步：模型驱动 ORM（元数据Metadata）

开发者编写实体类，标注 `ORM` 标记。

- **源生成器 SG 作用**：实时提取字段元数据，供编译时感知。同时为`Controller`生成`AOP 装饰器代理`；

- **模板生成器 xCodeGen Cli 作用**：运行`cli`，生成 `{Entity}Dto.g.cs` 和 `{Entity}Service.g.cs` 和对应的骨架文件。

### 第二步：数据服务与内部服务（DataService/DomainService）

生成的`{Entity}Service`自动继承`DomainDataService`：

- **不参与 AOP**：它们属于原子操作，不参与 AOP，直接以原始类注册到容器——不提取接口，不生成`AOP代理类（装饰器）`。

- **注册方式**：领域框架自动注册到领域容器。

- **标注拦截器**：在方法或接口上标注 `[AuthorityFilter]`、`[LoggingFilter]` 等 AOP 特性。

- **访问权限**：模板生成的**扩展查询方法**建议设为`internal`，保护领域边界。

### 第三步：业务控制器（DomainController）

开发者根据业务需求创建 `{Entity}Controller.cs`作为对外接口：

1. **定义契约**：手写`{Entity}Controller` 接口`I{Entity}Controller`，并继承 `IAopContract`；

2. **标注拦截器**：在接口或方法上标注 `[AuthorityFilter]`、`[Transactional]` 等`契约特性`；
   ——建议在控制器的方法上标注`契约特性`，随后用 Cli 自动提取生成`契约接口`（自动复制特性）

3. **调用逻辑**：通过 `User.Use<PaymentLogService>()` 或`User.UseAop<PaymentLogController>()`调用服务和控制器。

### 第四步：业务代码执行流入口（Execution Flow Entry）

外部程序（Web/UI/Job）想要访问领域层，**必须**通过 `DomainHost` 创建或绑定生命周期作用域。

框架提供了两种绝对安全的所有权管理模式：

* **逻辑切入 (重用宿主 Scope)**：如 `Web API` 中间件或 `Blazor CircuitHandler`，使用 `BeginSessionScopeAsync(sp, key)` 将现有的请求容器绑定到领域插座。

* **工厂创建 (生成独立 Scope)**：如 单元测试或控制台 Job，使用 `CreateSessionScopeAsync(key)` 创建全新的沙盒环境，业务结束后自动物理销毁（`Dispose`），防止资源一直被占用（如数据库连接溢出）。

---

## 四、代码示例与全景对比

### 1. 领域内部：数据服务层（非 AOP）

内部数据服务专注于高速读写。直接解析类，无装饰器开销。

```csharp
// PaymentLogService.g.cs (由 Cli 自动生成)
// 纯粹的数据访问者，采用 AsSelf 注册
internal partial class PaymentLogService : DomainDataService<DmpUserInfo, PaymentLog, PaymentLogDto>
{
    // ... 自动生成的 SelectByBatchAsync 等方法 ...
}
```

### 2. 领域边界：业务控制器（开启 AOP）

`Controller` 作为领域的门面，对外提供契约，框架利用 SG 自动生成装饰器包裹它。

```csharp
// IPaymentLogController.cs (手工编写，对外暴露的 AOP 契约)
public interface IPaymentLogController : IAopContract
{
    [AuthorityFilter] // 需要校验权限
    [Transactional]   // 需要开启事务
    Task<PaymentLogDto> ProcessRefundAsync(long logId);
}

// PaymentLogController.cs
public class PaymentLogController(DomainUser<DmpUserInfo> user) : DomainControllerBase<DmpUserInfo>(user), IPaymentLogController
{
    public async Task<PaymentLogDto> ProcessRefundAsync(long logId)
    {
        // 高频调用内部 Service 时，使用 Use<T>（内部实现了高效的构造工厂和插座寻找）
        var log = await Use<PaymentLogService>().GetByIdAsync(logId);

        // ... 执行退款业务编排逻辑 ...
        return log;
    }
}
```

### 3. 外部环境：多宿主环境的统一调度

外部无论在什么环境下调用领域逻辑，都遵循 **“开启业务 Scope -> 获得绑定容器后的 User -> 发起调用”** 的标准范式。

#### 场景 A：Web API 请求（绑定请求作用域）

```csharp
// SessionUserMiddleware.cs
public async Task InvokeAsync(HttpContext context)
{
    var sessionKey = GetSessionKeyFromRequest(context);

    // Begin 模式：将 Web 的 RequestServices 插入领域执行流，退出时不物理销毁
    await using (var scope = await domainHost.BeginSessionScopeAsync(context.RequestServices, sessionKey))
    {
        context.User = scope.User.ToClaimsPrincipal();
        await next(context);
    } // 退出时自动拔掉领域插座，保证线程池安全
}
```

#### 场景 B：控制台、后台服务 Job、单元测试（新建作用域并自我管理）

```csharp
// BackgroundJob / Test
public async Task RunJobAsync()
{
    // Create 模式：主动构建沙盒环境，拥有所有权
    await using (var scope = await domainHost.CreateSessionScopeAsync(jobSessionKey))
    {
        // 外部系统调用 AOP 契约，享受完整的拦截服务
        var controller = scope.User.UseAop<IPaymentLogController>();
        await controller.ProcessRefundAsync(12345);
    } // 退出时自动 UnBindScope 并物理 Dispose 底层数据库连接
}
```

#### 场景 C：Blazor Server（依靠电路处理器挂载环境）

在 Blazor Server 环境中，`CircuitHandler` 是管理领域作用域的最佳切入点。

V4 框架提供了 `DomainCircuitHandlerBase` 基类，通过“逻辑电路”与“物理连接”的双重绑定机制，确保在 SignalR 链路抖动、重连或销毁时，领域插座始终处于正确的状态，彻底杜绝多用户并发下的执行流污染。

```csharp
// DomainCircuitHandler.cs
public class DomainCircuitHandler(IServiceProvider sp) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken token)
    {
        // Blazor 等长生命周期 UI 框架，在电路开启时保持插电
        // 内部已调用：DomainUser<TUserInfo>.BindScope(sp);
        return base.OnCircuitOpenedAsync(circuit, token);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken token)
    {
        // 电路销毁时强制拔掉插座，UI 事件彻底释放
        DomainUser<TUserInfo>.UnBindScope();
        return base.OnCircuitClosedAsync(circuit, token);
    }
}
```

> `Blazor Server` 场景为什么需要四重保障机制？
> 
> 1. **逻辑层 (Circuit)**：`Opened/Closed` 对应用户会话的开始与结束。它是领域环境的“总开关”，确保 `Scoped` 容器在整个电路存续期间对领域层可见。
> 
> 2. **物理层 (Connection)**：`Up/Down` 对应 SignalR 链路的状态。由于 Blazor Server 共享线程池，当连接断开（Down）时及时 `UnBind`，可以防止该线程被归还给线程池后，其携带的 `AsyncLocal` 遗迹污染其他用户的任务。
> 
> 3. **重连鲁棒性**：在移动端等网络不稳定的场景下，`OnConnectionUp` 确保了每次链路恢复时，领域模型所需的上下文环境都能“满血恢复”，避免了因线程切换导致的插座丢失风险。

---

## 五、领域配置

TKWF.Domain V4 采用 **双层初始化器（Two-Level Initializer）** 架构，旨在将“`框架级的自动化装配`”与“`项目级的个性化配置`”完美解耦。

### 1. 框架级初始化器（Base Level）

`DomainHostInitializerBase<TUserInfo>` 负责处理领域环境运行所需的**工业级模板代码**：

* **元数据自动装配**：调用 `RegisterGeneratedDomainServices()`，依据 SG 提取的元数据完成 Service/Controller 的分流注册。

* **基础设施锚点**：提供 `ISessionManager`、`ILoggerFactory` 等核心组件的默认实现（如 `NoSessionManager`）。

* **生命周期生命控制**：定义 `ServiceProviderBuiltCallback` 回调，在 `IServiceProvider` 构建后的第一时间将其绑定到 `DomainHost.Root`，并激活全局过滤器（如权限校验、审计日志）。

### 2. 项目级初始化器（Project Level）

开发者通过继承基类实现具体项目的初始化（如 `DmpDomainInitializer`）：

* **环境验证**：在 `OnPreInitialize` 中检查连接字符串等关键配置。

* **独占资源注册**：在 `OnRegisterInfrastructureServices` 中初始化 `FreeSql`（ORM）、分布式缓存等单例资源。

* **手工服务补漏**：在 `OnRegisterDomainServices` 中注册未被 SG 覆盖的自定义服务或 `UserHelper` 实现。

### 3. 启动流程时序

1. **点火 (Pre-Init)**：配置检查与环境预设。

2. **装配 (Register)**：SG 自动注册与手动 DI 注册混合执行。

3. **联调 (Callback)**：容器构建完成，触发 `BindServiceProvider`，领域插座（AsyncLocal）获得电力。

4. **就绪 (Built)**：执行项目特定的验证逻辑或预热任务。

## 六、表现层配置

TKWF.Domain 领域框架针对不同的使用场景有不同的配置，但它们基本遵循一致的原则和风格。

> **核心原则**：**“根据宿主生命周期，寻找最合适的切入点进行插电（Bind绑定容器）与拔电（UnBind取消绑定容器）”**。
> 
> `V4`与`V3`最大不同：`Autofac`自动完成这部分功能，`V4`去`Autofac`后需要自行实现。

### 1. 场景 A：Web API / Blazor Web 请求级别挂载

> 详见《TKWF.Domain.Web 启动配置设计和使用说明》

适用于传统的“请求-响应”模型，如 Web、WebAPI 等。

* **机制**：通过 `SessionUserMiddleware` 中间件自动完成。

* **特征**：中间件自动调用 `BeginSessionScopeAsync` 重用 Web 容器，在每个 HTTP 请求进入时绑定作用域，请求结束时自动清理线程上下文。

### 2. 场景 B：Blazor Server（电路级别挂载）

> 详见《待补》

适用于长连接、线程池高度复用的环境。

* **机制**：通过 `DomainCircuitHandlerBase` 处理器。

* **特征**：实现“逻辑电路（Circuit）”与“物理连接（Connection）”的双重防护。在 SignalR 链路开启或恢复（Up）时插电，在链路中断（Down）或销毁时拔电，彻底防止多用户间的“AsyncLocal 污染”。

### 3. 场景 C：MAUI / Console（手动/单用户挂载）

> 详见《待补》

适用于桌面端或后台任务。

* **机制**：通过 `CreateSessionScopeAsync` 工厂。

* **特征**：领域层拥有物理作用域的绝对所有权。开发者通过 `await using` 手动控制环境的开启与物理销毁，适用于单用户隔离或独立的 Job 任务。

## 七、其它扩展

* **无状态 JWT 会话继承**：支持从 JWT Claim 中提取 SessionKey，在分布式环境下实现无感知的会话还原。

* **分布式会话存储扩展**：通过实现自定义 `ISessionManager`，支持 Redis 或数据库存储会话状态。

* **gRPC 领域适配**：针对 gRPC 的流式处理特性，提供专用的拦截器进行作用域绑定。

* * *

> **框架小结**：
> 通过 `User.Use<T>` 调用自动生成的 `DataService`、`DomainService`，通过`User.UseAop<T>`调用快速开发的 `DomainController`，
> `TKWF.Domain V4` 将**领域驱动设计**（DDD）的落地成本降低到了极致，让“**领域自治**”成为**性能与效率**的平衡点。
