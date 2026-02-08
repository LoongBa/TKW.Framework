### DMP Lite 系统架构完善：基于角色的权限过滤、全局异常处理、日志、事务及其他扩展

从 **Domain 框架（TKWF.Domain）** 角度，我们继续处理你提出的核心问题。以下内容严格遵循项目阶段1“优先简单、低成本、易扩展”的原则，同时保持领域自治、代码现代化（.NET 10 优势：async/await、null-safety、record 类型）、命名一致性（FilterAttribute / FlagAttribute），并确保与现有 AOP 机制（DomainInterceptor + DomainContext）无缝集成。

#### 1. 基于角色的权限过滤（Role-Based Access Control）

**目标**：在 AuthorityFilterAttribute 基础上，支持细粒度角色检查（如 [RequireRole("admin")]），并兼容全局/方法级/控制器级。

**设计思路**：

- 新增 `RequireRoleFlagAttribute` 作为标记（Flag）
- 在 `AuthorityFilterAttribute` 的 `PreProceedAsync` 中检查角色
- 支持多个角色（OR 关系）

**使用方式**：

```csharp
[RequireRoleFlag("admin", "manager")]
public async Task UpdateMerchantAsync(...) { ... }
```

**全局注册**（在 DomainInterceptor.GlobalFilters 中添加 `new AuthorityFilterAttribute<TUserInfo>()`）。

#### 2. 全局异常处理（未捕获、处理的异常）

**目标**：捕获所有未处理的异常，记录日志，返回统一响应（JSON 错误码），避免原始异常泄露。

**设计思路**：

- 实现 `IDomainGlobalExceptionFactory` 接口
- 在 `DomainInterceptor.OnException` 中调用
- 支持自定义错误码映射（401 → 401 Unauthorized，500 → InternalServerError）

**注册**（DmpDomainInitializerModule）：

```csharp
builder.RegisterType<DefaultDomainExceptionFactory>().As<IDomainGlobalExceptionFactory>().SingleInstance();
```

**未捕获异常**：在 DomainInterceptor.OnException 中调用此工厂。
**已处理异常**：Filter 中设置 `context.ExceptionHandled = true;` 则不会调用工厂。

#### 3. 日志——主要目的和功能

**目的**：

- 记录系统运行轨迹，便于问题诊断、审计、安全分析
- 监控性能（耗时、调用频率）
- 合规性要求（操作留痕）

**主要功能**（分层实现）：

- **全局日志**（LoggingFilterAttribute）：记录每个方法进入/退出、耗时、用户、参数（脱敏）
- **异常日志**（OnException + 全局工厂）：记录堆栈 + 上下文（方法名、用户）
- **业务日志**（领域服务内部）：如 `logger.LogInformation("商户 {MerchantUid} 对账完成", ...)` 
- **审计日志**（AuditLogFilterAttribute）：记录操作到数据库（谁、什么操作、何时、结果）

**推荐实现**（LoggingFilterAttribute 已提供）：

- 使用 Microsoft.Extensions.Logging（Serilog 作为 Sink）
- 结构化日志（{MethodName}、{UserName}、{DurationMs}）
- 级别控制：Information（正常）、Warning（异常）、Error（严重）

#### 4. 事务——主要目的和功能

**目的**：

- 保证数据一致性（ACID）
- 防止并发修改导致脏数据
- 支持回滚（异常时）

**主要功能**：

- **方法级事务**：标记 [TransactionFilter] 的方法自动包裹事务
- **全局事务**：所有领域服务默认事务（可选）
- **嵌套事务**：FreeSql 支持 SavePoints（嵌套回滚）

**实现**（TransactionFilterAttribute 已提供）：

- PreProceedAsync：Begin Transaction
- PostProceedAsync：Commit（成功） / Rollback（异常）
- 支持 SavePoint（嵌套事务）

#### 5. 其他重要扩展（逐步添加）

| 组件                         | 目的               | 实现方式（简要）                          |
| -------------------------- | ---------------- | --------------------------------- |
| CachingFilterAttribute     | 方法级缓存，减少数据库压力    | HybridCache + CacheKey            |
| ValidationFilterAttribute  | 输入验证，防止无效数据      | FluentValidation 集成               |
| RateLimitFilterAttribute   | 防止商户滥用（API 限流）   | HybridCache 计数                    |
| AuditLogFilterAttribute    | 操作审计（合规、追责）      | 插入 audit_log 表                    |
| CustomExceptionFactory     | 统一错误响应（JSON 格式）  | 已提供 DefaultDomainExceptionFactory |
| PerformanceFilterAttribute | 记录方法耗时、慢查询监控     | Stopwatch 计时                      |
| RetryFilterAttribute       | 自动重试（网络/数据库瞬时失败） | Polly 集成（后期）                      |

**优先级建议**：

1. 完成 AuthorityFilter + Role 支持（已提供）
2. 实现 DefaultDomainExceptionFactory（已提供）
3. 添加 LoggingFilterAttribute（全局日志）
4. 添加 TransactionFilterAttribute（核心业务一致性）
