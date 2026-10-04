using System;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Resolves outward normals on diamond (rhombus) edge midpoints for perpendicular terminal stubs.
/// </summary>
public static class FlowchartDiamondAttachHelper
{
    private const double EDGE_DISTANCE_TOLERANCE_PX = 3.0;
    private const double VERTEX_DISTANCE_TOLERANCE_PX = 1.0;

    /// <summary>
    /// Returns true when <paramref name="Shape"/> is a decision diamond.
    /// </summary>
    public static bool IsDiamondShape(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return false;
        }

        string Normalised = Shape.Trim();
        return string.Equals(Normalised, "diamond", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Normalised, "rhombus", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the outward unit normal at an attach point on a diamond boundary.
    /// </summary>
    public static bool TryResolveOutwardNormal(
        FlowchartLayoutNodeBounds Bounds,
        double AttachX,
        double AttachY,
        out double NormalX,
        out double NormalY)
    {
        NormalX = 0.0;
        NormalY = 0.0;

        double HalfWidth = Bounds.Width / 2.0;
        double HalfHeight = Bounds.Height / 2.0;
        double CenterX = Bounds.CenterX;
        double CenterY = Bounds.CenterY;

        double TopX = CenterX;
        double TopY = CenterY + HalfHeight;
        double RightX = CenterX + HalfWidth;
        double RightY = CenterY;
        double BottomX = CenterX;
        double BottomY = CenterY - HalfHeight;
        double LeftX = CenterX - HalfWidth;
        double LeftY = CenterY;

        if (TryResolveVertexAxisNormal(AttachX, AttachY, TopX, TopY, 0.0, 1.0, out NormalX, out NormalY)
            || TryResolveVertexAxisNormal(AttachX, AttachY, BottomX, BottomY, 0.0, -1.0, out NormalX, out NormalY)
            || TryResolveVertexAxisNormal(AttachX, AttachY, LeftX, LeftY, -1.0, 0.0, out NormalX, out NormalY)
            || TryResolveVertexAxisNormal(AttachX, AttachY, RightX, RightY, 1.0, 0.0, out NormalX, out NormalY))
        {
            return true;
        }

        double BestDistance = double.PositiveInfinity;
        double BestNormalX = 0.0;
        double BestNormalY = 0.0;

        ConsiderDiamondEdge(
            CenterX,
            CenterY,
            AttachX,
            AttachY,
            TopX,
            TopY,
            RightX,
            RightY,
            ref BestDistance,
            ref BestNormalX,
            ref BestNormalY);
        ConsiderDiamondEdge(
            CenterX,
            CenterY,
            AttachX,
            AttachY,
            RightX,
            RightY,
            BottomX,
            BottomY,
            ref BestDistance,
            ref BestNormalX,
            ref BestNormalY);
        ConsiderDiamondEdge(
            CenterX,
            CenterY,
            AttachX,
            AttachY,
            BottomX,
            BottomY,
            LeftX,
            LeftY,
            ref BestDistance,
            ref BestNormalX,
            ref BestNormalY);
        ConsiderDiamondEdge(
            CenterX,
            CenterY,
            AttachX,
            AttachY,
            LeftX,
            LeftY,
            TopX,
            TopY,
            ref BestDistance,
            ref BestNormalX,
            ref BestNormalY);

        if (double.IsPositiveInfinity(BestDistance) || BestDistance > EDGE_DISTANCE_TOLERANCE_PX)
        {
            return false;
        }

        double Length = Math.Sqrt((BestNormalX * BestNormalX) + (BestNormalY * BestNormalY));
        if (Length < 1e-6)
        {
            return false;
        }

        NormalX = BestNormalX / Length;
        NormalY = BestNormalY / Length;
        return true;
    }

    /// <summary>
    /// Returns the outward normal for an exit ray from the diamond centre toward (<paramref name="DirX"/>, <paramref name="DirY"/>).
    /// </summary>
    public static (double NormalX, double NormalY) ResolveOutwardNormalFromDirection(
        FlowchartLayoutNodeBounds Bounds,
        double DirX,
        double DirY)
    {
        (double ExitX, double ExitY) = FlowchartShapeLayoutHelper.RayExitShape(
            Bounds.CenterX,
            Bounds.CenterY,
            Bounds.Width,
            Bounds.Height,
            "diamond",
            DirX,
            DirY);

        if (TryResolveOutwardNormal(Bounds, ExitX, ExitY, out double NormalX, out double NormalY))
        {
            return (NormalX, NormalY);
        }

        double Length = Math.Sqrt((DirX * DirX) + (DirY * DirY));
        if (Length < 1e-6)
        {
            return (0.0, 1.0);
        }

        return (DirX / Length, DirY / Length);
    }

    /// <summary>
    /// Returns the axis direction through a diamond vertex when the attach point sits on that vertex.
    /// A connector at an apex must leave along the bisector of the two meeting sides, not along one slope's normal.
    /// </summary>
    private static bool TryResolveVertexAxisNormal(
        double AttachX,
        double AttachY,
        double VertexX,
        double VertexY,
        double AxisX,
        double AxisY,
        out double NormalX,
        out double NormalY)
    {
        NormalX = 0.0;
        NormalY = 0.0;

        double DeltaX = AttachX - VertexX;
        double DeltaY = AttachY - VertexY;
        if ((DeltaX * DeltaX) + (DeltaY * DeltaY) > VERTEX_DISTANCE_TOLERANCE_PX * VERTEX_DISTANCE_TOLERANCE_PX)
        {
            return false;
        }

        NormalX = AxisX;
        NormalY = AxisY;
        return true;
    }

    private static void ConsiderDiamondEdge(
        double CenterX,
        double CenterY,
        double AttachX,
        double AttachY,
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        ref double BestDistance,
        ref double BestNormalX,
        ref double BestNormalY)
    {
        double EdgeX = EndX - StartX;
        double EdgeY = EndY - StartY;
        double EdgeLengthSquared = (EdgeX * EdgeX) + (EdgeY * EdgeY);
        if (EdgeLengthSquared < 1e-6)
        {
            return;
        }

        double Parameter = (((AttachX - StartX) * EdgeX) + ((AttachY - StartY) * EdgeY)) / EdgeLengthSquared;
        if (Parameter < 0.0 || Parameter > 1.0)
        {
            return;
        }

        double ClosestX = StartX + (Parameter * EdgeX);
        double ClosestY = StartY + (Parameter * EdgeY);
        double Distance = Math.Sqrt(
            ((AttachX - ClosestX) * (AttachX - ClosestX))
            + ((AttachY - ClosestY) * (AttachY - ClosestY)));

        if (Distance >= BestDistance)
        {
            return;
        }

        double MidX = (StartX + EndX) / 2.0;
        double MidY = (StartY + EndY) / 2.0;
        double ToCenterX = CenterX - MidX;
        double ToCenterY = CenterY - MidY;

        double CandidateNormalAX = EdgeY;
        double CandidateNormalAY = -EdgeX;
        double CandidateNormalBX = -EdgeY;
        double CandidateNormalBY = EdgeX;

        double DotA = (CandidateNormalAX * ToCenterX) + (CandidateNormalAY * ToCenterY);
        double DotB = (CandidateNormalBX * ToCenterX) + (CandidateNormalBY * ToCenterY);

        if (DotA < DotB)
        {
            BestNormalX = CandidateNormalAX;
            BestNormalY = CandidateNormalAY;
        }
        else
        {
            BestNormalX = CandidateNormalBX;
            BestNormalY = CandidateNormalBY;
        }

        BestDistance = Distance;
    }
}
