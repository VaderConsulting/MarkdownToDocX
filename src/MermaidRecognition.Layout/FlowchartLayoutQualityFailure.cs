using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// One layout or routing defect found by the shared router or the rendered-SVG quality gate.
/// </summary>
public sealed class FlowchartLayoutQualityFailure
{
    /// <summary>
    /// Creates a quality failure.
    /// </summary>
    /// <param name="Kind">Stable kind token.</param>
    /// <param name="Description">Human-readable description.</param>
    /// <param name="InvolvedIds">Node or edge ids involved in the defect.</param>
    /// <param name="Justification">Why the defect could not be avoided, or null when unjustified.</param>
    public FlowchartLayoutQualityFailure(
        string Kind,
        string Description,
        IReadOnlyList<string> InvolvedIds,
        string? Justification)
    {
        this.Kind = Kind;
        this.Description = Description;
        this.InvolvedIds = InvolvedIds;
        this.Justification = Justification;
    }

    /// <summary>
    /// Stable kind token such as <c>NodeIntersection</c> or <c>EdgeCrossing</c>.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Node or edge ids involved in the defect.
    /// </summary>
    public IReadOnlyList<string> InvolvedIds { get; }

    /// <summary>
    /// Why the defect could not be avoided, or null when the defect is not justified.
    /// </summary>
    public string? Justification { get; }

    /// <summary>
    /// True when a search-backed reason explains why no failure-free option exists.
    /// </summary>
    public bool IsJustified
    {
        get
        {
            return !string.IsNullOrWhiteSpace(Justification);
        }
    }
}

/// <summary>
/// Quality-gate report for one rendered diagram.
/// </summary>
public sealed class FlowchartRenderedSvgQualityReport
{
    /// <summary>
    /// Creates a report.
    /// </summary>
    /// <param name="DiagramName">Fixture or sample name.</param>
    public FlowchartRenderedSvgQualityReport(string DiagramName)
    {
        this.DiagramName = DiagramName;
    }

    /// <summary>
    /// Fixture or sample name.
    /// </summary>
    public string DiagramName { get; }

    /// <summary>
    /// Defects that have no justified reason and therefore fail the gate.
    /// </summary>
    public List<FlowchartLayoutQualityFailure> Failures { get; } = [];

    /// <summary>
    /// Defects accepted only because a search showed they are unavoidable.
    /// </summary>
    public List<FlowchartLayoutQualityFailure> Tolerated { get; } = [];

    /// <summary>
    /// True when every remaining defect has a justified reason.
    /// </summary>
    public bool Passes
    {
        get
        {
            return Failures.Count == 0;
        }
    }
}
