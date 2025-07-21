using System;
using System.Collections.Generic;
using xCodeGen.Abstractions.Attributes;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Configuration;
using xCodeGen.Core.Debugging;
using xCodeGen.Core.Extraction;
using xCodeGen.Core.Templates;

namespace xCodeGen.Core.Engine
{
    public class GeneratorEngine
    {
        private readonly GeneratorConfig _config;
        private readonly IMetadataExtractor _extractor;
        private readonly TemplateExecutor _templateExecutor;
        private readonly DebugLogger _debugLogger;

        public GeneratorEngine(GeneratorConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _extractor = new RoslynExtractor(config.TargetProject);
            _templateExecutor = new TemplateExecutor(config);
            _debugLogger = new DebugLogger(config.Debug);
        }

        /// <summary>
        /// 启动代码生成流程
        /// </summary>
        public void Generate()
        {
            try
            {
                // 输出启动调试信息
                _debugLogger.LogStartupInfo(_config);

                // 提取元数据
                var metadata = _extractor.Extract();
                _debugLogger.LogExtractedMetadata(metadata);

                // 遍历元数据生成产物
                foreach (var classMeta in metadata.Classes)
                {
                    ProcessClass(classMeta);
                }
            }
            catch (Exception ex)
            {
                _debugLogger.LogError("生成过程失败", ex);
                throw;
            }
        }

        private void ProcessClass(ClassMetadata classMeta)
        {
            if (classMeta == null) return;

            foreach (var methodMeta in classMeta.Methods)
            {
                ProcessMethod(classMeta, methodMeta);
            }
        }

        private void ProcessMethod(ClassMetadata classMeta, MethodMetadata methodMeta)
        {
            if (methodMeta == null) return;

            var artifactAttr = methodMeta.GenerateArtifactAttribute 
                ?? classMeta.GenerateArtifactAttribute;

            if (artifactAttr == null) return;

            try
            {
                GenerateArtifact(classMeta, methodMeta, artifactAttr);
            }
            catch (Exception ex)
            {
                _debugLogger.LogError($"处理方法 {classMeta.Name}.{methodMeta.Name} 时出错", ex);
            }
        }

        private void GenerateArtifact(ClassMetadata classMeta, MethodMetadata methodMeta, 
            GenerateArtifactAttribute artifactAttr)
        {
            if (!_config.TemplateMappings.TryGetValue(artifactAttr.ArtifactType, out var typeTemplates) ||
                !typeTemplates.TryGetValue(artifactAttr.TemplateName, out string templatePath))
            {
                throw new KeyNotFoundException($"未找到 {artifactAttr.ArtifactType}:{artifactAttr.TemplateName} 的模板配置");
            }

            // 准备模板数据
            var templateData = new TemplateInput
            {
                Class = classMeta,
                Method = methodMeta,
                ArtifactType = artifactAttr.ArtifactType,
                Config = _config
            };

            // 执行模板
            string outputPath = _templateExecutor.Execute(templatePath, templateData, artifactAttr.Overwrite);
            
            // 记录生成结果
            _debugLogger.LogGeneratedFile(artifactAttr.ArtifactType, outputPath);
        }
    }
}    