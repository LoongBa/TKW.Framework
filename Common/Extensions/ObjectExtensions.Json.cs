using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKW.Framework.Common.Extensions
{
    public static class StringToJsonExtensions
    {
        public static readonly JsonSerializerOptions DefaultOptions = new()
        {
            // 设置序列化时是否缩进输出，true 表示缩进，使输出更易读
            WriteIndented = true,

            // 设置是否允许在 JSON 对象或数组末尾有尾随逗号，true 表示允许
            AllowTrailingCommas = false,

            // 设置数字处理方式，允许从字符串中读取数字并作为字符串写入
            // JsonNumberHandling.AllowReadingFromString: 允许从字符串中读取数字
            // JsonNumberHandling.WriteAsString: 将数字作为字符串写入
            // 使用按位与操作符 & 组合这两个选项
            NumberHandling = JsonNumberHandling.AllowReadingFromString & JsonNumberHandling.WriteAsString,

            // 创建一个编码器，不转义任何字符
            // Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)

            // 设置使用的编码器为 JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            // 该编码器避免转义中文字符，使输出更符合中文环境的需要
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 使用默认编码器，避免转义中文字符
        };

        /// <summary>
        /// 转换成 Json
        /// </summary>
        public static string ToJson(this object json, JsonSerializerOptions options = null)
        {
            options ??= DefaultOptions;
            return JsonSerializer.Serialize(json.AssertNotNull(), options);
        }

        /// <summary>
        /// 从 Json 反序列化
        /// </summary>
        public static T ToObjectFromJson<T>(this string json, JsonSerializerOptions options = null)
        {
            options ??= DefaultOptions;
            return JsonSerializer.Deserialize<T>(json.EnsureHasValue(), options);
        }

        /// <summary>
        /// 从 Json 反序列化为 dynamic
        /// </summary>
        public static dynamic ToDynamicFromJson(this string json, JsonSerializerOptions options = null)
        {
            options ??= DefaultOptions;
            return JsonSerializer.Deserialize<dynamic>(json.EnsureHasValue(), options);
        }
    }
}