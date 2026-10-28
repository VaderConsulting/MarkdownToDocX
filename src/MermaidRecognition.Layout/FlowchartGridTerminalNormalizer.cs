using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Joins sloping diamond edges along their actual surface normal. Only the
/// terminal run may be diagonal; the adjoining route keeps its allocated track.
/// </summary>
internal static class FlowchartGridTerminalNormalizer
{
    internal static void Apply(FlowchartLayoutResult Layout,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes)
    {
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            List<(double X, double Y)> Points = [.. Route.Points.Select(Point => (Point.X, Point.Y))];
            CorrectEnd(Points, Route.SourceId, Bounds, Nodes);
            Points.Reverse();
            CorrectEnd(Points, Route.TargetId, Bounds, Nodes);
            Points.Reverse();
            Route.Points.Clear();
            foreach ((double X, double Y) Point in Points)
            {
                Route.Points.Add(new() { X = Point.X, Y = Point.Y });
            }
        }
    }

    private static void CorrectEnd(List<(double X, double Y)> Points, string NodeId,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds,
        IReadOnlyDictionary<string, NodeContract> Nodes)
    {
        if (Points.Count < 3 || !Nodes.TryGetValue(NodeId, out NodeContract? Node)
            || !FlowchartDiamondAttachHelper.IsDiamondShape(Node.Shape)
            || !Bounds.TryGetValue(NodeId, out FlowchartLayoutNodeBounds? Box)
            || !FlowchartDiamondAttachHelper.TryResolveOutwardNormal(Box, Points[0].X, Points[0].Y, out double Nx, out double Ny)
            || Math.Abs(Nx) < 0.001 || Math.Abs(Ny) < 0.001)
        {
            return;
        }
        (double X, double Y) Port = Points[0];
        double Dx = Points[1].X - Port.X;
        double Dy = Points[1].Y - Port.Y;
        bool Horizontal = Math.Abs(Dy) < 0.5;
        bool Vertical = Math.Abs(Dx) < 0.5;
        if ((!Horizontal && !Vertical) || (Dx * Nx) + (Dy * Ny) <= 0)
        {
            return;
        }
        int Corner = 1;
        while (Corner + 1 < Points.Count && (Horizontal
            ? Math.Abs(Points[Corner + 1].Y - Port.Y) < 0.5
            : Math.Abs(Points[Corner + 1].X - Port.X) < 0.5))
        {
            Corner++;
        }
        if (Corner + 1 >= Points.Count)
        {
            return;
        }

        (double X, double Y) Bend = Points[Corner];
        (double X, double Y) Next = Points[Corner + 1];
        if (Horizontal ? Math.Abs(Bend.X - Next.X) > 0.5 : Math.Abs(Bend.Y - Next.Y) > 0.5)
        {
            return;
        }

        double Length = (2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) + DiagramGridMetrics.GRID_UNIT_PX;
        (double X, double Y) Stub = (X: Port.X + (Nx * Length), Y: Port.Y + (Ny * Length));
        (double X, double Y) Bridge = Horizontal ? (X: Bend.X, Y: Stub.Y) : (X: Stub.X, Y: Bend.Y);
        // The normal must reach the existing track without reversing either run.
        if (((Bridge.X - Stub.X) * Dx) + ((Bridge.Y - Stub.Y) * Dy) <= 0
            || ((Next.X - Bridge.X) * (Next.X - Bend.X)) + ((Next.Y - Bridge.Y) * (Next.Y - Bend.Y)) <= 0)
        {
            return;
        }
        Points.RemoveRange(1, Corner);
        Points.Insert(1, Stub);
        Points.Insert(2, Bridge);
    }

    internal static bool HasAxisAlignedInterior(IReadOnlyList<(double X, double Y)> Points)
    {
        if (Points.Count < 4)
        {
            return false;
        }

        for (int Index = 2; Index < Points.Count - 1; Index++)
        {
            if (Math.Abs(Points[Index].X - Points[Index - 1].X) > 0.5
                && Math.Abs(Points[Index].Y - Points[Index - 1].Y) > 0.5)
            {
                return false;
            }
        }
        return true;
    }
}
