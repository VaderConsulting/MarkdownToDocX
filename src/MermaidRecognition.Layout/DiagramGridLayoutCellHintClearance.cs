using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Plans all hinted-terminal obstacle displacements before publishing a complete cell repair.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Clears every requested terminal against one complete tentative region set, then commits all moves or none.</summary>
    /// <param name="State">Owning diagram, fixed hints, declared regions and shared named repair budget.</param>
    private static void ResolveHintedTerminalClearanceCells(GridLayoutState State)
    {
        var Plan = new CellHintClearancePlan(State).TryBuild();
        if (Plan == null || Plan.Nodes.Count == 0 || State.MoveBudget.AcceptedMoves >= FlowchartLayoutRules.RepairChainDepth) return;
        var Ids = Plan.Nodes.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        if (State.Contract.Edges.Count(Edge => Ids.Contains(Edge.SourceId) || Ids.Contains(Edge.TargetId))
            > FlowchartLayoutRules.RepairChainLines) return;
        if (Plan.TryApply()) State.MoveBudget.AcceptedMoves++;
    }

    /// <summary>Builds one tentative complete region set while leaving the live grid untouched.</summary>
    private sealed class CellHintClearancePlan
    {
        /// <summary>Owning diagram and named candidate limits.</summary>
        private readonly GridLayoutState State;
        /// <summary>Complete unpublished targets, including every earlier obstacle considered in this repair.</summary>
        private Dictionary<string, FlowchartCellRegion> Targets;

        /// <summary>Copies only declared cell ownership, never rendered positions.</summary>
        /// <param name="State">Owning cell-bound diagram.</param>
        internal CellHintClearancePlan(GridLayoutState State)
        {
            this.State = State;
            Targets = new(FlowchartCellRepair.Capture(State.Placed).Regions, StringComparer.Ordinal);
        }

        /// <summary>Builds all leave/enter clearances before returning one final assignment.</summary>
        /// <returns>A complete validated plan, or null without mutation if any terminal cannot be cleared.</returns>
        internal FlowchartCellRepair.Assignment? TryBuild()
        {
            var ById = State.Placed.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
            List<(GridNode Node, string Side, double Length, EdgeContract Edge)> Terminals = [];
            foreach (var Edge in State.Contract.Edges.Where(Edge => Edge.RoutingHint?.IsActive == true).OrderBy(Edge => Edge.CreationIndex))
            {
                var Hint = Edge.RoutingHint!;
                if (Hint.Leave.HasValue && ById.TryGetValue(Edge.SourceId, out var Source))
                    Terminals.Add((Source, HintSide(Hint.Leave.Value), 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX, Edge));
                if (Hint.Enter.HasValue && ById.TryGetValue(Edge.TargetId, out var Target))
                    Terminals.Add((Target, HintSide(Hint.Enter.Value), DiagramGridRouteBuilder.CORNER_RADIUS_PX
                        + DiagramGridRouteBuilder.ARROWHEAD_LENGTH_PX, Edge));
            }
            foreach (var Terminal in Terminals)
                if (!ClearRay(Terminal.Node, Terminal.Side, Terminal.Length, Terminal.Edge)) return null;
            // A later displaced obstacle may itself be an earlier link's endpoint.
            // Validate the entire final assignment rather than trusting earlier rays.
            foreach (var Terminal in Terminals)
                if (!ClearRay(Terminal.Node, Terminal.Side, Terminal.Length, Terminal.Edge, CheckOnly: true)) return null;
            return FlowchartCellRepair.PlanAssignment(State.Placed, Targets);
        }

        /// <summary>Accumulates uncommitted target rectangles for one terminal; failure discards the entire caller's repair.</summary>
        /// <param name="Endpoint">Hinted connection endpoint.</param>
        /// <param name="Side">Requested eligible compass side.</param>
        /// <param name="Length">Measured straight terminal clearance.</param>
        /// <param name="Edge">Declared link whose other endpoint is excluded as an obstacle.</param>
        /// <param name="CheckOnly">True for the final all-terminal validation, which cannot propose any more moves.</param>
        /// <returns>Whether the ray can be cleared by complete legal target assignments within the existing limits.</returns>
        private bool ClearRay(GridNode Endpoint, string Side, double Length, EdgeContract Edge, bool CheckOnly = false)
        {
            if (State.HintedNodePositions.ContainsKey(Endpoint.Id)) return true;
            var Box = Endpoint.CellNode!.Bounds(Targets[Endpoint.Id], Endpoint.GeometryProvider);
            var Geometry = Box.Geometry ?? NodeGeometry.Rectangle(Box.Width, Box.Height);
            if (!Geometry.ConnectionBoundary.TryPort(Side, 0.5, out var Port)) return false;
            bool Routing = Endpoint.CellNode.UsesRoutingCoordinates;
            double X1 = Box.CenterX + Port.X, Y1 = Box.CenterY + (Routing ? -Port.Y : Port.Y);
            double Dx = Side == "right" ? Length : Side == "left" ? -Length : 0;
            double Dy = Side == "bottom" ? Length : Side == "top" ? -Length : 0;
            if (Routing) Dy = -Dy;
            double X2 = X1 + Dx, Y2 = Y1 + Dy;
            bool Vertical = Side is "top" or "bottom";
            var Ray = (X1, Y1, X2, Y2, Vertical);
            for (int Guard = 0; Guard < State.Placed.Count; Guard++)
            {
                var Obstacle = State.Placed.FirstOrDefault(Node => Node.Id != Edge.SourceId && Node.Id != Edge.TargetId
                    && Blocked(Node, Targets[Node.Id], Ray));
                if (Obstacle == null) return true;
                if (CheckOnly) return false;
                if (Obstacle.PlacementHintLocked || State.HintedNodePositions.ContainsKey(Obstacle.Id)
                    || RouteRepairStage.HasHintedContainingGroup(State.Contract, Obstacle.Id)
                    || HasExternalCellHintDependent(State.Contract, Obstacle.Id, [Obstacle])) return false;
                var Before = Targets[Obstacle.Id];
                bool Found = false;
                foreach (int Distance in Enumerable.Range(1, FlowchartLayoutRules.RepairChainDepth))
                {
                    foreach (int Sign in new[] { -1, 1 })
                    {
                        if (State.MoveBudget.CandidateMoves >= FlowchartLayoutRules.RepairCandidateMoves) return false;
                        State.MoveBudget.CandidateMoves++;
                        var Proposed = new FlowchartCellRegion(checked(Before.StartRow + (Vertical ? 0 : Sign * Distance)),
                            checked(Before.StartColumn + (Vertical ? Sign * Distance : 0)), Before.RowSpan, Before.ColumnSpan);
                        var Complete = new Dictionary<string, FlowchartCellRegion>(Targets, StringComparer.Ordinal)
                            { [Obstacle.Id] = Proposed };
                        if (Blocked(Obstacle, Proposed, Ray) || FlowchartCellRepair.PlanAssignment(State.Placed, Complete) == null) continue;
                        Targets = Complete;
                        Found = true;
                        break;
                    }
                    if (Found) break;
                }
                if (!Found) return false;
            }
            return false;
        }

        /// <summary>Measures ray clearance against bounds projected from an explicit candidate rectangle.</summary>
        /// <param name="Obstacle">Foreign node, retaining its renderer geometry definition.</param>
        /// <param name="Region">Complete tentative reservation.</param>
        /// <param name="Ray">Finite terminal ray in the same drawing coordinate view.</param>
        /// <returns>Whether the finite terminal ray enters the node's drawing bounds.</returns>
        private static bool Blocked(GridNode Obstacle, FlowchartCellRegion Region,
            (double X1, double Y1, double X2, double Y2, bool Vertical) Ray)
        {
            var Bounds = Obstacle.CellNode!.Bounds(Region, Obstacle.GeometryProvider);
            double Left = Bounds.CenterX - Bounds.Width / 2, Right = Bounds.CenterX + Bounds.Width / 2;
            double Top = Bounds.CenterY - Bounds.Height / 2, Bottom = Bounds.CenterY + Bounds.Height / 2;
            return Ray.Vertical
                ? Ray.X1 > Left + 0.5 && Ray.X1 < Right - 0.5 && Math.Max(Math.Min(Ray.Y1, Ray.Y2), Top) < Math.Min(Math.Max(Ray.Y1, Ray.Y2), Bottom) - 0.5
                : Ray.Y1 > Top + 0.5 && Ray.Y1 < Bottom - 0.5 && Math.Max(Math.Min(Ray.X1, Ray.X2), Left) < Math.Min(Math.Max(Ray.X1, Ray.X2), Right) - 0.5;
        }
    }
}
