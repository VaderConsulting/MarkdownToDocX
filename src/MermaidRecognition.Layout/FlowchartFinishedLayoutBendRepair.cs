using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Consumes proven finished-layout straightening, interior-route and facing-hub candidates under the existing repair limits.</summary>
internal static class FlowchartFinishedLayoutBendRepair
{
    /// <summary>Reports complete candidate decisions for catalogue measurements; it cannot alter acceptance.</summary>
    internal static Action<string, bool>? CandidateObserver;
    /// <summary>Tries complete independent alternatives, publishing an atomic cell move only after every route and score guard passes.</summary>
    /// <param name="Contract">Original declarations and honoured hints.</param>
    /// <param name="Layout">Complete production model.</param>
    /// <returns>Number of accepted complete candidates.</returns>
    internal static int Apply(DiagramContract Contract, FlowchartLayoutResult Layout) => ApplyCore(Contract, Layout, true);

    /// <summary>Applies the independently verified route-only part while whole-cell move activation awaits its catalogue guards.</summary>
    /// <param name="Contract">Original declarations and honoured hints.</param>
    /// <param name="Layout">Complete selected layout whose cell regions remain fixed.</param>
    /// <returns>Accepted interior/facing route assignments, with no placement changes.</returns>
    internal static int ApplyRoutesOnly(DiagramContract Contract, FlowchartLayoutResult Layout) => ApplyCore(Contract, Layout, false);

    /// <summary>Shares the bounded complete-candidate implementation between verified route repair and the isolated movement trial.</summary>
    /// <param name="Contract">Original source.</param>
    /// <param name="Layout">Finished mutable route model.</param>
    /// <param name="IncludeCellMoves">Whether this invocation is the isolated full movement experiment.</param>
    /// <returns>The accepted candidate count.</returns>
    private static int ApplyCore(DiagramContract Contract, FlowchartLayoutResult Layout, bool IncludeCellMoves)
    {
        // Sequence, class and other catalogue kinds have their own DOCX
        // renderers. A synthetic flowchart placement is not their drawn model.
        if (!string.Equals(Contract.Kind, "flowchart", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Contract.Kind, "graph", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        int Accepted = 0, Considered = 0;
        bool Enterprise = Contract.Nodes.Any(Node => Node.Metadata.ContainsKey("enterpriseElementType"));
        // For flow/EIP, a supporting route is immutable unless the same new
        // diagnosis flagged it in the original complete model.
        HashSet<string> Eligible = new HashSet<string>(StringComparer.Ordinal);
        if (!Enterprise)
        {
            foreach (FlowchartLayoutEdgeRoute? Line in Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2))
            {
                if ((IncludeCellMoves && FlowchartLayoutAlternatives.Straighten(Contract, Layout, Line) != null)
                    || FlowchartLayoutAlternatives.Interior(Contract, Layout, Line) != null
                    || FlowchartLayoutAlternatives.FarSide(Layout, Line))
                {
                    Eligible.Add(Line.EdgeId);
                }
            }
        }

        bool Progress;
        do
        {
            Progress = false;
            HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowchartLayoutEdgeRoute? Line in Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2).ToArray())
            {
                if (Considered >= FlowchartLayoutRules.RepairCandidateMoves)
                {
                    break;
                }

                if (Visited.Contains(Line.SourceId) || Visited.Contains(Line.TargetId))
                {
                    continue;
                }

                FlowchartLayoutAlternatives.Witness? Candidate = null;
                if (IncludeCellMoves && Line.Points.Count > 2)
                {
                    Candidate = FlowchartLayoutAlternatives.Straighten(Contract, Layout, Line);
                }

                Candidate ??= FlowchartLayoutAlternatives.Interior(Contract, Layout, Line);
                if (Contract.Edges.Count(Edge => Edge.Stroke != "invisible" && (Edge.SourceId == Line.TargetId || Edge.TargetId == Line.TargetId)) >= 3)
                {
                    Candidate ??= FlowchartLayoutAlternatives.FacingHub(Contract, Layout, Line);
                }

                if (Candidate == null)
                {
                    continue;
                }

                Considered++;
                Visited.Add(Line.SourceId); Visited.Add(Line.TargetId);
                FlowchartLayoutEdgeRoute[] Changed = [.. Candidate.Layout.EdgeRoutes.Where(Route => !Same(Route, Layout.EdgeRoutes.Single(Old => Old.EdgeId == Route.EdgeId)))];
                if (Changed.Length > FlowchartLayoutRules.RepairChainLines || (!Enterprise && Changed.Any(Route => !Eligible.Contains(Route.EdgeId))))
                {
                    CandidateObserver?.Invoke(Candidate.LineId + "|" + Candidate.Detail, false);
                    continue;
                }
                if (Candidate.Move != null && !Candidate.Move.TryApply())
                {
                    CandidateObserver?.Invoke(Candidate.LineId + "|" + Candidate.Detail, false);
                    continue;
                }
                // No failure-prone work follows the atomic move: all geometry
                // and routes below were completed and checked in private state.
                Layout.NodeBoundsMutable.Clear(); Layout.NodeBoundsMutable.AddRange(Candidate.Layout.NodeBounds);
                foreach (FlowchartLayoutEdgeRoute? Route in Changed)
                {
                    FlowchartLayoutEdgeRoute Target = Layout.EdgeRoutes.Single(Old => Old.EdgeId == Route.EdgeId);
                    Target.ExitSide = Route.ExitSide; Target.EntrySide = Route.EntrySide;
                    Target.ExitNormalX = Route.ExitNormalX; Target.ExitNormalY = Route.ExitNormalY;
                    Target.EntryNormalX = Route.EntryNormalX; Target.EntryNormalY = Route.EntryNormalY;
                    Target.Points.Clear(); Target.Points.AddRange(Route.Points.Select(Point => new FlowchartLayoutPoint { X = Point.X, Y = Point.Y }));
                }
                Accepted++; Progress = true;
                CandidateObserver?.Invoke(Candidate.LineId + "|" + Candidate.Detail, true);
            }
        } while (Progress && Considered < FlowchartLayoutRules.RepairCandidateMoves);
        return Accepted;
    }

    /// <summary>Compares complete drawn geometry and exact terminal metadata before publishing supporting routes.</summary>
    /// <param name="A">Candidate route.</param>
    /// <param name="B">Original route.</param>
    /// <returns>Whether the route is wholly unchanged.</returns>
    private static bool Same(FlowchartLayoutEdgeRoute A, FlowchartLayoutEdgeRoute B) => A.ExitSide == B.ExitSide && A.EntrySide == B.EntrySide
        && A.ExitNormalX == B.ExitNormalX && A.ExitNormalY == B.ExitNormalY && A.EntryNormalX == B.EntryNormalX && A.EntryNormalY == B.EntryNormalY
        && A.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(B.Points.Select(Point => (Point.X, Point.Y)));

}
