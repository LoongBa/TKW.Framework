# TKWF.Domain 领域基础框架 V3.0

TKWF.Domain 是 TKW 系列框架的领域层核心基础框架，始于 .NET 2.0/ASP.NET 2 时代，在 .NET 4.x 时代趋于成熟。

目前采用 .NET 10+ 重新构建（2006年1月）。

其核心目标是为应用提供一套轻量、现代、可扩展且高度领域自治的基础设施。

框架分为几个主要部分：

- **TKWF.Common**

- **TKWF.Domain**：领域基础框架
  
  - **TKWF.Domain.Web**
  
  - **TKWF.Domain.Blazor**
  
  - **TKWF.Domain.MAUI**
  
  - ... ...

- **TKWF.EntityORM**：数据库与对象映射（暂未升级，目前先使用 FreeSql）

- **TKWF.xCodeGen**：基于元数据和模板的代码生成器（简称 xCodeGen）

- **TKWF.Tools**：若干工具模块

- **TKWF.Extensions**：若干扩展模块

## 1. 核心设计理念

- **领域自治**：业务逻辑完全封装在领域层。表现层（如 Web API、Blazor、MAUI 等）仅充当“会话提供者”和“调用入口”，绝不在表现层控制器或视图中处理任何核心业务逻辑。

- **强类型上下文驱动**：所有领域服务必须通过唯一入口 `DomainUser<TUserInfo>` 获取。这确保了请求链路中的身份与权限绝对安全，实现了基于强类型的租户或用户隔离。

- **现代化 AOP 拦截体系**：提供方法级、控制器级、全局级的强类型拦截（Filter 与 Flag）。后续将全面抛弃基于反射的动态代理，转向基于 Source Generator（源生成器）的编译时代理，彻底消除异步调用的死锁隐患，并完美支持 .NET 的 Native AOT。

- **异常不吞没原则**：领域层坚持“仅诊断，不决策”。针对异常仅进行日志记录和上下文填充，真实业务异常（如基础框架中定义的 `DomainException`）将直接向外抛出，交由特定宿主层（如 Web 层的 `DomainExceptionMiddleware`）进行协议级（如 HTTP 状态码）的映射和统一响应返回。

## 2. 宿主集成与快速上手

- **系统接入与初始化**：应用启动时，通过统一的扩展入口 `ConfigTkwDomain` 即可完成领域容器的初始化与宿主管道的有序编排。框架严格保护领域自治，表现层配置通过 `DomainOptions` 传递后，领域层可在 `OnPreInitialize` 钩子中进行“守门员”式修正（例如检测到生产库连接时强制关闭开发模式开关）。在 Web 等宿主环境中，系统管道被显式划分为 `BeforeRouting`（路由前）、`Routing`（路由分水岭）和 `AfterRouting`（路由后）三个阶段，以保证中间件执行顺序的绝对确定性。

- **业务服务调用规范**：开发中严禁直接从系统的依赖注入（DI）容器获取业务服务。业务调用必须严格遵守代理模式：
  
  1. 从上下文（如 HttpContext 或本地会话）中获取已认证的用户实例 `DomainUser<TUserInfo>`。
  
  2. 通过 `user.UseAop<IService>()` 获取受 AOP 拦截包裹的领域服务，或者通过 `user.Use<IService>()` 获取普通内部服务。
  
  3. 触发业务方法，框架底层会自动穿透并执行权限校验、日志记录、事务控制等各个生命周期拦截器。

## 3. 核心模块与扩展生态职责分工

为了保持 `TKWF.Domain` 核心框架的绝对纯净（不引入任何与表现层相关的第三方依赖包），特定技术栈的适配逻辑被严格分离到了独立的扩展项目中：

- **TKWF.Domain (核心层)**：定义系统的心脏，包括 `DomainHost` 宿主管理、`DomainUser` 会话流转、基础拦截器契约、核心 Filter 机制以及基础的 `DomainException` 异常定义。

- **TKWF.Domain.Web (Web 宿主扩展)**：处理与 HTTP 协议相关的一切逻辑。包含将业务异常转换为标准 JSON 响应的 `DomainExceptionMiddleware`、解析 Cookie/Header 会话的 `SessionUserMiddleware`，以及 Web 管道的扩展编排方法。

- **TKWF.Domain.Blazor (Blazor 扩展)**：专注 Blazor 的状态与缓存支持。提供 `DomainAuthenticationStateProvider` 以桥接领域用户与 Blazor 认证体系，以及受保护的浏览器存储封装。

- **TKWF.Domain.Maui / ... 等跨平台扩展**：负责桌面端、移动端的底层适配。封装 SecureStorage 等本地安全存储，处理离线或弱网环境下的领域用户状态保持。

- **TKWF.xCodeGen (代码生成引擎)**：作为独立的跨平台代码生成工具。它与领域运行时代码完全解耦，主要职责是读取数据库或结构化元数据，并基于模板引擎自动化生成实体模型、数据传输对象（DTO）以及标准化的服务框架骨架代码。

- **TKWF.EntityORM**：暂未升级，目前先使用 FreeSql 提供数据持久化支持。
