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
            // 1. 初始化模板引擎
            if (!string.IsNullOrEmpty(config.TemplatesPath))
                templateEngine.LoadTemplates(config.TemplatesPath);

            // 2. 收集原始元数据并记录计数
            var (enabledExtractors, _) = GetEnabledExtractors(options.MetadataSources);
            var allRawMetadata = new List<RawMetadata>();
            foreach (var extractor in enabledExtractors)
            {
                var extracted = await extractor.ExtractAsync(new ExtractorOptions { ProjectPath = options.ProjectPath });
                if (extracted != null)
                {
                    var metaList = extracted.ToList();
                    allRawMetadata.AddRange(metaList);
                    // 修正：记录元数据来源计数
                    result.AddExtracted(extractor.SourceType.ToString(), metaList.Count);
                }
            }

            // 3. 执行生成流水线
            foreach (var raw in allRawMetadata)
            {
                var classMeta = metadataConverter.Convert(raw);
                if (classMeta == null) continue;

                // 注入全局 CustomSettings
                if (config.CustomSettings != null)
                {
                    foreach (var setting in config.CustomSettings)
                        classMeta.GenerateCodeSettings[setting.Key] = setting.Value;
                }

                // 4. 遍历配置的产物类型
                foreach (var mapping in config.TemplateMappings)
                {
                    var artifactType = mapping.Key;
                    var templatePath = mapping.Value;

                    try
                    {
                        // A. 处理生成的产物 (.generated.cs)
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
                            // 记录跳过的详细路径
                            result.SkippedFiles[$"{classMeta.ClassName} [{artifactType}]"] = genPath;
                        }

                        // B. 处理骨架文件 (.cs)
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
                                // 修正：唯一化骨架键名，防止 DTO 和 Model 骨架冲突
                                result.SkeletonFiles[$"{classMeta.ClassName} [{artifactType} Skel]"] = skelPath;
                            }
                            else
                            {
                                // 若已存在，归类为跳过
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
        finally
        {
            timer.Stop();
            result.ElapsedMilliseconds = timer.ElapsedMilliseconds;
        }
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