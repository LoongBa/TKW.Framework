using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TKW.Framework.Common.DataType;

/// <summary>
/// System.Text.Json 自定义转换器 for DateTimeRange
/// </summary>
public class DateTimeRangeJsonConverter : JsonConverter<DateTimeRange>
{
    public override DateTimeRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var start = default(DateTime);
        var end = default(DateTime);
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
                if (propName == nameof(DateTimeRange.Start))
                    start = reader.GetDateTime();
                else if (propName == nameof(DateTimeRange.End))
                    end = reader.GetDateTime();
            }
        }
        return new DateTimeRange(start, end);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeRange value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(DateTimeRange.Start), value.Start);
        writer.WriteString(nameof(DateTimeRange.End), value.End);
        writer.WriteEndObject();
    }
}