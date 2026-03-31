using PubQuizMaster.Core.Models.Contents;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubQuizMaster.Core.Services
{
    /// <summary>
    /// Polymorphic JSON converter for AnswerBase (AnswerBool / AnswerPoint).
    /// Writes a "$type" discriminator so deserialization knows which subclass to use.
    /// </summary>
    public class AnswerBaseJsonConverter : JsonConverter<AnswerBase>
    {
        public override AnswerBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var type = root.GetProperty("$type").GetString();
            return type switch
            {
                nameof(AnswerBool) => JsonSerializer.Deserialize<AnswerBool>(root.GetRawText(), options)!,
                nameof(AnswerPoint) => JsonSerializer.Deserialize<AnswerPoint>(root.GetRawText(), options)!,
                _ => throw new JsonException($"Unknown AnswerBase type: {type}")
            };
        }

        public override void Write(Utf8JsonWriter writer, AnswerBase value, JsonSerializerOptions options)
        {
            var type = value.GetType().Name;
            var json = JsonSerializer.SerializeToElement(value, value.GetType(), options);

            writer.WriteStartObject();
            writer.WriteString("$type", type);
            foreach (var prop in json.EnumerateObject())
                prop.WriteTo(writer);
            writer.WriteEndObject();
        }
    }
}