using System.Text.Json;
using System.Text.Json.Serialization;
using TKW.Framework.Common.Extensions;

namespace TKWF.ExcelImporter;

public class ExcelTemplateRegistry
{
    private readonly string _ConfigDirectory;
    private readonly Dictionary<string, ExcelTemplateConfiguration> _Cache = [];
    private readonly JsonSerializerOptions _JsonOptions;
    // ReSharper disable once ChangeFieldTypeToSystemThreadingLock
    private readonly object _LockObject = new();

    public ExcelTemplateRegistry(string configDirectory)
    {
        configDirectory.AssertNotEmptyOrNull(nameof(configDirectory));
        configDirectory = Path.Combine(configDirectory, "TemplateConfigs");

        _ConfigDirectory = configDirectory;
        _JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        if (!Directory.Exists(configDirectory))
            Directory.CreateDirectory(configDirectory!);
    }

    public ExcelTemplateConfiguration GetTemplate(string templateId)
    {
        lock (_LockObject)
        {
            if (_Cache.TryGetValue(templateId, out var config))
                return config;

            var filePath = GetTemplateFilePath(templateId);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"模板配置文件不存在: {filePath}");

            var json = File.ReadAllText(filePath);
            config = JsonSerializer.Deserialize<ExcelTemplateConfiguration>(json, _JsonOptions);

            _Cache[templateId] = config ?? throw new InvalidOperationException($"模板配置文件为空或格式错误: {filePath}");
            return config;
        }
    }

    public void SaveTemplate(ExcelTemplateConfiguration config)
    {
        lock (_LockObject)
        {
            var filePath = GetTemplateFilePath(config.Id);
            var json = JsonSerializer.Serialize(config, _JsonOptions);
            File.WriteAllText(filePath, json);
            _Cache[config.Id] = config;
        }
    }

    private string GetTemplateFilePath(string templateId)
    {
        return Path.Combine(_ConfigDirectory, $"{templateId}.json");
    }
}