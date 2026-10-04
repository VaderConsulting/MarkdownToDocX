using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MermaidRecognition.Layout;

/// <summary>
/// Serialises routing-intent diagnostic results to JSON for automated validation.
/// </summary>
public static class FlowchartRoutingIntentJsonSerializer
{
    private static readonly JsonSerializerOptions _SerializerOptions = CreateOptions();

    /// <summary>
    /// Serialises one diagram routing-intent result to indented JSON.
    /// </summary>
    public static string SerializeDiagram(FlowchartRoutingIntentDiagramResult Result) => JsonSerializer.Serialize(Result, _SerializerOptions);

    /// <summary>
    /// Deserialises one diagram routing-intent result from JSON.
    /// </summary>
    public static FlowchartRoutingIntentDiagramResult DeserializeDiagram(string Json)
    {
        FlowchartRoutingIntentDiagramResult? Result = JsonSerializer.Deserialize<FlowchartRoutingIntentDiagramResult>(
            Json,
            _SerializerOptions);
        if (Result is null)
        {
            throw new JsonException("Routing-intent diagram JSON deserialised to null.");
        }

        return Result;
    }

    /// <summary>
    /// Serialises a ruleset document to indented JSON.
    /// </summary>
    public static string SerializeRuleset(FlowchartRoutingIntentRuleset Ruleset) => JsonSerializer.Serialize(Ruleset, _SerializerOptions);

    /// <summary>
    /// Deserialises a ruleset document from JSON.
    /// </summary>
    public static FlowchartRoutingIntentRuleset DeserializeRuleset(string Json)
    {
        FlowchartRoutingIntentRuleset? Ruleset = JsonSerializer.Deserialize<FlowchartRoutingIntentRuleset>(
            Json,
            _SerializerOptions);
        if (Ruleset is null)
        {
            throw new JsonException("Routing-intent ruleset JSON deserialised to null.");
        }

        return Ruleset;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        Options.Converters.Add(new JsonStringEnumConverter());
        return Options;
    }
}
