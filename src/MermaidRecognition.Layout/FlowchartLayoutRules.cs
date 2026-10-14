using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace MermaidRecognition.Layout;

/// <summary>Reads layout score parameters from the embedded canonical rules.yaml.</summary>
internal static class FlowchartLayoutRules
{
    private static readonly IReadOnlyDictionary<string, double> _parameters = LoadParameters();

    internal static double BendScore => Get("score_bend");
    internal static double CrossingScore => Get("score_crossing");
    internal static double DisconnectedEndScore => Get("score_disconnected_end");
    internal static double TargetAspectWidth => Get("target_aspect_width");
    internal static double TargetAspectHeight => Get("target_aspect_height");
    internal static double ShapePoints => Get("score_shape_points");
    internal static double ShapeStep => Get("score_shape_step");
    internal static double EmptyPointsLow => Get("score_empty_points_low");
    internal static double EmptyThreshold => Get("score_empty_threshold");
    internal static double EmptyPointsHigh => Get("score_empty_points_high");
    internal static double NodeGroupHintScore => Get("score_hint_node_group");
    internal static double LineHintScore => Get("score_hint_line");
    internal static double CrossEndpointStandoff => Get("cross_endpoint_standoff");
    internal static double GroupMemberMinimumInset => Get("group_member_min_inset") * Get("grid_step");

    private static double Get(string Id) => _parameters.TryGetValue(Id, out double Value)
        ? Value
        : throw new InvalidDataException("rules.yaml is missing score parameter '" + Id + "'.");

    private static IReadOnlyDictionary<string, double> LoadParameters()
    {
        Assembly Assembly = typeof(FlowchartLayoutRules).Assembly;
        using Stream Stream = Assembly.GetManifestResourceStream("MermaidRecognition.Layout.rules.yaml")
            ?? throw new InvalidOperationException("The canonical docs/Instructions/rules.yaml resource is missing.");
        using var Reader = new StreamReader(Stream);
        var Values = new Dictionary<string, double>(StringComparer.Ordinal);
        string? CurrentId = null;
        while (Reader.ReadLine() is { } Line)
        {
            string Trimmed = Line.Trim();
            if (Trimmed.StartsWith("- id: \"", StringComparison.Ordinal)
                && Trimmed.EndsWith('\"'))
            {
                CurrentId = Trimmed[7..^1];
                continue;
            }
            if (CurrentId == null || !Trimmed.StartsWith("value:", StringComparison.Ordinal)) continue;
            string Text = Trimmed[6..].Trim();
            if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double Value))
                throw new InvalidDataException("Invalid numeric value for rules.yaml parameter '" + CurrentId + "'.");
            if (!Values.TryAdd(CurrentId, Value))
                throw new InvalidDataException("rules.yaml contains duplicate parameter '" + CurrentId + "'.");
            CurrentId = null;
        }
        return Values;
    }
}
