using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Detects unobstructed axis-aligned connections between resolved attach points.
/// </summary>
public static class FlowchartEdgeConnectionGeometry
{
    /// <summary>
    /// Tolerance for treating two attach coordinates as sharing a column or row.
    /// </summary>
    public const double ALIGN_TOLERANCE_PX = 0.75;

    /// <summary>
    /// Returns true when attach points share a vertical column or horizontal row.
    /// </summary>
    public static bool AreAttachPointsAxisAligned(
        double SourceAttachX,
        double SourceAttachY,
        double TargetAttachX,
        double TargetAttachY)
    {
        bool SharesColumn = Math.Abs(SourceAttachX - TargetAttachX) <= ALIGN_TOLERANCE_PX;
        bool SharesRow = Math.Abs(SourceAttachY - TargetAttachY) <= ALIGN_TOLERANCE_PX;
        return SharesColumn || SharesRow;
    }

    /// <summary>
    /// Returns true when a connector may render as a single straight segment between attach points.
    /// </summary>
    public static bool ShouldRenderStraightConnector(
        double SourceAttachX,
        double SourceAttachY,
        double TargetAttachX,
        double TargetAttachY) => AreAttachPointsAxisAligned(
            SourceAttachX,
            SourceAttachY,
            TargetAttachX,
            TargetAttachY);

    /// <summary>
    /// Returns true when a four-point stub-expanded guide is a single straight connector between boundaries.
    /// </summary>
    public static bool IsCollinearStubStraightRoute(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count != 4)
        {
            return false;
        }

        return AreAttachPointsAxisAligned(
            Points[0].X,
            Points[0].Y,
            Points[Points.Count - 1].X,
            Points[Points.Count - 1].Y);
    }
}
