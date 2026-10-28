using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Inserts a single right-angle crossing when a route leaves or enters a group.
/// Inspired by <c>GroupBoundaryCrossing</c> and <c>ObstacleTree.AddGroupIntersectionsToRestrictedRay</c>
/// in the adopted MSAGL rectilinear router.
/// </summary>
public static class FlowchartGroupBoundaryCrossingHelper
{
    private const double STUB_PX = 12.0;

    /// <summary>
    /// Rewrites <paramref name="Points"/> so each group the edge must leave or enter is crossed
    /// once, at a right angle, on the facing side.
    /// </summary>
    /// <param name="Contract">Diagram.</param>
    /// <param name="BoundsById">Node and group bounds.</param>
    /// <param name="Edge">Edge being routed.</param>
    /// <param name="Points">Guide points. Replaced in place.</param>
    public static void InsertPerpendicularCrossings(
        DiagramContract Contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        EdgeContract Edge,
        List<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return;
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            if (!BoundsById.TryGetValue(Group.Id, out FlowchartLayoutNodeBounds? GroupBounds)
                || GroupBounds == null)
            {
                continue;
            }

            bool SourceInside = MemberOf(Contract, Group, Edge.SourceId);
            bool TargetInside = MemberOf(Contract, Group, Edge.TargetId);
            if (SourceInside == TargetInside)
            {
                continue;
            }

            InsertOneCrossing(GroupBounds, Points, Leaving: SourceInside);
        }
    }

    private static void InsertOneCrossing(
        FlowchartLayoutNodeBounds GroupBounds,
        List<(double X, double Y)> Points,
        bool Leaving)
    {
        double Left = GroupBounds.CenterX - (GroupBounds.Width / 2.0);
        double Right = GroupBounds.CenterX + (GroupBounds.Width / 2.0);
        double Top = GroupBounds.CenterY + (GroupBounds.Height / 2.0);
        double Bottom = GroupBounds.CenterY - (GroupBounds.Height / 2.0);

        int CrossingAfter = -1;
        double CrossX = 0.0;
        double CrossY = 0.0;
        for (int Index = 0; Index < Points.Count - 1; Index++)
        {
            bool StartInside = PointInside(Points[Index].X, Points[Index].Y, Left, Right, Top, Bottom);
            bool EndInside = PointInside(Points[Index + 1].X, Points[Index + 1].Y, Left, Right, Top, Bottom);
            if (StartInside == EndInside)
            {
                continue;
            }

            if (!TryIntersectBorder(
                Points[Index].X,
                Points[Index].Y,
                Points[Index + 1].X,
                Points[Index + 1].Y,
                Left,
                Right,
                Top,
                Bottom,
                out CrossX,
                out CrossY))
            {
                continue;
            }

            CrossingAfter = Index;
            break;
        }

        if (CrossingAfter < 0)
        {
            return;
        }

        (double InsideX, double InsideY) = Leaving ? Points[CrossingAfter] : Points[CrossingAfter + 1];
        double StubX = CrossX;
        double StubY = CrossY;
        if (Math.Abs(CrossX - Left) <= 0.6 || Math.Abs(CrossX - Right) <= 0.6)
        {
            StubY = InsideY;
            CrossY = InsideY;
            StubX = CrossX + (Math.Abs(CrossX - Left) <= 0.6 ? -STUB_PX : STUB_PX);
        }
        else
        {
            StubX = InsideX;
            CrossX = InsideX;
            StubY = CrossY + (Math.Abs(CrossY - Top) <= 0.6 ? STUB_PX : -STUB_PX);
        }

        if (Leaving)
        {
            Points.Insert(CrossingAfter + 1, (CrossX, CrossY));
            Points.Insert(CrossingAfter + 2, (StubX, StubY));
        }
        else
        {
            Points.Insert(CrossingAfter + 1, (StubX, StubY));
            Points.Insert(CrossingAfter + 2, (CrossX, CrossY));
        }
    }

    private static bool TryIntersectBorder(
        double StartX,
        double StartY,
        double EndX,
        double EndY,
        double Left,
        double Right,
        double Top,
        double Bottom,
        out double CrossX,
        out double CrossY)
    {
        CrossX = 0.0;
        CrossY = 0.0;
        double BestT = double.PositiveInfinity;
        TryEdge(StartX, StartY, EndX, EndY, Left, Bottom, Left, Top, ref BestT, ref CrossX, ref CrossY);
        TryEdge(StartX, StartY, EndX, EndY, Right, Bottom, Right, Top, ref BestT, ref CrossX, ref CrossY);
        TryEdge(StartX, StartY, EndX, EndY, Left, Top, Right, Top, ref BestT, ref CrossX, ref CrossY);
        TryEdge(StartX, StartY, EndX, EndY, Left, Bottom, Right, Bottom, ref BestT, ref CrossX, ref CrossY);
        return !double.IsPositiveInfinity(BestT);
    }

    private static void TryEdge(
        double Ax,
        double Ay,
        double Bx,
        double By,
        double Cx,
        double Cy,
        double Dx,
        double Dy,
        ref double BestT,
        ref double CrossX,
        ref double CrossY)
    {
        double Rdx = Bx - Ax;
        double Rdy = By - Ay;
        double Sdx = Dx - Cx;
        double Sdy = Dy - Cy;
        double Denom = (Rdx * Sdy) - (Rdy * Sdx);
        if (Math.Abs(Denom) < 0.000001)
        {
            return;
        }

        double T = (((Cx - Ax) * Sdy) - ((Cy - Ay) * Sdx)) / Denom;
        double U = (((Cx - Ax) * Rdy) - ((Cy - Ay) * Rdx)) / Denom;
        if (T < 0.0 || T > 1.0 || U < 0.0 || U > 1.0 || T >= BestT)
        {
            return;
        }

        BestT = T;
        CrossX = Ax + (Rdx * T);
        CrossY = Ay + (Rdy * T);
    }

    private static bool PointInside(double X, double Y, double Left, double Right, double Top, double Bottom)
    {
        return X >= Left - 0.5 && X <= Right + 0.5 && Y <= Top + 0.5 && Y >= Bottom - 0.5;
    }

    private static bool MemberOf(DiagramContract Contract, GroupContract Group, string NodeId)
    {
        foreach (string ChildId in Group.ChildNodeIds)
        {
            if (string.Equals(ChildId, NodeId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (string ChildGroupId in Group.ChildGroupIds)
        {
            foreach (GroupContract Child in Contract.Groups)
            {
                if (string.Equals(Child.Id, ChildGroupId, StringComparison.Ordinal)
                    && MemberOf(Contract, Child, NodeId))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
