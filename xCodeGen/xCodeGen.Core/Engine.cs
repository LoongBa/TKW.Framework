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
/// 核心引擎：支持多产物并行与配置注入
/// </summary>
public class Engine(
    IEnumerable<IMetaDataExtractor> extractors,
    IMetadataConverter metadataConverter,
    ITemplateEngine templateEngine,
    IFileWriter fileWriter,
    NamingService namingService,
    IncrementalChecker incrementalChecker)
{
    public async Task<GenerateResult> GenerateAsync(GenerateOptions options, CodeGenConfig config)
    {
        var result = new GenerateResult();
        var timer = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (config == null) throw new ArgumentNullException(nameof(config));

            // 1. 获取提取器
            var (enabledExtractors, _) = GetEnabledExtractors(options.MetadataSources);
            if (enabledExtractors.Count == 0) throw new Exception("未找到有效的提取器。");

            // 2. 加载模板
            if (!string.IsNullOrEmpty(config.TemplatesPath))
                templateEngine.LoadTemplates(config.TemplatesPath);

            // 3. 收集元数据并更新统计
            var allRawMetadata = new List<RawMetadata>();
            foreach (var extractor in enabledExtractors)
            {
                var extracted = await extractor.ExtractAsync(new ExtractorOptions { ProjectPath = options.ProjectPath });
                if (extracted != null)
                {
                    var metaList = extracted.ToList();
                    allRawMetadata.AddRange(metaList);
                    // 修复统计显示 Bug
                    result.AddExtracted(extractor.SourceType.ToString(), metaList.Count);
                }
            }

            // 4. 执行生成循环
            foreach (var raw in allRawMetadata)
            {
                var classMeta = metadataConverter.Convert(raw);
                if (classMeta == null) continue;

                // 核心注入：将配置参数注入到实体的生成设置中
                if (config.CustomSettings != null)
                {
                    foreach (var setting in config.CustomSettings)
                    {
                        classMeta.GenerateCodeSettings[setting.Key] = setting.Value;
                    }
                }

                foreach (var mapping in config.TemplateMappings)
                {
                    var artifactType = mapping.Key;
                    var templatePath = mapping.Value;

                    try
                    {
                        var targetName = namingService.GetTargetName(classMeta.ClassName, artifactType);
                        var subDir = config.OutputDirectories.GetValueOrDefault(artifactType, artifactType);
                        var outputBase = Path.Combine(config.OutputRoot, subDir);

                        var genPath = fileWriter.ResolveOutputPath(outputBase, targetName, "{ClassName}.generated.cs");

                        if (incrementalChecker.NeedRegenerate(classMeta, outputBase, targetName))
                        {
                            var code = await templateEngine.RenderAsync(classMeta, templatePath);
                            fileWriter.Write(code, genPath, true);
                            result.AddGenerated($"{targetName}[Logic]", genPath);
                        }
                        else { result.SkippedCount++; }

                        // 处理骨架文件 (.cs)
                        var extPath = genPath.Replace(".generated.cs", ".cs");
                        if (!fileWriter.Exists(extPath) && config.SkeletonMappings?.TryGetValue(artifactType, out var skel) == true)
                        {
                            var skelCode = await templateEngine.RenderAsync(classMeta, skel);
                            fileWriter.Write(skelCode, extPath, false);
                            result.AddGenerated($"{targetName}[Skeleton]", extPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AddError($"产物 {artifactType} ({classMeta.ClassName}) 失败: {ex.Message}");
                    }
                }
            }
            result.Success = !result.Errors.Any();
        }
        catch (Exception ex) { result.AddError($"引擎异常: {ex.Message}"); }
        finally { timer.Stop(); result.ElapsedMilliseconds = timer.ElapsedMilliseconds; }
        return result;
    }

    private (List<IMetaDataExtractor> enabled, List<string> invalid) GetEnabledExtractors(IEnumerable<string> sources)
    {
        var enabled = new List<IMetaDataExtractor>();
        var invalid = new List<string>();
        foreach (var s in sources ?? Enumerable.Empty<string>())
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