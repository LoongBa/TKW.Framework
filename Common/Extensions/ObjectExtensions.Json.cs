using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace TKW.Framework.Common.Extensions
{
    public static class StringToJsonExtensions
    {
        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString & JsonNumberHandling.WriteAsString,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), //不转义所有字符
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
    }
}