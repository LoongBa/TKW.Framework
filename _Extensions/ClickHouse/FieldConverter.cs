namespace TKWF.Extensions.ClickHouse;

public class FieldConverter
{
    public string FieldName { get; set; } = string.Empty;
    // 注意：此委托无法通过 JSON 反序列化，需手动注册
    public Func<object?, object?> Converter { get; set; } = obj => obj;
}