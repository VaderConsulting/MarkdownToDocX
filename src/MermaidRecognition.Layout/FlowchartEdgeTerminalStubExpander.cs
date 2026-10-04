using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Inserts explicit straight terminal stub waypoints outside node boundaries before curve routing.
/// </summary>
public static class FlowchartEdgeTerminalStubExpander
{
    private const double MIN_TERMINAL_STUB_PX = 8.0;

    private const double MAX_TERMINAL_STUB_PX = 12.0;

    private const double TERMINAL_STUB_FRACTION = 0.10;

    private const double DUPLICATE_TOLERANCE_PX = 0.75;

    private const double NORMAL_ALIGNMENT_COSINE = 0.999;

    /// <summary>
    /// Ensures both ends of <paramref name="Points"/> include a straight stub of minimum length along the face normal.
    /// </summary>
    public static List<(double X, double Y)> Apply(
        IReadOnlyList<(double X, double Y)> Points,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape)
    {
        if (Points.Count < 2)
        {
            return new List<(double X, double Y)>(Points);
        }

        List<(double X, double Y)> Expanded = new List<(double X, double Y)>(Points);
        EnsureSourceTerminalStub(Expanded, SourceBounds, SourceShape);
        EnsureTargetTerminalStub(Expanded, TargetBounds, TargetShape);
        return Expanded;
    }

    /// <summary>
    /// Minimum terminal stub length for a node (8-12 px or 10% of the smaller node dimension).
    /// </summary>
    public static double ComputeTerminalStubLength(FlowchartLayoutNodeBounds Bounds)
    {
        double SmallerDimension = Math.Min(Bounds.Width, Bounds.Height);
        double FractionStub = SmallerDimension * TERMINAL_STUB_FRACTION;
        double StubLength = Math.Max(MIN_TERMINAL_STUB_PX, Math.Min(MAX_TERMINAL_STUB_PX, FractionStub));
        return StubLength;
    }

    /// <summary>
    /// Returns the exterior stub point just outside a node boundary, toward <paramref name="TowardX"/>/<paramref name="TowardY"/>.
    /// </summary>
    public static (double X, double Y) ResolveExteriorStubPoint(
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double BoundaryX,
        double BoundaryY,
        double TowardX,
        double TowardY)
    {
        double StubLength = ComputeTerminalStubLength(Bounds);
        double NormalX;
        double NormalY;

        if (Math.Abs(TowardX - BoundaryX) <= FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
        {
            NormalX = 0.0;
            NormalY = TowardY < BoundaryY ? -1.0 : 1.0;
        }
        else if (Math.Abs(TowardY - BoundaryY) <= FlowchartEdgeConnectionGeometry.ALIGN_TOLERANCE_PX)
        {
            NormalX = TowardX < BoundaryX ? -1.0 : 1.0;
            NormalY = 0.0;
        }
        else if (!TryResolveOutwardNormal(
                     Bounds,
                     Shape,
                     BoundaryX,
                     BoundaryY,
                     TowardX,
                     TowardY,
                     out NormalX,
                     out NormalY))
        {
            double DeltaX = TowardX - BoundaryX;
            double DeltaY = TowardY - BoundaryY;
            double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
            if (Length < 1e-6)
            {
                return (BoundaryX, BoundaryY);
            }

            NormalX = DeltaX / Length;
            NormalY = DeltaY / Length;
        }

        return (BoundaryX + (NormalX * StubLength), BoundaryY + (NormalY * StubLength));
    }

    private static void EnsureSourceTerminalStub(
        List<(double X, double Y)> Points,
        FlowchartLayoutNodeBounds SourceBounds,
        string? SourceShape)
    {
        (double BoundaryX, double BoundaryY) = Points[0];
        (double InteriorX, double InteriorY) = Points[1];
        double StubLength = ComputeTerminalStubLength(SourceBounds);

        if (!TryResolveOutwardNormal(
                SourceBounds,
                SourceShape,
                BoundaryX,
                BoundaryY,
                InteriorX,
                InteriorY,
                out double NormalX,
                out double NormalY))
        {
            return;
        }

        double StubX = BoundaryX + (NormalX * StubLength);
        double StubY = BoundaryY + (NormalY * StubLength);
        double ExistingDeltaX = InteriorX - BoundaryX;
        double ExistingDeltaY = InteriorY - BoundaryY;
        double ExistingLength = Math.Sqrt((ExistingDeltaX * ExistingDeltaX) + (ExistingDeltaY * ExistingDeltaY));
        if (ExistingLength + DUPLICATE_TOLERANCE_PX >= StubLength)
        {
            if (ShouldInsertStub(Points.Count, ExistingDeltaX, ExistingDeltaY, ExistingLength, NormalX, NormalY))
            {
                InsertStubWaypoint(Points, 1, StubX, StubY);
            }

            return;
        }

        Points[1] = (StubX, StubY);
    }

    private static void EnsureTargetTerminalStub(
        List<(double X, double Y)> Points,
        FlowchartLayoutNodeBounds TargetBounds,
        string? TargetShape)
    {
        int BoundaryIndex = Points.Count - 1;
        int InteriorIndex = Points.Count - 2;
        (double BoundaryX, double BoundaryY) = Points[BoundaryIndex];
        (double InteriorX, double InteriorY) = Points[InteriorIndex];
        double StubLength = ComputeTerminalStubLength(TargetBounds);

        if (!TryResolveOutwardNormal(
                TargetBounds,
                TargetShape,
                BoundaryX,
                BoundaryY,
                InteriorX,
                InteriorY,
                out double NormalX,
                out double NormalY))
        {
            return;
        }

        double StubX = BoundaryX + (NormalX * StubLength);
        double StubY = BoundaryY + (NormalY * StubLength);
        double ExistingDeltaX = InteriorX - BoundaryX;
        double ExistingDeltaY = InteriorY - BoundaryY;
        double ExistingLength = Math.Sqrt((ExistingDeltaX * ExistingDeltaX) + (ExistingDeltaY * ExistingDeltaY));
        if (ExistingLength + DUPLICATE_TOLERANCE_PX >= StubLength)
        {
            if (ShouldInsertStub(Points.Count, ExistingDeltaX, ExistingDeltaY, ExistingLength, NormalX, NormalY))
            {
                InsertStubWaypoint(Points, BoundaryIndex, StubX, StubY);
            }

            return;
        }

        Points[InteriorIndex] = (StubX, StubY);
    }

    /// <summary>
    /// Returns true when a long end segment leaves the boundary off the face normal, so a stub must be added.
    /// Two-point routes are straight chords (spec §15.0A) and never get a stub.
    /// </summary>
    private static bool ShouldInsertStub(int PointCount, double ExistingDeltaX, double ExistingDeltaY, double ExistingLength, double NormalX, double NormalY)
    {
        if (PointCount < 3 || ExistingLength < 1e-6)
        {
            return false;
        }

        double Alignment = ((ExistingDeltaX * NormalX) + (ExistingDeltaY * NormalY)) / ExistingLength;
        return Alignment < NORMAL_ALIGNMENT_COSINE;
    }

    private static void InsertStubWaypoint(
        List<(double X, double Y)> Points,
        int InsertIndex,
        double StubX,
        double StubY)
    {
        if (InsertIndex < Points.Count)
        {
            (double ExistingX, double ExistingY) = Points[InsertIndex];
            if (Math.Abs(ExistingX - StubX) <= DUPLICATE_TOLERANCE_PX
                && Math.Abs(ExistingY - StubY) <= DUPLICATE_TOLERANCE_PX)
            {
                return;
            }
        }

        Points.Insert(InsertIndex, (StubX, StubY));
    }

    private static bool TryResolveOutwardNormal(
        FlowchartLayoutNodeBounds Bounds,
        string? Shape,
        double BoundaryX,
        double BoundaryY,
        double InteriorX,
        double InteriorY,
        out double NormalX,
        out double NormalY)
    {
        NormalX = 0.0;
        NormalY = 0.0;

        if (FlowchartDiamondAttachHelper.IsDiamondShape(Shape)
            && FlowchartDiamondAttachHelper.TryResolveOutwardNormal(
                Bounds,
                BoundaryX,
                BoundaryY,
                out NormalX,
                out NormalY))
        {
            return true;
        }

        FlowchartEdgeAttachFace Face = FlowchartPerpendicularAttachValidator.ResolveAttachFace(
            Bounds,
            BoundaryX,
            BoundaryY);

        switch (Face)
        {
            case FlowchartEdgeAttachFace.Top:
                NormalX = 0.0;
                NormalY = 1.0;
                return true;

            case FlowchartEdgeAttachFace.Bottom:
                NormalX = 0.0;
                NormalY = -1.0;
                return true;

            case FlowchartEdgeAttachFace.Left:
                NormalX = -1.0;
                NormalY = 0.0;
                return true;

            case FlowchartEdgeAttachFace.Right:
                NormalX = 1.0;
                NormalY = 0.0;
                return true;

            default:
                double DeltaX = InteriorX - BoundaryX;
                double DeltaY = InteriorY - BoundaryY;
                double Length = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
                if (Length < 1e-6)
                {
                    return false;
                }

                NormalX = DeltaX / Length;
                NormalY = DeltaY / Length;
                return true;
        }
    }
}
