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
            if (!string.IsNullOrEmpty(config.TemplatesPath))
                templateEngine.LoadTemplates(config.TemplatesPath);

            var (enabledExtractors, _) = GetEnabledExtractors(options.MetadataSources);
            var allRawMetadata = new List<RawMetadata>();
            foreach (var extractor in enabledExtractors)
            {
                var extracted = await extractor.ExtractAsync(new ExtractorOptions { ProjectPath = options.ProjectPath });
                if (extracted != null)
                {
                    var metaList = extracted.ToList();
                    allRawMetadata.AddRange(metaList);
                    // 确保元数据计数被正确记录
                    result.AddExtracted(extractor.SourceType.ToString(), metaList.Count);
                }
            }

            foreach (var raw in allRawMetadata)
            {
                var classMeta = metadataConverter.Convert(raw);
                if (classMeta == null) continue;

                if (config.CustomSettings != null)
                {
                    foreach (var setting in config.CustomSettings)
                        classMeta.GenerateCodeSettings[setting.Key] = setting.Value;
                }

                foreach (var mapping in config.TemplateMappings)
                {
                    var artifactType = mapping.Key;
                    var templatePath = mapping.Value;

                    try
                    {
                        // 1. 处理 .generated.cs 文件
                        var pattern = config.FileNamePatterns.GetValueOrDefault(artifactType, "{ClassName}.generated.cs");
                        var subDir = config.OutputDirectories.GetValueOrDefault(artifactType, artifactType);
                        var outputBase = Path.Combine(config.OutputRoot, subDir);
                        var genPath = fileWriter.ResolveOutputPath(outputBase, classMeta.ClassName, pattern);

                        if (!config.EnableSkipUnchanged || incrementalChecker.NeedRegenerate(classMeta, outputBase, classMeta.ClassName, pattern))
                        {
                            var code = await templateEngine.RenderAsync(classMeta, templatePath);
                            fileWriter.Write(code, genPath, true);
                            result.AddGenerated($"{classMeta.ClassName} [{artifactType}]", genPath);
                        }
                        else
                        {
                            result.SkippedCount++;
                            result.SkippedFiles[$"{classMeta.ClassName} [{artifactType}]"] = genPath;
                        }

                        // 2. 处理骨架文件 (.cs 手写部分)
                        var skeletonKey = artifactType + "Empty";
                        if (config.SkeletonMappings?.TryGetValue(skeletonKey, out var skelTemplate) == true)
                        {
                            var skelPattern = config.FileNamePatterns.GetValueOrDefault(skeletonKey, "{ClassName}.cs");
                            var skelSubDir = config.OutputDirectories.GetValueOrDefault(skeletonKey, subDir);
                            var skelOutputBase = Path.Combine(config.OutputRoot, skelSubDir);
                            var skelPath = fileWriter.ResolveOutputPath(skelOutputBase, classMeta.ClassName, skelPattern);

                            if (!fileWriter.Exists(skelPath))
                            {
                                var skelCode = await templateEngine.RenderAsync(classMeta, skelTemplate);
                                fileWriter.Write(skelCode, skelPath, false);
                                // 使用 artifactType 区分骨架类型，避免 Key 重复
                                result.SkeletonFiles[$"{classMeta.ClassName} [{artifactType} Skel]"] = skelPath;
                            }
                            else
                            {
                                result.SkippedCount++;
                                result.SkippedFiles[$"{classMeta.ClassName} [{artifactType} Skel]"] = skelPath;
                            }
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