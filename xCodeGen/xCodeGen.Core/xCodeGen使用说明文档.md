# xCodeGen 使用说明文档 (Ver 1.2)

**版本**：Ver 1.2 

**更新日期**：2026-02-20 

**状态**：更新端到端示例与一步步配置指南，同步最新代码（RazorLight 引擎、--watch 优化）

本指南指导开发人员利用 xCodeGen 从 Model 层自动化生成全栈配套代码（如 DTO、Repository）。

与 TKWF.Domain 集成时，确保生成代码支持领域自治（e.g., DomainUser 注入）。

## 1. 快速入门

### 1.1 环境要求

- .NET 10+ SDK（兼容 .NET 6.0+，但推荐 10+ 以支持 required/init）。
- 引用 xCodeGen.Abstractions 类库（NuGet 或项目引用）。

### 1.2 安装 CLI 工具

进入 xCodeGen.Cli 目录：dotnet build。

将 bin/Debug/net10.0/xcodegen.exe 添加到 PATH。 

**最新**：支持 dotnet tool install --local（manifest 创建）。

## 2. 配置说明 (xcodegen.config.json)

系统一切行为受根目录 config.json 驱动。示例如设计文档中所示。 

**最新**：新增 Debug 配置，支持 _Debug/ 输出。

```json
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

## 3. 工作流程

### 第一步：标记您的 Model

如设计文档示例：添加 [GenerateArtifact] + XML 注释。

### 第二步：运行生成命令

- **常规生成**：xcodegen gen（自动解析元数据生成）。
- **开发模式 (Watch)**：xcodegen gen --watch（监听 Models/ 变更，实时更新）。
- **最新**：添加 --dry-run（模拟生成，不写文件）。

### 第三步：编写业务逻辑

查看 Generated/ 目录：*.generated.cs（基础）、*.cs（扩展）。在 *.cs 中添加 TKWF.Domain 逻辑。

## 4. 最佳实践

1. **单一事实来源**：Model 作为数据结构的唯一权威。
2. **模板复用**：使用 @Model.Summary 渲染 XML，确保 IntelliSense。
3. **构建集成**：PreBuildEvent 中加 xcodegen gen，确保最新。
4. **TKWF.Domain 适配**：模板中注入 DomainUser<TUserInfo> 支持。
5. **检验与完善**：遵循设计文档端到端步骤，逐步验证增量/不覆盖。

**提示**：新增支持 FreeSql/GraphQL 只需加模板 + 映射。详见《设计文档》端到端示例与配置步骤。
