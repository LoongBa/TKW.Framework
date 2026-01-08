# ExcelTool (TKWF.ExcelImporter)

概述
---

ExcelTool 提供了一组从 Excel/CSV 导入数据到 .NET 对象的实用方法。目标是：

- 保持对原始简单方法签名的兼容性；
- 提供增强功能：流式读取、批量属性设置、动态属性、逐行回调、失败明细收集与可配置错误策略（遇错停止或记录跳过）；
- 在大量数据场景下具有较好的性能（属性 setter 编译缓存、避免不必要的重复反射、可选的流式读取以降低内存）；
- 简洁的日志回调接口，方便集成现有日志框架（Serilog / Microsoft.Extensions.Logging 等）。

核心文件
---

- ExcelTools.cs
  - 导出的主要方法：
    - Task<IEnumerable<T>> ImportDataFromExcel<T>(string filename, StringDictionary columnMapping, string? unmappedJsonColumnName = null, int sheetIndex = 0)
      - 保持与旧代码的签名和行为兼容（默认遇错不停止）。
    - Task<ImportResult<T>> ImportDataFromExcelWithResult<T>(...)
      - 增强版本，返回 ExcelImportResult<T>，包含成功项和失败明细，并提供更多配置项（batchProperties、dynamicBatchProperties、batchOverridesExcel、onRecordCreated、log、stopOnFirstError）。
    - Task<IEnumerable<dynamic>> ImportDynamicObjectFromExcel(...)
      - 兼容旧的动态导入方法（返回 ExpandoObject 列表）。
  - 辅助类型：
    - ExcelImportResult<T>：Items、Failures、SuccessCount、FailedCount。
    - ExcelImportFailure：RowIndex、ErrorMessage、Exception、RowValues（原始列值）。
  - 主要设计考虑：
    - 使用 ExcelDataReader 进行流式读取（减少内存占用）。
    - 预构建属性 setter（Expression 编译）以减少反射开销。
    - 提供 ConvertToTarget 以处理常见类型转换（nullable、enum、Guid、DateTime、bool、数字等）。
    - 提供批量设置与动态设置（行号等计算值）。
    - 可选 stopOnFirstError 行为（true：遇到第一个失败立即抛出；false：记录失败，继续导入其它行）。

使用说明
---

1. 简单兼容用法（与旧实现相同）
   
   ```csharp
   var items = await ExcelTools.ImportDataFromExcel<MyDto>(filePath, columnMapping);
   ```

2. 获取详细导入结果（建议用于需要处理失败行或展示导入报告的场景）
   
   ```csharp
   var result = await ExcelTools.ImportDataFromExcelWithResult<MyDto>(
    filename,
    columnMapping,
    unmappedJsonColumnName: "OtherColumns",
    sheetIndex: 0,
    batchProperties: new Dictionary<string, object?> { ["Creator"] = "admin", ["CreateTime"] = DateTime.UtcNow },
    dynamicBatchProperties: new Dictionary<string, Func<int, object?>> { ["RowNumber"] = idx => idx + 1 },
    batchOverridesExcel: false,
    onRecordCreated: (rec, idx) => { /* 可用于建立衍生字段 */ },
    log: s => Console.WriteLine(s),
    stopOnFirstError: false // 如果需要在第一条错误时停止并抛出异常，设为 true
   );
   ```

Console.WriteLine($"成功 {result.SuccessCount}, 失败 {result.FailedCount}");
foreach (var fail in result.Failures)
{
    Console.WriteLine($"Row {fail.RowIndex} failed: {fail.ErrorMessage}");
    // fail.RowValues 包含原始列值
}

```
3. 使用动态导入（返回 ExpandoObject）
```csharp
var dynamicList = await ExcelTools.ImportDynamicObjectFromExcel(filePath, columnMapping, "OtherColumns");
foreach (dynamic d in dynamicList)
{
    Console.WriteLine(d.Name);
    Console.WriteLine(d.OtherColumns); // JsonObject
}
```

配置项解释
---

- batchProperties: IDictionary<string, object?>，用于给每个实例统一设置的固定属性（例如 Creator、CreateTime）。
- dynamicBatchProperties: IDictionary<string, Func<int, object?>>，按行动态计算并设置属性（例如 RowNumber）。
- batchOverridesExcel: bool，若为 true，则在映射 Excel 列后再应用 batchProperties，从而覆盖 Excel 中同名列的值。
- onRecordCreated: Action<T, int>，为每条记录创建后的回调，可用于复杂/跨列计算（例如计算 DisplayName）。
- log: Action<string>，简单日志回调，便于将日志发布到现有日志系统。
- stopOnFirstError: bool，若 true 则在导入过程中一旦遇到错误（转换/赋值/批量设置/回调异常）立即停止并抛出异常；若 false 则记录失败并继续处理后续行（默认以兼容旧行为）。

设计要点与权衡
---

1. 性能
   
   - 采用 Expression 编译 setter，避免大量 PropertyInfo.SetValue 引发的性能开销，适合大行数场景。
   - 使用 ExcelDataReader 的流式读取（不必一次性载入到 DataSet）以降低内存峰值。但兼容性方法 ImportDynamicObjectFromExcel 保持了原始 AsDataSet 行为以避免破坏旧逻辑。

2. 容错与错误策略
   
   - 导入过程中会尝试将可疑错误记录到 ImportFailure 中，不会默认中断整个导入（除非 stopOnFirstError = true）。
   - 对于无法转换的列会回退给类型的默认值（避免在大量数据导入时因单行错误中断整个导入），并把失败信息记录在 ImportFailure 中。

3. 易用性
   
   - 提供了简单的 log 回调接口，避免直接引入 ILogger 强依赖。若需要可很容易把 log 参数与 ILogger 适配。
   - 保留原始方法的签名与行为（兼容性优先）。

4. 类型转换
   
   - ConvertToTarget 支持常用类型转换，但无法覆盖所有自定义场景（例如自定义复合解析规则）。如需扩展，请在调用方提供已转换的 batchProperties 或在 onRecordCreated 中做更复杂的赋值逻辑。

是否有必要保留原始实现中的某些部分？
---

对照你给出的原始代码，这里做了调整并保留了关键兼容点：

- ImportDataFromExcel 的旧签名被保留并调用增强版本以返回与历史行为一致的结果；
- ImportDynamicObjectFromExcel 保持对外签名兼容并继续使用 AsDataSet（与原逻辑等价）。
- 原实现中针对 null/默认值的专门分支（SetDefaultValue / SetPropertyValue）被合并并用 ConvertToTarget + GetDefaultOrNull 等方法统一处理，减少重复代码并提升可维护性。若你依赖某些非常特殊的旧逻辑（例如引用类型默认设为空串而非 null），可以在 ConvertToTarget 或调用方（onRecordCreated）中调整。

后续可扩展的方向
---

- 日志适配：把 log: Action<string> 换成 Microsoft.Extensions.Logging.ILogger 的适配器。
- 导入策略：支持按行验证策略（规则集合）并返回验证报告；支持将错误行导出到错误 Excel。
- 并行处理：当单行处理（回调）耗时很大时，可在读取阶段聚合原始行数据再通过并行任务处理，但要注意 ExcelDataReader 读取必须串行。
- 映射规则扩展：支持复杂映射（表达式、AutoMapper 集成、映射配置文件/注解驱动）。
- 单元测试覆盖：为 ConvertToTarget、批量属性应用、stopOnFirstError 等行为添加详尽单元测试。
- 增加导入结果的行级元数据，例如处理耗时、行来源（sheet/列索引）等。
- 把 ExcelImportResult 扩展为包含处理耗时/状态枚举/导入统计报告（如耗时分布）

常见注意事项
---

- ExcelDataReader 返回的数字通常为 double，当映射到 int/decimal 时 Convert.ChangeType 会执行合适的转换；但如果 Excel 中存在格式化的字符串，可能需要先清洗或在 ConvertToTarget 中增加更多解析逻辑。
- 对于 Csv 文件，ExcelDataReader 的 CsvReader 解析依赖流的编码，确保正确指定或使用正确编码读取。
- 当对性能极端敏感时，测试不同的转换/赋值实现（例如直接 IL.Emit / Hand-written fast paths）可能带来额外收益。
- 如果需要完整保留 Excel 中的单元格格式/公式/样式等信息，ExcelDataReader 不是最适合的工具；考虑使用 EPPlus / NPOI 等库。

```

```