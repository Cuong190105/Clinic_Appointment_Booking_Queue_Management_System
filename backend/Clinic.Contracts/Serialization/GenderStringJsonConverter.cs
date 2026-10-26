using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clinic.Contracts.Serialization;

/// <summary>Accepts the API's historical numeric gender values and canonical string values.</summary>
public sealed class GenderStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return value?.Trim().ToLowerInvariant() switch
            {
                "male" or "0" => "Male",
                "female" or "1" => "Female",
                "other" or "2" => "Other",
                _ => throw new JsonException("Gender must be Male, Female, Other, 0, 1, or 2.")
            };
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
        {
            return numericValue switch
            {
                0 => "Male",
                1 => "Female",
                2 => "Other",
                _ => throw new JsonException("Numeric gender must be 0, 1, or 2.")
            };
        }

        throw new JsonException("Gender must be a string or numeric value.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
