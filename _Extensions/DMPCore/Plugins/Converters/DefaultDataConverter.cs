using TKWF.DMP.Core.Interfaces;
using TKWF.DMP.Core.Models;

namespace TKWF.DMP.Core.Plugins.Converters;

/// <summary>
/// 数据类型转换器，用于转换数据字段类型
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class DefaultDataConverter<T>(Dictionary<string, Func<object, object>> converters) : IDataPreprocessor<T>
    where T : class
{
    // 从配置创建转换器
    public static DefaultDataConverter<T> CreateFromConfig(MetricConfig config)
    {
        var converters = new Dictionary<string, Func<object, object>>();

        foreach (var kvp in config.PropertyMap)
        {
            if (kvp.Key.EndsWith("Converter"))
            {
                var propertyName = kvp.Key[..^"Converter".Length];

                // 根据转换类型创建转换器
                converters[propertyName] = kvp.Value.ToLower() switch
                {
                    "int" => value => Convert.ToInt32(value),
                    "decimal" => value => Convert.ToDecimal(value),
                    "datetime" => value => Convert.ToDateTime(value),
                    "string" => value => value,
                    _ => throw new NotSupportedException($"不支持的转换器类型: {kvp.Value}")
                };
            }
        }

        return new DefaultDataConverter<T>(converters);
    }

    public IEnumerable<T> Process(IEnumerable<T> data)
    {
        if (converters.Count == 0)
        {
            foreach (var item in data)
            {
                yield return item;
            }
            yield break;
        }

        foreach (var item in data)
        {
            foreach (var converter in converters)
            {
                try
                {
                    var property = typeof(T).GetProperty(converter.Key);
                    if (property != null && property.CanWrite)
                    {
                        var value = property.GetValue(item);
                        if (value != null)
                        {
                            var convertedValue = converter.Value(value);
                            property.SetValue(item, convertedValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录转换错误或忽略
                    Console.WriteLine($"数据转换错误: {ex.Message}");
                }
            }

            yield return item;
        }
    }
}