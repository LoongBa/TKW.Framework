# TKWF.Tools.Tags 标签提取引擎技术规范

**状态**: 核心基础设施 (Core Infrastructure) | **版本**: V4.1 | **框架**: .NET 10

**核心约束**: 零分配 (Zero-Allocation), Native AOT 兼容, 异步友好, 逻辑解耦

---

## 一、 需求分析 (Demand Analysis)

在领域驱动设计 (DDD) 的应用层中，常需对非结构化文本进行特征提取（打标）。

- **性能瓶颈**：传统 `foreach` + `Contains` 匹配在规则过千时性能呈线性下降，且产生大量临时字符串。

- **语义缺失**：无法区分词法边界（如“苹果”与“青苹果”）。

- **冲突处理**：缺乏优先级（Priority）和互斥（Exclusion）逻辑，导致标签结果冗余或矛盾。

- **配置耦合**：规则硬编码在业务逻辑中，无法通过配置中心实现无缝热加载。

## 二、 设计原理 (Design Principles)

本引擎采用 **“数据与处理解耦的流水线架构 (Pipeline Architecture)”**。

### 1. 结构分层

- **输入级 (`ITokenizer`)**：分词坐标生成。采用流式回调，仅传递 `TokenText`（基于 `int` 偏移量的坐标），彻底避免字符串截取分配。

- **处理级 (`ITagMatcher`)**：策略驱动匹配。内置支持 `TokenExact`（精确分词匹配）、`Contains`、`Regex` 等。

- **清洗级 (`ITagPipelinePostProcessor`)**：结果集后处理。负责根据 `Priority` 在 `ExclusionGroup`（互斥组）内进行择优过滤。

### 2. 性能基石

- **零分配坐标系**：
  
  ```csharp
  public readonly struct TokenText(int startIndex, int length) // 仅占 8 字节
  ```

- **无缝内存视图**：全量匹配逻辑基于 `ReadOnlySpan<char>` 执行，不产生 `Substring` 调用。

- **强类型配置**：深度集成 `DomainOptions.TagRules`，利用 .NET 10 的 `FrozenDictionary`（若规则固定）实现极速路由。

---

## 三、 使用说明 (Usage Guide)

### 3.1 宿主集成 (Hosting)

利用 `DomainAppBuilder` 扩展方法，引擎会自动关联 `DomainOptions` 中的 `TagRules` 配置。

```csharp
// Program.cs 或模块入口
builder.UseTagService(); // 默认从 options.TagRules 加载规则

// 或者手动传入外部规则
builder.UseTagService(customRules);
```

### 3.2 业务调用 (Business Logic)

注入门面服务 `TagService`，获取结构化的命中结果。

```csharp
public class AnalysisService(TagService tagService)
{
    public void Process(string input)
    {
        // 极简 API，内部自动调度 Pipeline
        var hits = tagService.GetTags(input); 

        foreach (var hit in hits)
        {
            // hit 包含：维度、标签名、命中位置、原词、优先级等
            _logger.LogInformation("命中标签: {Tag} (位置: {Start})", hit.TagName, hit.StartIndex);
        }
    }
}
```

---

## 四、 核心组件清单 (Component List)

| **组件**                          | **职责**           | **默认实现**                            |
| ------------------------------- | ---------------- | ----------------------------------- |
| **`TagService`**                | 业务单例门面，持有并管理规则集。 | 内置                                  |
| **`ITokenizer`**                | 执行文本拆分，生成坐标流。    | `DefaultTokenizer` (标点/空格)          |
| **`ITagMatcher`**               | 具体的匹配算法实现。       | `TokenExactMatcher`, `RegexMatcher` |
| **`ITagPipelinePostProcessor`** | 处理互斥逻辑和排序。       | `ExclusionGroupProcessor`           |

---

## 五、 扩展与维护规范 (Extension & Maintenance)

### 1. 扩展新算法

如果后续有更复杂的算法需求（如高并发下的 AC 自动机实现），只需在此架构下增加一个新的 `Matcher` 实现即可。

若需支持 **语义相似度打标** 或 **AC 自动机**：

1. 实现 `ITagMatcher` 接口。

2. 通过 `services.TryAddEnumerable` 注册。流水线将自动根据 `MatchMode` 路由任务。

### 2. 接入第三方 NLP

若需接入 **Jieba** 或 **HanLP**：

1. 实现 `ITokenizer`。

2. 在 `UseTagService()` 之前注入 DI，引擎将自动替换默认的简单分词器。

---

## 六、 AI Agent 协作契约 (AI Agent Prompting Guide)

> [!CAUTION]
> 
> **绝对指令：在进行任何代码生成或重构时，严禁触碰以下红线。**

1. **禁分配 (No Allocations)**：禁止在 `Matcher` 的核心循环中使用 `string.Substring`、`string.Split` 或 `Linq.Select`。

2. **禁反射 (No Reflection)**：严禁使用 `Reflection.Emit`。所有组件必须保证 Native AOT 编译通过。

3. **坐标纯粹性**：`TokenText` 必须保持为 `readonly struct`，且**不得**缓存任何字符串引用（`string` 或 `ReadOnlyMemory`）。

4. **接口封闭**：所有的命中结果输出必须为 `IReadOnlyList<TagHit>`，禁止下游业务逻辑修改原始命中数据。

---

### 文档信息

- **归档日期**: 2026-05-14

- **维护团队**: play / TKW Framework Team

- **审批状态**: 定稿 (V4.1)
