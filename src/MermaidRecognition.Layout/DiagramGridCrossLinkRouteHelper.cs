using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Orthogonal cross-link tracks for 2×2 complete bipartite pairs (rulebook 2.9 and 5.5).
/// </summary>
internal static class DiagramGridCrossLinkRouteHelper
{
    private const double COLLINEAR_EPSILON_PX = 0.75;

    /// <summary>
    /// Rewrites routes between paired ranks so crossed lines use spread ports and gap tracks.
    /// </summary>
    internal static void Apply(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        IReadOnlyList<DiagramGridLayout.GridNode> Placed,
        FlowchartDirection Direction)
    {
        if (Contract == null || Layout == null || Placed.Count == 0)
        {
            return;
        }

        bool Horizontal = Direction == FlowchartDirection.LeftToRight
            || Direction == FlowchartDirection.RightToLeft;
        if (Horizontal)
        {
            return;
        }

        Dictionary<string, DiagramGridLayout.GridNode> PlacedById = new Dictionary<string, DiagramGridLayout.GridNode>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            PlacedById[Node.Id] = Node;
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = BuildBoundsFromPlaced(Placed);

        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByKey = new Dictionary<string, FlowchartLayoutEdgeRoute>(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            RouteByKey[Route.SourceId + "->" + Route.TargetId] = Route;
        }

        int RankCount = 0;
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            RankCount = Math.Max(RankCount, Node.Rank + 1);
        }

        for (int Rank = 0; Rank + 1 < RankCount; Rank++)
        {
            List<DiagramGridLayout.GridNode> Upper = NodesInRank(Placed, Rank);
            List<DiagramGridLayout.GridNode> Lower = NodesInRank(Placed, Rank + 1);
            if (Upper.Count != 2 || Lower.Count != 2)
            {
                continue;
            }

            Upper.Sort(delegate(DiagramGridLayout.GridNode Left, DiagramGridLayout.GridNode Right)
            {
                return Left.Column.CompareTo(Right.Column);
            });
            Lower.Sort(delegate(DiagramGridLayout.GridNode Left, DiagramGridLayout.GridNode Right)
            {
                return Left.Column.CompareTo(Right.Column);
            });

            DiagramGridLayout.GridNode ParentA = Upper[0];
            DiagramGridLayout.GridNode ParentB = Upper[1];
            DiagramGridLayout.GridNode ChildC = Lower[0];
            DiagramGridLayout.GridNode ChildD = Lower[1];
            if (!IsCompleteBipartite(Contract, ParentA.Id, ParentB.Id, ChildC.Id, ChildD.Id))
            {
                continue;
            }

            if (!BoundsById.TryGetValue(ParentA.Id, out FlowchartLayoutNodeBounds BoundsA)
                || !BoundsById.TryGetValue(ParentB.Id, out FlowchartLayoutNodeBounds BoundsB)
                || !BoundsById.TryGetValue(ChildC.Id, out FlowchartLayoutNodeBounds BoundsC)
                || !BoundsById.TryGetValue(ChildD.Id, out FlowchartLayoutNodeBounds BoundsD))
            {
                continue;
            }

            RewriteStraight(RouteByKey, BoundsA, BoundsC, ParentA.Id, ChildC.Id);
            RewriteStraight(RouteByKey, BoundsB, BoundsD, ParentB.Id, ChildD.Id);
            RewriteCrossed(
                RouteByKey,
                BoundsA,
                BoundsD,
                ParentA.Id,
                ChildD.Id,
                ParentOnLeft: true);
            RewriteCrossed(
                RouteByKey,
                BoundsB,
                BoundsC,
                ParentB.Id,
                ChildC.Id,
                ParentOnLeft: false);
        }
    }

    private static void RewriteStraight(
        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByKey,
        FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target,
        string SourceId,
        string TargetId)
    {
        if (!RouteByKey.TryGetValue(SourceId + "->" + TargetId, out FlowchartLayoutEdgeRoute? Route))
        {
            return;
        }

        double SourceY = Source.CenterY - (Source.Height / 2.0);
        double TargetY = Target.CenterY + (Target.Height / 2.0);
        Route.Points.Clear();
        Route.Points.Add(new FlowchartLayoutPoint { X = Source.CenterX, Y = SourceY });
        Route.Points.Add(new FlowchartLayoutPoint { X = Target.CenterX, Y = TargetY });
    }

    private static void RewriteCrossed(
        Dictionary<string, FlowchartLayoutEdgeRoute> RouteByKey,
        FlowchartLayoutNodeBounds Source,
        FlowchartLayoutNodeBounds Target,
        string SourceId,
        string TargetId,
        bool ParentOnLeft)
    {
        if (!RouteByKey.TryGetValue(SourceId + "->" + TargetId, out FlowchartLayoutEdgeRoute? Route))
        {
            return;
        }

        double SourceHalfWidth = Source.Width / 2.0;
        double SourceBottom = Source.CenterY - (Source.Height / 2.0);
        double TargetHalfWidth = Target.Width / 2.0;
        double TargetTop = Target.CenterY + (Target.Height / 2.0);
        double GapMidY = (SourceBottom + TargetTop) / 2.0;

        double SourceX = ParentOnLeft
            ? Source.CenterX + (SourceHalfWidth * 0.35)
            : Source.CenterX - (SourceHalfWidth * 0.35);
        double TargetX = ParentOnLeft
            ? Target.CenterX - (TargetHalfWidth * 0.35)
            : Target.CenterX + (TargetHalfWidth * 0.35);

        if (Math.Abs(Source.CenterX - Target.CenterX) <= COLLINEAR_EPSILON_PX)
        {
            SourceX = Source.CenterX;
            TargetX = Target.CenterX;
        }

        Route.Points.Clear();
        Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(SourceX), Y = SourceBottom });
        Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(SourceX), Y = GapMidY });
        Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(TargetX), Y = GapMidY });
        Route.Points.Add(new FlowchartLayoutPoint { X = Math.Round(TargetX), Y = TargetTop });
    }

    private static bool IsCompleteBipartite(
        DiagramContract Contract,
        string ParentA,
        string ParentB,
        string ChildC,
        string ChildD)
    {
        return HasEdge(Contract, ParentA, ChildC)
            && HasEdge(Contract, ParentA, ChildD)
            && HasEdge(Contract, ParentB, ChildC)
            && HasEdge(Contract, ParentB, ChildD);
    }

    private static bool HasEdge(DiagramContract Contract, string SourceId, string TargetId)
    {
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Edge.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Edge.TargetId, TargetId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, FlowchartLayoutNodeBounds> BuildBoundsFromPlaced(
        IReadOnlyList<DiagramGridLayout.GridNode> Placed)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            BoundsById[Node.Id] = new FlowchartLayoutNodeBounds
            {
                NodeId = Node.Id,
                CenterX = Node.X,
                CenterY = Node.Y,
                Width = Node.Width,
                Height = Node.Height
            };
        }

        return BoundsById;
    }

    private static List<DiagramGridLayout.GridNode> NodesInRank(IReadOnlyList<DiagramGridLayout.GridNode> Placed, int Rank)
    {
        List<DiagramGridLayout.GridNode> Nodes = [];
        foreach (DiagramGridLayout.GridNode Node in Placed)
        {
            if (Node.Rank == Rank)
            {
                Nodes.Add(Node);
            }
        }

        return Nodes;
    }
}
