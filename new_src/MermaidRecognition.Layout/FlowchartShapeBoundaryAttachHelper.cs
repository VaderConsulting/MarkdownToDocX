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
    /// Returns true when source and target share the spine column within tolerance.
    /// </summary>
    public static bool AreSpineColumnAligned(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        double TolerancePx = 30.0)
    {
        return Math.Abs(SourceBounds.CenterX - TargetBounds.CenterX) <= TolerancePx;
    }

    /// <summary>
    /// Returns true when the source is vertically above the target in TB layout space.
    /// </summary>
    public static bool IsSourceAboveTarget(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds)
    {
        return SourceBounds.CenterY > TargetBounds.CenterY + COLLINEAR_EPSILON_PX;
    }
}
