using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Abstractions.Templates;
using xCodeGen.Core.Configuration;
using xCodeGen.Core.IO;
using xCodeGen.Core.Models;

namespace xCodeGen.Core;

public class Engine(
    ITemplateEngine templateEngine,
    IFileWriter fileWriter,
    IncrementalChecker incrementalChecker)
{
    public async Task<GenerateResult> GenerateAsync(IProjectMetaContext projectContext, GenerateOptions options, CodeGenConfig config)
    {
        var result = new GenerateResult();
        var timer = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. 初始化模板引擎 (保持不变)
            if (!string.IsNullOrEmpty(config.TemplatesPath))
                templateEngine.LoadTemplates(config.TemplatesPath);

            // 2. 模板分组 (保持不变)
            var entityArtifacts = config.Artifacts.Where(x => x.Value.Scope.Equals("Entity", StringComparison.OrdinalIgnoreCase)).ToList();
            var projectArtifacts = config.Artifacts.Where(x => x.Value.Scope.Equals("Project", StringComparison.OrdinalIgnoreCase)).ToList();

            // 3. 执行 Entity 作用域生成：调用每一个模板
            foreach (var classMeta in projectContext.Entities)
                foreach (var artPair in entityArtifacts)
                {
                    try
                    {
                        await ProcessEntityArtifactAsync(classMeta, artPair.Key, 
                            artPair.Value, config, result);
                    }
                    catch (Exception e)
                    {
                        result.AddError($"实体模板 [{artPair.Key}] 异常: {e.Message}");
                        if (config.Debug.Enabled) result.AddError(e.StackTrace ?? "");
                    }

                }

            // 5. 执行 Project 作用域生成 —— 关键修改：直接传 projectContext 进去
            // 不再 new ClassMetadata，而是让模板直接拿到整个上下文
            if (projectArtifacts.Any())
            {
                foreach (var artPair in projectArtifacts)
                {
                    try
                    {
                        // 注意：这里需要 ProcessProjectArtifactAsync 支持传入 IProjectMetaContext
                        await ProcessProjectArtifactAsync(projectContext,
                            artPair.Key, artPair.Value, config, result);
                    }
                    catch (Exception e)
                    {
                        result.AddError($"项目模板 [{artPair.Key}] 异常: {e.Message}");
                        if (config.Debug.Enabled) result.AddError(e.StackTrace ?? "");
                    }
                }
            }

            result.Success = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            result.AddError($"引擎异常：{ex.Message}");
            if (config.Debug.Enabled) result.AddError(ex.StackTrace ?? "");
        }
        finally
        {
            timer.Stop();
            result.ElapsedMilliseconds = timer.ElapsedMilliseconds;
        }
        return result;
    }

    private async Task ProcessEntityArtifactAsync(ClassMetadata entity, string artifactName, ArtifactConfig art, CodeGenConfig config, GenerateResult result)
    {
        // A. 处理标准模板 (支持增量跳过)
        if (!string.IsNullOrEmpty(art.Template))
        {
            var genPath = fileWriter.ResolveOutputPath(art.OutputDir, entity.ClassName, art.OutputPattern);
            var fullTemplatePath = Path.Combine(config.TemplatesPath, art.Template);

            if (!File.Exists(fullTemplatePath))
            {
                result.AddError($"找不到模板: {art.Template}");
                return;
            }

            var templateContent = await File.ReadAllTextAsync(fullTemplatePath);

            // 💡 确保 currentHash 被赋值，不再受 EnableSkipUnchanged 短路影响
            var isChanged = incrementalChecker.NeedRegenerate(entity, art.OutputDir, entity.ClassName, art.OutputPattern, templateContent, out var currentHash);
            var shouldGenerate = !config.EnableSkipUnchanged || isChanged;

            if (shouldGenerate)
            {
                entity.GenerateCodeSettings["MetadataHash"] = currentHash;

                var code = await templateEngine.RenderAsync(entity, art.Template);
                fileWriter.Write(code, genPath, true);
                result.AddGenerated($"{entity.ClassName} [{artifactName}]", genPath);
            }
            else
            {
                result.SkippedCount++;
                result.SkippedFiles[$"{entity.ClassName} [{artifactName}]"] = genPath;
            }
        }

        // B. 处理骨架模板 (一次性，存在即跳过)
        if (!string.IsNullOrEmpty(art.SkeletonTemplate))
        {
            var skelDir = !string.IsNullOrEmpty(art.SkeletonDir) ? art.SkeletonDir : art.OutputDir;
            var skelPattern = !string.IsNullOrEmpty(art.SkeletonPattern) ? art.SkeletonPattern : art.OutputPattern.Replace(".g.cs", ".cs");
            var skelPath = fileWriter.ResolveOutputPath(skelDir, entity.ClassName, skelPattern);

            if (!fileWriter.Exists(skelPath))
            {
                var skelCode = await templateEngine.RenderAsync(entity, art.SkeletonTemplate);
                fileWriter.Write(skelCode, skelPath, false);
                result.SkeletonFiles[$"{entity.ClassName} [{artifactName} Skel]"] = skelPath;
            }
        }
    }
    private async Task ProcessProjectArtifactAsync(IProjectMetaContext project, string artifactName, ArtifactConfig art, CodeGenConfig config, GenerateResult result)
    {
        // A. 处理标准模板 (支持增量跳过)
        var projectName = "Project";
        if (!string.IsNullOrEmpty(art.Template))
        {
            var genPath = fileWriter.ResolveOutputPath(art.OutputDir, projectName, art.OutputPattern);
            var fullTemplatePath = Path.Combine(config.TemplatesPath, art.Template);

            if (!File.Exists(fullTemplatePath))
            {
                result.AddError($"找不到模板: {art.Template}");
                return;
            }

            var templateContent = await File.ReadAllTextAsync(fullTemplatePath);

            // 💡 确保 currentHash 被赋值，不再受 EnableSkipUnchanged 短路影响
            var isChanged = incrementalChecker.NeedRegenerate(project, art.OutputDir, projectName, art.OutputPattern, templateContent, out var currentHash);
            var shouldGenerate = !config.EnableSkipUnchanged || isChanged;

            if (shouldGenerate)
            {
                project.Configuration.CustomProperties["MetadataHash"] = currentHash;
                var code = await templateEngine.RenderAsync(project, art.Template);
                fileWriter.Write(code, genPath, true);
                result.AddGenerated($"{project} [{artifactName}]", genPath);
            }
            else
            {
                result.SkippedCount++;
                result.SkippedFiles[$"{projectName} [{artifactName}]"] = genPath;
            }
        }

        // B. 处理骨架模板 (一次性，存在即跳过)
        if (!string.IsNullOrEmpty(art.SkeletonTemplate))
        {
            var skelDir = !string.IsNullOrEmpty(art.SkeletonDir) ? art.SkeletonDir : art.OutputDir;
            var skelPattern = !string.IsNullOrEmpty(art.SkeletonPattern) ? art.SkeletonPattern : art.OutputPattern.Replace(".g.cs", ".cs");
            var skelPath = fileWriter.ResolveOutputPath(skelDir, projectName, skelPattern);

            if (!fileWriter.Exists(skelPath))
            {
                var skelCode = await templateEngine.RenderAsync(project, art.SkeletonTemplate);
                fileWriter.Write(skelCode, skelPath, false);
                result.SkeletonFiles[$"{projectName} [{artifactName} Skel]"] = skelPath;
            }
        }
    }
}