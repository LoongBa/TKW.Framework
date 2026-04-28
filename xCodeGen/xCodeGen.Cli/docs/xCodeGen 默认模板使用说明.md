# xCodeGen 默认模板使用说明 V1.27

## 1. 概述

### 1.1 什么是 xCodeGen 默认模板

xCodeGen 默认模板（V1.27）是专为 TKWF.Domain V3.0 领域框架设计的代码生成模板集合。它根据数据库表结构或实体元数据，自动生成适配框架的 Entity、DTO、Service 骨架代码，大幅减少重复编码工作。

### 1.2 核心特性

- 自动生成 Entity 实体类，实现 IDomainEntity 接口
- 自动生成 Dto 类（单一类 + 场景标志区分 Create/Update/Query 场景）
- 自动生成 Entity 与 DTO 之间的 ApplyToEntity 映射方法
- 自带场景化验证逻辑（EnumSceneFlags）
- 支持智能查询条件解析（ParseSearchValue 规则）
- 自动生成 Service 接口与实现类骨架，集成 FreeSql 数据访问

### 1.3 适用场景

- 新项目快速搭建领域模型
- 数据库变更后同步更新代码
- 统一团队代码风格和命名规范
- 减少手写 CRUD 代码的工作量

---

## 2. 快速开始

### 2.1 安装 xCodeGen

通过 .NET CLI 安装全局工具：

```powershell
dotnet tool install -g TKWF.xCodeGen.Cli
```

### 2.2 配置生成参数

在项目根目录创建 xCodeGen.json 配置文件：

```json
{
  "TargetProject": "../MyProject.Domain.csproj",
  "EnableSkipUnchanged": true,
  "OutputRoot": "Generated",
  "TemplatesPath": "Templates",
  "TemplateMappings": {
    "Service": "Service.cshtml",
    "Entity": "Entity.cshtml",
    "Dto": "Dto.cshtml"
  },
  "SkeletonMappings": {
    "EntityEmpty": "EntityEmpty.cshtml",
    "DtoEmpty": "DtoEmpty.cshtml"
  },
  "FileNamePatterns": {
    "Service": "{ClassName}Service.g.cs",
    "EntityEmpty": "{ClassName}.biz.cs",
    "Entity": "{ClassName}.g.cs",
    "DtoEmpty": "{ClassName}Dto.cs",
    "Dto": "{ClassName}Dto.g.cs"
  },
  "OutputDirectories": {
    "Service": "....Services",
    "EntityEmpty": "....Entities",
    "Entity": "....Entities",
    "DtoEmpty": "....EntitiesDTOs",
    "Dto": "....EntitiesDTOs"
  },
  "CustomSettings": {
    "EnableLogging": "false",
    "AutoManagedFields": "Id,CreateTime,UpdateTime,IsDeleted,TenantId,UId"
  }
}
```

### 2.3 执行代码生成

命令行执行生成：

```powershell
tkw-codegen generate --config xCodeGen.json
```

或指定单表生成：

```powershell
tkw-codegen generate --table Merchant --config xCodeGen.json
```

---

## 3. 生成的代码结构说明

### 3.1 Entity 实体类

生成的 Entity 类是领域核心，直接映射数据库表，同时包含业务状态。

核心结构示例：

```csharp
public partial class MerchantEntity : IDomainEntity
{
    // 持久化来源标记（影响验证逻辑）
    [JsonIgnore]
    public bool IsFromPersistentSource { get; set; } = false;

    // 属性定义（由数据库字段生成）
    public required string Name { get; set; }
    public decimal Balance { get; set; }

    // 场景化验证入口
    public void Validate(EnumSceneFlags scene = EnumSceneFlags.ForceValidate)
    {
        if (IsFromPersistentSource && (scene & EnumSceneFlags.ForceValidate) == 0) return;
        var results = ValidateDataCore(scene).ToList();
        OnBusinessValidate(scene, results);
        if (results.Any()) throw new ValidationResultsException(results);
    }

    // 验证规则链（由模板自动生成）
    protected IEnumerable<ValidationResult> ValidateDataCore(EnumSceneFlags scene)
    {
        var results = new List<ValidationResult>();
        // .Required(nameof(Name), e => e.Name)
        // .MaxLength(nameof(Name), 100, e => e.Name)
        return results;
    }

    // 扩展点（在 .biz.cs 文件中实现）
    partial void OnBusinessValidate(EnumSceneFlags scene, List<ValidationResult> results);
}
```

关键要点：

- Entity 使用 .NET 10 的 required 关键字保证必填字段完整性
- Entity 实现 IDomainEntity 接口，适配 TKWF.Domain 框架
- IsFromPersistentSource 标记字段，区分数据是否来自数据库（影响验证逻辑）
- Validate 方法支持场景化验证，通过 EnumSceneFlags 控制
- 禁止将 Entity 直接返回给前端或 Web API

### 3.2 DTO 数据传输对象

xCodeGen 为每个 Entity 生成单一 Dto 类，通过 EnumSceneFlags 区分不同场景。

核心结构示例：

```csharp
public partial record MerchantDto : DomainDtoBase<MerchantEntity>
{
    // 属性定义
    public required string Name { get; init; }
    public decimal Balance { get; init; }

    // 静态工厂：Entity -> Dto
    public static MerchantDto FromEntity(MerchantEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new MerchantDto
        {
            IsFromPersistentSource = entity.IsFromPersistentSource,
            Name = entity.Name,
            Balance = entity.Balance,
        };
    }

    // 映射方法：Dto -> Entity
    public override MerchantEntity ApplyToEntity(MerchantEntity entity, EnumSceneFlags scene)
    {
        ArgumentNullException.ThrowIfNull(entity);
        this.CreateMapper(entity, scene)
            .Map(nameof(Name), d => d.Name, (e, v) => e.Name = v)
            .Map(nameof(Balance), d => d.Balance, (e, v) => e.Balance = v)
        ;
        return entity;
    }

    // 场景化验证入口
    public override void ValidateData(EnumSceneFlags scene)
    {
        if (IsFromPersistentSource && (scene & EnumSceneFlags.ForceValidate) == 0) return;
        var results = ValidateDataCore(scene).ToList();
        OnCustomValidate(scene, results);
        if (results.Any()) throw new ValidationResultsException(results);
    }

    // 验证规则链（由模板自动生成）
    protected IEnumerable<ValidationResult> ValidateDataCore(EnumSceneFlags scene)
    {
        var results = new List<ValidationResult>();
        // .Required(nameof(Name), d => d.Name)
        // .MaxLength(nameof(Name), 100, d => d.Name)
        return results;
    }

    // 扩展点（在 Dto.cs 文件中实现）
    partial void OnCustomValidate(EnumSceneFlags scene, List<ValidationResult> results);
}
```

关键要点：

- Dto 继承 DomainDtoBase<TEntity>，框架提供基础映射与验证能力
- FromEntity 静态工厂方法，实现 Entity -> Dto 单向转换
- ApplyToEntity 方法实现 Dto -> Entity 映射，支持 Masking、导航属性过滤、自动字段跳过
- ValidateData 方法支持场景化验证，通过 EnumSceneFlags 控制
- OnCustomValidate 为 partial 方法，可在 Dto.cs 文件中扩展自定义验证

### 3.3 Service 服务类

xCodeGen 为每个 Entity 生成 Service 类，集成 FreeSql 数据访问与框架基础能力。

核心结构示例：

```csharp
public partial class MerchantService : DomainServiceBase<DmpUserInfo>
{
    protected readonly IFreeSql _fsql;
    protected IBaseRepository<MerchantEntity> Repo => _fsql.GetRepository<MerchantEntity>();

    public MerchantService(DomainUser<DmpUserInfo> user, IFreeSql fsql) : base(user)
    {
        _fsql = fsql ?? throw new ArgumentNullException(nameof(fsql));
    }

    // 公共查询接口（DTO 返回值）
    public async Task<MerchantDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var entity = await InternalGetAsync(x => x.Id == id, ct);
        return MerchantDto.FromEntity(entity);
    }

    // 公共写操作（DTO 驱动）
    public virtual async Task<MerchantDto> CreateAsync(MerchantDto dto, CancellationToken ct = default)
    {
        dto.ValidateData(EnumSceneFlags.Create);
        var entity = new MerchantEntity();
        dto.ApplyToEntity(entity, EnumSceneFlags.Create);
        var result = await InternalCreateAsync(entity, ct);
        return MerchantDto.FromEntity(result);
    }

    // 内部核心逻辑（Internal 级原子操作）
    internal async Task<MerchantEntity> InternalCreateAsync(MerchantEntity entity, CancellationToken ct = default)
    {
        OnBeforeCreate(entity);
        entity.Validate(EnumSceneFlags.Create | EnumSceneFlags.ForceValidate);
        await Repo.InsertAsync(entity, ct);
        OnAfterCreate(entity);
        return entity;
    }

    // 扩展点（在 Service.partial.cs 文件中实现）
    partial void OnQueryFiltering(ref ISelect<MerchantEntity> query);
    partial void OnBeforeCreate(MerchantEntity entity);
    partial void OnAfterCreate(MerchantEntity entity);
}
```

关键要点：

- Service 继承 `DomainServiceBase<TUserInfo>`，可通过 `DomainUser` 获取
- 注入 `IFreeSql` 进行数据访问，Repo 属性提供便捷仓储操作
- 公共接口返回 DTO，内部方法操作 Entity，实现防腐隔离
- `Internal*` 方法为原子操作，支持事务与 AOP 拦截
- `partial` 方法为扩展点，可在不修改生成代码的前提下扩展业务逻辑

---

## 4. 与 TKWF.Domain 框架的集成

### 4.1 Entity 与框架的关系

生成的 Entity 类直接适配 `TKWF.Domain` 框架：

- 实现 `IDomainEntity` 接口，框架可识别为领域实体
- `IsFromPersistentSource` 标记字段，控制验证逻辑（持久化来源可跳过部分校验）
- Validate 方法支持 EnumSceneFlags 场景标志，与 Dto 验证体系一致
- 可直接用于 FreeSql 的 CodeFirst 模式，支持 [Column]、[Navigate] 等特性

### 4.2 DTO 与框架的关系

生成的 Dto 类与框架深度集成：

- 继承 `DomainDtoBase<TEntity>`，框架提供 CreateMapper、Map 等映射工具
- ApplyToEntity 方法支持 Masking、导航属性过滤、自动字段跳过等高级映射逻辑
- ValidateData 方法与 Entity.Validate 体系一致，支持场景化验证
- FromEntity 静态工厂方法，实现 Entity -> Dto 单向安全转换

### 4.3 Service 与框架的关系

生成的 Service 类完全适配框架：

- 继承 `DomainServiceBase<TUserInfo>`，可通过 `DomainUser` 获取
- 可使用 [TransactionFilter]、[RequireRoleFlag] 等框架特性进行 AOP 拦截
- 内部方法（`Internal*`）支持事务与异常传播，与框架异常处理机制无缝集成
- partial 方法扩展点，支持在不修改生成代码的前提下注入业务逻辑

---

## 5. 生成的代码使用指南

### 5.1 在业务代码中使用 Entity

```csharp
// 从数据库查询实体
var merchant = await _fsql.Select<MerchantEntity>()
    .Where(a => a.Id == id)
    .FirstAsync();

// 修改实体状态（业务逻辑）
if (merchant.Balance < amount)
    throw new DomainException(4001, "商户余额不足");
merchant.Balance -= amount;

// 保存变更（事务由 [TransactionFilter] 自动管理）
await _fsql.Update<MerchantEntity>().SetSource(merchant).ExecuteAffrowsAsync();
```

### 5.2 在业务代码中使用 DTO

```csharp
// 创建场景
var dto = new MerchantDto { Name = "测试商户", Balance = 1000 };
dto.ValidateData(EnumSceneFlags.Create); // 显式验证（可选，Service 中已调用）
var entity = dto.ApplyToEntity(new MerchantEntity(), EnumSceneFlags.Create);
await _fsql.Insert<MerchantEntity>().AppendData(entity).ExecuteAffrowsAsync();

// 更新场景
var updateDto = new MerchantDto { Name = "新名称" };
var entity = await GetByIdAsync(id);
updateDto.ApplyToEntity(entity, EnumSceneFlags.Update);
await _fsql.Update<MerchantEntity>().SetSource(entity).ExecuteAffrowsAsync();
```

### 5.3 在业务代码中使用 Service

```csharp
// 从 DomainUser 获取服务（严禁直接从 DI 容器获取）
var user = context.Items["DomainUser"] as DomainUser<DmpUserInfo>;
var merchantService = user.UseAop<MerchantService>();

// 调用服务方法（自动执行权限、日志、事务等 Filter）
await merchantService.CreateAsync(createDto);
```

---

## 6. 高级功能

### 6.1 场景化验证（EnumSceneFlags）

模板支持根据场景标志生成不同的验证逻辑：

```csharp
public enum EnumSceneFlags
{
    None = 0,
    Create = 1,
    Update = 2,
    Query = 4,
    ForceValidate = 8
}
```

在 DTO/Entity 中根据场景标志执行不同验证：

```csharp
public override void ValidateData(EnumSceneFlags scene)
{
    // 持久化来源且非强制验证时跳过（避免重复校验）
    if (IsFromPersistentSource && (scene & EnumSceneFlags.ForceValidate) == 0) return;

    var results = ValidateDataCore(scene).ToList();
    OnCustomValidate(scene, results); // 扩展点
    if (results.Any()) throw new ValidationResultsException(results);
}
```

### 6.2 智能查询引擎（ParseSearchValue 规则）

Service 模板生成的搜索方法支持智能解析：

- * 前缀或后缀：表示模糊匹配（如 "*测试" -> Contains("测试")）
- ** 双星号：表示转义，搜索 literal * 字符

示例：

```csharp
// 按名称模糊查询
var results = await merchantService.SelectByNameAsync("*测试*");

// 按时间范围查询
var results = await merchantService.SelectByCreateTimeAsync(
    beginCreateTime: DateTime.Now.AddDays(-7),
    endCreateTime: DateTime.Now
);
```

### 6.3 自动赋值机制（ApplyToEntity）

ApplyToEntity 方法由模板自动生成，支持以下高级特性：

- Masking 智能逻辑：若字段标记 [DtoField(Masking = true)]，映射时自动调用脱敏函数
- 导航属性过滤：自动跳过 [Navigate] 标记的关联属性，防止 ORM 级联污染
- 自动字段跳过：系统管理字段（如 Id、CreatedTime）自动排除，避免业务代码误改
- 只读字段保护：[DtoField(CanModify = false)] 标记的字段禁止回填

### 6.4 扩展点使用指南

所有生成类均为 partial，支持在不修改生成代码的前提下扩展：

```csharp
// MerchantDto.cs（非 .g.cs 文件）
public partial record MerchantDto
{
    partial void OnCustomValidate(EnumSceneFlags scene, List<ValidationResult> results)
    {
        // 自定义交叉验证
        if (scene.HasFlag(EnumSceneFlags.Create) && this.Balance < 0)
            results.Add(new ValidationResult("初始余额不能为负", new[] { nameof(Balance) }));
    }
}

// MerchantService.partial.cs
public partial class MerchantService
{
    partial void OnBeforeCreate(MerchantEntity entity)
    {
        // 自动填充审计字段
        entity.CreatedTime = DateTime.Now;
        entity.CreatorId = User.CurrentId;
    }
}
```

---

## 7. 文件命名与目录约定

### 7.1 生成文件命名规则

| 类型           | 文件模式                          | 说明              | 是否可手动修改 |
| ------------ | ----------------------------- | --------------- | ------- |
| Entity 主文件   | {ClassName}.g.cs              | 引擎生成，包含核心逻辑     | 否       |
| Entity 扩展文件  | {ClassName}.biz.cs            | 开发者扩展，partial 类 | 是       |
| Dto 主文件      | {ClassName}Dto.g.cs           | 引擎生成，包含映射与验证    | 否       |
| Dto 扩展文件     | {ClassName}Dto.cs             | 开发者扩展，partial 类 | 是       |
| Service 主文件  | {ClassName}Service.g.cs       | 引擎生成，包含 CRUD 骨架 | 否       |
| Service 扩展文件 | {ClassName}Service.partial.cs | 开发者扩展，partial 类 | 是       |

### 7.2 输出目录结构

    MyProject.Domain/
    ├── Entities/
    │   ├── Merchant.g.cs          ← Entity 主文件
    │   ├── Merchant.biz.cs        ← Entity 扩展文件
    │   ├── MerchantDto.g.cs       ← Dto 主文件
    │   └── MerchantDto.cs         ← Dto 扩展文件
    └── Services/
        ├── MerchantService.g.cs   ← Service 主文件
        └── MerchantService.partial.cs ← Service 扩展文件

---

## 8. 常见问题

### 8.1 生成的代码可以修改吗？

可以，但建议遵循以下原则：

- 不要直接修改 .g.cs 结尾的生成文件，避免重新生成时覆盖
- 使用 partial 类在独立文件中扩展业务逻辑（如 .biz.cs 或 .partial.cs）
- 如需修改生成逻辑，应自定义模板而非手动改代码

### 8.2 数据库变更后如何同步代码？

重新执行生成命令，引擎会检测元数据哈希值：

- 若哈希未变，跳过生成（增量模式，EnableSkipUnchanged = true）
- 若哈希变更，提示冲突并支持覆盖/跳过策略
- 建议配合版本控制，生成前确认变更影响

### 8.3 Entity 可以直接返回给前端吗？

绝对禁止。Entity 是领域核心对象，包含数据库映射和业务状态，必须通过 DTO 进行防腐隔离：

- 查询接口返回 DTO（Service.Select* 方法已自动转换）
- 创建/更新接口接收 DTO（Service.CreateAsync/UpdateAsync 已自动映射）
- 前端仅感知 DTO，领域层仅操作 Entity

### 8.4 如何处理复杂业务逻辑？

生成的 Service 是骨架代码，复杂业务逻辑可通过以下方式扩展：

- 在 partial 方法中注入逻辑（如 OnBeforeCreate、OnQueryFiltering）
- 组合多个 Service 方法实现业务流程
- 使用领域事件、规约模式等 DDD 模式解耦复杂逻辑

---

## 9. 版本历史

### V1.27（当前版本）

- 适配 TKWF.Domain V3.0 与 .NET 10 required 关键字
- 增强 ApplyToEntity 映射机制，支持 Masking、导航属性过滤、自动字段跳过
- 优化场景化验证逻辑，支持 EnumSceneFlags.ForceValidate 强制校验
- 改进 Service 模板，集成 FreeSql 仓储与软删除支持

### V1.26

- 初始支持 TKWF.Domain V2.0
- 基础 Entity/DTO/Service 生成
- 简单验证规则链式生成

---

## 10. 相关文档

- 《TKWF.Domain 使用说明》：了解领域框架核心概念与 Entity/Dto/Service 使用规范
- 《TKWF.Domain 设计方案》：了解框架架构原理与 AOP 拦截机制
- 《xCodeGen 设计和使用说明》：了解代码生成引擎整体架构
- 《xCodeGen 如何设计自定义模板》：学习自定义模板开发与扩展点使用

# xCodeGen 设计和使用说明 V2.2

## 1. 概述

### 1.1 定位

xCodeGen 是独立的跨平台代码生成工具，与领域运行时代码完全解耦。仅模板与 TKWF.Domain 相关，代码生成本身与 TKWF.Domain 关系不大。

### 1.2 核心职责

- 读取数据库 Schema 或结构化元数据
- 基于 Razor 模板引擎生成 Entity、DTO、Service 骨架代码
- 自动生成 Entity 与 DTO 之间的赋值/映射机制
- 支持增量生成与元数据哈希比对

### 1.3 架构演进

**老版本架构（内置 EntityORM）**：
Database -> Model（纯数据表映射）-> Entity（领域层聚合包装）-> ViewModel/Dto（表现层）

**V3.0 新版本架构（实用主义 DDD）**：
Database -> Entity（兼顾持久化映射与领域行为）-> Dto（由 xCodeGen 自动生成）

**演进优势**：

- 砍掉中间商：去除无意义的 Model 到 Entity 手动属性 Copy
- xCodeGen 价值凸显：自动生成安全的 Dto，自带验证与赋值机制
- 认知统一：开发者看到 Entity 就知道是对应数据库且包含核心状态的类

---

## 2. 架构设计

### 2.1 模块组成

| 模块                     | 职责                                                                |
| ---------------------- | ----------------------------------------------------------------- |
| xCodeGen.Abstractions  | 抽象接口与契约，定义 ClassMetadata、GenerateCodeSettings、CodeGenPolicy 等核心抽象 |
| xCodeGen.Core          | 核心生成引擎，负责元数据解析和代码生成流程                                             |
| xCodeGen.Cli           | 命令行工具，提供交互式生成体验                                                   |
| xCodeGen.Cli/Templates | 默认模板集合（与 TKWF.Domain 深度集成）                                        |

### 2.2 模板引擎

xCodeGen 采用 Razor 模板引擎（.cshtml 文件），支持以下语法：

- 变量输出：@Model.ClassName 输出类名
- 代码块：@{ var x = 1; } 执行 C# 逻辑
- 循环迭代：@foreach (var p in Model.Properties) { ... } 遍历字段集合
- 条件判断：@if (condition) { ... } else { ... } 动态生成不同代码
- 片段引入：@{ await IncludeAsync("_InternalValidation.cshtml", Model); } 复用验证逻辑

### 2.3 生成流程

第一步：元数据收集
引擎从数据库或配置文件中读取表结构信息，包括表名、字段名、字段类型、约束条件等。

第二步：元数据标准化
将不同数据库的字段类型映射为统一的标准类型（如 SQL Server 的 nvarchar 映射为 string，int 映射为 int32 等）。

第三步：模板渲染
引擎遍历预定义的模板文件，将标准化后的元数据注入模板上下文，执行渲染生成最终代码。

第四步：代码输出
将生成的代码写入指定目录，支持增量更新和冲突检测。

---

## 3. 模板上下文变量

### 3.1 基础信息

| 变量名       | 类型     | 说明               |
| --------- | ------ | ---------------- |
| ClassName | string | 实体类名（PascalCase） |
| Namespace | string | 目标命名空间           |
| FullName  | string | 完整类型名            |
| Summary   | string | 类注释/说明           |

### 3.2 字段集合（Properties）

每个 Property 对象包含以下属性：

| 属性名        | 类型     | 说明                                     |
| ---------- | ------ | -------------------------------------- |
| Name       | string | 属性名（PascalCase）                        |
| TypeName   | string | .NET 类型（如 string、int、DateTime?）        |
| IsNullable | bool   | 是否可为空                                  |
| Summary    | string | 字段注释                                   |
| Attributes | List   | 特性集合（如 [Column]、[Required]、[DtoField]） |

### 3.3 特性读取工具（CodeGenPolicy）

| 方法                                     | 说明            |
| -------------------------------------- | ------------- |
| GetBoolProp(attr, "IsRequired", false) | 读取布尔属性        |
| GetStringProp(attr, "MaskPattern")     | 读取字符串属性       |
| IsAutoManaged(prop, customAutoFields)  | 判断是否为系统自动管理字段 |
| GetSearchGroups(Model)                 | 解析搜索分组配置      |

### 3.4 生成配置（GenerateCodeSettings）

| 属性                 | 说明                       |
| ------------------ | ------------------------ |
| GeneratedNamespace | 生成代码的目标命名空间              |
| MetadataHash       | 元数据哈希，用于增量生成检测           |
| Type               | 生成类型标识（"Entity" 或 "Dto"） |

---

## 4. 配置文件详解

### 4.1 配置文件结构

xCodeGen 通过项目根目录下的 xCodeGen.json 文件进行全局配置。

### 4.2 核心配置项

| 配置项                 | 说明                    | 示例值                          |
| ------------------- | --------------------- | ---------------------------- |
| TargetProject       | 指定待分析的 .csproj 项目文件路径 | "../MyProject.Domain.csproj" |
| EnableSkipUnchanged | 启用增量生成模式              | true                         |
| OutputRoot          | 所有生成文件的相对根目录          | "Generated"                  |
| TemplatesPath       | 指定 .cshtml 模板文件的存放目录  | "Templates"                  |

### 4.3 模板映射配置

TemplateMappings：定义生成类型与主模板文件的对应关系

```json
"TemplateMappings": {
  "Service": "Service.cshtml",
  "Entity": "Entity.cshtml",
  "Dto": "Dto.cshtml"
}
```

SkeletonMappings：生成 partial 扩展文件，供开发者手写业务逻辑

```json
"SkeletonMappings": {
  "EntityEmpty": "EntityEmpty.cshtml",
  "DtoEmpty": "DtoEmpty.cshtml"
}
```

### 4.4 文件命名与目录规则

FileNamePatterns：输出文件命名规则

```json
"FileNamePatterns": {
  "Service": "{ClassName}Service.g.cs",
  "EntityEmpty": "{ClassName}.biz.cs",
  "Entity": "{ClassName}.g.cs",
  "DtoEmpty": "{ClassName}Dto.cs",
  "Dto": "{ClassName}Dto.g.cs"
}
```

OutputDirectories：输出子目录配置

```json
"OutputDirectories": {
  "Service": "....Services",
  "EntityEmpty": "....Entities",
  "Entity": "....Entities",
  "DtoEmpty": "....EntitiesDTOs",
  "Dto": "....EntitiesDTOs"
}
```

### 4.5 自定义设置

```json
"CustomSettings": {
  "EnableLogging": "false",
  "AutoManagedFields": "Id,CreateTime,UpdateTime,IsDeleted,TenantId,UId"
}
```

- EnableLogging：控制台输出详细生成过程，便于调试模板
- AutoManagedFields：定义由系统自动维护的字段列表，生成代码时自动跳过这些字段的 DTO 赋值与验证

---

## 5. 使用指南

### 5.1 安装与配置

通过 .NET CLI 安装全局工具：

```powershell
dotnet tool install -g TKWF.xCodeGen.Cli
```

在项目根目录创建 xCodeGen.json 配置文件（参考第 4 节）。

### 5.2 执行代码生成

命令行执行生成：

    tkw-codegen generate --config xCodeGen.json

或指定单表生成：

    tkw-codegen generate --table Merchant --config xCodeGen.json

### 5.3 增量生成

启用 EnableSkipUnchanged = true 后，引擎会比对模板头部生成的 @@[xCodeGen.Hash: xxx] 注释哈希值：

- 若哈希未变，跳过生成
- 若哈希变更，重新生成该文件

---

## 6. 高级功能

### 6.1 场景化验证

模板支持 EnumSceneFlags 场景标志，实现差异化验证：

```csharp
public enum EnumSceneFlags
{
    None = 0,
    Create = 1,
    Update = 2,
    Query = 4,
    ForceValidate = 8
}
```

### 6.2 智能查询引擎

QueryDto 支持智能搜索条件解析：

- * 前缀或后缀：表示模糊匹配
- ** 双星号：表示转义，搜索 literal * 字符

### 6.3 自动赋值机制

ApplyToEntity 方法由模板自动生成，支持：

- Masking 智能逻辑
- 导航属性过滤
- 自动字段跳过
- 只读字段保护

### 6.4 字段特性控制

可通过 [DtoField] 特性控制生成行为：

```csharp
[DtoField(IsRequired = true, CanModify = false, Masking = true)]
public string Name { get; set; }
```

---

## 7. 与 TKWF.Domain 的集成

xCodeGen 生成的代码完全适配 TKWF.Domain 框架：

- Entity 实现 IDomainEntity 接口
- DTO 自带验证逻辑和 ApplyToEntity 映射方法
- Service 骨架继承 DomainServiceBase<TUserInfo>

详细参考《TKWF.Domain 使用说明》。

---

## 8. 版本历史

### V2.2（当前版本）

- 适配 TKWF.Domain V3.0 与 .NET 10 required 关键字
- 增强 ApplyToEntity 映射机制
- 优化场景化验证逻辑
- 改进配置文件结构

### V2.1

- 初始支持 TKWF.Domain V2.0
- 基础 Entity/DTO/Service 生成

---

## 9. 相关文档

- 《TKWF.Domain 使用说明》：了解领域框架核心概念
- 《TKWF.Domain 设计方案》：了解框架架构原理
- 《xCodeGen 默认模板使用说明》：了解默认模板生成的代码结构与使用方法
- 《xCodeGen 如何设计自定义模板》：学习自定义模板开发

# xCodeGen 如何设计自定义模板

## 1. 概述

### 1.1 为什么需要自定义模板

默认模板（V1.27）适配 TKWF.Domain V3.0 框架，满足大多数场景需求。但在以下情况下，您可能需要自定义模板：

- 项目有特殊的命名规范或代码风格要求
- 需要生成默认模板未覆盖的代码类型（如 Repository、Validator、Mapper 等）
- 需要集成第三方库或框架（如 EF Core、Dapper、MediatR 等）
- 需要定制验证规则或映射逻辑

### 1.2 自定义模板的优势

- 保持代码生成自动化优势的同时，满足项目特定需求
- 统一团队代码风格，减少 Code Review 成本
- 可随框架升级灵活调整，无需手动修改大量生成代码

---

## 2. 模板开发基础

### 2.1 Razor 模板语法

xCodeGen 使用 Razor 模板引擎（.cshtml 文件），核心语法如下：

变量输出：

```csharp
@Model.ClassName
```

代码块：

```csharp
@{
    var isNavigate = p.Attributes.Any(a => a.TypeFullName.Contains("NavigateAttribute"));
}
```

循环迭代：

```csharp
@foreach (var p in Model.Properties)
{
    public @p.TypeName @p.Name { get; set; }
}
```

条件判断：

```csharp
@if (p.IsNullable)
{
    public @p.TypeName? @p.Name { get; set; }
}
else
{
    public required @p.TypeName @p.Name { get; set; }
}
```

片段引入：

```csharp
@{ await IncludeAsync("_InternalValidation.cshtml", Model); }
```

### 2.2 模板上下文变量

模板渲染时可访问的核心上下文变量：

| 变量                   | 说明                       |
| -------------------- | ------------------------ |
| Model                | ClassMetadata 对象，包含实体元数据 |
| Model.ClassName      | 实体类名（PascalCase）         |
| Model.Namespace      | 目标命名空间                   |
| Model.Properties     | 字段集合                     |
| CodeGenPolicy        | 特性读取工具类                  |
| GenerateCodeSettings | 生成配置对象                   |

### 2.3 字段元数据结构

每个 Property 对象包含以下属性：

| 属性         | 说明              |
| ---------- | --------------- |
| Name       | 属性名（PascalCase） |
| TypeName   | .NET 类型         |
| IsNullable | 是否可为空           |
| Summary    | 字段注释            |
| Attributes | 特性集合            |

---

## 3. 自定义模板开发步骤

### 3.1 准备模板目录

创建独立的模板目录，建议结构如下：

    MyCustomTemplates/
    ├── Entity.cshtml
    ├── EntityEmpty.cshtml
    ├── Dto.cshtml
    ├── DtoEmpty.cshtml
    ├── Service.cshtml
    ├── _InternalValidation.cshtml
    └── config.json（可选）

### 3.2 复制默认模板作为起点

从 xCodeGen.Cli/Templates/目录复制默认模板文件到您的自定义目录，作为修改起点。

### 3.3 修改模板逻辑

根据项目需求修改模板，常见修改点包括：

**修改命名规则**：

```csharp
// 默认：MerchantEntity
// 自定义：Merchant
@Model.ClassName  // 直接输出类名，不加 Entity 后缀
```

**修改基类/接口**：

```csharp
// 默认：IDomainEntity
// 自定义：IEntity
public partial class @Model.ClassName : IEntity
```

**添加自定义特性**：

```csharp
@foreach (var p in Model.Properties)
{
    @if (!p.Attributes.Any(a => a.TypeFullName.Contains("DtoFieldIgnore")))
    {
        [MyCustomAttribute]
        public @p.TypeName @p.Name { get; set; }
    }
}
```

**修改验证规则**：

```csharp
// 在 _InternalValidation.cshtml 中添加自定义验证
@if (p.TypeName == "string")
{
    @:.Required(nameof(@p.Name), d => d.@p.Name)
    @:.CustomStringValidation(nameof(@p.Name), @p.MaxLength, d => d.@p.Name)
}
```

### 3.4 配置模板路径

修改 xCodeGen.json 中的 TemplatesPath 配置：

```csharp
{
  "TemplatesPath": "MyCustomTemplates",
  ...
}
```

或使用绝对路径：

```csharp
{
  "TemplatesPath": "C:/Templates/MyProject.V1",
  ...
}
```

---

## 4. 模板扩展点

### 4.1 自定义字段特性

可通过 [DtoField] 特性控制生成行为：

```csharp
[DtoField(IsRequired = true, CanModify = false, Masking = true)]
public string Name { get; set; }
```

在模板中读取特性：

```csharp
@{
    var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("DtoFieldAttribute"));
    bool isRequired = CodeGenPolicy.GetBoolProp(dtoAttr, "IsRequired", false);
    bool canModify = CodeGenPolicy.GetBoolProp(dtoAttr, "CanModify", true);
    bool masking = CodeGenPolicy.GetBoolProp(dtoAttr, "Masking", false);
}
```

### 4.2 自定义验证规则

在 DtoEmpty.cshtml 或 EntityEmpty.cshtml 中实现 partial 方法：

```csharp
partial void OnCustomValidate(EnumSceneFlags scene, List<ValidationResult> results)
{
    if (this.StartTime > this.EndTime)
        results.Add(new ValidationResult("开始时间不能晚于结束时间", new[] { nameof(StartTime) }));
}
```

### 4.3 自定义服务逻辑

在 Service 模板生成的 partial 方法中扩展业务：

```csharp
partial void OnBeforeCreate(MerchantEntity entity)
{
    entity.CreatedTime = DateTime.Now;
    entity.CreatorId = User.CurrentId;
}
```

### 4.4 自定义搜索分组

通过 [DomainGenerateCode] 特性配置搜索分组：

```csharp
[DomainGenerateCode(SearchGroups = new[] { "Name,Code", "CreateTime" })]
public class MerchantEntity { ... }
```

在模板中解析搜索分组：

```csharp
@{
    var searchGroups = CodeGenPolicy.GetSearchGroups(Model);
}
```

---

## 5. 模板调试技巧

### 5.1 启用详细日志

在 xCodeGen.json 中设置：

```json
{
  "CustomSettings": {
    "EnableLogging": "true"
  }
}
```

或在命令行添加 --verbose 参数：

    tkw-codegen generate --config xCodeGen.json --verbose

### 5.2 预览模式

使用 --preview 参数预览生成结果而不写入文件：

    tkw-codegen generate --config xCodeGen.json --preview

### 5.3 单表测试

使用 --table 参数指定单表进行快速迭代测试：

    tkw-codegen generate --table Merchant --config xCodeGen.json

### 5.4 检查生成哈希

生成文件头部包含元数据哈希注释：

    // @@[xCodeGen.Hash: abc123...]

可用于判断元数据是否变更，验证增量生成是否生效。

---

## 6. 模板发布与分发

### 6.1 打包为 NuGet 包

将模板打包为 NuGet 包，便于团队共享：

    dotnet pack MyCustomTemplates.csproj -c Release

### 6.2 独立目录分发

将模板目录放入版本控制，团队成员克隆后配置 TemplatesPath 即可使用。

### 6.3 多模板包切换

支持多模板包切换，适应不同项目需求：

```json
{
  "TemplatesPath": "Templates/ProjectA",  // 项目 A 模板
  ...
}
```

---

## 7. 常见场景示例

### 7.1 生成 Repository 层

创建 Repository.cshtml 模板：

```csharp
public interface I@Model.ClassNameRepository
{
    Task<@Model.ClassName> GetByIdAsync(long id);
    Task<IEnumerable<@Model.ClassName>> GetAllAsync();
    Task<@Model.ClassName> CreateAsync(@Model.ClassName entity);
    Task UpdateAsync(@Model.ClassName entity);
    Task DeleteAsync(long id);
}
```

在 xCodeGen.json 中添加映射：

```json
{
  "TemplateMappings": {
    "Repository": "Repository.cshtml",
    ...
  },
  "FileNamePatterns": {
    "Repository": "I{ClassName}Repository.cs",
    ...
  },
  "OutputDirectories": {
    "Repository": "....Repositories",
    ...
  }
}
```

### 7.2 生成 MediatR 命令/查询

创建 MediatRCommand.cshtml 模板：

```csharp
public record @Model.ClassNameCreateCommand : IRequest<@Model.ClassNameDto>
{
    public required string Name { get; init; }
    public decimal Balance { get; init; }
}

public class @Model.ClassNameCreateCommandHandler : IRequestHandler<@Model.ClassNameCreateCommand, @Model.ClassNameDto>
{
    public async Task<@Model.ClassNameDto> Handle(@Model.ClassNameCreateCommand request, CancellationToken cancellationToken)
    {
        // 处理逻辑
    }
}
```

### 7.3 生成 FluentValidation 验证器

创建 FluentValidator.cshtml 模板：

```csharp
public class @Model.ClassNameDtoValidator : AbstractValidator<@Model.ClassNameDto>
{
    public @Model.ClassNameDtoValidator()
    {
@foreach (var p in Model.Properties)
{
    @if (p.TypeName == "string")
    {
        RuleFor(x => x.@p.Name).NotEmpty().MaximumLength(@p.MaxLength);
    }
}
    }
}
```

---

## 8. 最佳实践

### 8.1 保持模板简洁

- 模板中只包含必要的生成逻辑
- 复杂逻辑封装到 CodeGenPolicy 或扩展方法中
- 使用片段引入（IncludeAsync）复用公共代码

### 8.2 遵循命名约定

- 主模板文件：Entity.cshtml、Dto.cshtml、Service.cshtml
- 扩展模板文件：EntityEmpty.cshtml、DtoEmpty.cshtml
- 公共片段：_InternalValidation.cshtml、_CommonHelpers.cshtml

### 8.3 版本控制模板

- 将模板文件纳入版本控制
- 模板变更时更新版本号
- 记录模板变更日志，便于追踪

### 8.4 测试生成代码

- 每次修改模板后，执行完整生成流程
- 检查生成代码是否能正常编译
- 验证生成代码是否符合预期功能

---

## 9. 常见问题

### 9.1 模板修改后不生效？

- 检查 xCodeGen.json 中的 TemplatesPath 配置是否正确
- 清除生成目录后重新执行生成命令
- 检查模板语法是否有错误（启用 EnableLogging 查看详细日志）

### 9.2 如何处理数据库类型与 .NET 类型的映射？

xCodeGen 通过 ClassMetadata.Properties.TypeName 提供标准化 .NET 类型，模板中直接使用 @p.TypeName 输出即可。如需自定义映射，可在元数据解析阶段介入。

### 9.3 如何处理复合主键？

模板中通过查找 [Column(IsPrimary = true)] 特性识别主键，复合主键场景下可遍历所有主键字段生成联合查询条件。

### 9.4 如何处理外键关联？

导航属性通过 [Navigate] 特性标记，模板中自动跳过其验证与赋值，避免 ORM 级联操作污染。如需生成关联 DTO，可在模板中扩展导航属性处理逻辑。

---

## 10. 相关文档

- 《xCodeGen 默认模板使用说明》：了解默认模板生成的代码结构与使用方法
- 《xCodeGen 设计和使用说明》：了解代码生成引擎整体架构与配置
- 《TKWF.Domain 使用说明》：了解领域框架核心概念与 Entity/Dto/Service 使用规范
