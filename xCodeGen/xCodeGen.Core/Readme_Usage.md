# xCodeGen 使用说明文档 (Ver 1.2)

本指南旨在指导开发人员如何利用 xCodeGen 快速从 Model 层自动化生成全栈配套代码。

## 1. 快速入门

### 1.1 环境要求

- .NET 6.0 或更高版本 SDK 。
  
  +1

- 在项目中引用 `xCodeGen.Abstractions` 类库。

### 1.2 安装 CLI 工具

通过终端进入 `xCodeGen.Cli` 目录并运行构建。完成后，您可以将生成的 `xcodegen` 可执行文件加入系统环境变量。

## 2. 配置说明 (`xCodeGen.config.json`)

系统的一切行为受项目根目录下的配置文件驱动 。

+1

JSON

```
{
  "OutputRoot": "src/MyProject/Generated",   // 生成产物的根目录
  "TemplatesPath": "Templates",               // 存放 Razor 模板的目录
  "TemplateMappings": {                       // 产物类型与逻辑模板的映射
    "Dto": "Templates/Dto.cshtml",
    "Repository": "Templates/Repository.cshtml"
  },
  "SkeletonMappings": {                       // 产物类型与业务扩展骨架模板的映射
    "Repository": "Templates/Skeletons/RepoSkeleton.cshtml"
  },
  "OutputDirectories": {                      // 各类产物的存放子目录
    "Dto": "Dtos",
    "Repository": "Repositories"
  },
  "NamingRules": [                            // 命名规范定义
    { "ArtifactType": "Dto", "Pattern": "{Name}Dto" },
    { "ArtifactType": "Repository", "Pattern": "I{Name}Repository" }
  ]
}
```

## 3. 开发工作流

### 第一步：标记您的 Model

在业务模型类上添加特性。建议同时编写标准的 XML 注释，这些注释将被自动继承到 DTO 和 Repository 中。

C#

```
/// <summary>
/// 核心用户信息模型
/// </summary>
[GenerateArtifact(ArtifactType = "Dto")]
[GenerateArtifact(ArtifactType = "Repository")]
public class User {
    public int Id { get; set; }
    /// <summary>
    /// 用户登录名
    /// </summary>
    public string UserName { get; set; }
}
```

### 第二步：运行生成命令

在项目根目录下打开终端：

- **常规生成**：
  
  Bash
  
  ```
  xcodegen gen
  ```
  
  系统将自动解析元数据并生成代码 。
  
  +1

- **开发模式 (Watch)**：
  
  Bash
  
  ```
  xcodegen gen --watch
  ```
  
  在该模式下，系统会监听源码变更。您在 IDE 中修改 Model 字段并保存后，配套的 DTO 会实时更新。

### 第三步：编写业务逻辑

- **生成的代码**：查看 `Generated/` 目录下的 `*.generated.cs` 文件，确认字段和基础接口已生成。

- **手动扩展**：在对应的 `*.cs` 文件（非 generated）中编写自定义方法。由于类被标记为 `partial`，您可以自由扩展逻辑而不用担心下次生成时被覆盖。

## 4. 最佳实践

1. **单一事实来源**：始终将 Model 作为数据结构的唯一权威来源 。
   
   +1

2. **模板复用**：在模板中使用 `@Model.Summary` 渲染 XML 文档，以保证生成的 API 具备良好的 IntelliSense 提示。

3. **构建集成**：将 `xcodegen gen` 配置为编译前置任务，确保代码始终是最新的。

---

**提示**：如需添加对 FreeSql 或 GraphQL 的支持，只需新增对应的 Razor 模板并在配置文件中增加映射即可。
