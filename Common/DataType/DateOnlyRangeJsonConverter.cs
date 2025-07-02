using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKW.Framework.Common.DataType;

/// <summary>
/// System.Text.Json 自定义转换器 for DateOnlyRange
/// </summary>
public class DateOnlyRangeJsonConverter : JsonConverter<DateOnlyRange>
{
    public override DateOnlyRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var start = default(DateOnly);
        var end = default(DateOnly);
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propName = reader.GetString();
                reader.Read();
                if (propName == nameof(DateOnlyRange.Start))
                    start = DateOnly.Parse(reader.GetString()!);
                else if (propName == nameof(DateOnlyRange.End))
                    end = DateOnly.Parse(reader.GetString()!);
            }
        }
        return new DateOnlyRange(start, end);
    }

    public override void Write(Utf8JsonWriter writer, DateOnlyRange value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(DateOnlyRange.Start), value.Start.ToString("yyyy-MM-dd"));
        writer.WriteString(nameof(DateOnlyRange.End), value.End.ToString("yyyy-MM-dd"));
        writer.WriteEndObject();
    }
}