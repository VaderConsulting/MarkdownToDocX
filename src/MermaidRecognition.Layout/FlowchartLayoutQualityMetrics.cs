using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Counts of piercings, crossings, and parallel overlaps on a quality report.
/// </summary>
public sealed class FlowchartLayoutQualityMetrics
{
    /// <summary>
    /// Creates metrics from a rendered-SVG quality report. Justified and unjustified defects both count.
    /// </summary>
    /// <param name="Report">Quality report.</param>
    public FlowchartLayoutQualityMetrics(FlowchartRenderedSvgQualityReport Report)
    {
        if (Report == null)
        {
            throw new ArgumentNullException(nameof(Report));
        }

        NodeIntersections = CountKind(Report, "NodeIntersection");
        EdgeCrossings = CountKind(Report, "EdgeCrossing");
        ParallelOverlaps = CountKind(Report, "ParallelSeparation");
        ForeignGroupTraversals = CountKind(Report, "ForeignGroupTraversal")
            + CountKind(Report, "GroupBorderDiagonal")
            + CountKind(Report, "GroupBorderReentry");
        MixedStyle = CountKind(Report, "MixedStyle");
        UnjustifiedFailures = Report.Failures.Count;
        ToleratedFailures = Report.Tolerated.Count;
    }

    /// <summary>
    /// Edges that pierce a non-endpoint node.
    /// </summary>
    public int NodeIntersections { get; }

    /// <summary>
    /// Distinct edge-pair crossings.
    /// </summary>
    public int EdgeCrossings { get; }

    /// <summary>
    /// Parallel runs closer than the gate threshold.
    /// </summary>
    public int ParallelOverlaps { get; }

    /// <summary>
    /// Illegal group-border or foreign-group interior runs.
    /// </summary>
    public int ForeignGroupTraversals { get; }

    /// <summary>
    /// Mixed orthogonal-plus-curve style defects.
    /// </summary>
    public int MixedStyle { get; }

    /// <summary>
    /// Defects with no specific justification.
    /// </summary>
    public int UnjustifiedFailures { get; }

    /// <summary>
    /// Defects accepted with a specific justification.
    /// </summary>
    public int ToleratedFailures { get; }

    /// <summary>
    /// True when this result is no worse than <paramref name="Oracle"/> on piercings, crossings, and parallel overlaps.
    /// </summary>
    /// <param name="Oracle">Hand-made reference metrics.</param>
    /// <returns>True when every compared count is less than or equal to the oracle.</returns>
    public bool IsNoWorseThan(FlowchartLayoutQualityMetrics Oracle)
    {
        if (Oracle == null)
        {
            throw new ArgumentNullException(nameof(Oracle));
        }

        return NodeIntersections <= Oracle.NodeIntersections
            && EdgeCrossings <= Oracle.EdgeCrossings
            && ParallelOverlaps <= Oracle.ParallelOverlaps;
    }

    /// <summary>
    /// Formats the counts for assertions.
    /// </summary>
    /// <returns>A single-line summary.</returns>
    public override string ToString()
    {
        return "piercings="
            + NodeIntersections.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " crossings="
            + EdgeCrossings.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " parallels="
            + ParallelOverlaps.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " foreignGroup="
            + ForeignGroupTraversals.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " mixedStyle="
            + MixedStyle.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " unjustified="
            + UnjustifiedFailures.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " tolerated="
            + ToleratedFailures.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int CountKind(FlowchartRenderedSvgQualityReport Report, string Kind)
    {
        int Count = 0;
        foreach (FlowchartLayoutQualityFailure Failure in Report.Failures)
        {
            if (string.Equals(Failure.Kind, Kind, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        foreach (FlowchartLayoutQualityFailure Tolerated in Report.Tolerated)
        {
            if (string.Equals(Tolerated.Kind, Kind, StringComparison.Ordinal))
            {
                Count++;
            }
        }

        return Count;
    }
}
