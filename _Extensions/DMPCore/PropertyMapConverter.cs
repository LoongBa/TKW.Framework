using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKWF.DMP.Core;

public class PropertyMapConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var jsonString = reader.GetString();
            if (string.IsNullOrEmpty(jsonString))
                return [];

            return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString) ?? throw new InvalidOperationException();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options) ?? throw new InvalidOperationException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, string> value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}