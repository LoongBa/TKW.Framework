using System;
using System.Collections.Generic;
using System.IO;
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
/// 优化后的核心引擎：支持多产物并行、双模板驱动与双文件保护
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
    /// 执行代码生成流程
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
            // 1. 获取并验证启用的提取器
            var (enabledExtractors, invalidSources) = GetEnabledExtractors(options.MetadataSources);
            if (enabledExtractors.Count == 0) throw new Exception("未找到有效提取器");

            // 2. 加载所有配置的模板
            templateEngine.LoadTemplates(config.TemplatesPath);

            // 3. 从提取器收集原始元数据
            var allRawMetadata = new List<RawMetadata>();
            foreach (var extractor in enabledExtractors)
            {
                var extracted = await extractor.ExtractAsync(new ExtractorOptions { ProjectPath = options.ProjectPath });
                allRawMetadata.AddRange(extracted);
            }

            // 4. 遍历元数据，根据产物类型映射进行多目标生成
            foreach (var raw in allRawMetadata)
            {
                var classMeta = metadataConverter.Convert(raw);

                foreach (var mapping in config.TemplateMappings)
                {
                    var artifactType = mapping.Key;   // 如 "Dto", "Repository"
                    var logicTemplatePath = mapping.Value; // 如 "Templates/Repository.cshtml"

                    try
                    {
                        // A. 计算目标名称与输出路径
                        string targetName = namingService.GetTargetName(classMeta.ClassName, artifactType);
                        string subDir = config.OutputDirectories.TryGetValue(artifactType, out var dir) ? dir : artifactType;
                        string outputBase = Path.Combine(config.OutputRoot, subDir);

                        // B. 生成并处理逻辑文件 (.generated.cs)
                        var genPath = fileWriter.ResolveOutputPath(outputBase, targetName, "{ClassName}.generated.cs");
                        if (incrementalChecker.NeedRegenerate(classMeta, outputBase, targetName))
                        {
                            var generatedCode = await templateEngine.RenderAsync(classMeta, logicTemplatePath);
                            fileWriter.Write(generatedCode, genPath, true);
                            result.AddGenerated($"{targetName}[Logic]", genPath);
                        }
                        else { result.SkippedCount++; }

                        // C. 生成并处理扩展文件骨架 (.cs) - 模板驱动
                        var extensionPath = genPath.Replace(".generated.cs", ".cs");
                        if (!fileWriter.Exists(extensionPath))
                        {
                            // 检查配置中是否有该产物类型的骨架模板定义
                            if (config.SkeletonMappings.TryGetValue(artifactType, out var skeletonTemplatePath))
                            {
                                var skeletonCode = await templateEngine.RenderAsync(classMeta, skeletonTemplatePath);
                                fileWriter.Write(skeletonCode, extensionPath, false);
                                result.AddGenerated($"{targetName}[Skeleton]", extensionPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AddError($"产物 {artifactType} 处理失败: {ex.Message}");
                    }
                }
            }
            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.AddError($"引擎崩溃: {ex.Message}");
        }
        finally { timer.Stop(); result.ElapsedMilliseconds = timer.ElapsedMilliseconds; }

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
