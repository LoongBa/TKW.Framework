# TKWF.DomainV3 设计方案 V1.1

## 1. 架构概述

### 1.1 框架分层

- **TKWF.Common**：基础工具类、自定义异常、通用扩展。

- **TKWF.Domain**：领域核心框架（Host、AOP、Session、Context）。

- **TKWF.Domain.Web/Blazor/Maui**：宿主适配扩展，处理协议层差异。

- **TKWF.xCodeGen**：独立代码生成工具，驱动 Entity/DTO/Service 自动化。

- **持久层**：目前以 FreeSql 作为默认 ORM 实现（支持 CodeFirst）。

### 1.2 架构演进：实用主义 DDD

**V3.0+ 核心变革（3 层架构）**：

`Database ↔ Entity (兼顾映射与领域行为) ↔ Dto (xCodeGen 自动生成)`

**演进优势**：

- **消除冗余映射**：废弃 Model 层，移除无意义的属性 Copy。

- **认知统一**：Entity 是包含业务状态与数据库特性的唯一真相来源。

- **自动契约**：DTO 由生成器保证与 Entity 同步，自带高性能 `ApplyToEntity` 映射逻辑。

### 1.3 配置真相来源 (Single Source of Truth)

**框架坚持“领域修正原则”：**

1. `appsettings.json` 提供基础底色。

2. `EnvironmentVariables` 提供环境差异。

3. `ConfigTkwDomain` 回调提供运行时注入。

4. `DmpDomainInitializer.OnPreInitialize` 拥有最终否决权（如强制锁定生产环境的连接字符串加密）。

---

## 2. 核心模块设计

### 2.1 双重同步初始化与守门员机制

框架采用 `DomainOptions`（核心）与 `DomainWebOptions`（表现层）双重初始化。表现层配置传递后，领域层在 `OnPreInitialize` 钩子中拥有最终决定权，确保如 `IsDevelopment` 等关键状态不被表现层错误配置。

### 2.2 AOP 拦截体系：物理隔离与作用域锚定

这是框架保证异步数据一致性的核心机制。

#### 2.2.1 异步拦截器 (DomainInterceptor)

- **按需开启作用域**：拦截器在每次方法调用时从根容器执行 `BeginLifetimeScope()`。

- **逻辑沙箱**：每个 AOP 调用都拥有独立的物理子容器，确保数据库连接、事务和缓存上下文在并发请求下绝对隔离。

#### 2.2.2 作用域锚定 (Scope Anchoring)

- **AsyncLocal 驱动**：拦截器将子作用域赋值给 `DomainUser._ActiveScope`。

- **穿透解析**：在异步 `await` 链路中，`User.Use<T>` 通过该 `AsyncLocal` 变量定位到正确的子容器，解决了原生 DI 在异步环境下可能导致的“DbContext 已释放”或“跨请求共享连接”的问题。

### 2.3 验证体系：三层防御与物理阻断

框架构建了从入口到出口的严密验证链：

1. **DTO 验证 (入口层)**：利用 `EnumSceneFlags` 实现场景化校验（如 Create/Update 场景不同）。

2. **逻辑钩子 (扩展层)**：在 `*.biz.cs` 中实现 `OnBusinessValidate`，处理跨表、状态机等业务规则。

3. **物理阻断 (出口层)**：在 Service 的生成的 `Update/Create` 方法落库前，强制调用 `entity.Validate(ForceValidate)`。即使表现层漏掉了校验，领域层也会在最后关头阻断脏数据入库。

---

## 3. Web 宿主集成设计 (TKWF.Domain.Web)

### 3.1 三段式管道编排

通过 `DomainPipelineBuilder` 严格规定中间件顺序，解决 `Program.cs` 逻辑混乱问题：

- **BeforeRouting**：异常捕获（最外层）、静态文件、CORS。

- **Routing 阶段**：显式调用 `UseAspNetCoreRouting`，作为路由分水岭。

- **AfterRouting**：身份识别 (`SessionUserMiddleware`)、授权、端点映射 (`MapControllers`)。

### 3.2 异常映射机制

- **只记录，不吞没**：拦截器记录业务日志，但不处理异常。

- **协议转换**：`WebExceptionMiddleware` 捕获 `DomainException`，将其转换为标准的 HTTP 400/500 JSON 响应，确保领域层不感知 HTTP 状态码。

---

## 4. 依赖注入 (DI) 策略

- **确定性覆盖**：
  
  - `RegisterTypeForced`：核心业务服务，使用 `.PreserveExistingDefaults()`，防止被表现层意外替换。
  
  - `RegisterTypeReplaceable`：基础设施（日志、缓存），遵循最后注册胜出，允许表现层自定义实现。

- **性能优化**：`DomainContext` 等高频对象采用直接 `new` 实例化，避开 DI 容器解析开销。

---

## 5. xCodeGen 设计规范 V2.2

### 5.1 双轨制协作模式

- **生成轨道 (*.g.cs)**：由工具完全控制，包含属性定义、基础验证、仓储 Internal 调用、生命周期钩子定义。

- **手写轨道 (*.cs / *.biz.cs)**：由开发者维护，实现 `partial method` 钩子和复杂业务编排，保证代码同步时不被覆盖。

### 5.2 核心职责

- 自动生成 Entity 与 DTO 之间的硬编码赋值映射（替代 AutoMapper）。

- 基于数据库 Schema 自动生成验证元数据。

- 自动构建符合 `TKWF.Domain` 规范的服务骨架。

---

## 6. 后续演进 (Roadmap)

- **V4.0 目标：移除 Castle 依赖**：规划使用 Roslyn Source Generator 在编译时生成 Proxy 类，进一步提升 AOP 性能并简化堆栈调试。

- **宿主扩展**：完善 Blazor Server 专用路由阶段与 MAUI 安全存储适配。

- **事件驱动**：引入领域事件 (Domain Events) 异步发布机制。
