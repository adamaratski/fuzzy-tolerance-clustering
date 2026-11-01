using System.Text.Json;
using System.Text.Json.Serialization;
using MathFuzzy.Inference.Systems;

namespace MathFuzzy.Inference.Serialization;

/// <summary>
/// JSON form of a <see cref="FuzzyInferenceSystem"/>: camelCase properties, enums as
/// strings, membership functions tagged by <c>"type"</c> (triangular | trapezoidal | gaussian).
/// </summary>
public static class FisJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters           = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(FuzzyInferenceSystem fis) => JsonSerializer.Serialize(fis, Options);

    /// <summary>Parses and validates a system; throws <see cref="JsonException"/> or <see cref="InvalidOperationException"/> on bad input.</summary>
    public static FuzzyInferenceSystem Deserialize(string json)
    {
        var fis = JsonSerializer.Deserialize<FuzzyInferenceSystem>(json, Options)
                  ?? throw new JsonException("Empty FIS document.");
        fis.Validate();
        return fis;
    }
}
