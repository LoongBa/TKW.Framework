using System;
using System.IO;
using System.Text;
using xCodeGen.Abstractions.Metadata;
using xCodeGen.Core.Configuration;

namespace xCodeGen.Core.Core.Debugging
{
    public class DebugLogger
    {
        private readonly DebugConfig _debugConfig;
        private readonly string _logDirectory;
        private readonly string _sessionId;

        public DebugLogger(DebugConfig debugConfig)
        {
            _debugConfig = debugConfig ?? new DebugConfig();
            _sessionId = DateTime.Now.ToString("yyyyMMddHHmmss");
            _logDirectory = _debugConfig.Enabled 
                ? Path.Combine(_debugConfig.Directory, _sessionId) 
                : null;

            // 确保日志目录存在
            if (_debugConfig.Enabled && !Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        /// <summary>
        /// 记录启动信息
        /// </summary>
        public void LogStartupInfo(GeneratorConfig config)
        {
            if (!_debugConfig.Enabled) return;

            string content = $"xCodeGen 启动日志 - {DateTime.Now}\r\n";
            content += $"目标项目: {config.TargetProject}\r\n";
            content += $"输出根目录: {config.OutputRoot}\r\n";
            content += $"模板映射数: {config.TemplateMappings.Count}\r\n";
            content += $"启用的工具类: {string.Join(", ", config.EnabledUtilities)}\r\n";

            WriteToFile("startup.log", content);
        }

        /// <summary>
        /// 记录提取的元数据
        /// </summary>
        public void LogExtractedMetadata(xCodeGen.Abstractions.Metadata.ExtractedMetadata metadata)
        {
            if (!_debugConfig.Enabled) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"提取的元数据 - {DateTime.Now}");
            sb.AppendLine($"类数量: {metadata.Classes.Count}");
            sb.AppendLine();

            foreach (ClassMetadata classMeta in metadata.Classes)
            {
                sb.AppendLine($"类: {classMeta.FullName}");
                sb.AppendLine($"  方法数量: {classMeta.Methods.Count}");
                
                foreach (MethodMetadata methodMeta in classMeta.Methods)
                {
                    sb.AppendLine($"  方法: {methodMeta.Name} ({methodMeta.ReturnType})");
                    sb.AppendLine($"    签名: {methodMeta.UniqueSignature}");
                    sb.AppendLine($"    参数数量: {methodMeta.Parameters.Count}");
                    
                    foreach (ParameterMetadata paramMeta in methodMeta.Parameters)
                    {
                        sb.AppendLine($"    参数: {paramMeta.Name} ({paramMeta.TypeName})");
                        sb.AppendLine($"      全限定类型: {paramMeta.TypeFullName}");
                        sb.AppendLine($"      可空: {paramMeta.IsNullable}");
                        sb.AppendLine($"      集合: {paramMeta.IsCollection} {(string.IsNullOrEmpty(paramMeta.CollectionItemType) ? "" : $"({paramMeta.CollectionItemType})")}");
                        sb.AppendLine($"      特性数量: {paramMeta.Attributes.Count}");
                    }
                }
                sb.AppendLine();
            }

            WriteToFile("metadata.log", sb.ToString());
        }

        /// <summary>
        /// 记录生成的文件
        /// </summary>
        public void LogGeneratedFile(string artifactType, string path)
        {
            if (!_debugConfig.Enabled) return;

            string content = $"[{DateTime.Now:HH:mm:ss}] 生成 {artifactType}: {path}\r\n";
            WriteToFile("generated_files.log", content, append: true);
        }

        /// <summary>
        /// 记录错误信息
        /// </summary>
        public void LogError(string message, Exception ex)
        {
            if (!_debugConfig.Enabled) return;

            string content = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}\r\n";
            content += $"异常类型: {ex.GetType().Name}\r\n";
            content += $"消息: {ex.Message}\r\n";
            content += $"堆栈跟踪: {ex.StackTrace}\r\n\r\n";

            WriteToFile("errors.log", content, append: true);
        }

        private void WriteToFile(string fileName, string content, bool append = false)
        {
            if (!_debugConfig.Enabled) return;

            try
            {
                string path = Path.Combine(_logDirectory, fileName);
                if (append && File.Exists(path))
                {
                    File.AppendAllText(path, content);
                }
                else
                {
                    File.WriteAllText(path, content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入调试日志失败: {ex.Message}");
            }
        }
    }
}