using System;
using System.IO;
using RazorLight;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Configuration;
using xCodeGen.Utilities;
using System.Collections.Generic;

namespace xCodeGen.Core.Templates
{
    public class TemplateInput
    {
        public ClassMetadata Class { get; set; }
        public MethodMetadata Method { get; set; }
        public string ArtifactType { get; set; }
        public GeneratorConfig Config { get; set; }
        public UtilitiesContainer Utilities { get; set; } = new UtilitiesContainer();
    }

    public class UtilitiesContainer
    {
        public NamingUtility Naming { get; set; } = new NamingUtility();
        public TypeUtility Type { get; set; } = new TypeUtility();
        public ValidationUtility Validation { get; set; } = new ValidationUtility();
    }

    public class TemplateExecutor
    {
        private readonly GeneratorConfig _config;
        private readonly RazorLightEngine _engine;

        public TemplateExecutor(GeneratorConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _engine = new RazorLightEngineBuilder()
                .UseFileSystemProject(Directory.GetCurrentDirectory())
                .UseMemoryCachingProvider()
                .Build();
        }

        public string Execute(string templatePath, TemplateInput input, bool overwrite)
        {
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("模板文件不存在", templatePath);
            }

            // 确保输出目录存在
            string outputDir = GetOutputDirectory(input);
            Directory.CreateDirectory(outputDir);

            // 生成输出文件名
            string fileName = GenerateFileName(input);
            string outputPath = Path.Combine(outputDir, fileName);

            // 检查文件是否已存在
            if (File.Exists(outputPath) && !overwrite)
            {
                return outputPath; // 不覆盖现有文件
            }

            // 读取模板内容
            string templateContent = File.ReadAllText(templatePath);

            // 执行模板
            string result = _engine.CompileRenderStringAsync(
                Guid.NewGuid().ToString(), 
                templateContent, 
                input).Result;

            // 写入输出文件
            File.WriteAllText(outputPath, result);

            return outputPath;
        }

        private string GetOutputDirectory(TemplateInput input)
        {
            // 获取产物类型对应的输出目录
            if (_config.OutputDirectories != null && _config.OutputDirectories.TryGetValue(input.ArtifactType, out string typeDir))
            {
                return Path.Combine(_config.OutputRoot, typeDir);
            }

            // 默认使用产物类型作为目录名
            return Path.Combine(_config.OutputRoot, input.ArtifactType);
        }

        private string GenerateFileName(TemplateInput input)
        {
            // 生成文件名：类名_方法签名_产物类型.cs
            string safeSignature = input.Method.UniqueSignature
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("?", "Nullable");
                
            return $"{input.Class.Name}_{safeSignature}_{input.ArtifactType}.cs";
        }
    }
}