# xCodeGen 设计和使用说明 V2.2

## 1. 概述

xCodeGen 是一个基于元数据的模板生成工具，包含几个主要部分：

Abstraction 抽象类、Core 核心业务、SourceGenerator 源生成器（提取元数据）、Cli 命令行工具（调用Core，调用 RazorLight 模板生成代码或文件）

### 1.1 定位

xCodeGen 是独立的跨平台代码生成工具，与领域运行时代码完全解耦。仅模板与 TKWF.Domain 相关，代码生成本身与 TKWF.Domain 关系不大。

### 1.2 核心职责

- 读取数据库 Schema 或结构化元数据
- 基于模板引擎生成 Entity、DTO、Service 骨架代码
- 自动生成 Entity 与 DTO 之间的赋值/映射机制

---

## 2. 架构设计

### 2.1 模块组成

- xCodeGen.Abstractions：抽象接口与契约
- xCodeGen.Core：核心生成引擎
- xCodeGen.Cli：命令行工具
- xCodeGen.SourceGenerator：Roslyn 源生成器（规划中）

### 2.2 模板引擎

（待补充：当前使用的模板引擎类型）

---

## 3. 使用指南

### 3.1 安装与配置

（待补充）

### 3.2 生成 Entity

从数据库 Schema 生成 Entity 类，支持 CodeFirst 和 DbFirst 模式。

### 3.3 生成 DTO

**CreateDto**：创建场景，包含必填字段验证

**UpdateDto**：更新场景，可选字段 + 业务规则验证

**QueryDto**：查询场景，支持动态搜索条件

### 3.4 生成 Service 骨架

（待补充）

---

## 4. 高级功能

### 4.1 场景化验证

- EnumSceneFlags 场景标志
- ValidateData 方法由生成代码实现

### 4.2 智能查询引擎

- 自动生成搜索条件解析逻辑
- ParseSearchValue 规则（* 模糊匹配，** 转义）

### 4.3 自动赋值机制

生成的 DTO 自带 ApplyToEntity 方法，实现 Entity 与 DTO 之间的高效映射，无需手写 Mapping 代码。

---

## 5. 自定义模板

（待补充：如何编写和注册自定义模板）

---

## 6. 与 TKWF.Domain 的集成

xCodeGen 生成的代码完全适配 TKWF.Domain 框架：

- Entity 继承 IEntity 接口
- DTO 自带验证逻辑和 ApplyToEntity 映射方法
- Service 骨架继承 DomainServiceBase

详细参考《TKWF.Domain 使用说明》第 4.1 节。
