using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Exchanges fan ports only when the new routes remove crossings safely.</summary>
internal static class FlowchartBranchRouteOrder
{
    internal static void RemoveAvoidableCrossings(FlowchartLayoutResult Layout, DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds, Dictionary<string, NodeContract> Nodes,
        FlowchartDirection Direction, IReadOnlySet<FlowchartLayoutEdgeRoute>? ReservedStraightRoutes = null)
    {
        bool Horizontal = Direction == FlowchartDirection.LeftToRight || Direction == FlowchartDirection.RightToLeft;
        var Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        var Routes = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2
            && Bounds.ContainsKey(Route.SourceId) && Bounds.ContainsKey(Route.TargetId)
            && Nodes.ContainsKey(Route.SourceId) && Nodes.ContainsKey(Route.TargetId)
            && !Contract.Edges.Any(Edge => (Edge.Id == Route.EdgeId
                || string.IsNullOrEmpty(Edge.Id) && Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)
                && (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                    || Edge.RoutingHint?.IsActive == true))
            && !Back.Any(Edge => Edge.SourceId == Route.SourceId && Edge.TargetId == Route.TargetId)).ToList();
        foreach (bool SourceEnd in new[] { true, false })
        foreach (var Fan in Routes.GroupBy(Route => SourceEnd ? Route.SourceId : Route.TargetId))
        {
            var Branches = Fan.ToList();
            for (int First = 0; First < Branches.Count; First++)
            for (int Second = First + 1; Second < Branches.Count; Second++)
            {
                var A = Branches[First]; var B = Branches[Second];
                if (A.SourceId == B.SourceId && A.TargetId == B.TargetId) continue;
                // Matched partner continuations own their centre slots before
                // crossings are considered. Other routes may need frame repair.
                if (ReservedStraightRoutes?.Contains(A) == true || ReservedStraightRoutes?.Contains(B) == true) continue;
                // Retain the flow-facing sides allocated to ordinary boxes.
                // Circular junctions may exchange their free cardinal sides.
                if (Nodes[Fan.Key].Shape != "circle"
                    && (SourceEnd ? A.ExitSide != B.ExitSide : A.EntrySide != B.EntrySide)) continue;
                var OldA = Points(A); var OldB = Points(B);
                if (Intersections(OldA, OldB).Crossings == 0) continue;
                var NewA = Rebuild(A, B, SourceEnd); var NewB = Rebuild(B, A, SourceEnd);
                if (!IsClear(A, NewA) || !IsClear(B, NewB)) continue;
                var Before = Intersections(OldA, OldB); var After = Intersections(NewA, NewB);
                if (After.Crossings != 0 || After.Overlap > Before.Overlap + 0.01) continue;
                bool Safe = true;
                foreach (var Other in Layout.EdgeRoutes.Where(Route => Route != A && Route != B))
                {
                    var Path = Points(Other);
                    var OldFirst = Intersections(OldA, Path); var OldSecond = Intersections(OldB, Path);
                    var NewFirst = Intersections(NewA, Path); var NewSecond = Intersections(NewB, Path);
                    if (NewFirst.Crossings + NewSecond.Crossings > OldFirst.Crossings + OldSecond.Crossings
                        || NewFirst.Overlap + NewSecond.Overlap > OldFirst.Overlap + OldSecond.Overlap + 0.01)
                    { Safe = false; break; }
                }
                if (!Safe) continue;
                var SideA = SourceEnd ? A.ExitSide : A.EntrySide;
                var SideB = SourceEnd ? B.ExitSide : B.EntrySide;
                if (SourceEnd) { A.ExitSide = SideB; B.ExitSide = SideA; }
                else { A.EntrySide = SideB; B.EntrySide = SideA; }
                Replace(A, NewA); Replace(B, NewB);
            }
        }

        List<(double X, double Y)> Rebuild(FlowchartLayoutEdgeRoute Route, FlowchartLayoutEdgeRoute Other, bool SourceEnd)
        {
            var Start = SourceEndPoint(Route, Other, true, SourceEnd);
            var End = SourceEndPoint(Route, Other, false, SourceEnd);
            return DiagramGridRouteBuilder.BuildRoute(Start.X, Start.Y, Bounds[Route.SourceId], Nodes[Route.SourceId].Shape,
                SourceEnd ? Other.ExitSide : Route.ExitSide, End.X, End.Y, Bounds[Route.TargetId], Nodes[Route.TargetId].Shape,
                SourceEnd ? Route.EntrySide : Other.EntrySide, Bounds, Route.SourceId, Route.TargetId, Contract, Direction, Horizontal);
        }

        (double X, double Y) SourceEndPoint(FlowchartLayoutEdgeRoute Route, FlowchartLayoutEdgeRoute Other, bool Start, bool SourceEnd)
        {
            var Owner = SourceEnd == Start ? Other : Route;
            var Point = Start ? Owner.Points[0] : Owner.Points[^1];
            return (Point.X, Point.Y);
        }

        bool IsClear(FlowchartLayoutEdgeRoute Route, List<(double X, double Y)> Path)
        {
            if (Path.Count < 2) return false;
            for (int Index = 1; Index < Path.Count; Index++)
            {
                var A = Path[Index - 1]; var B = Path[Index];
                if (Math.Abs(A.X - B.X) > 0.01 && Math.Abs(A.Y - B.Y) > 0.01) return false;
                foreach (var Box in Bounds.Values)
                {
                    if (Box.NodeId == Route.SourceId || Box.NodeId == Route.TargetId
                        || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.SourceId)
                        || FlowchartCompoundEndpointHelper.Contains(Contract, Box.NodeId, Route.TargetId)) continue;
                    if (Math.Max(A.X, B.X) > Box.CenterX - Box.Width / 2 - 6
                        && Math.Min(A.X, B.X) < Box.CenterX + Box.Width / 2 + 6
                        && Math.Max(A.Y, B.Y) > Box.CenterY - Box.Height / 2 - 6
                        && Math.Min(A.Y, B.Y) < Box.CenterY + Box.Height / 2 + 6) return false;
                }
            }
            return true;
        }
    }

    private static List<(double X, double Y)> Points(FlowchartLayoutEdgeRoute Route) =>
        Route.Points.Select(Point => (Point.X, Point.Y)).ToList();

    private static void Replace(FlowchartLayoutEdgeRoute Route, List<(double X, double Y)> Path)
    {
        Route.Points.Clear();
        foreach (var Point in Path) Route.Points.Add(new() { X = Point.X, Y = Point.Y });
    }

    /// <summary>Measures actual segment crossings and shared lengths, including diamond-normal terminals.</summary>
    /// <param name="First">The first complete route.</param>
    /// <param name="Second">The second complete route.</param>
    /// <returns>Distinct crossing points and the positive collinear overlap length in pixels.</returns>
    internal static (int Crossings, double Overlap) Intersections(
        IReadOnlyList<(double X, double Y)> First, IReadOnlyList<(double X, double Y)> Second)
    {
        var Crossings = new HashSet<(double X, double Y)>();
        double Overlap = 0;
        for (int I = 1; I < First.Count; I++)
        for (int J = 1; J < Second.Count; J++)
        {
            var A = First[I - 1]; var B = First[I]; var C = Second[J - 1]; var D = Second[J];
            double Ax = B.X - A.X, Ay = B.Y - A.Y, Bx = D.X - C.X, By = D.Y - C.Y;
            double FirstLength = Math.Sqrt(Ax * Ax + Ay * Ay);
            double SecondLength = Math.Sqrt(Bx * Bx + By * By);
            if (FirstLength < 0.001 || SecondLength < 0.001) continue;
            double Denominator = Ax * By - Ay * Bx;
            double Cx = C.X - A.X, Cy = C.Y - A.Y;
            if (Math.Abs(Denominator) < 0.000001)
            {
                if (Math.Abs(Cx * Ay - Cy * Ax) / FirstLength > 0.001) continue;
                double Start = (Cx * Ax + Cy * Ay) / FirstLength;
                double End = ((D.X - A.X) * Ax + (D.Y - A.Y) * Ay) / FirstLength;
                Overlap += Math.Max(0, Math.Min(FirstLength, Math.Max(Start, End)) - Math.Max(0, Math.Min(Start, End)));
                continue;
            }
            double T = (Cx * By - Cy * Bx) / Denominator;
            double U = (Cx * Ay - Cy * Ax) / Denominator;
            if (T < -0.000001 || T > 1.000001 || U < -0.000001 || U > 1.000001) continue;
            var Point = (X: Math.Round(A.X + T * Ax, 6), Y: Math.Round(A.Y + T * Ay, 6));
            // Shared graph terminals are connections, not crossings.
            bool EndOfFirst = Near(Point, First[0]) || Near(Point, First[^1]);
            bool EndOfSecond = Near(Point, Second[0]) || Near(Point, Second[^1]);
            if (!EndOfFirst || !EndOfSecond) Crossings.Add(Point);
        }
        return (Crossings.Count, Overlap);
    }

    private static bool Near((double X, double Y) A, (double X, double Y) B) =>
        Math.Abs(A.X - B.X) < 0.01 && Math.Abs(A.Y - B.Y) < 0.01;
}
