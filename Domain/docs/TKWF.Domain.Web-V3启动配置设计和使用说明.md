# TKWF.Domain.Web V3 启动配置设计和使用说明

文档版本：V1.2

## 1 设计目标

TKWF.Domain.Web 是 TKWF.DomainV3 框架的官方 Web 宿主扩展，专一处理 HTTP 协议相关适配逻辑，与核心领域层完全解耦，确保 Domain 层不依赖任何 Web 环境。
**职责边界**

- **Web 层**：会话解析与注入、请求管道有序编排、领域异常→HTTP 响应标准化转换、路由阶段管控。
- **Domain 层**：业务逻辑、AOP 拦截、权限校验、事务与数据一致性保障。
  两层无交叉渗透，Web 层仅做协议适配，不承载任何业务规则。

---

## 2 核心设计原则

1. **协议与领域完全解耦**：Web 层不侵入 Domain 逻辑，仅作为请求入口与会话提供者。
2. **强类型链式构建**：按阶段返回不同构建器类型，由编译器强制约束配置顺序，禁止乱序/跳过。
3. **IStartupFilter 管道驱动**：所有中间件配置收集为动作列表，由 `DomainPipelineFilter` 统一按序注入，保证执行顺序可控。
4. **配置即时绑定+启动校验**：配置支持闭包内即时生效，启动时完成数据注解与自定义校验，快速失败。
5. **异常统一收口**：全局异常中间件捕获所有未处理异常，输出标准化 JSON 响应。

---

## 3 构建器链路结构

### 3.1 完整链路（代码实际实现）

```
ConfigWebAppDomain<TUserInfo, TInitializer>(cfg => ...)
 └─ 返回 WebAppBuilder<TUserInfo>
 │
 ├─ .NoSession() → SessionSetupBuilder（注入 NoSessionManager）
 └─ .UseWebSession(...) → SessionSetupBuilder（注册会话管理器+会话中间件）
 │
 ├─ .NoRouting(...) → 极简模式（终止链式）
 └─ .BeforeRouting(...) → BeforeRoutingBuilder（路由前配置）
 │
 ├─ .UseAspNetCoreRouting(...) → RoutingBuilder
 └─ .UseCustomRouting(...) → RoutingBuilder
 │
 └─ .AfterRouting(...) → AfterRoutingBuilder（可多次链式）
```

### 3.2 构建器职责

| 构建器类型                    | 所属阶段  | 核心职责                                                         |
| ------------------------ | ----- | ------------------------------------------------------------ |
| WebAppBuilder<TUserInfo> | 入口构建  | 注册 DomainPipelineFilter、自动注入 WebExceptionMiddleware、提供会话模式配置 |
| SessionSetupBuilder      | 会话配置后 | 承接路由前配置或极简无路由模式                                              |
| BeforeRoutingBuilder     | 路由前阶段 | 提供标准/自定义路由配置，自动处理认证授权注册                                      |
| RoutingBuilder           | 路由分水岭 | 配置请求终结点映射                                                    |
| AfterRoutingBuilder      | 路由后阶段 | 支持多次追加终结点配置                                                  |
| DomainPipelineFilter     | 管道注入  | 按注册顺序执行所有中间件动作，最后执行系统默认管道                                    |

---

## 4 构建流程详解

### 4.1 入口：ConfigWebAppDomain 扩展方法

```csharp
public static WebAppBuilder<TUserInfo> ConfigWebAppDomain<TUserInfo, TInitializer>(
 this WebApplicationBuilder builder,
 Action<DomainWebOptions>? configure = null)
```

**执行逻辑**

1. 实例化 DomainWebOptions，自动同步 `IsDevelopment = builder.Environment.IsDevelopment()`。
2. 执行用户配置回调，支持覆盖任意选项。
3. 默认自动注册 `IHttpContextAccessor`。
4. 接入 Autofac 容器工厂，初始化 DomainHost。
5. 返回 WebAppBuilder，自动注册 DomainPipelineFilter 并按需注入 WebExceptionMiddleware。
   
   ### 4.2 WebAppBuilder：会话模式配置
   
   #### 4.2.1 NoSession()
   
   注册 `NoSessionManager<TUserInfo>`，适用于无会话后台服务/定时任务，返回 SessionSetupBuilder。
   
   #### 4.2.2 UseWebSession（3 个重载）
6. 默认使用 `WebSessionManager<TUserInfo>`
7. 支持自定义 `TSessionManager`
8. 支持传入已有 `ISessionManager<TUserInfo>` 实例
   **公共逻辑**：配置 WebSessionOptions、自动注册 IIdGenerator、注册会话管理器、加入 SessionUserMiddleware、返回 SessionSetupBuilder。
   
   ### 4.3 SessionSetupBuilder
- `BeforeRouting(...)`：进入路由前配置阶段
- `NoRouting(...)`：极简模式，标记路由完成，终止链式
  
  ### 4.4 BeforeRoutingBuilder
  
  #### 4.4.1 UseAspNetCoreRouting
  
  标记 `HasRoutingPhase = true`，添加 `UseRouting`，自动注册并启用认证授权中间件，返回 RoutingBuilder。
  
  #### 4.4.2 UseCustomRouting
  
  标记 `HasRoutingPhase = true`，执行自定义路由逻辑，返回 RoutingBuilder。
  
  ### 4.5 RoutingBuilder / AfterRoutingBuilder
  
  `AfterRouting(...)` 内部调用 `app.UseEndpoints` 配置终结点，支持多次链式追加，返回 AfterRoutingBuilder。
  
  ### 4.6 DomainPipelineFilter（管道执行核心）
  
  ```csharp
  internal sealed class DomainPipelineFilter(List<Action<IApplicationBuilder>> actions) : IStartupFilter
  {
  public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
  {
  return app =>
  {
  foreach (var action in actions) action(app);
  next(app);
  };
  }
  }
  ```

---

## 5 核心中间件

### 5.1 WebExceptionMiddleware 统一异常中间件

**注入时机**：管道首位，自动注入
**异常映射**
| 异常类型 | HTTP 状态码 | ErrorCode |
|----------|-------------|-----------|
| AuthenticationException | 401 | AUTH_UNAUTHENTICATED |
| UnauthorizedAccessException | 403 | AUTH_FORBIDDEN |
| DomainException | 400 | domainEx.ErrorCode |
| 其他异常 | 500 | INTERNAL_ERROR |
**响应格式**：标准化 JSON，DEBUG 模式附加堆栈，生产环境隐藏敏感信息。

### 5.2 SessionUserMiddleware 会话用户中间件

**SessionKey 提取优先级**：Header → Cookie → Query → Form（仅 POST+HasFormContentType）
**执行流程**：提取 Key → 加载/创建会话 → 注入 DomainUser → 构建 ClaimsPrincipal → 执行后续管道
**异常处理**：直接抛出，由 WebExceptionMiddleware 统一处理。

### 5.3 WebSessionManager 会话管理器

基于 HybridCache 实现二级缓存，支持会话全生命周期管理，采用**半程滑动过期**优化，减少缓存写入。
---

## 6 配置体系

### 6.1 DomainWebOptions（继承 DomainOptions）

| 属性                         | 类型                | 默认值   |
| -------------------------- | ----------------- | ----- |
| UseWebExceptionMiddleware  | bool              | true  |
| AutoAddHttpContextAccessor | bool              | true  |
| SuppressRoutingWarning     | bool              | false |
| HasRoutingPhase            | bool（internal）    | false |
| WebSession                 | WebSessionOptions | new() |

### 6.2 WebSessionOptions（继承 DomainSessionOptions）

| 属性         | 类型           | 默认值              |
| ---------- | ------------ | ---------------- |
| CookieName | string       | 同 SessionKeyName |
| MaxAge     | TimeSpan     | 30分钟             |
| SameSite   | SameSiteMode | Lax              |
| HttpOnly   | bool         | true             |
| HeaderName | string       | X-Session-Key    |
| QueryName  | string       | sk               |
| FormName   | string       | sessionKey       |

### 6.3 DomainConfigurationBinder 配置绑定

支持配置节即时绑定、DI 注册、数据注解验证、启动时强制校验，配置不存在直接抛出异常。
---

## 7 路由警告机制（RoutingWarningHostedService）

启动时检查 `HasRoutingPhase`，未配置路由且未关闭警告时输出日志，提示 404/CORS/授权失效等问题及解决方案。
---

## 8 WebDomainUserAccessor 用户访问器

- 抽象基类：从 IHttpContextAccessor 安全获取 DomainUser，实现 IDomainUserAccessor
- 扩展方法：`GetDomainUser<TUserInfo>()`，为空则抛出异常
- 屏蔽 HttpContext 依赖，向领域层提供强类型用户访问。

---

## 9 宿主适配（WebApplicationBuilderAdapter）

实现 IDomainAppBuilderAdapter，适配 WebApplicationBuilder 到框架通用接口，封装服务/配置访问，对接 Autofac 容器。
---

## 10 阶段约束机制

1. **顺序强制**：强类型构建器限制可调用方法，不可乱序
2. **路由必选**：未配置路由启动警告
3. **路由唯一**：仅可配置一次标准/自定义路由
4. **会话安全**：无会话场景使用 NoSessionManager 严格阻断

---

## 11 后续扩展方向

1. **Blazor Server 专用路由阶段**：针对 Blazor Circuit 生命周期做专属会话与管道适配
2. **MAUI / Console 宿主适配器**：非 HTTP 环境的 Session 管理、异常处理与宿主适配
3. **gRPC 宿主扩展**：gRPC 状态码映射、流式请求拦截、会话传递适配
4. **TKWF.Domain.Web.Common 共享层**：提取 DomainUser→ClaimsPrincipal、常量等通用逻辑，支持 Blazor WASM 复用，避免引入 AspNetCore.Http 依赖
5. **分布式会话存储扩展**：基于 HybridCache 扩展支持 Redis、MongoDB 等分布式会话存储方案
6. **无状态 JWT 会话集成**：提供 StatelessSessionManager 实现，适配纯 JWT 无状态鉴权场景

---
