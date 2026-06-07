using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Structural edge path construction for native Sugiyama SVG (spec §11.8–11.9).
/// </summary>
public static class FlowchartEdgePathBuilder
{
    private const double LR_RETRY_LOOP_CLEARANCE_PX = 28.0;

    private const double LR_RETRY_LOOP_CORNER_APPROACH_RATIO = 0.4;

    private const double LR_RETRY_LOOP_FACE_STUB_PX = 12.0;

    /// <summary>
    /// Returns true when an LR/RL edge is an unlabelled retry loop (F34).
    /// </summary>
    public static bool IsLrRetryLoopEdge(
        EdgeContract Edge,
        DiagramContract Contract,
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        string? SourceShape,
        string? TargetShape)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            return false;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        if (Direction != FlowchartDirection.LeftToRight
            && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        if (IsDecisionShape(SourceShape) || IsDecisionShape(TargetShape))
        {
            return false;
        }

        const double MIN_BACKWARD_FLOW_PX = 0.75;
        if (Direction == FlowchartDirection.LeftToRight)
        {
            return TargetBounds.CenterX < SourceBounds.CenterX - MIN_BACKWARD_FLOW_PX;
        }

        return TargetBounds.CenterX > SourceBounds.CenterX + MIN_BACKWARD_FLOW_PX;
    }

    /// <summary>
    /// Builds guide points for an LR/RL back-edge loop corridor in layout coordinates (MSAGL Y-up).
    /// Render with <see cref="FlowchartCurvePathBuilder"/> using <c>flowchart.curve</c> (default curveBasis).
    /// </summary>
    public static bool TryBuildLrRetryLoopPath(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        IReadOnlyList<FlowchartLayoutNodeBounds> AllBounds,
        FlowchartDirection Direction,
        out List<(double X, double Y)> LayoutPoints,
        EdgeEndpointMarker MarkerStart = EdgeEndpointMarker.None,
        EdgeEndpointMarker MarkerEnd = EdgeEndpointMarker.None)
    {
        LayoutPoints = new List<(double X, double Y)>();

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        double SourceHalfWidth = SourceBounds.Width / 2.0;
        double TargetHalfHeight = TargetBounds.Height / 2.0;
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(MarkerStart);
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(MarkerEnd);

        double SourceAttachX;
        double SourceAttachY = SourceBounds.CenterY;
        double TargetAttachX = TargetBounds.CenterX;
        double TargetAttachY;

        if (RightToLeft)
        {
            SourceAttachX = SourceBounds.CenterX - SourceHalfWidth - SourceGapPx;
            TargetAttachY = TargetBounds.CenterY + TargetHalfHeight + TargetGapPx;
        }
        else
        {
            SourceAttachX = SourceBounds.CenterX + SourceHalfWidth + SourceGapPx;
            TargetAttachY = TargetBounds.CenterY + TargetHalfHeight + TargetGapPx;
        }

        double TopY = Math.Min(SourceAttachY, TargetAttachY);
        foreach (FlowchartLayoutNodeBounds Bounds in AllBounds)
        {
            double NodeTop = Bounds.CenterY + (Bounds.Height / 2.0);
            if (NodeTop > TopY)
            {
                TopY = NodeTop;
            }
        }

        TopY += LR_RETRY_LOOP_CLEARANCE_PX;

        double ExitCornerY = InterpolateRetryLoopCornerY(SourceAttachY, TopY);
        double EntryCornerY = InterpolateRetryLoopCornerY(TopY, TargetAttachY);
        double SourceStubX = RightToLeft
            ? SourceAttachX - LR_RETRY_LOOP_FACE_STUB_PX
            : SourceAttachX + LR_RETRY_LOOP_FACE_STUB_PX;

        AddLayoutPointIfDistinct(LayoutPoints, SourceAttachX, SourceAttachY);
        AddLayoutPointIfDistinct(LayoutPoints, SourceStubX, SourceAttachY);
        AddLayoutPointIfDistinct(LayoutPoints, SourceStubX, ExitCornerY);
        AddLayoutPointIfDistinct(LayoutPoints, SourceStubX, TopY);
        AddLayoutPointIfDistinct(LayoutPoints, TargetAttachX, TopY);
        AddLayoutPointIfDistinct(LayoutPoints, TargetAttachX, EntryCornerY);
        AddLayoutPointIfDistinct(LayoutPoints, TargetAttachX, TargetAttachY);

        return LayoutPoints.Count >= 2;
    }

    private static double InterpolateRetryLoopCornerY(double FromY, double ToY)
    {
        return FromY + ((ToY - FromY) * LR_RETRY_LOOP_CORNER_APPROACH_RATIO);
    }

    private static void AddLayoutPointIfDistinct(List<(double X, double Y)> Points, double X, double Y)
    {
        if (Points.Count == 0)
        {
            Points.Add((X, Y));
            return;
        }

        (double LastX, double LastY) = Points[Points.Count - 1];
        if (Math.Abs(LastX - X) > 0.75 || Math.Abs(LastY - Y) > 0.75)
        {
            Points.Add((X, Y));
        }
    }

    private static bool IsDecisionShape(string? Shape)
    {
        if (string.IsNullOrEmpty(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }
}
