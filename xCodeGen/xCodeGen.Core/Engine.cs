using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Extractors;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Abstractions.Templates;
using xCodeGen.Core.Configuration;
using xCodeGen.Core.IO;
using xCodeGen.Core.Models;
using xCodeGen.Core.Services;

namespace xCodeGen.Core;

/// <summary>
/// 重构后的核心引擎：支持多产物并行生成
/// </summary>
public class Engine(
    IEnumerable<IMetaDataExtractor> extractors,
    IMetadataConverter metadataConverter,
    ITemplateEngine templateEngine,
    IFileWriter fileWriter,
    NamingService namingService,
    IncrementalChecker incrementalChecker)
{
    /// <summary>
    /// 执行代码生成，支持多产物类型映射
    /// </summary>
    /// <param name="options">生成选项配置</param>
    /// <param name="config">代码生成全局配置</param>
    /// <returns>生成结果对象，包含成功/失败状态和统计信息</returns>
    public async Task<GenerateResult> GenerateAsync(GenerateOptions options, CodeGenConfig config)
    {
        var result = new GenerateResult();
        var timer = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. 提取器加载与校验
            var (enabledExtractors, invalidSources) = GetEnabledExtractors(options.MetadataSources);
            foreach (var inv in invalidSources) result.AddWarning($"无效来源: {inv}");

            if (enabledExtractors.Count == 0)
            {
                result.AddError("未找到可用的元数据提取器。");
                return result;
            }

            // 2. 预加载所有配置的模板
            templateEngine.LoadTemplates(config.TemplatesPath);

            // 3. 提取原始元数据
            var allRawMetadata = new List<RawMetadata>();
            foreach (var extractor in enabledExtractors)
            {
                var extracted = await extractor.ExtractAsync(new ExtractorOptions { ProjectPath = options.ProjectPath });
                var metadatas = extracted as RawMetadata[] ?? extracted.ToArray();
                allRawMetadata.AddRange(metadatas);
                result.AddExtracted(extractor.SourceType.ToString(), metadatas.Length);
            }

            // 4. 转换并生成多产物
            foreach (var raw in allRawMetadata)
            {
                var classMeta = metadataConverter.Convert(raw);

                // 核心重构：遍历配置中的所有产物类型映射
                // 示例：若配置了 Dto 和 Validator，则同一个 classMeta 会进入两次循环
                foreach (var mapping in config.TemplateMappings)
                {
                    var artifactType = mapping.Key;   // 例如: "Dto"
                    var templatePath = mapping.Value; // 例如: "Templates/Artifact/Dto.cshtml"

                    try
                    {
                        // A. 计算目标产物名称（通过 NamingService）
                        var targetName = namingService.GetTargetName(classMeta.ClassName, artifactType);

                        // B. 确定输出子目录
                        var subDir = config.OutputDirectories.TryGetValue(artifactType, out var dir) ? dir : artifactType;
                        var outputBase = System.IO.Path.Combine(config.OutputRoot, subDir);

                        // C. 增量检查：判断当前元数据对应的产物是否需要重新生成
                        // 参数 1: 当前类的元数据; 参数 2: 产物存放的绝对目录
                        if (!incrementalChecker.NeedRegenerate(classMeta, outputBase, targetName))
                        {
                            result.SkippedCount++; // 记录跳过的文件数
                            continue;              // 跳过本次生成流程
                        }

                        // D. 渲染与写入
                        var code = await templateEngine.RenderAsync(classMeta, templatePath);
                        var finalPath = fileWriter.ResolveOutputPath(outputBase, targetName, options.FileNameFormat);

                        fileWriter.Write(code, finalPath, options.Overwrite);
                        result.AddGenerated($"{classMeta.ClassName}[{artifactType}]", finalPath);
                    }
                    catch (Exception ex)
                    {
                        result.AddError($"生成产物 {artifactType} (源: {classMeta.ClassName}) 失败: {ex.Message}");
                    }
                }
            }
            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.AddError($"引擎崩溃: {ex.Message}");
            result.Success = false;
        }
        finally
        {
            timer.Stop();
            result.ElapsedMilliseconds = timer.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// 获取启用的元数据提取器
    /// </summary>
    /// <param name="sources">元数据来源集合</param>
    /// <returns>元组：第一个元素为有效的提取器列表，第二个元素为无效的来源列表</returns>
    private (List<IMetaDataExtractor> enabled, List<string> invalid) GetEnabledExtractors(IEnumerable<string> sources)
    {
        var enabled = new List<IMetaDataExtractor>();
        var invalid = new List<string>();
        foreach (var s in sources)
        {
            if (Enum.TryParse<MetadataSource>(s, true, out var type))
            {
                var ex = extractors.FirstOrDefault(e => e.SourceType == type);
                if (ex != null) enabled.Add(ex); else invalid.Add(s);
            }
            else invalid.Add(s);
        }
        return (enabled, invalid);
    }
}
