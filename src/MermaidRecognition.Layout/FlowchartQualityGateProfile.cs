using System;

namespace MermaidRecognition.Layout;

/// <summary>
/// Options for the one shared rendered-SVG quality gate. Profiles pick parse and
/// style rules; they do not create a second gate.
/// </summary>
public sealed class FlowchartQualityGateProfile
{
    private FlowchartQualityGateProfile(
        string Name,
        bool KeepFirstRelationshipConnector,
        bool SkipFilledRelationshipMarkers,
        bool IgnoreGroupChromeStrokes,
        bool TreatDashedAndSolidAsOneStyle)
    {
        this.Name = Name;
        this.KeepFirstRelationshipConnector = KeepFirstRelationshipConnector;
        this.SkipFilledRelationshipMarkers = SkipFilledRelationshipMarkers;
        this.IgnoreGroupChromeStrokes = IgnoreGroupChromeStrokes;
        this.TreatDashedAndSolidAsOneStyle = TreatDashedAndSolidAsOneStyle;
    }

    /// <summary>
    /// Flowchart paint: one path per edge, keep the longest sample.
    /// </summary>
    public static FlowchartQualityGateProfile Flowchart { get; } = new FlowchartQualityGateProfile(
        "flowchart",
        KeepFirstRelationshipConnector: false,
        SkipFilledRelationshipMarkers: false,
        IgnoreGroupChromeStrokes: false,
        TreatDashedAndSolidAsOneStyle: false);

    /// <summary>
    /// ArchiMate paint: first connector path is the edge; filled markers and group
    /// chrome are decorations, not strokes. Dashed and solid orthogonal runs are
    /// one right-angle style.
    /// </summary>
    public static FlowchartQualityGateProfile ArchiMate { get; } = new FlowchartQualityGateProfile(
        "archimate",
        KeepFirstRelationshipConnector: true,
        SkipFilledRelationshipMarkers: true,
        IgnoreGroupChromeStrokes: true,
        TreatDashedAndSolidAsOneStyle: true);

    /// <summary>
    /// Profile name used in reports.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// When true, keep the first open connector in a relationship group.
    /// </summary>
    public bool KeepFirstRelationshipConnector { get; }

    /// <summary>
    /// When true, skip filled or closed marker paths in a relationship group.
    /// </summary>
    public bool SkipFilledRelationshipMarkers { get; }

    /// <summary>
    /// When true, ignore group-tab chrome that is not an edge.
    /// </summary>
    public bool IgnoreGroupChromeStrokes { get; }

    /// <summary>
    /// When true, dashed and solid orthogonal runs are one right-angle style.
    /// </summary>
    public bool TreatDashedAndSolidAsOneStyle { get; }

    /// <summary>
    /// Detects the paint profile from SVG markup.
    /// </summary>
    /// <param name="Svg">Rendered SVG.</param>
    /// <returns>ArchiMate when relationship or element classes are present.</returns>
    public static FlowchartQualityGateProfile Detect(string Svg)
    {
        if (string.IsNullOrWhiteSpace(Svg))
        {
            return Flowchart;
        }

        if (Svg.Contains("class=\"relationship", StringComparison.OrdinalIgnoreCase)
            || Svg.Contains("class='relationship", StringComparison.OrdinalIgnoreCase)
            || Svg.Contains("application-component", StringComparison.OrdinalIgnoreCase)
            || Svg.Contains("class=\"element ", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiMate;
        }

        return Flowchart;
    }
}
