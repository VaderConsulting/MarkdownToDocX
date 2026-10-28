using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;

namespace MermaidRecognition.RuleValidation;

public static partial class UnnecessaryBendChecker
{
    /// <summary>Checks allocation, whole-cell alignment, outside-margin detours and far-side hub entries against proven complete alternatives.</summary>
    /// <param name="DiagramId">Catalogue identity.</param>
    /// <param name="Contract">Unchanged declarations and honoured hints.</param>
    /// <param name="Layout">Completed production model, never mutated by this check.</param>
    /// <returns>Expanded findings with exact better route endpoints or a named far-side violation.</returns>
    private static IEnumerable<UnnecessaryBendViolation> CheckLayoutAlternatives(string DiagramId, DiagramContract Contract, FlowchartLayoutResult Layout)
    {
        // These witnesses describe the shared production renderer. Other kinds
        // require their own drawn geometry, not a synthetic flowchart model.
        if (!Contract.Kind.Equals("flowchart", StringComparison.OrdinalIgnoreCase)
            && !Contract.Kind.Equals("graph", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        Dictionary<string, FlowchartLayoutEdgeRoute> ById = Layout.EdgeRoutes.Where(Route => Route.Points.Count >= 2).ToDictionary(Route => Route.EdgeId);
        if (Layout.NodeBounds.Count == 0)
        {
            yield break;
        }

        foreach (FlowchartBendRemovingPortRepair.Improvement? Improvement in FlowchartBendRemovingPortRepair.Inspect(Contract, Layout).GroupBy(Item => Item.LineId).Select(Group => Group.Last()))
        {
            FlowchartLayoutEdgeRoute Route = ById[Improvement.LineId];
            (double X, double Y) First = Improvement.Points[0]; (double X, double Y) Last = Improvement.Points[^1];
            yield return Expanded(DiagramId, Route, 'P', Improvement.Points,
                $"port-allocation bend; better ports ({First.X:R},{First.Y:R}) -> ({Last.X:R},{Last.Y:R})");
        }
        foreach (FlowchartLayoutEdgeRoute? Route in ById.Values)
        {
            bool Hinted = DiagramGridLayout.FindContractEdge(Contract, Route)?.RoutingHint?.IsActive == true;
            if (Route.Points.Count > 2 && !Hinted)
            {
                FlowchartLayoutAlternatives.Witness? Move = FlowchartLayoutAlternatives.Straighten(Contract, Layout, Route);
                if (Move != null)
                {
                    yield return Expanded(DiagramId, Route, 'S', Better(Move), "placement bend under 5.10; " + Move.Detail);
                }
            }
            FlowchartLayoutAlternatives.Witness? Interior = FlowchartLayoutAlternatives.Interior(Contract, Layout, Route);
            if (Interior != null)
            {
                yield return Expanded(DiagramId, Route, 'M', Better(Interior), Interior.Detail);
            }

            int Degree = Contract.Edges.Count(Edge => Edge.Stroke != "invisible" && (Edge.SourceId == Route.TargetId || Edge.TargetId == Route.TargetId));
            if (Degree >= 3 && !Hinted && FlowchartLayoutAlternatives.FarSide(Layout, Route))
            {
                FlowchartLayoutAlternatives.Witness? Facing = FlowchartLayoutAlternatives.FacingHub(Contract, Layout, Route);
                yield return Expanded(DiagramId, Route, 'H', Facing == null ? [] : Better(Facing),
                    "hub entry on far side " + Route.EntrySide + (Facing == null ? "; no non-worsening joint alternative found within the existing limits" : "; " + Facing.Detail));
            }
        }
    }

    /// <summary>Reads a proven complete candidate's exact better route without returning mutable model points.</summary>
    /// <param name="Witness">Independently owned candidate.</param>
    /// <returns>Exact better polyline.</returns>
    private static (double X, double Y)[] Better(FlowchartLayoutAlternatives.Witness Witness) =>
        [.. Witness.Layout.EdgeRoutes.Single(Route => Route.EdgeId == Witness.LineId).Points.Select(Point => (Point.X, Point.Y))];

    /// <summary>Publishes a named expanded finding using the original line's bend and length measurements.</summary>
    /// <param name="DiagramId">Catalogue identity.</param>
    /// <param name="Route">Original immutable line.</param>
    /// <param name="Check">Case-sensitive independent baseline key: P, S, M or H.</param>
    /// <param name="Better">Proven alternative, or no route when a far-side convention violation has no found repair.</param>
    /// <param name="Detail">Exact ports, whole-cell move or violated face.</param>
    /// <returns>An independently stored finding.</returns>
    private static UnnecessaryBendViolation Expanded(string DiagramId, FlowchartLayoutEdgeRoute Route, char Check,
        IReadOnlyList<(double X, double Y)> Better, string Detail) => new(DiagramId, Route.EdgeId, Check,
            FlowchartLayoutScore.CountCorners(Route.Points), Enumerable.Range(1, Route.Points.Count - 1).Sum(Index =>
                Math.Abs(Route.Points[Index].X - Route.Points[Index - 1].X) + Math.Abs(Route.Points[Index].Y - Route.Points[Index - 1].Y)),
            Better.Select(Point => new BendPoint(Point.X, Point.Y)).ToArray(), Detail);
}
