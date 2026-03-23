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

```
@Model.ClassName
```

代码块：

```
@{
    var isNavigate = p.Attributes.Any(a => a.TypeFullName.Contains("NavigateAttribute"));
}
```

循环迭代：

```
@foreach (var p in Model.Properties)
{
    public @p.TypeName @p.Name { get; set; }
}
```

条件判断：

```
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

```
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

```
MyCustomTemplates/
├── Entity.cshtml
├── EntityEmpty.cshtml
├── Dto.cshtml
├── DtoEmpty.cshtml
├── Service.cshtml
├── _InternalValidation.cshtml
└── config.json（可选）
```

### 3.2 复制默认模板作为起点

从 xCodeGen.Cli/Templates/目录复制默认模板文件到您的自定义目录，作为修改起点。

### 3.3 修改模板逻辑

根据项目需求修改模板，常见修改点包括：

**修改命名规则**：

```
// 默认：MerchantEntity
// 自定义：Merchant

@Model.ClassName  // 直接输出类名，不加 Entity 后缀
```

**修改基类/接口**：

```
// 默认：IDomainEntity
// 自定义：IEntity

public partial class @Model.ClassName : IEntity
```

**添加自定义特性**：

```
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

```
// 在 _InternalValidation.cshtml 中添加自定义验证
@if (p.TypeName == "string")
{
    @:.Required(nameof(@p.Name), d => d.@p.Name)
    @:.CustomStringValidation(nameof(@p.Name), @p.MaxLength, d => d.@p.Name)
}
```

### 3.4 配置模板路径

修改 xcodegen.config.json 中的 TemplatesPath 配置：

```
{
  "TemplatesPath": "MyCustomTemplates",
  ...
}
```

或使用绝对路径：

```
{
  "TemplatesPath": "C:/Templates/MyProject.V1",
  ...
}
```

---

## 4. 模板扩展点

### 4.1 自定义字段特性

可通过 [DtoField] 特性控制生成行为：

```
[DtoField(IsRequired = true, CanModify = false, Masking = true)]
public string Name { get; set; }
```

在模板中读取特性：

```
@{
    var dtoAttr = p.Attributes.FirstOrDefault(a => a.TypeFullName.Contains("DtoFieldAttribute"));
    bool isRequired = CodeGenPolicy.GetBoolProp(dtoAttr, "IsRequired", false);
    bool canModify = CodeGenPolicy.GetBoolProp(dtoAttr, "CanModify", true);
    bool masking = CodeGenPolicy.GetBoolProp(dtoAttr, "Masking", false);
}
```

### 4.2 自定义验证规则

在 DtoEmpty.cshtml 或 EntityEmpty.cshtml 中实现 partial 方法：

```
partial void OnCustomValidate(EnumSceneFlags scene, List<ValidationResult> results)
{
    if (this.StartTime > this.EndTime)
        results.Add(new ValidationResult("开始时间不能晚于结束时间", new[] { nameof(StartTime) }));
}
```

### 4.3 自定义服务逻辑

在 Service 模板生成的 partial 方法中扩展业务：

```
partial void OnBeforeCreate(MerchantEntity entity)
{
    entity.CreatedTime = DateTime.Now;
    entity.CreatorId = User.CurrentId;
}
```

### 4.4 自定义搜索分组

通过 [GenerateCode] 特性配置搜索分组：

```
[GenerateCode(SearchGroups = new[] { "Name,Code", "CreateTime" })]
public class MerchantEntity { ... }
```

在模板中解析搜索分组：

```
@{
    var searchGroups = CodeGenPolicy.GetSearchGroups(Model);
}
```

---

## 5. 模板调试技巧

### 5.1 启用详细日志

在 xcodegen.config.json 中设置：

```
{
  "CustomSettings": {
    "EnableLogging": "true"
  }
}
```

或在命令行添加 --verbose 参数：

```
tkw-codegen generate --config xcodegen.config.json --verbose
```

### 5.2 预览模式

使用 --preview 参数预览生成结果而不写入文件：

```
tkw-codegen generate --config xcodegen.config.json --preview
```

### 5.3 单表测试

使用 --table 参数指定单表进行快速迭代测试：

```
tkw-codegen generate --table Merchant --config xcodegen.config.json
```

### 5.4 检查生成哈希

生成文件头部包含元数据哈希注释：

```
// @@[xCodeGen.Hash: abc123...]
```

可用于判断元数据是否变更，验证增量生成是否生效。

---

## 6. 模板发布与分发

### 6.1 打包为 NuGet 包

将模板打包为 NuGet 包，便于团队共享：

```
dotnet pack MyCustomTemplates.csproj -c Release
```

### 6.2 独立目录分发

将模板目录放入版本控制，团队成员克隆后配置 TemplatesPath 即可使用。

### 6.3 多模板包切换

支持多模板包切换，适应不同项目需求：

```
{
  "TemplatesPath": "Templates/ProjectA",  // 项目 A 模板
  ...
}
```

---

## 7. 常见场景示例

### 7.1 生成 Repository 层

创建 Repository.cshtml 模板：

```
public interface I@Model.ClassNameRepository
{
    Task<@Model.ClassName> GetByIdAsync(long id);
    Task<IEnumerable<@Model.ClassName>> GetAllAsync();
    Task<@Model.ClassName> CreateAsync(@Model.ClassName entity);
    Task UpdateAsync(@Model.ClassName entity);
    Task DeleteAsync(long id);
}
```

在 xcodegen.config.json 中添加映射：

```
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

```
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

```
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

- 检查 xcodegen.config.json 中的 TemplatesPath 配置是否正确
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
