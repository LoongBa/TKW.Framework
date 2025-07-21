以下是基于 `xCodeGen` 命名的中性化设计文档及核心代码文件更新，彻底剥离业务概念，聚焦元数据驱动的通用代码生成能力：

# xCodeGen Ver1.0 实现说明

xCodeGen V1.0 是一个元数据驱动的通用代码生成框架，旨在通过解析代码结构并结合模板生成各种类型的代码产物。该版本包含以下核心功能：

1. **元数据提取**：基于 Roslyn 编译器平台，从目标项目中提取类、方法、参数及其特性等元数据。
2. **模板系统**：集成 RazorLight 模板引擎，支持通过 Razor 模板定义代码生成规则。
3. **工具类**：提供命名转换、类型处理和验证规则生成等通用工具，可在模板中调用。
4. **配置系统**：通过 JSON 配置文件管理模板映射、输出目录和工具类等设置。
5. **调试系统**：详细记录生成过程中的元数据和操作日志，便于问题排查。
6. **命令行接口**：提供简单的命令行工具，方便集成到构建流程中。
   
   ### 使用流程
7. 在目标项目中添加 `[GenerateArtifact]` 特性标记需要生成代码的类或方法
8. 配置 `xcodegen.config.json` 文件，指定模板路径、输出目录等
9. 创建自定义 Razor 模板或使用现有模板
10. 运行命令行工具执行代码生成
    该实现保持了高度的灵活性和扩展性，完全与业务逻辑解耦，可通过添加新模板支持各种类型的代码生成需求。

# xCodeGen 设计文档（Ver1.0）

## 一、项目定位

`xCodeGen` 是一个**元数据驱动的通用代码生成框架**，通过解析目标项目中的类、方法、参数等元数据，结合可定制模板生成代码文件（称为“Artifact”）。框架本身不包含任何业务逻辑（如DTO、验证器等），仅通过“产物类型（ArtifactType）”关联模板，实现“提取-映射-生成”的中性流程。

## 二、核心架构

```
xCodeGen/
├─ xCodeGen.Abstractions/ // 抽象定义（用户项目引用）
│ ├─ Attributes/
│ │ ├─ GenerateArtifactAttribute.cs // 标记需要生成产物的类/方法
│ │ └─ ArtifactMetadataAttribute.cs // 补充产物元数据（如描述、版本）
│ └─ Metadata/ // 元数据模型（中性结构）
│ ├─ ClassMetadata.cs // 类元数据（命名空间、名称、方法等）
│ ├─ MethodMetadata.cs // 方法元数据（名称、参数、特性等）
│ └─ ParameterMetadata.cs // 参数元数据（名称、类型、特性等）
├─ xCodeGen.Core/ // 核心引擎
│ ├─ Engine/
│ │ ├─ GeneratorEngine.cs // 核心引擎：协调提取、模板执行、输出
│ │ └─ TemplateExecutor.cs // 模板执行器：加载模板、传递数据
│ ├─ Extraction/ // 元数据提取（基于Roslyn）
│ │ └─ RoslynExtractor.cs // 解析代码生成元数据
│ └─ Debugging/
│ └─ DebugLogger.cs // 调试信息输出
├─ xCodeGen.Utilities/ // 通用工具（模板可调用）
│ ├─ Naming/
│ │ └─ NamingUtility.cs // 命名转换（PascalCase、CamelCase等）
│ ├─ TypeHandling/
│ │ └─ TypeUtility.cs // 类型简化、可空性处理等
│ └─ Validation/
│ └─ ValidationUtility.cs // 验证规则生成辅助（中性方法）
├─ xCodeGen.Templates/ // 模板管理
│ └─ RazorTemplateProvider.cs // Razor模板加载与渲染
└─ xCodeGen.Configuration/ // 配置系统
 └─ GeneratorConfig.cs // 生成器配置（模板映射、输出目录等）
```

## 三、核心概念

1. **Artifact（产物）**：生成器输出的代码文件（如类、接口等），完全由模板定义，框架不限制类型。 
2. **ArtifactType（产物类型）**：字符串标识（如“Dto”“Validator”“Query”），用于关联模板（框架不解析具体含义）。 
3. **元数据（Metadata）**：从目标项目中提取的类、方法、参数信息（名称、类型、特性等），作为模板输入。 
4. **模板（Template）**：定义代码生成规则的文件（如Razor模板），接收元数据和工具类，输出代码。 

## 四、核心流程

5. **标记目标**：用户在目标类/方法上添加 `[GenerateArtifact(ArtifactType = "Dto")]` 特性，指定需要生成的产物类型。 

6. **提取元数据**：`RoslynExtractor` 解析目标项目，提取标记了特性的类、方法、参数元数据。 

7. **模板匹配**：`GeneratorEngine` 根据 `ArtifactType` 从配置中查找对应的模板路径。 

8. **代码生成**：`TemplateExecutor` 加载模板，传递元数据和工具类，渲染并输出代码到指定目录。 

9. **调试输出**：`DebugLogger` 在 `_Debug` 目录输出提取的元数据和生成过程日志。 

## 五、配置示例（xcodegen.config.json）

```json
{
  "TargetProject": "src/MyProject",       // 目标项目路径
  "OutputRoot": "src/MyProject/Generated", // 代码输出根目录
  "Debug": {
    "Enabled": true,
    "Directory": "_Debug"
  },
  "TemplateMappings": {                   // ArtifactType → 模板路径映射
    "Dto": "Templates/Artifact/Dto.cshtml",
    "Validator": "Templates/Artifact/Validator.cshtml"
  },
  "OutputDirectories": {                  // ArtifactType → 输出子目录
    "Dto": "Dtos",
    "Validator": "Validators"
  },
  "EnabledUtilities": [                   // 允许模板调用的工具类
    "xCodeGen.Utilities.Naming.NamingUtility",
    "xCodeGen.Utilities.TypeHandling.TypeUtility"
  ]
}
```

## 六、核心代码文件实现

### 1. 抽象定义（xCodeGen.Abstractions）

##### 1) `GenerateArtifactAttribute.cs`（标记需要生成产物的元素）

```csharp
using System;

namespace xCodeGen.Abstractions.Attributes;

/// <summary>
/// 标记需要生成代码产物的类或方法
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class GenerateArtifactAttribute : Attribute
{
    /// <summary>
    /// 产物类型（用于匹配模板）
    /// </summary>
    public string ArtifactType { get; set; }

    /// <summary>
    /// 模板名称（从配置中查找对应模板）
    /// </summary>
    public string TemplateName { get; set; } = "Default";

    /// <summary>
    /// 是否覆盖已有文件
    /// </summary>
    public bool Overwrite { get; set; } = false;
}
```

##### 2) `ParameterMetadata.cs`（参数元数据模型）

```csharp
using System.Collections.Generic;
using xCodeGen.Abstractions.Attributes;

namespace xCodeGen.Abstractions.Metadata;

/// <summary>
/// 方法参数的元数据
/// </summary>
public class ParameterMetadata
{
    /// <summary>
    /// 参数名称（原名称）
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 参数类型名称（短名称，如 int、string）
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// 参数类型全限定名（如 System.Int32）
    /// </summary>
    public string TypeFullName { get; set; }

    /// <summary>
    /// 是否为可空类型（如 int?、string?）
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// 是否为集合类型（如 IEnumerable<T>、List<T>）
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// 集合元素类型（如 List<User> → User）
    /// </summary>
    public string CollectionItemType { get; set; }

    /// <summary>
    /// 参数上的特性元数据
    /// </summary>
    public List<AttributeMetadata> Attributes { get; set; } = new();
}

/// <summary>
/// 特性的元数据（提取特性名称和参数）
/// </summary>
public class AttributeMetadata
{
    public string TypeFullName { get; set; } // 特性类型全限定名
    public Dictionary<string, object> Properties { get; set; } = new(); // 特性参数
}
```

### 2. 核心引擎（xCodeGen.Core）

##### 1) `GeneratorEngine.cs`（核心引擎入口）

```csharp
using System;
using System.Linq;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Configuration;
using xCodeGen.Core.Debugging;
using xCodeGen.Core.Extraction;
using xCodeGen.Core.Templates;

namespace xCodeGen.Core.Engine;

public class GeneratorEngine
{
    private readonly GeneratorConfig _config;
    private readonly IMetadataExtractor _extractor;
    private readonly TemplateExecutor _templateExecutor;
    private readonly DebugLogger _debugLogger;

    public GeneratorEngine(GeneratorConfig config)
    {
        _config = config;
        _extractor = new RoslynExtractor(config.TargetProject); // 基于Roslyn的元数据提取器
        _templateExecutor = new TemplateExecutor(config); // 模板执行器
        _debugLogger = new DebugLogger(config.Debug); // 调试日志
    }

    /// <summary>
    /// 启动代码生成流程
    /// </summary>
    public void Generate()
    {
        try
        {
            // 1. 输出启动调试信息
            _debugLogger.LogStartupInfo(_config);

            // 2. 提取元数据（所有标记了[GenerateArtifact]的类和方法）
            var metadata = _extractor.Extract();
            _debugLogger.LogExtractedMetadata(metadata); // 输出元数据调试信息

            // 3. 遍历元数据，生成对应产物
            foreach (var classMeta in metadata.Classes)
            {
                foreach (var methodMeta in classMeta.Methods)
                {
                    // 跳过未标记[GenerateArtifact]的方法
                    var artifactAttr = methodMeta.GenerateArtifactAttribute;
                    if (artifactAttr == null) continue;

                    // 生成产物
                    GenerateArtifact(classMeta, methodMeta, artifactAttr);
                }
            }
        }
        catch (Exception ex)
        {
            _debugLogger.LogError("生成过程失败", ex);
            throw;
        }
    }

    /// <summary>
    /// 生成单个产物
    /// </summary>
    private void GenerateArtifact(ClassMetadata classMeta, MethodMetadata methodMeta, 
        GenerateArtifactAttribute artifactAttr)
    {
        // 查找模板路径
        var templatePath = _config.TemplateMappings[artifactAttr.ArtifactType][artifactAttr.TemplateName];

        // 准备模板输入数据（元数据 + 工具类）
        var templateData = new TemplateInput
        {
            Class = classMeta,
            Method = methodMeta,
            ArtifactType = artifactAttr.ArtifactType,
            Utilities = _config.GetEnabledUtilities() // 加载配置中启用的工具类
        };

        // 执行模板并生成文件
        var outputPath = _templateExecutor.Execute(templatePath, templateData);

        // 输出生成日志
        _debugLogger.LogGeneratedFile(artifactAttr.ArtifactType, outputPath);
    }
}
```

##### 2) `RoslynExtractor.cs`（基于Roslyn的元数据提取器）

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Metadata;

namespace xCodeGen.Core.Extraction;

public interface IMetadataExtractor
{
    ExtractedMetadata Extract();
}

public class RoslynExtractor : IMetadataExtractor
{
    private readonly string _targetProjectPath;

    public RoslynExtractor(string targetProjectPath)
    {
        _targetProjectPath = targetProjectPath;
    }

    /// <summary>
    /// 提取元数据（简化版：仅展示核心逻辑）
    /// </summary>
    public ExtractedMetadata Extract()
    {
        // 1. 加载目标项目的语法树（实际实现需处理项目编译）
        var syntaxTrees = LoadSyntaxTrees();

        // 2. 解析所有类
        var classes = new List<ClassMetadata>();
        foreach (var tree in syntaxTrees)
        {
            var root = tree.GetRoot();
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                // 提取类元数据（简化逻辑）
                var classMeta = ExtractClassMetadata(classDecl);
                if (classMeta != null)
                {
                    classes.Add(classMeta);
                }
            }
        }

        return new ExtractedMetadata { Classes = classes };
    }

    /// <summary>
    /// 提取类元数据（仅处理标记了[GenerateArtifact]的类）
    /// </summary>
    private ClassMetadata? ExtractClassMetadata(ClassDeclarationSyntax classDecl)
    {
        // 检查类是否标记了[GenerateArtifact]
        var hasClassAttribute = classDecl.AttributeLists
            .Any(a => a.Attributes.Any(attr => 
                attr.Name.ToString() == nameof(GenerateArtifactAttribute)));
        if (!hasClassAttribute) return null;

        // 提取类基础信息
        var classMeta = new ClassMetadata
        {
            Namespace = classDecl.Parent is NamespaceDeclarationSyntax ns 
                ? ns.Name.ToString() 
                : string.Empty,
            Name = classDecl.Identifier.Text,
            Methods = new List<MethodMetadata>()
        };

        // 提取方法元数据
        var methodDeclarations = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var methodDecl in methodDeclarations)
        {
            var methodMeta = ExtractMethodMetadata(methodDecl);
            if (methodMeta != null)
            {
                classMeta.Methods.Add(methodMeta);
            }
        }

        return classMeta;
    }

    /// <summary>
    /// 提取方法元数据（仅处理标记了[GenerateArtifact]的方法）
    /// </summary>
    private MethodMetadata? ExtractMethodMetadata(MethodDeclarationSyntax methodDecl)
    {
        // 检查方法是否标记了[GenerateArtifact]
        var artifactAttr = ExtractGenerateArtifactAttribute(methodDecl);
        if (artifactAttr == null) return null;

        // 提取方法基础信息
        return new MethodMetadata
        {
            Name = methodDecl.Identifier.Text,
            ReturnType = methodDecl.ReturnType.ToString(),
            Parameters = methodDecl.ParameterList.Parameters
                .Select(ExtractParameterMetadata)
                .ToList(),
            GenerateArtifactAttribute = artifactAttr
        };
    }

    /// <summary>
    /// 提取参数元数据
    /// </summary>
    private ParameterMetadata ExtractParameterMetadata(ParameterSyntax paramSyntax)
    {
        // 简化实现：提取参数名称、类型、可空性等
        return new ParameterMetadata
        {
            Name = paramSyntax.Identifier.Text,
            TypeName = paramSyntax.Type?.ToString() ?? "unknown",
            IsNullable = paramSyntax.Type?.ToString().EndsWith("?") ?? false,
            // 省略：TypeFullName、IsCollection等复杂属性的提取（需基于语义分析）
            Attributes = ExtractParameterAttributes(paramSyntax)
        };
    }

    // 省略：ExtractGenerateArtifactAttribute、ExtractParameterAttributes等辅助方法
}
```

### 3. 工具类（xCodeGen.Utilities）

##### 1) `NamingUtility.cs`（命名转换工具）

```csharp
namespace xCodeGen.Utilities.Naming;

/// <summary>
/// 命名转换工具（供模板调用）
/// </summary>
public class NamingUtility
{
    /// <summary>
    /// 转换为帕斯卡命名法（首字母大写）
    /// </summary>
    public string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// 转换为骆驼命名法（首字母小写）
    /// </summary>
    public string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// 生成产物名称（如：UserService_GetUser → UserServiceGetUserDto）
    /// </summary>
    public string GenerateArtifactName(string className, string methodName, string artifactType)
    {
        return $"{className}{methodName}{artifactType}";
    }
}
```

### 4. 模板示例（Dto.cshtml）

```cshtml
@model xCodeGen.Core.Templates.TemplateInput
@using xCodeGen.Utilities.Naming

<!-- 自动生成的产物（类型：@Model.ArtifactType） -->
namespace @Model.Class.Namespace.Generated.@Model.ArtifactType;

public partial class @Model.Utilities.Naming.GenerateArtifactName(Model.Class.Name, Model.Method.Name, Model.ArtifactType)
{
    @foreach (var param in Model.Method.Parameters)
    {
        // 生成属性（使用工具类转换命名）
        public @param.TypeName @Model.Utilities.Naming.ToPascalCase(param.Name) { get; set; }
    }
}
```

## 总结

`xCodeGen` 通过中性化设计实现了“元数据提取→模板映射→代码生成”的通用流程，核心优势： 

1. **业务无关**：通过 `ArtifactType` 和模板映射剥离业务概念，支持任意类型代码生成。 
2. **灵活扩展**：新增产物类型只需添加模板和配置，无需修改核心引擎。 
3. **工具解耦**：工具类按需启用，模板自主调用，支持扩展验证、数据结构解析等工具。 
   后续可扩展模板引擎支持（如Scriban）、增量生成优化、跨语言模板等功能。
