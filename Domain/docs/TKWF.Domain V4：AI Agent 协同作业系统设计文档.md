# TKWF.Domain V4：AI Agent 协同作业系统设计文档

## 1. 三层知识架构 （Knowledge Architecture）

为了实现云端模型（架构编排）与本地模型（代码实现）的高效协同，我们将知识库划分为三个颗粒度不同的层次。

### L1: Framework DNA （框架公理库）

- **输出文件**：DomainDNA.md

- **性质**：**固定/只读**。通过 CLI 模板一次性生成或复制。

- **内容**：
  
  - **核心法则**：如`DomainUser`是唯一合法入口。
  
  - **方法论**：`Use<T>`与`UseAop<T>`分流原则。
  
  - **生命周期**：`Begin`与`Create`用域的适用场景。

- **目的**：赋予 Agent “框架级世界观”，防止其写出违背自治原则的代码。

### L2: Domain Map （领域全景图）

- **输出文件**：DomainMap.md

- **性质**：**自动更新/快照**。由`cli agent`命令扫描元数据生成。

- **内容**：
  
  - **实体清单**：字段、类型、关联关系（ER 简述）。
  
  - **能力矩阵**：区分`IEntityDAC`标准方法、自动生成的扩展查询。
  
  - **契约索引**：`IAopContract`的接口定义与拦截标记。

- **目的**：提供“当前世界的地图”，供云端模型进行任务编排和路由。

### L2：业务契约

+ **输出文件**：DomainApi.md

+ **性质**：定义外部可访问的方法，比如用于表现层等。

+ **内容**：
  
  - `IController` 契约接口：方法签名、XML 注释、AOP 拦截标记（如 `[Transactional]`, `[AuthorityFilter]`） 。
  
  - `Service` 的公共业务方法 。

+ **目的**：作为任务编排（Task Orchestration）的参考，Agent 知道通过调用哪些接口来实现用户的复杂需求。

### L3: Business Implementation Delta（增量记录）

- **输出文件**：Domain_ChangedLog.md

- **性质**：**增量记录，记录修改、变化**。

- **内容**：
  
  - Agent 自身的修改记录、开发者手动总结的避坑指南，`Controller`的复杂业务逻辑描述。
  
  - `Agent`自己总结的“避坑指南”或特定业务补丁说明。提供长期记忆，避免在迭代中引入回归错误。

---

## 2. Agent 协同流程 （SOP）

| **阶段** | **动作**                 | **参与者**  | **触发条件**    |
| ------ | ---------------------- | -------- | ----------- |
| **感知** | 读取 L1/L2/L3 全文或索引      | OpenCode | 接收到新任务      |
| **设计** | 下达“翻译”后的底层指令           | OpenCode | 方案评审通过      |
| **执行** | 编写代码 / 修改 Entity       | OpenCode | 接收到指令       |
| **同步** | **调用`cli agent`更新 L2** | OpenCode | 代码修改完成且编译通过 |
| **反思** | **在 L3 记录本次修改的增量逻辑**   | OpenCode | 提交任务前       |

---

## 3. L2 CLI 实现规格书 （CLI Agent Command）

接下来你将为`cli agent`编写实现代码，其核心逻辑应包含：

### **A. 扫描器 （Scanner）**

- **Entity 扫描**：基于`IDomainEntity`，提取属性名、类型、是否主键、是否导航属性。

- **Service 扫描**：识别继承自`DomainDataService`的类，区分哪些是模板生成的（通常在 `g.cs`），哪些是 `rtial``的。

- **Contract 扫描**：提取所有继承自`IAopContract`的接口，读取方法签名及其标注的 `ilterAttribute`。

以上部分已经由 SG 提取并保存在 IProjectMetaContext 中。

### **B. 格式化器 （AI-Optimized Formatter）**

- **Markdown 输出**：使用极简的 MD 格式。

- **Token 压缩逻辑**：
  
  - **省略继承**：明确声明“所有 DataService 默认支持`IEntityDAC<T>`接口”。
  
  - **合并描述**：将同属于一个业务子领域的多个 Entity 聚合在同一个 MD 章节中。
  
  - **标记变化**：为最近修改过的方法打上`[New]`或 `Updated]`签（通过对比文件修改时间）。

### **C. 接口契约导出 （Contract Exporter）**

- 针对表现层需求，额外生成一份`contracts.md`。

- 内容包含：路由路径、入参 Dto 结构、出参 Dto 结构、权限要求（来自`AuthorityFilter`）。

---

## 4. 下一步：编写 CLI 代码的重点提示

在实现`cli agent`时，请注意以下细节，这将直接影响`Agent`的表现：

1. **方法分类**：在生成`MD`时，强制将方法分为三组：
   
   - `[Infrastructure]`：标准`CRUDQ`（一句话带过）。
   
   - `[Generated]`：自动生成的扩展查询（只留签名）。
   
   - `[Custom]`：手写的方法（保留`XML`文档注释中的`<summary>`）。

2. **Dto 映射逻辑**：简要标注`Entity`与 `to`对应关系，这能帮助 ``nt` `写出正确的 `M`ToDto` 调`

3. **注入路径描述**：在 L2 中明确指出每个`Controller`应该使用 `seAop<T>`析，避免`Agent`在生成代码时混淆。
