using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Abstractions.Templates;
using xCodeGen.Core.IO;
using xCodeGen.Core.Models;

namespace xCodeGen.Core;

/// <summary>
/// 代码生成核心引擎，协调元数据提取、转换、模板渲染和文件输出的全流程
/// </summary>
public class Engine
{
    private readonly IEnumerable<IMetaDataExtractor> _extractors;
    private readonly IMetadataConverter _metadataConverter;
    private readonly ITemplateEngine _templateEngine;
    private readonly IFileWriter _fileWriter;
    private readonly IncrementalChecker _incrementalChecker;

    /// <summary>
    /// 初始化引擎实例
    /// </summary>
    public Engine(
        IEnumerable<IMetaDataExtractor> extractors,
        IMetadataConverter metadataConverter,
        ITemplateEngine templateEngine,
        IFileWriter fileWriter,
        IncrementalChecker incrementalChecker)
    {
        _extractors = extractors ?? throw new ArgumentNullException(nameof(extractors));
        _metadataConverter = metadataConverter ?? throw new ArgumentNullException(nameof(metadataConverter));
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
        _incrementalChecker = incrementalChecker ?? throw new ArgumentNullException(nameof(incrementalChecker));
    }

    /// <summary>
    /// 执行代码生成流程
    /// </summary>
    /// <param name="options">生成配置选项</param>
    /// <returns>生成结果信息</returns>
    public async Task<GenerateResult> GenerateAsync(GenerateOptions options)
    {
        var result = new GenerateResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. 验证输入参数
            ValidateOptions(options, result);
            if (!result.Success)
                return result;

            // 2. 加载并验证启用的提取器
            var (enabledExtractors, invalidSources) = GetEnabledExtractors(options.MetadataSources);
            if (invalidSources.Count != 0)
            {
                result.AddError($"无效的元数据来源: {string.Join(", ", invalidSources)}");
                return result;
            }

            if (enabledExtractors.Count != 0)
            {
                result.AddError("未找到任何启用的元数据提取器");
                return result;
            }

            // 3. 加载模板
            try
            {
                _templateEngine.LoadTemplates(options.TemplatePath);
            }
            catch (Exception ex)
            {
                result.AddError($"模板加载失败: {ex.Message}");
                return result;
            }

            // 4. 从所有启用的提取器收集元数据
            var allRawMetadata = new List<RawMetadata>();
            foreach (var extractor in enabledExtractors)
            {
                // 获取提取器对应的字符串配置
                options.ExtractorConfigs.TryGetValue(extractor.SourceType.ToString(), out var configString);

                var extractorOptions = new ExtractorOptions
                {
                    ProjectPath = options.ProjectPath,
                    OutputPath = options.MetaOutputPath,
                    // 关键修复：将字符串配置转换为字典
                    ExtractorConfig = ParseConfig(configString),
                    EnableDetailedLogging = options.EnableDetailedLogging,
                    TimeoutMs = options.ExtractorTimeoutMs
                };

                try
                {
                    var extracted = await extractor.ExtractAsync(extractorOptions)
                        .ConfigureAwait(false);
                    var metaDataList = extracted.ToList();
                    allRawMetadata.AddRange(metaDataList);
                    result.AddExtracted(extractor.SourceType.ToString(), metaDataList.Count);
                }
                catch (Exception ex)
                {
                    result.AddError($"提取器 {extractor.SourceType} 执行失败: {ex.Message}");
                }
            }

            if (allRawMetadata.Count == 0 && result.Errors.Count == 0)
            {
                result.AddWarning("未提取到任何元数据");
                return result;
            }

            // 5. 转换原始元数据为抽象元数据
            var abstractMetadata = allRawMetadata
                .Select(meta => _metadataConverter.Convert(meta))
                .Where(meta => meta != null)
                .ToList();

            // 6. 增量生成校验
            var needGenerate = abstractMetadata
                .Where(meta => _incrementalChecker.NeedRegenerate(meta, options.OutputPath))
                .ToList();

            result.SkippedCount = abstractMetadata.Count - needGenerate.Count;

            // 7. 渲染模板并输出代码
            foreach (var metadata in needGenerate)
            {
                try
                {
                    var templateName = GetTemplateName(metadata);
                    var code = await _templateEngine.RenderAsync(metadata, templateName)
                        .ConfigureAwait(false);

                    var outputPath = _fileWriter.ResolveOutputPath(
                        options.OutputPath,
                        metadata.ClassName,
                        options.FileNameFormat);

                    _fileWriter.Write(code, outputPath, options.Overwrite);
                    result.AddGenerated(metadata.ClassName, outputPath);
                }
                catch (Exception ex)
                {
                    result.AddError($"生成 {metadata.ClassName} 失败: {ex.Message}");
                }
            }

            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.AddError($"引擎执行失败: {ex.Message}");
            result.Success = false;
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 将字符串配置转换为字典（支持JSON格式和简单键值对）
    /// </summary>
    private static Dictionary<string, object> ParseConfig(string configString)
    {
        // 使用不区分大小写的字典比较器
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(configString))
            return result;

        try
        {
            // 尝试解析JSON格式（使用System.Text.Json）
            var jsonConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(configString, DefaultJsonSerializerOptions);
            return jsonConfig ?? result;
        }
        catch (JsonException)
        {
            // JSON解析失败时尝试简单键值对格式（key1=value1;key2=value2）
            var keyValuePairs = configString.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in keyValuePairs)
            {
                var parts = pair.Split(['='], 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = ParseValue(parts[1].Trim());
                    result[key] = value;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 将字符串值转换为适当的类型（bool/int/double/string）
    /// </summary>
    private static object ParseValue(string value)
    {
        if (bool.TryParse(value, out var boolValue))
            return boolValue;

        if (int.TryParse(value, out var intValue))
            return intValue;

        if (double.TryParse(value, out var doubleValue))
            return doubleValue;

        return value;
    }

    /// <summary>
    /// 验证生成选项的有效性
    /// </summary>
    private static void ValidateOptions(GenerateOptions options, GenerateResult result)
    {
        if (string.IsNullOrEmpty(options.ProjectPath) && !options.AllowEmptyProjectPath)
        {
            result.AddError("项目路径不能为空");
        }

        if (string.IsNullOrEmpty(options.OutputPath))
        {
            result.AddError("输出路径不能为空");
        }

        if (string.IsNullOrEmpty(options.TemplatePath))
        {
            result.AddError("模板路径不能为空");
        }

        if (options.MetadataSources == null || !options.MetadataSources.Any())
        {
            result.AddError("至少需要指定一个元数据来源");
        }
    }

    /// <summary>
    /// 获取启用的提取器并检测无效来源
    /// </summary>
    private (List<IMetaDataExtractor> enabledExtractors, List<string> invalidSources) GetEnabledExtractors(
        IEnumerable<string> sourceNames)
    {
        var enabled = new List<IMetaDataExtractor>();
        var invalid = new List<string>();

        foreach (var sourceName in sourceNames)
        {
            // 尝试将字符串转换为MetadataSource枚举
            if (Enum.TryParse<MetadataSource>(sourceName, true, out var sourceType))
            {
                var extractor = _extractors.FirstOrDefault(e => e.SourceType == sourceType);
                if (extractor != null)
                {
                    enabled.Add(extractor);
                }
                else
                {
                    invalid.Add(sourceName);
                }
            }
            else
            {
                invalid.Add(sourceName);
            }
        }

        return (enabled, invalid);
    }

    /// <summary>
    /// 根据元数据类型选择合适的模板
    /// </summary>
    private static string GetTemplateName(ClassMetadata metadata) // 修正：使用接口而非具体实现
    {
        // 优先使用元数据指定的模板名
        if (!string.IsNullOrEmpty(metadata.TemplateName))
        {
            return metadata.TemplateName;
        }

        // 否则根据来源类型选择默认模板
        return metadata.SourceType switch
        {
            nameof(MetadataSource.Code) => "ClassTemplate.cshtml",
            nameof(MetadataSource.Database) => "DatabaseTableTemplate.cshtml",
            _ => "DefaultTemplate.cshtml"
        };
    }
}
