# 《xCodeGen 领域驱动服务开发规范与最佳实践》

# V1.27

本设计以 **“标注驱动生成 + 手写自治”** 为核心，xCodeGen 负责生成 80% 以上的场景化映射、基础验证及数据访问代码，剩余复杂业务逻辑由开发者在 `.Logic.cs` 中手写扩展。

## 1. 核心设计原则（Foundational Principles）

为了平衡性能、安全性与开发效率，本系统遵循以下基础策略：

- **持久化信任（Persistence Trust）**：来自数据库（持久化层）的数据在 DTO 层面默认信任其已满足物理约束。当 `IsFromPersistentSource == true` 时，DTO 自动跳过重复的长度、格式等物理校验。状态位由 `Model -> DTO` 链路强制透传 。

- **回填驱动验证（Mapping-Driven Validation）**：仅对当前场景下允许修改（`CanModify`）且需要回填的字段执行验证。**不修改 $\rightarrow$ 不回填 $\rightarrow$ 不验证** 。

- **模型终验制（Model Final Validation）**：Model 层的 `Create/Update` 动作一律显式调用 `Validate()`。Model 负责所有数据（包含系统自动填充字段）的最终一致性守卫。

- **单一数据源（Single Source of Truth）**：通过源生成器的“反射回填”机制，将特性类的属性默认值直接注入元数据字典，消除模板侧的硬编码。

---

## 2. 属性行为决策矩阵（Property Decision Matrix）

行为由元数据（`Column`、`DtoField`）与场景（`EnumSceneFlags`）通过 `CodeGenPolicy` 决策引擎共同决定：

| **模式/特性组合**                 | **DTO 定义** | **ApplyToEntity（回填）** | **ValidateData（验证）** | **FromEntity（脱敏）** | **典型例子**      |
| --------------------------- | ---------- | --------------------- | -------------------- | ------------------ | ------------- |
| **`Ignore`**                | 否          | 否                     | 否                    | -                  | 内部敏感字段        |
| **`CanModify = false`**     | 是          | 否                     | 否                    | 正常                 | `UId`, `Code` |
| **`UpdateReadOnly = true`** | 是          | 仅 `Create` 场景可写       | 仅 `Create` 场景校验      | 正常                 | 证件号、初始类别      |
| **`Masking / MaskPattern`** | 是          | 取决于 `CanModify`       | 取决于 `CanModify`      | 根据 Pattern 执行掩码替换  | 手机号、邮箱        |
| `Navigate` /`IsIgnore`      | 是          | 绝对禁止 (防级联污染)          | 否                    | 正常                 | 关联子表、计算字段     |
| `DtoFieldIgnore`            | 否          | 否                     | 否                    | -                  | 内部敏感字段        |

**注**：只要属性配置了 `MaskPattern`，系统自动开启脱敏逻辑（`Masking = true`），除非显式声明 `Masking = false` 。

---

## 3. 自动生成查询方法（Query Method Automation）

本章节定义了根据模型元数据自动在 `Service.generated.cs` 中构建高可用数据访问接口的标准。

### 3.1 自动生成规则与优先级 (Generation Rules)

采用“冲突回避”机制，优先级由高到低如下：

1. **P1 (最高) 主键查询**：基于 `Column(IsPrimary = true)` 生成 `GetByIdAsync` 。

2. **P2 语义化与动态搜索**：基于 `DtoField(SearchGroup/IsSearchable)` 生成四位一体查询组 。

3. **P3 唯一索引**：基于 `Index(IsUnique = true)` 生成 `GetBy...` 精确获取接口 。

4. **P4 (最低) 普通索引**：基于普通 `Index` 生成支持列表与分页的动态接口 。

### 3.2 命名算法逻辑 (Naming Convention)

1. **单字段优先**：单一属性索引或搜索字段直接使用属性名。

2. **SearchGroup 优先**：使用 `SearchGroup` 命名作为后缀。

3. **智能剥离类名**：对于复合索引（`idx_...`），程序会自动剥离当前实体类名前缀（如 `MerchantInfoCode` $\rightarrow$ `Code`） 。

### 3.3 统一查询与统计体系 (Unified Query System)

1. **SearchGroup 与 IsSearchable 解耦**：
   
   - `IsSearchable = true`：触发生成独立查询组，并为该字段开启全局模糊/范围匹配。
   
   - `SearchGroup = "X"`：将字段归属到复合查询组 "X" 中。若字段未标 `IsSearchable`，在组内仅支持精确匹配。

2. **智能字符串匹配约定 (Smart String Parsing)**： 所有生成的字符串搜索参数均接入 `ParseSearchValue` 解析器 ：
   
   - **默认**：精确匹配（输入 `"VIP"` $\rightarrow$ `== "VIP"`）。
   
   - **`*` 前缀**：模糊匹配（输入 `"*VIP"` $\rightarrow$ `.Contains("VIP")`）。
   
   - **`**` 前缀**：转义匹配（输入 `"**VIP"` $\rightarrow$ `== "*VIP"`）。

3. **范围与可空匹配**：
   
   - **日期类型**：若开启 `IsSearchable`，自动裂变为 `beginDate` 和 `endDate` 。
   
   - **非唯一参数**：统一转化为 `Nullable<T>`，支持 `null` 触发全量查询 。

### 3.4 字段忽略优先级规范

为了确保代码生成的严谨性，开发者需识别以下三种“忽略”语义的区别：

1. **`[DtoFieldIgnore]` (业务层忽略)**：
   
   - **作用**：该字段**不出现在 DTO 类定义中**。
   
   - **后果**：回填引擎（ApplyToEntity）不会对其赋值，验证引擎（_InternalValidation）不会对其生成规则。
   
   - **场景**：仅限服务端内部使用的逻辑字段、敏感状态位。

2. **`[Column(IsIgnore = true)]` (持久层忽略)**：
   
   - **作用**：该字段不映射数据库列。
   
   - **后果**：DTO 仍可能包含此字段（作为计算属性），但验证引擎会自动跳过基于物理列定义的 `MaxLength` 等约束。

3. **`[Navigate]` (关系层忽略)**：
   
   - **作用**：定义导航属性（一对多、多对一）。
   
   - **后果**：DTO 可以包含此属性用于展示，但回填引擎**绝对禁止**将其回填至 Model，以防触发 FreeSql 的级联保存/更新。

---

## 4. 掩码系统规格 （Masking System Specification）

引入动态掩码逻辑，由 `MaskHelper` 静态类实现：

- **模式字符**：`?` (保留单字符)、`?*` (保留多个)、`#` (遮蔽单字符)、`#*` (遮蔽多个)。

- **掩码写回防护**：在 `ApplyToEntity` 时，若输入值等于该字段生成的掩码字符串（判定用户未编辑），自动跳过回填，防止掩码字符污染数据库 。

---

## 5. 核心元数据系统 (Metadata System)

### 5.1 反射回填机制 (SG Backfill)

源生成器在编译期利用反射技术实例化 `Abstractions` 库中的特性类（如 `GenerateCodeAttribute`），将其内部定义的属性默认值作为底座注入元数据字典。

- **优势**：实现了全链路“单一数据源”，开发者仅需在特性类定义中修改默认值，即可自动改变生成的逻辑及模板的 Fallback 行为。

### 5.2 验证信任机制的安全闭环 (Persistence Trust Closed-Loop)

- **入口拦截 (DB $\rightarrow$ Model)**：在 Service 层的底层查询原语中（如 `InternalGet/SelectAsync`），从数据库读取实体后自动强制挂载 `IsFromPersistentSource = true`。

- **状态透传 (Model $\leftrightarrow$ DTO)**：`FromEntity` 转换时同步该标记。**注意**：实体和 DTO 的基类中，必须为该属性加上 `[JsonIgnore]`，防止前端通过 JSON 提交伪造的信任标记。

- **出口终验 (Model 落库前)**：为了防止 DTO 因规则跳过校验，Service 层的 `InternalCreate/Update` 动作会在执行 ORM 写入前，强制调用 `entity.Validate(scene | EnumSceneFlags.ForceValidate)`。此举将强行击穿信任标记，完成入库前的终极防守。

---

## 6. 自动化服务层逻辑 （Service Generation Specs）

### 6.1 基础架构

- **用户类型参数化**：通过 `GenerateCode(BaseUserType = "...")` 动态指定 Service 的用户标识类型 。

- **分页模型**：`SelectPageAsync` 统一返回 `PageResult<T>` 对象（包含 `TotalCount`, `PageIndex`, `PageSize`） 。

### 6.2 内部接口安全护城河

- **边界检查**：所有 `internal` 级集合获取方法强制应用 `Math.Clamp`（如限制 `limit` 最大为配置的 `DefaultLimit`），防止恶意参数导致数据库 OOM 。

### 6.3 状态管理与删除逻辑

- **状态切换**：识别 `IsEnabled/IsDisabled` 字段并生成统一的状态控制指令。

- **智能删除**：根据模型是否包含 `IsDeleted`，自动在 `DeleteAsync` 公共方法中路由至“软删除”或“物理硬删除”逻辑 。

### 6.4 业务钩子 （Partial Hooks）

- **查询过滤**：`OnQueryFiltering`（基础查询过滤，自动应用软删除过滤）、`OnGraphQLFiltering`。

- **动作周期**：`OnBefore/After` + `Create/Update/Delete`。

## 6.5 可空引用类型 (NRTs) 与初始化规范

生成代码全量开启 `#nullable enable`，并遵循以下最佳实践化解 C# 编译警告 (`CS8618`)：

- **DTO 层 (数据契约)**：对于逻辑必填或非空值类型，生成器会自动追加 `required` 关键字，强制调用方在对象初始化器中显式赋值。

- **Model 层 (ORM 实体)**：为兼容 FreeSql/EF Core 的无参构造函数实例化机制，开发者应在手动编写/调整实体类时，对非空属性使用 `= default!;` 进行初始化屏蔽。

---

## 7. 接口能力矩阵示例 (Capabilities Matrix)

针对 `MerchantInfo` 模型，生成的公开接口如下：

| **方法名称**                | **参数结构**                          | **说明**                             |
| ----------------------- | --------------------------------- | ---------------------------------- |
| `GetByIdAsync`          | `long id`                         | 强主键获取，返回单条 DTO。                    |
| `SelectPageAsync`       | `predicate, pageNumber, pageSize` | 默认标准分页查询，返回 `PageResult`。          |
| `GetByCreditCodeAsync`  | `string creditcode`               | 唯一键获取，智能剥离了冗余类名。                   |
| `SelectPageByNameAsync` | `string name` (支持智能匹配)            | 独立生成的模糊搜索接口，支持 `*` 语法。             |
| `SelectByStatusAsync`   | `MerchantStatusEnum? status`      | 基于索引生成的动态状态筛选接口。                   |
| `DeleteAsync`           | `long id`                         | 统一删除入口，自动处理软/硬删除路由。                |
| `GetGraphQLQueryable`   | 无参数                               | 提供 `IQueryable` 供 GraphQL 进行动态投影 。 |

---

## 8. 特性配置深度指南（Annotation Guide）

### 8.1 [GenerateCode] 服务全局配置

该特性挂载于实体类头部，定义了生成 Service 的物理底座：

- **`BaseUserType` (必填)**：指定 Service 继承的泛型用户类型（如 `DmpUserInfo`），直接决定了 `User` 属性的强类型支持。

- **`IsView` (默认为 false)**：若标记为 `true`，生成器将自动屏蔽所有写入操作（Create/Update/Delete），仅保留查询矩阵。

- **分页参数覆盖**：可通过 `DefaultPageSize` 和 `MaxSearchLimit` 针对特定大数据量表优化性能。

### 8.2 [DtoField] 字段级精细控制

- **`IsSearchable` (搜索开关)**：标记为 `true` 时，系统将为该字段生成**独立的四位一体查询组**。若字段为 `string` 类型，则自动开启 `*` 前缀模糊查询协议。

- **`SearchGroup` (逻辑分组)**：将多个字段归类为同一个业务查询入口（如 `SearchGroup = "Keyword"`）。组内字段的匹配模式（精确/模糊）由各自的 `IsSearchable` 标记独立决定。

- **`CanModify / UpdateReadOnly` (权限守卫)**：
  
  - `CanModify = false`：该字段在 DTO 中依然存在，但在 `ApplyToEntity` 映射时会被彻底忽略，常用于 `UId`、`TenantId`。
  
  - `UpdateReadOnly = true`：仅在 `Create` 场景下允许回填，`Update` 场景自动跳过，常用于“初始类别”或“证件号码”。

- **`MaskPattern` (脱敏掩码)**：一旦定义此属性，系统自动开启 `FromEntity` 阶段的脱敏逻辑，除非显式设置 `Masking = false`。

### 8.3 [Column] 物理隔离与 MapType

当属性配置了 `MapType`（例如将 `Enum` 映射为 `string`，或将复杂对象映射为 JSON）时，系统自动识别此字段为**物理映射失配**：

- **物理约束让步**：在此场景下，生成的验证链条将**自动跳过** `MaxLength` 物理长度验证。

- **防御目的**：防止验证器对非纯字符串类型执行长度检查从而引发编译期的类型推断错误。此类字段的长度安全性应交由数据库层约束或在 `.Logic.cs` 中手动处理。

---

## 9. CUD 生命周期钩子最佳实践（CUD Hooks Best Practices）

生成的 Service 提供了对称的 `OnBefore` 和 `OnAfter` 偏函数钩子，用于在不修改生成代码的前提下植入核心业务逻辑。

### 9.1 创建钩子 (Create Hooks)

- **`OnBeforeCreate(entity)`**：
  
  - **典型用途**：自动填充业务流水号（非数据库自增 ID）、初始化状态机、计算复杂的冗余字段。

- **`OnAfterCreate(entity)`**：
  
  - **典型用途**：发送创建成功的领域消息（MQ）、同步写入搜索索引、记录操作审计日志。

### 9.2 更新钩子 (Update Hooks)

- **`OnBeforeUpdate(entity)`**：
  
  - **典型用途**：版本号（Optimistic Locking）冲突检测、核心敏感字段变更前的状态对比。

- **`OnAfterUpdate(entity)`**：
  
  - **典型用途**：清理关联的分布式缓存。

### 9.3 删除钩子 (Delete Hooks)

- **`OnBeforeDelete(entity)`**：
  
  - **典型用途**：**级联删除检查**。在执行物理或软删除前，检查是否存在关联的从表数据，若存在则抛出业务异常阻断操作。

- **`OnAfterDelete(entity)`**：
  
  - **典型用途**：清理已下架资源的物理文件（如 OSS 上的图片）。

---

## 10. 综合示例：复杂业务场景配置

假设我们需要为一个“商户表”配置如下规则：

1. 既支持通过 `Code` 精确查询，也支持 `Name` 的模糊搜索。

2. `Code` 和 `Name` 都要参与“全局关键字”搜索组。

3. `AuditPassTime` 属于审计字段，禁止 DTO 回填。

**模型标注参考：**

```csharp
[GenerateCode(BaseUserType = nameof(DmpUserInfo), DefaultPageSize = 50)]
public partial class MerchantInfo
{
    [DtoField(IsUnique = true, SearchGroup = "Keyword")]
    public string Code { get; set; } // 独立精确查 + 组内精确查

    [DtoField(IsSearchable = true, SearchGroup = "Keyword")]
    public string Name { get; set; } // 独立模糊查 + 组内模糊查

    [DtoField(CanModify = false)]
    public DateTime AuditPassTime { get; set; } // 禁止回填
}
```

通过上述配置，`xCodeGen` 将自动为您闭环所有复杂的 `if/else` 动态 SQL 拼接逻辑，开发者仅需在 `Service.Logic.cs` 中专注于实现具体的业务钩子即可。
