using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Builds horizontal-flow guide polylines per Mermaid spec §15 and Naiad <c>FlowchartRenderer</c> (F33 LR fan).
/// </summary>
public static class FlowchartHorizontalRouteGuideBuilder
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    private const double PARALLEL_BRANCH_COLUMN_ALIGNMENT_TOLERANCE_PX = 8.0;

    private const double PARALLEL_BRANCH_CORRIDOR_MARGIN_PX = 16.0;

    /// <summary>
    /// F33 LR side fan: <c>(P0, (midX, P0.Y), (midX, P3.Y), P3)</c>.
    /// </summary>
    public static List<(double X, double Y)> BuildHorizontalFanSRouteGuidedPoints(
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        double MidX = (StartX + EndX) / 2.0;
        if (Math.Abs(EndY - StartY) < COLLINEAR_EPSILON_PX)
        {
            return
            [
                (StartX, StartY),
                (MidX, StartY),
                (EndX, EndY)
            ];
        }

        return
        [
            (StartX, StartY),
            (MidX, StartY),
            (MidX, EndY),
            (EndX, EndY)
        ];
    }

    /// <summary>
    /// F33 LR fan-out: upper / centre / lower slots on the outward source face.
    /// </summary>
    public static (double X, double Y) ResolveLrFanOutSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        int BranchIndex,
        int BranchCount,
        bool RightToLeft,
        EdgeEndpointMarker SourceEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = SourceBounds.Width / 2.0;
        double HalfHeight = SourceBounds.Height / 2.0;
        double CentreX = SourceBounds.CenterX;
        double CentreY = SourceBounds.CenterY;
        double TopEdgeY = CentreY + HalfHeight;
        double Span = 2.0 * HalfHeight;
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);

        double AttachY = ResolveInboardFaceSlotY(TopEdgeY, Span, BranchIndex, BranchCount);
        double FaceX = RightToLeft
            ? CentreX - HalfWidth - SourceGapPx
            : CentreX + HalfWidth + SourceGapPx;

        return (FaceX, AttachY);
    }

    /// <summary>
    /// F33 LR fan-out target: centre of the inward face.
    /// </summary>
    public static (double X, double Y) ResolveLrFanOutTargetAttach(
        FlowchartLayoutNodeBounds TargetBounds,
        bool RightToLeft,
        EdgeEndpointMarker TargetEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = TargetBounds.Width / 2.0;
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
        double FaceX = RightToLeft
            ? TargetBounds.CenterX + HalfWidth + TargetGapPx
            : TargetBounds.CenterX - HalfWidth - TargetGapPx;

        return (FaceX, TargetBounds.CenterY);
    }

    /// <summary>
    /// F33 LR fan-in source: centre of the outward face.
    /// </summary>
    public static (double X, double Y) ResolveLrFanInSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        bool RightToLeft,
        EdgeEndpointMarker SourceEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = SourceBounds.Width / 2.0;
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);
        double FaceX = RightToLeft
            ? SourceBounds.CenterX - HalfWidth - SourceGapPx
            : SourceBounds.CenterX + HalfWidth + SourceGapPx;

        return (FaceX, SourceBounds.CenterY);
    }

    /// <summary>
    /// F33 LR fan-in target: upper / centre / lower slots on the inward face.
    /// </summary>
    public static (double X, double Y) ResolveLrFanInTargetAttach(
        FlowchartLayoutNodeBounds TargetBounds,
        int BranchIndex,
        int BranchCount,
        bool RightToLeft,
        EdgeEndpointMarker TargetEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = TargetBounds.Width / 2.0;
        double HalfHeight = TargetBounds.Height / 2.0;
        double TopEdgeY = TargetBounds.CenterY + HalfHeight;
        double Span = 2.0 * HalfHeight;
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
        double AttachY = ResolveInboardFaceSlotY(TopEdgeY, Span, BranchIndex, BranchCount);
        double FaceX = RightToLeft
            ? TargetBounds.CenterX + HalfWidth + TargetGapPx
            : TargetBounds.CenterX - HalfWidth - TargetGapPx;

        return (FaceX, AttachY);
    }

    /// <summary>
    /// F113 LR parallel branch column: source right face at branch Y, target left face at matching Y.
    /// </summary>
    public static (double X, double Y) ResolveParallelBranchColumnSourceAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        bool RightToLeft,
        EdgeEndpointMarker SourceEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = SourceBounds.Width / 2.0;
        double SourceGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(SourceEndpointMarker);
        double FaceX = RightToLeft
            ? SourceBounds.CenterX - HalfWidth - SourceGapPx
            : SourceBounds.CenterX + HalfWidth + SourceGapPx;

        return (FaceX, SourceBounds.CenterY);
    }

    /// <summary>
    /// F113 LR parallel branch column: inbound attach on the target left face at the source row.
    /// </summary>
    public static (double X, double Y) ResolveParallelBranchColumnTargetAttach(
        FlowchartLayoutNodeBounds SourceBounds,
        FlowchartLayoutNodeBounds TargetBounds,
        bool RightToLeft,
        EdgeEndpointMarker TargetEndpointMarker = EdgeEndpointMarker.None)
    {
        double HalfWidth = TargetBounds.Width / 2.0;
        double HalfHeight = TargetBounds.Height / 2.0;
        double TargetGapPx = FlowchartEdgeAttachConstants.GetExteriorGapPx(TargetEndpointMarker);
        double FaceX = RightToLeft
            ? TargetBounds.CenterX + HalfWidth + TargetGapPx
            : TargetBounds.CenterX - HalfWidth - TargetGapPx;
        double ClampedY = Math.Max(
            TargetBounds.CenterY - HalfHeight,
            Math.Min(TargetBounds.CenterY + HalfHeight, SourceBounds.CenterY));

        return (FaceX, ClampedY);
    }

    /// <summary>
    /// Returns an exterior corridor X to the outboard side of a stacked source column.
    /// </summary>
    public static double ResolveParallelBranchColumnCorridorX(
        IReadOnlyList<FlowchartLayoutNodeBounds> SourceBoundsList,
        FlowchartDirection Direction)
    {
        double MaxStubLength = 0.0;
        if (Direction == FlowchartDirection.LeftToRight)
        {
            double MaxRightEdge = double.NegativeInfinity;
            foreach (FlowchartLayoutNodeBounds SourceBounds in SourceBoundsList)
            {
                MaxStubLength = Math.Max(
                    MaxStubLength,
                    FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds));
                MaxRightEdge = Math.Max(MaxRightEdge, SourceBounds.CenterX + (SourceBounds.Width / 2.0));
            }

            return MaxRightEdge + MaxStubLength + PARALLEL_BRANCH_CORRIDOR_MARGIN_PX;
        }

        double MinLeftEdge = double.PositiveInfinity;
        foreach (FlowchartLayoutNodeBounds SourceBounds in SourceBoundsList)
        {
            MaxStubLength = Math.Max(
                MaxStubLength,
                FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(SourceBounds));
            MinLeftEdge = Math.Min(MinLeftEdge, SourceBounds.CenterX - (SourceBounds.Width / 2.0));
        }

        return MinLeftEdge - MaxStubLength - PARALLEL_BRANCH_CORRIDOR_MARGIN_PX;
    }

    /// <summary>
    /// Appends boundary → stub → exterior corridor → stub → boundary for stacked parallel fan-in.
    /// </summary>
    public static void AppendParallelBranchColumnFanInRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double CorridorX)
    {
        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));

        if (Math.Abs(CorridorX - SourceStubX) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((CorridorX, SourceStubY));
        }

        if (Math.Abs(TargetStubY - SourceStubY) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((CorridorX, TargetStubY));
        }

        if (Math.Abs(TargetStubX - CorridorX) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((TargetStubX, TargetStubY));
        }

        Points.Add((TargetX, TargetY));
    }

    /// <summary>
    /// Returns true when many same-column LR sources fan into one target to the right.
    /// </summary>
    public static bool IsParallelBranchColumnFanIn(
        IReadOnlyList<EdgeContract> Incoming,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartLayoutNodeBounds TargetBounds,
        FlowchartDirection Direction)
    {
        if (Incoming.Count <= 3)
        {
            return false;
        }

        if (Direction != FlowchartDirection.LeftToRight
            && Direction != FlowchartDirection.RightToLeft)
        {
            return false;
        }

        bool RightToLeft = Direction == FlowchartDirection.RightToLeft;
        double ReferenceCenterX = double.NaN;
        foreach (EdgeContract IncomingEdge in Incoming)
        {
            if (!BoundsById.TryGetValue(IncomingEdge.SourceId, out FlowchartLayoutNodeBounds SourceBounds))
            {
                return false;
            }

            if (RightToLeft)
            {
                if (SourceBounds.CenterX <= TargetBounds.CenterX + COLLINEAR_EPSILON_PX)
                {
                    return false;
                }
            }
            else if (SourceBounds.CenterX >= TargetBounds.CenterX - COLLINEAR_EPSILON_PX)
            {
                return false;
            }

            if (double.IsNaN(ReferenceCenterX))
            {
                ReferenceCenterX = SourceBounds.CenterX;
            }
            else if (Math.Abs(SourceBounds.CenterX - ReferenceCenterX) > PARALLEL_BRANCH_COLUMN_ALIGNMENT_TOLERANCE_PX)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// F34 LR decision fan-out: outward bow using the target leg (|P3.Y − hub|).
    /// </summary>
    public static (double X, double Y) BuildHorizontalDecisionBranchGuidePoint(
        double HubCentreY,
        double SourceX,
        double SourceY,
        double TargetX,
        double TargetY,
        FlowchartDecisionPortRole Role)
    {
        double MidX = (SourceX + TargetX) / 2.0;
        if (Math.Abs(TargetY - SourceY) < COLLINEAR_EPSILON_PX)
        {
            return (MidX, SourceY);
        }

        double Spread = Math.Abs(TargetY - HubCentreY);
        double GuideY;
        if (SourceY < HubCentreY - COLLINEAR_EPSILON_PX)
        {
            GuideY = HubCentreY - Spread;
        }
        else if (SourceY > HubCentreY + COLLINEAR_EPSILON_PX)
        {
            GuideY = HubCentreY + Spread;
        }
        else
        {
            GuideY = TargetY;
        }

        return (MidX, GuideY);
    }

    /// <summary>
    /// Appends boundary → source stub → horizontal decision guide → target stub → boundary (F34).
    /// </summary>
    public static void AppendHorizontalDecisionBranchRoute(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape,
        double HubCentreY,
        FlowchartDecisionPortRole Role)
    {
        double CorridorY = TargetBounds.CenterY;
        bool LevelTarget = Math.Abs(TargetBounds.CenterY - HubCentreY) <= COLLINEAR_EPSILON_PX
            || Role == FlowchartDecisionPortRole.BottomVertex;

        Points.Add((SourceX, SourceY));
        if (LevelTarget)
        {
            Points.Add((TargetX, HubCentreY));
            return;
        }

        if (Math.Abs(SourceY - CorridorY) > COLLINEAR_EPSILON_PX)
        {
            Points.Add((SourceX, CorridorY));
        }

        Points.Add((TargetX, CorridorY));
    }

    /// <summary>
    /// Appends boundary → source stub → horizontal S corridor → target stub → boundary.
    /// </summary>
    public static void AppendHorizontalFanSRouteWithStubs(
        List<(double X, double Y)> Points,
        double SourceX,
        double SourceY,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        double TargetX,
        double TargetY,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape)
    {
        (double SourceStubX, double SourceStubY) = ComputeStubOuterPoint(
            SourceX,
            SourceY,
            SourceBounds,
            SourceShape);
        (double TargetStubX, double TargetStubY) = ComputeStubOuterPoint(
            TargetX,
            TargetY,
            TargetBounds,
            TargetShape);

        double MidX = (SourceStubX + TargetStubX) / 2.0;

        Points.Add((SourceX, SourceY));
        Points.Add((SourceStubX, SourceStubY));

        if (Math.Abs(TargetStubY - SourceStubY) < COLLINEAR_EPSILON_PX)
        {
            Points.Add((MidX, SourceStubY));
        }
        else
        {
            Points.Add((MidX, SourceStubY));
            Points.Add((MidX, TargetStubY));
        }

        Points.Add((TargetStubX, TargetStubY));
        Points.Add((TargetX, TargetY));
    }

    private static double ResolveInboardFaceSlotY(
        double TopEdgeY,
        double Span,
        int BranchIndex,
        int BranchCount)
    {
        if (BranchCount >= 3 && BranchIndex == 1)
        {
            return TopEdgeY - (Span / 2.0);
        }

        if (BranchIndex == 0)
        {
            return TopEdgeY - (Span * (1.0 / 6.0));
        }

        return TopEdgeY - (Span * (5.0 / 6.0));
    }

    private static (double X, double Y) ComputeStubOuterPoint(
        double BoundaryX,
        double BoundaryY,
        FlowchartLayoutNodeBounds Bounds,
        string? Shape)
    {
        double StubLength = FlowchartEdgeTerminalStubExpander.ComputeTerminalStubLength(Bounds);

        if (FlowchartDiamondAttachHelper.IsDiamondShape(Shape)
            && FlowchartDiamondAttachHelper.TryResolveOutwardNormal(
                Bounds,
                BoundaryX,
                BoundaryY,
                out double NormalX,
                out double NormalY))
        {
            return (BoundaryX + (NormalX * StubLength), BoundaryY + (NormalY * StubLength));
        }

        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            Bounds,
            BoundaryX,
            BoundaryY);

        switch (Face)
        {
            case FlowchartEdgeAttachFace.Top:
                return (BoundaryX, BoundaryY + StubLength);

            case FlowchartEdgeAttachFace.Bottom:
                return (BoundaryX, BoundaryY - StubLength);

            case FlowchartEdgeAttachFace.Left:
                return (BoundaryX - StubLength, BoundaryY);

            case FlowchartEdgeAttachFace.Right:
                return (BoundaryX + StubLength, BoundaryY);

            default:
                return (BoundaryX + StubLength, BoundaryY);
        }
    }
}
