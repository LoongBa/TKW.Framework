# TKWF.Domain V4 领域自治框架：最佳实践架构指南

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
>   > 这一阶段，已经脱离开发工作，主要精力在商务协调和产品设计。
> 
> + `V3` 基于 `.NET Core`重写：完整实现领域自动配置、依赖注入、代码自动生成、`AOP框架`。
>   
>   + 前期，基于`T4`实现代码生成、基于`Autofac Dynamic Proxy`实现`AOP框架`、基于`FreeSql`代替原`ORM`，`领域控制器`可在`控制台/桌面`、`Web/Web Api`、`测试环境`中独立应用；
>   
>   + 后期，基于`.NET 10`重写代生成器`xCodeGen`：通过`源生成器 SourceGenerator`提取元数据，再通过`xCodeGen Cli`调用模板生成代码。
>   
>   > 这一阶段，已经脱离软件开发，纯粹是兴趣爱好。
> 
> + `V4` 基于`.NET 10`升级：基于`SourceGenerator` + `装饰器模式`实现`静态 AOP 框架`，完全不再依赖`Autofac`
>   
>   + `ORM 框架层`：重新引入`IEntityDAC<Entity>`、`DataService<Entity>`，基于`FreeSql`封装`FreeSqlEntityDAC`，后续可扩展；
>   
>   + `AOP 框架层`：自动注册`DataService<Entity>`、手写的`DomainControllerBase<Entity>`+`IAopContrace`契约，自动提供服务。
>   
>   > 为了让多 AI Agent 更好的工作，重新升级、优化。

## 1. 领域内部分层

在 TKWF.Domain 体系下，一个标准的功能模块分为三个概念层级：

| **层级**     | **组件名称**        | **维护方式**                                            | **核心职责**                                                          | **基类与特性**              | 存储位置           |
| ---------- | --------------- | --------------------------------------------------- | ----------------------------------------------------------------- | ---------------------- | -------------- |
| **元数据层**   | **Entity/View** | **CodeFirst**方式手写、标注<br/>**SourceGenerator**自动提取元数据 | 定义数据模型、DB 映射与验证契约。                                                | `IDomainEntity`        | /Entities      |
| **数据服务层**  | **Dto**         | `xCodeGen.CLI`根据元数据调用模板生成                           | **Dto**用于外部访问，自动与**Entity**验证、转换。                                 | `IDomainDto`           | /Entities/Dtos |
|            | **DataService** | `xCodeGen.CLI`根据元数据调用模板生成                           | 内部访问的基于 `IEntityDAC` 实现的 **CRUDQ** 方法<br/>公开访问的基于数据库约束等关系的扩展查询方法。 | `DomainDataService`    | /Services      |
| **业务控制器层** | **Controller**  | 手工编写、维护（**AOP契约**）                                  | 业务编排、跨服务协作、**AOP 拦截控制**。                                          | `DomainControllerBase` | /Controllers   |

> 注：领域框架自动发现 DomainDataService、DomainControllerBase 的子类，
> 将之视为数据服务、业务控制器，并自动注册到容器。
> 
> ——没有继承两者的类，需要手动注册到容器。

---

## 2. 开发流程：从数据到业务

### 第一阶段：模型驱动 (Metadata)

开发者编写实体类，标注 ORM 标记。

- **SG 作用**：实时提取字段元数据，供编译时感知。

- **Cli 作用**：运行 `xCodeGen cli`，生成`{Entity}Dto.g.cs`和`{Entity}Service.g.cs`。

### 第二阶段：数据服务 (DataService)

生成的`{Entity}Service`自动继承`DomainDataService`：

- **不参与 AOP**：不提取接口，不生成`AOP代理类（装饰器）`。

- **注册方式**：领域框架自动注册到领域容器。

- **访问权限**：自动生成的**扩展查询方法**建议设为`internal`，保护领域边界。

### 第三阶段：业务控制器 (Hand-written Controller)

开发者根据业务需求创建 `{Entity}Controller.cs`：

1. **定义契约**：手写 `{Entity}Controller` 接口`I{Entity}Controller`，并继承 `IAopContract`。

2. **标注拦截器**：在接口或方法上标注 `[AuthorityFilter]`、`[Transactional]` 等。

3. **编写逻辑**：通过 `Use<PaymentLogService>()` 调用底层原子方法进行业务编排。

---

## 3. 代码示例对比

### 3.1 自动生成：数据服务层 (无 AOP)

```csharp
// PaymentLogService.g.cs
// 继承自 DomainDataService，作为纯粹的数据访问者
internal partial class PaymentLogService : DomainDataService<DmpUserInfo, PaymentLog, PaymentLogDto>
{
    // ... 自动生成的 SelectByBatchAsync 等方法 ...
}
```

### 3.2 手工编写：业务控制器 (开启 AOP)

```csharp
// IPaymentLogController.cs (AOP 契约)
public interface IPaymentLogController : IAopContract
{
    [AuthorityFilter] // 仅在需要的地方开启拦截
    Task<PaymentLogDto> ProcessRefundAsync(long logId);
}

// PaymentLogController.cs
public class PaymentLogController(DomainUser<DmpUserInfo> user) : DomainControllerBase<DmpUserInfo>(user), IPaymentLogController
{
    public async Task<PaymentLogDto> ProcessRefundAsync(long logId)
    {
        // 调用底层的 Data Service
        var log = await Use<PaymentLogService>().GetByIdAsync(logId);
        // ... 执行退款业务逻辑 ...
        return log;
    }
}
```

---

## 4. 领域配置





## 5. 表现层配置



你是否需要我针对这个“Controller -> Service”的调用链，再优化一下 `DomainUser.Use<T>` 的解析性能？
