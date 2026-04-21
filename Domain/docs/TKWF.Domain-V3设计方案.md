# TKWF.DomainV3 设计方案

版本号：V1.2
基于 V1.1 迭代，完全对齐最新代码实现，优化架构描述、完善初始化流程、修正 AOP 拦截细节、统一配置与上下文模型，保留原有核心章节结构

---

## 目录

1. 架构概述
2. 核心模块设计
3. 依赖注入（DI）策略
4. DomainOptions 领域配置
5. DomainUser 领域用户模型
6. 后续演进（Roadmap）

---

## 1 架构概述

### 1.1 框架分层

- TKWF.Common：基础工具类、自定义异常、通用扩展（EnsureNotNull 等）
- TKWF.Domain：领域核心框架（Host、AOP 拦截体系、Session、Context）
- TKWF.Domain.Web / Blazor / Maui：宿主适配扩展，处理协议层差异，与核心框架完全解耦
- TKWF.xCodeGen：独立代码生成工具，驱动 Entity / DTO / Service 自动化
- 持久层：以 FreeSql 作为默认 ORM 实现，支持 CodeFirst
  
  ### 1.2 架构演进：实用主义 DDD
  
  V3.0+ 核心变革（3 层架构）
  
  ```
  Database ↔ Entity（兼顾映射与领域行为）↔ Dto（xCodeGen 自动生成）
  ```
  
  演进优势：
- 消除冗余映射：废弃 Model 层，移除无意义的属性复制
- 认知统一：Entity 是包含业务状态与数据库特性的唯一真相来源
- 自动契约：DTO 由生成器保证与 Entity 同步，自带高性能硬编码 ApplyToEntity 映射逻辑
  
  ### 1.3 配置真相来源（Single Source of Truth）
  
  框架坚持**领域修正原则**，配置优先级由低到高依次为：
1. appsettings.json：基础底色
2. EnvironmentVariables：环境差异
3. ConfigTkwDomain 回调：运行时注入
4. OnPreInitialize 钩子：领域层最终否决权（如强制锁定生产环境连接字符串加密）

---

## 2 核心模块设计

### 2.1 DomainHost：主机生命周期

DomainHost<TUserInfo> 是整个框架的核心单例，通过静态属性 Root 对外暴露。

#### 2.1.1 初始化流程

初始化入口为静态工厂方法 DomainHost<TUserInfo>.Initialize<TDomainInitializer>()，核心流程：

1. 禁止重复初始化，保证单例安全
2. 注册核心类型：DomainContext、DomainInterceptor、DomainOptions
3. 执行初始化器 DI 注册流程，触发 OnPreInitialize 守门员逻辑
4. 创建 DomainHost 实例，完成 UserHelper 与 Host 内聚绑定
5. 注册 Root 到容器，执行容器构建回调
6. 配置全局过滤器，默认启用权限过滤器
   
   #### 2.1.2 关键设计决策
- 外部容器兼容：支持接入 Autofac 等外部容器，框架不主动执行 Build
- 双重同步：DomainOptions 单例注册，可被 SessionManager 等组件构造注入
- UserHelper 内聚：通过 AttachHost 关联宿主，消除对静态 Root 的隐式依赖
- 合同缓存：使用 ConcurrentDictionary 缓存控制器与方法特性，降低 AOP 反射开销
  
  ### 2.2 AOP 拦截体系：物理隔离与作用域锚定
  
  AOP 是框架保证异步数据一致性的核心机制，实现并发安全、作用域隔离。
  
  #### 2.2.1 拦截器层次结构
  
  ```
  IAsyncInterceptor + IInterceptor
  ↑
  BaseInterceptor<TUserInfo>（双 AsyncLocal 上下文、生命周期钩子）
  ↑
  DomainInterceptor<TUserInfo>（作用域管理、Filter 编排）
  ```
  
  #### 2.2.2 双 AsyncLocal 并发安全设计
  
  BaseInterceptor 采用**双 AsyncLocal**彻底解决并发状态覆盖：
- 存储当前拦截上下文，对外只读暴露
- 存储领域执行上下文，子类安全读写
- 同步/异步流程结束后统一清理，避免上下文污染
  
  #### 2.2.3 生命周期钩子顺序
1. 初始化：InitialSync / InitialAsync（必须实现，开启作用域）
2. 前置：PreProceedSync / PreProceedAsync（执行 Filter 链）
3. 执行：调用原始业务方法
4. 后置：PostProceedSync / PostProceedAsync（逆序执行 Filter 链）
5. 清理：CleanUpSync / CleanUpAsync（释放作用域）
6. 异常：LogException（记录异常，不吞没）
   
   #### 2.2.4 物理隔离作用域
   
   DomainInterceptor 每次方法调用均从根容器创建**独立子作用域**，确保：
- 数据库连接、事务、缓存完全隔离
- 异步 await 链路中对象生命周期安全
- 同步/异步均正确释放作用域
  
  #### 2.2.5 作用域锚定（Scope Anchoring）
  
  创建 DomainContext 时将子作用域写入 AsyncLocal，驱动 User.Use<T> 穿透解析，彻底解决原生 DI 在异步下的 DbContext 释放/共享问题。
  
  ### 2.3 Filter 执行体系
  
  #### 2.3.1 三级 Filter 层次
- Global：全局生效，AddGlobalFilter 注册
- Controller：类级别，标记在领域服务接口
- Method：方法级别，标记在接口方法
  
  #### 2.3.2 执行顺序
- 前置：Global → Controller → Method（由外到内）
- 后置：Method → Controller → Global（逆序，洋葱模型）
- 去重规则：按 TypeId 去重，同类型 Filter 不重复执行
  
  #### 2.3.3 内置 Filter
- AuthorityFilter：默认全局启用，负责权限验证
- LoggingFilter：需显式启用，负责 AOP 方法调用日志
  
  ### 2.4 DomainHostInitializerBase：初始化器基类
  
  #### 2.4.1 核心回调链
  
  容器构建后触发 ContainerBuiltCallback，完成：
- 日志工厂绑定，避免重复覆盖
- 全局过滤器配置，默认启用权限校验
- 异常日志工厂解析，支持自定义与兜底
- 触发子类 OnContainerBuilt 扩展
  
  #### 2.4.2 异常日志工厂优先级
1. 优先从容器解析表现层注册的自定义工厂
2. 兜底：new DefaultExceptionLoggerFactory()，保证始终可用
   
   ### 2.5 验证体系：三层防御与物理阻断
   
   框架构建从入口到出口的严密校验链：
3. DTO 验证：表现层调用前，场景化校验（Create/Update 差异化）
4. 业务钩子：*.biz.cs 的 OnBusinessValidate，处理跨字段、状态机规则
5. 物理阻断：Repo.Insert/Update 前强制校验，阻断脏数据入库

---

## 3 依赖注入（DI）策略

- RegisterTypeForced：核心业务服务，使用 PreserveExistingDefaults，防止被意外替换
- RegisterTypeReplaceable：基础设施（日志、缓存、SessionManager），允许自定义覆盖
- NoSessionManager：默认 ISessionManager 实现，支持被真实实现覆盖
- 高频对象（DomainContext）直接实例化，避开容器解析开销

---

## 4 DomainOptions 领域配置

统一承载领域层核心配置，与 Web 环境解耦：

- IsDevelopment：开发环境标识
- ConnectionString：数据库连接字符串
- EnableDomainLogging：是否开启 AOP 日志
- Session 配置：过期时间、SessionKey 名称、缓存前缀
- 应用名称、ID 生成器类型等全局配置

---

## 5 DomainUser 领域用户模型

- 基于 AsyncLocal 维护当前活跃作用域，支持安全解析服务
- 提供 Use<TDomainService>/UseAop<TAopContract> 服务解析
- 内置角色校验、Claims 转换、登录与会话激活能力
- 与 DomainHost 强绑定，支持配置项读取与日志创建

---

## 6 后续演进（Roadmap）

- V4.0：移除 Castle 依赖，使用 Roslyn Source Generator 编译时生成代理，提升 AOP 性能
- 宿主扩展：完善 Blazor Server 路由、MAUI 安全存储适配
- 事件驱动：引入领域事件异步发布机制
- 性能优化：增强缓存策略、减少反射、提升高并发表现
