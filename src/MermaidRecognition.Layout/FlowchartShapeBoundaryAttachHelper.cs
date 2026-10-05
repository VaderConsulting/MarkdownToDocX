using System;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves edge attach coordinates on non-rectangular node boundaries using shape ray exit.
/// </summary>
public static class FlowchartShapeBoundaryAttachHelper
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Returns an inbound attach on the target boundary facing the source, with a small exterior gap only for cross markers.
    /// </summary>
    public static bool TryResolveInboundAttach(
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double SourceX,
        double SourceY,
        EdgeEndpointMarker TargetEndpointMarker,
        out double AttachX,
        out double AttachY)
    {
        AttachX = 0.0;
        AttachY = 0.0;

        double DirX = SourceX - TargetBounds.CenterX;
        double DirY = SourceY - TargetBounds.CenterY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return false;
        }

        DirX /= Length;
        DirY /= Length;

        (double ExitX, double ExitY) = FlowchartShapeLayoutHelper.RayExitShape(
            TargetBounds.CenterX,
            TargetBounds.CenterY,
            TargetBounds.Width,
            TargetBounds.Height,
            TargetShape,
            DirX,
            DirY);

        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
        AttachX = ExitX + (DirX * GapPx);
        AttachY = ExitY + (DirY * GapPx);
        return true;
    }

    /// <summary>
    /// Returns an outbound attach on the source boundary toward the target, with a small exterior gap only for cross markers.
    /// </summary>
    public static bool TryResolveOutboundAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        EdgeEndpointMarker SourceEndpointMarker,
        out double AttachX,
        out double AttachY)
    {
        AttachX = 0.0;
        AttachY = 0.0;

        double DirX = TargetX - SourceBounds.CenterX;
        double DirY = TargetY - SourceBounds.CenterY;
        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return false;
        }

        DirX /= Length;
        DirY /= Length;

        (double ExitX, double ExitY) = FlowchartShapeLayoutHelper.RayExitShape(
            SourceBounds.CenterX,
            SourceBounds.CenterY,
            SourceBounds.Width,
            SourceBounds.Height,
            SourceShape,
            DirX,
            DirY);

        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);
        AttachX = ExitX + (DirX * GapPx);
        AttachY = ExitY + (DirY * GapPx);
        return true;
    }

    /// <summary>
    /// Clips an approach point onto the visible outline of the node. Every shape uses this helper.
    /// </summary>
    /// <param name="Bounds">Node layout box.</param>
    /// <param name="Shape">Mermaid shape name.</param>
    /// <param name="TowardX">The neighbouring route point facing this node.</param>
    /// <param name="TowardY">The neighbouring route point facing this node.</param>
    /// <param name="Marker">Endpoint marker; only a cross keeps an exterior gap.</param>
    /// <returns>The outline attach point.</returns>
    public static (double X, double Y) ClipToShapeOutline(
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double TowardX,
        double TowardY,
        EdgeEndpointMarker Marker)
    {
        if (TryResolveInboundAttach(Bounds, Shape, TowardX, TowardY, Marker, out double AttachX, out double AttachY))
        {
            return (AttachX, AttachY);
        }

        if (TryResolveOutboundAttach(Bounds, Shape, TowardX, TowardY, Marker, out AttachX, out AttachY))
        {
            return (AttachX, AttachY);
        }

        (double ExitX, double ExitY) = FlowchartShapeLayoutHelper.RayExitShape(
            Bounds.CenterX,
            Bounds.CenterY,
            Bounds.Width,
            Bounds.Height,
            Shape,
            0.0,
            -1.0);
        double GapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(Marker);
        return (ExitX, ExitY - GapPx);
    }

    /// <summary>
    /// Returns true when source and target share the spine column within tolerance.
    /// </summary>
    public static bool AreSpineColumnAligned(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        double TolerancePx = 30.0) => Math.Abs(SourceBounds.CenterX - TargetBounds.CenterX) <= TolerancePx;

    /// <summary>
    /// Returns true when the source is vertically above the target in TB layout space.
    /// </summary>
    public static bool IsSourceAboveTarget(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds) => SourceBounds.CenterY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX;
}
