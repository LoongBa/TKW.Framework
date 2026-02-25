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
                    result.AddExtracted(extractor.SourceType.ToString(), metaList.Count);
                }
            }

            foreach (var raw in allRawMetadata)
            {
                // 1. 转换元数据，此时 MetadataConverter.Convert 内部会调用 SanitizeMetadata 进行清洗
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
                        var pattern = config.FileNamePatterns.GetValueOrDefault(artifactType, "{ClassName}.generated.cs");
                        var subDir = config.OutputDirectories.GetValueOrDefault(artifactType, artifactType);
                        var outputBase = Path.Combine(config.OutputRoot, subDir);
                        var genPath = fileWriter.ResolveOutputPath(outputBase, classMeta.ClassName, pattern);

                        // 2. 读取模板原文内容 (用于感知模板变更)
                        var fullTemplatePath = Path.Combine(config.TemplatesPath, templatePath);
                        var templateContent = File.Exists(fullTemplatePath) ? await File.ReadAllTextAsync(fullTemplatePath) : string.Empty;

                        // 3. 修复 CS0165：显式计算哈希，确保变量在所有分支路径下都被赋值
                        var currentHash = IncrementalChecker.ComputeLogicHash(classMeta, templateContent);

                        // 4. 执行增量检查
                        var shouldGenerate = !config.EnableSkipUnchanged ||
                                             incrementalChecker.NeedRegenerate(classMeta, outputBase, classMeta.ClassName, pattern, templateContent, out currentHash);

                        if (shouldGenerate)
                        {
                            // 5. 注入稳定的逻辑指纹到元数据设置中
                            classMeta.GenerateCodeSettings["MetadataHash"] = currentHash;

                            var code = await templateEngine.RenderAsync(classMeta, templatePath);
                            fileWriter.Write(code, genPath, true);
                            result.AddGenerated($"{classMeta.ClassName} [{artifactType}]", genPath);
                        }
                        else
                        {
                            result.SkippedCount++;
                            result.SkippedFiles[$"{classMeta.ClassName} [{artifactType}]"] = genPath;
                        }

                        // 骨架处理 (Skeleton)
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
        foreach (var s in sources ?? [])
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