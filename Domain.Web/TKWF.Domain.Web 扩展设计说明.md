# TKWF.Domain.Web 扩展设计说明

**将环境特定（表现层/接入层）的代码从核心 `TKWF.Domain` 中分离出来**，这是构建一个高质量、可复用领域框架的正确做法。

下面我系统地回答你的问题，并给出**下一步的工作计划**和**具体建议**。

---

### 1. 为什么需要扩展项目？

核心原因：

- `TKWF.Domain` 必须保持**纯净、轻量、无环境依赖**（不引用 `Microsoft.AspNetCore.*`、`Maui`、`Blazor` 等包）
- 不同宿主环境（WebAPI、Blazor Server、Blazor WASM、MAUI、Console 等）的集成方式差异很大
- 便于维护、版本独立发布、减少核心包的依赖膨胀

**推荐的项目结构**（建议采用）：

```
TKWF.Domain/                  ← 核心领域框架（当前项目）
├── TKWF.Domain.Web/          ← WebAPI / Minimal API / Blazor Server 专用
├── TKWF.Domain.Blazor/       ← Blazor WASM + Server 通用扩展
├── TKWF.Domain.Maui/         ← MAUI / WPF / WinUI 桌面扩展
├── TKWF.Domain.Console/      ← Console / Worker Service 扩展
└── TKWF.Domain.Testing/      ← 测试辅助（Mock SessionManager 等）
```

---

### 2. 类似的扩展点有哪些？（按优先级排序）

基于你已有的 `SessionUserMiddleware`，我把常见且高价值的扩展点整理如下：

#### **高优先级（建议第一阶段完成）**

| 扩展包                    | 推荐内容（Middleware / Helper / Extension）                                                                                                                                                   | 说明                       |
| ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------ |
| **TKWF.Domain.Web**    | `SessionUserMiddleware`（你已有）<br>`DomainExceptionMiddleware`（统一 JSON 错误响应）<br>`DomainUserClaimsPrincipalMiddleware`（映射为 ClaimsPrincipal）<br>`DomainHostSetupExtensions`（Program.cs 扩展方法） | Web 最常用                  |
| **TKWF.Domain.Blazor** | `DomainAuthenticationStateProvider`<br>`ProtectedSessionStorageHelper`<br>`BlazorDomainUserService`（Scoped）                                                                             | Blazor Server / WASM 都需要 |
| **TKWF.Domain.Maui**   | `MauiSessionStorageHelper`（SecureStorage）<br>`DomainUserService`（Singleton + INotifyPropertyChanged）<br>`MauiExceptionHandler`                                                          | 桌面应用核心                   |

#### **中优先级（第二阶段）**

- `TKWF.Domain.Console`：`ConsoleSessionHelper`（命令行参数 / 配置文件读取 SessionKey）
- `TKWF.Domain.Testing`：`MockDomainHostBuilder`、`MockSessionManager<TUserInfo>`
- `TKWF.Domain.Web.FastEndpoints` / `TKWF.Domain.Web.GraphQL`：特定框架的集成扩展

#### **长期 / 高级扩展**

- `IDomainModule` 接口 + 模块化 Filter 注册系统
- `DomainHostBuilder`（类似 `IHostBuilder` 的流式配置）
- OpenTelemetry 集成扩展
- 缓存、验证、审计日志的 Filter 扩展包

---

### 3. 下一步工作计划（建议分阶段）

#### **阶段 1（本周 - 下周，优先完成）** —— Web 基础集成

目标：让 WebAPI / Blazor Server 项目能快速接入 TKWF.Domain

1. 创建 `TKWF.Domain.Web` 项目

2. 把你当前的 `SessionUserMiddleware` 移动进去，并优化（见下面建议）

3. 增加 `DomainExceptionMiddleware`（统一返回 JSON 错误）

4. 增加扩展方法：
   
   ```csharp
   public static class DomainWebExtensions
   {
       public static void UseDomainSession(this IApplicationBuilder app)
       {
           app.UseMiddleware<SessionUserMiddleware<TUserInfo>>();
       }
   
       public static void UseDomainExceptionHandler(this IApplicationBuilder app)
       {
           app.UseMiddleware<DomainExceptionMiddleware>();
       }
   }
   ```

5. 写一个 `README.md` + 示例 Program.cs

#### **阶段 2（后续 1-2 周）**

- 创建 `TKWF.Domain.Blazor` 项目
- 实现 `DomainAuthenticationStateProvider`
- 实现 `ProtectedLocalStorage` / IndexedDB 持久化 SessionKey 的 Helper
- 创建 `TKWF.Domain.Maui` 项目（SecureStorage + DomainUserService）

#### **阶段 3（中期）**

- 实现 `IDomainModule` 接口，支持插件化 Filter 注册
- 提供官方 Starter Templates（WebAPI + Blazor + MAUI）

---

### 4. 对你当前 SessionUserMiddleware 的优化建议

你的中间件已经写得很好，我建议做以下小幅优化：

```csharp
// SessionUserMiddleware.cs（优化版关键点）

public async Task InvokeAsync(HttpContext context)
{
    var sessionKey = GetSessionKeyFromRequest(context);
    DomainUser<TUserInfo>? domainUser = null;

    if (!string.IsNullOrWhiteSpace(sessionKey))
    {
        domainUser = await TryGetUserFromSessionAsync(sessionKey);
    }

    // 没有有效 SessionKey → 创建 Guest 并返回新 SessionKey
    if (domainUser == null)
    {
        var guestSession = await DomainHost<TUserInfo>.Root!.NewGuestSessionAsync();
        domainUser = guestSession.User!;

        // 返回新 SessionKey（同时支持 Cookie + Header）
        SetSessionKeyToResponse(context, guestSession.Key);
    }

    // 注入到 HttpContext（供 Controller / Endpoint 使用）
    context.Items[KeyName_DomainUser] = domainUser;
    context.User = domainUser.ToClaimsPrincipal();   // 推荐封装成扩展方法

    await next(context);
}
```

**建议新增的辅助方法**（让代码更清晰）：

- `TryGetUserFromSessionAsync(string sessionKey)`
- `SetSessionKeyToResponse(HttpContext context, string sessionKey)`
- `ToClaimsPrincipal()` 扩展方法（把 DomainUser 转 ClaimsPrincipal）

---

### TKWF.Domain.Web 和 TKWF.Domain.Blazor 是否有必要分开？

**结论：有必要分开，但可以有公共部分（通过共享项目或 NuGet 引用）**

#### 为什么分开？

- **Blazor WASM** 是客户端运行，不依赖 HttpContext、Middleware，而是依赖 ProtectedLocalStorage、AuthenticationStateProvider
- **Blazor Server** 依赖 HttpContext 和 SignalR，会话管理更接近 WebAPI
- **WebAPI** 完全是服务器端中间件和 Controller 风格
- 分开可以让每个包的依赖最小化（Blazor WASM 不需要 Microsoft.AspNetCore.Http）

#### 公共部分建议

可以创建一个共享项目（或 NuGet 包）：

text

```
TKWF.Domain.Web.Common
├── DomainUserClaimsPrincipalExtensions.cs   // DomainUser → ClaimsPrincipal 转换
├── DomainUserService.cs                     // 通用 DomainUser 服务接口
├── SessionKeyConstants.cs                   // SessionKey 常量
└── DomainExceptionResponse.cs               // 统一错误响应模型
```

然后：

- TKWF.Domain.Web 引用它（用于 WebAPI / Blazor Server）
- TKWF.Domain.Blazor 引用它（用于 WASM + Server）

**Blazor WASM 是否会引用 TKWF.Domain.Web？**

**不建议直接引用**。

- WASM 项目不需要 HttpContext、Middleware、IHttpModule
- 如果 WASM 需要从 API 获取 SessionKey，建议让 API 返回 SessionKey（通过响应 Header），WASM 端存入 ProtectedLocalStorage
- 公共部分（如 DomainUser 到 ClaimsPrincipal 的转换逻辑）可以放在共享项目中，避免重复。

**推荐结构（当前最优）**：

text

```
TKWF.Domain (核心，无 UI 依赖)
├── TKWF.Domain.Web.Common (共享：DomainUser 扩展、常量、错误模型)
├── TKWF.Domain.Web (WebAPI / Blazor Server / Razor Pages)
│   └── SessionUserMiddleware
│   └── DomainExceptionMiddleware
│   └── UseDomainSession() 扩展
├── TKWF.Domain.Blazor (Blazor WASM + Server 通用)
│   └── DomainAuthenticationStateProvider
│   └── ProtectedSessionStorageHelper
│   └── BlazorDomainUserService (Scoped)
└── TKWF.Domain.Maui (后续)
```

这样：

- Blazor WASM 不引用任何 Microsoft.AspNetCore.Http 相关包
- Blazor Server 可以选择性引用 TKWF.Domain.Web（如果需要 Middleware）
- 公共逻辑放在 TKWF.Domain.Web.Common 中，避免重复

如果你当前项目是 Blazor Server 为主，可以暂时把 Middleware 放在 TKWF.Domain.Blazor 中，后续再拆分。
