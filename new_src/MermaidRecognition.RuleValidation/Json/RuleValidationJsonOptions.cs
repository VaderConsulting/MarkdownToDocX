using System.Text.Json;
using System.Text.Json.Serialization;

namespace MermaidRecognition.RuleValidation.Json;

/// <summary>
/// Shared JSON serialisation options for rule-validation artefacts.
/// </summary>
public static class RuleValidationJsonOptions
{
    private static readonly JsonSerializerOptions _Options = CreateOptions();

    /// <summary>
    /// Serialiser options with PascalCase property names matching the specification artefacts.
    /// </summary>
    public static JsonSerializerOptions Options
    {
        get
        {
            return _Options;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return Options;
    }
}
