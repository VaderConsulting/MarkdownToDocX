using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace MermaidRecognition.Layout;

/// <summary>Reads layout score parameters from the embedded canonical rules.yaml.</summary>
internal static class FlowchartLayoutRules
{
    /// <summary>Approved candidate-derived expressions; all other parameters remain numeric constants.</summary>
    private static readonly Dictionary<string, string> _expressions = new(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, double> _parameters = LoadParameters();

    internal static double BendScore => Get("score_bend");
    /// <summary>Fixed configured width of one logical cell, independent of diagram labels.</summary>
    internal static double BaseCellWidth => Get("base_cell_width");
    /// <summary>Fixed configured height of one logical cell, independent of diagram labels.</summary>
    internal static double BaseCellHeight => Get("base_cell_height");
    /// <summary>Configured minimum node text inset in pixels.</summary>
    internal static double NodeTextPadding => Get("node_text_padding");
    /// <summary>Initial row gap in pixels, before measured clearance growth.</summary>
    internal static double BaseRowGap => Get("base_rank_gap") * Get("grid_step");
    /// <summary>Initial column gap in pixels, before measured clearance growth.</summary>
    internal static double BaseColumnGap => Get("base_column_gap") * Get("grid_step");
    /// <summary>Configured track pitch in pixels, independent of the minimum drawn line and port separation.</summary>
    internal static double TrackSpacing => Get("track_spacing") * Get("grid_step");
    /// <summary>Configured minimum parallel-line and same-edge port separation, expressed directly in page pixels.</summary>
    internal static double LineSpacing => Get("line_spacing");
    /// <summary>Float tolerance for shared-track, overlap, port-spacing and clearance comparisons.</summary>
    internal static double GeometryTolerancePx => Get("geometry_tolerance_px");
    /// <summary>Terminal arrowhead length used when sizing 4.4 clearance into a bounding outline.</summary>
    internal static double ArrowheadLengthPx => Get("arrowhead_length_px");
    /// <summary>Approved fractional buffer added to forecast traffic when initial corridor gaps are sized.</summary>
    internal static double CorridorCapacityBuffer => Get("corridor_capacity_buffer");
    internal static double CrossingScore => Get("score_crossing");
    internal static double DisconnectedEndScore => Get("score_disconnected_end");
    internal static double EmptyPointsLow => Get("score_empty_points_low");
    internal static double EmptyThreshold => Get("score_empty_threshold");
    internal static double EmptyPointsHigh => Get("score_empty_points_high");
    internal static double NodeGroupHintScore => Get("score_hint_node_group");
    internal static double LineHintScore => Get("score_hint_line");
    internal static double CrossEndpointStandoff => Get("cross_endpoint_standoff");
    internal static double DiagramUsableWidthPx => Get("diagram_usable_width_px");
    internal static double GroupMemberMinimumInset => Get("group_member_min_inset") * Get("grid_step");
    internal static int RepairCandidateMoves => checked((int)Get("repair_candidate_moves"));
    internal static int RepairChainDepth => checked((int)Get("repair_chain_depth"));
    internal static int RepairChainLines => checked((int)Get("repair_chain_lines"));
    internal static int RepairSearchPatience => checked((int)Get("repair_search_patience"));
    internal static int RepairSearchPatienceStress => checked((int)Get("repair_search_patience_stress"));
    internal static int RepairSearchPatienceStressMinRoutes => checked((int)Get("repair_search_patience_stress_min_routes"));
    /// <summary>Evaluates the approved complete-fan limit without changing the ordinary configured bound.</summary>
    /// <param name="FanLineCount">Number of declared lines in the complete fan being moved atomically.</param>
    /// <returns>The exact fan inventory size, never a fixed replacement for the ordinary limit.</returns>
    /// <exception cref="InvalidDataException">The canonical expression is absent or unsupported.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The supplied inventory is not a fan.</exception>
    internal static int RepairChainLinesFanHub(int FanLineCount)
    {
        if (!_expressions.TryGetValue("repair_chain_lines_fan_hub", out string? Expression) || Expression != "fan_line_count")
        {
            throw new InvalidDataException("rules.yaml must define repair_chain_lines_fan_hub as fan_line_count.");
        }

        if (FanLineCount < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(FanLineCount));
        }

        return FanLineCount;
    }
    internal static double RepairLengthWeight => Get("score_repair_length_per_grid_step");
    /// <summary>Approved routing-length weight before the once-per-layout bounded normalization.</summary>
    internal static double RouteLengthWeight => Get("score_route_length_per_grid_step");
    internal static double RepairMovedLineWeight => Get("score_repair_moved_line");

    private static double Get(string Id) => _parameters.TryGetValue(Id, out double Value)
        ? Value
        : throw new InvalidDataException("rules.yaml is missing score parameter '" + Id + "'.");

    private static IReadOnlyDictionary<string, double> LoadParameters()
    {
        Assembly Assembly = typeof(FlowchartLayoutRules).Assembly;
        using Stream Stream = Assembly.GetManifestResourceStream("MermaidRecognition.Layout.rules.yaml")
            ?? throw new InvalidOperationException("The canonical docs/Instructions/rules.yaml resource is missing.");
        using StreamReader Reader = new StreamReader(Stream);
        Dictionary<string, double> Values = new Dictionary<string, double>(StringComparer.Ordinal);
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
            if (CurrentId == null || !Trimmed.StartsWith("value:", StringComparison.Ordinal))
            {
                continue;
            }

            string Text = Trimmed[6..].Trim();
            if (CurrentId == "repair_chain_lines_fan_hub" && Text == "\"fan_line_count\"")
            {
                if (!_expressions.TryAdd(CurrentId, "fan_line_count"))
                {
                    throw new InvalidDataException("rules.yaml contains duplicate parameter '" + CurrentId + "'.");
                }

                CurrentId = null;
                continue;
            }
            if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double Value))
            {
                throw new InvalidDataException("Invalid numeric value for rules.yaml parameter '" + CurrentId + "'.");
            }

            if (!Values.TryAdd(CurrentId, Value))
            {
                throw new InvalidDataException("rules.yaml contains duplicate parameter '" + CurrentId + "'.");
            }

            CurrentId = null;
        }
        return Values;
    }
}
