using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Wraps isolated simple paths onto the measured diagram grid.</summary>
internal static partial class FlowchartChainWrapping
{
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Nodes.Count < FlowchartWrapPlan.MinimumRun + 1)
        {
            return false;
        }

        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        List<EdgeContract> Edges = Contract.Edges
            .Where(Edge => ById.ContainsKey(Edge.SourceId) && ById.ContainsKey(Edge.TargetId)
                && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Dictionary<string, List<string>> Outgoing = Nodes.ToDictionary(Node => Node.Id, _ => new List<string>(), StringComparer.Ordinal);
        Dictionary<string, List<string>> Incoming = Nodes.ToDictionary(Node => Node.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (EdgeContract Edge in Edges)
        {
            Outgoing[Edge.SourceId].Add(Edge.TargetId);
            Incoming[Edge.TargetId].Add(Edge.SourceId);
        }

        var Components = new List<List<DiagramGridLayout.GridNode>>();
        var Seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Seed in Nodes)
        {
            if (!Seen.Add(Seed.Id)) continue;
            Components.Add(CollectComponent(Seed.Id, ById, Incoming, Outgoing, Seen));
        }

        // Disconnected parts are packed together as one operation; moving only one can enlarge the whole diagram.
        if (Components.Count != 1)
        {
            return false;
        }

        bool Changed = false;
        foreach (List<DiagramGridLayout.GridNode> Component in Components)
        {
            List<DiagramGridLayout.GridNode>? Path = OrderedPath(Component, ById, Incoming, Outgoing);
            if (Path == null || Path.Count < FlowchartWrapPlan.MinimumRun + 1) continue;
            if (Path.Select(Node => Node.GroupId).Distinct(StringComparer.Ordinal).Count() != 1) continue;

            int NodeWidth = Path.Max(Node => Node.Width);
            int NodeHeight = Path.Max(Node => Node.Height);
            int ColumnGap = Horizontal
                ? Math.Max(DiagramGridMetrics.NODE_GAP_PX, DiagramGridMetrics.MIN_LR_RANK_GAP_PX)
                : DiagramGridMetrics.NODE_GAP_PX;
            int RowGap = DiagramGridMetrics.NODE_GAP_PX;
            FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
            foreach (EdgeContract Edge in Edges.Where(Edge => !string.IsNullOrWhiteSpace(Edge.Label)))
            {
                FlowchartLabelDimensions Size = FlowchartNodeSizeEstimator.MeasureWrappedLabel(
                    Edge.Label, Options, Options.EdgeLabelFontSizePx);
                ColumnGap = Math.Max(ColumnGap, RoundGap(FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    Size, Options.EdgeLabelFontSizePx, Horizontal: true) + DiagramGridRouteBuilder.CORNER_RADIUS_PX));
                RowGap = Math.Max(RowGap, RoundGap(FlowchartEdgeLabelGapHelper.ComputeRequiredSegmentLengthPx(
                    Size, Options.EdgeLabelFontSizePx, Horizontal: false) + DiagramGridRouteBuilder.CORNER_RADIUS_PX));
            }
            if (Path.Any(Node => Node.CellNode != null))
            {
                Changed |= ApplyCellPath(Path, Horizontal, ColumnGap, RowGap);
                continue;
            }
            FlowchartWrapPlan.Plan Plan = FlowchartWrapPlan.Choose(
                Path.Count, NodeWidth, NodeHeight, ColumnGap, RowGap, Horizontal);
            if (Plan.Columns >= Path.Count || Plan.Rows >= Path.Count) continue;

            double OldWidth = Path.Max(Node => Node.X + Node.Width / 2.0) - Path.Min(Node => Node.X - Node.Width / 2.0);
            double OldHeight = Path.Max(Node => Node.Y + Node.Height / 2.0) - Path.Min(Node => Node.Y - Node.Height / 2.0);
            double OldDistance = RatioDistance(OldWidth, OldHeight);
            if (RatioDistance(Plan.Width, Plan.Height) >= OldDistance - 1e-9) continue;

            Dictionary<string, (double X, double Y)> Before = Path.ToDictionary(Node => Node.Id, Node => (Node.X, Node.Y), StringComparer.Ordinal);
            double Left = Path.Min(Node => Node.X - Node.Width / 2.0);
            double Top = Path.Min(Node => Node.Y - Node.Height / 2.0);
            for (int Index = 0; Index < Path.Count; Index++)
            {
                int Column;
                int Row;
                if (Horizontal)
                {
                    Column = Index % Plan.Columns;
                    Row = Index / Plan.Columns;
                }
                else
                {
                    Column = Index / Plan.Rows;
                    Row = Index % Plan.Rows;
                }

                Path[Index].X = Left + NodeWidth / 2.0 + Column * (NodeWidth + ColumnGap);
                Path[Index].Y = Top + NodeHeight / 2.0 + Row * (NodeHeight + RowGap);
            }

            HashSet<string> ComponentIds = Path.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
            if (HasOverlap(Path, Nodes.Where(Node => !ComponentIds.Contains(Node.Id))))
            {
                foreach (DiagramGridLayout.GridNode Node in Path)
                    (Node.X, Node.Y) = Before[Node.Id];
                continue;
            }

            Changed = true;
        }

        return Changed;
    }

    private static List<DiagramGridLayout.GridNode> CollectComponent(string Seed,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> ById,
        IReadOnlyDictionary<string, List<string>> Incoming,
        IReadOnlyDictionary<string, List<string>> Outgoing,
        HashSet<string> Seen)
    {
        var Result = new List<DiagramGridLayout.GridNode>();
        var Pending = new Queue<string>();
        Pending.Enqueue(Seed);
        while (Pending.Count > 0)
        {
            string Id = Pending.Dequeue();
            Result.Add(ById[Id]);
            foreach (string Other in Incoming[Id].Concat(Outgoing[Id]))
                if (Seen.Add(Other)) Pending.Enqueue(Other);
        }

        return Result;
    }

    private static List<DiagramGridLayout.GridNode>? OrderedPath(List<DiagramGridLayout.GridNode> Component,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> ById,
        IReadOnlyDictionary<string, List<string>> Incoming,
        IReadOnlyDictionary<string, List<string>> Outgoing)
    {
        if (Component.Any(Node => Incoming[Node.Id].Count > 1 || Outgoing[Node.Id].Count > 1)) return null;
        DiagramGridLayout.GridNode[] Starts = Component.Where(Node => Incoming[Node.Id].Count == 0).ToArray();
        if (Starts.Length != 1) return null;
        var Result = new List<DiagramGridLayout.GridNode>();
        var Seen = new HashSet<string>(StringComparer.Ordinal);
        DiagramGridLayout.GridNode Current = Starts[0];
        while (Seen.Add(Current.Id))
        {
            Result.Add(Current);
            if (Outgoing[Current.Id].Count == 0) break;
            Current = ById[Outgoing[Current.Id][0]];
        }

        return Result.Count == Component.Count ? Result : null;
    }

    private static bool HasOverlap(IEnumerable<DiagramGridLayout.GridNode> Moved,
        IEnumerable<DiagramGridLayout.GridNode> Others)
    {
        foreach (DiagramGridLayout.GridNode A in Moved)
        foreach (DiagramGridLayout.GridNode B in Others)
            if (Math.Abs(A.X - B.X) < (A.Width + B.Width) / 2.0 + DiagramGridMetrics.NODE_GAP_PX
                && Math.Abs(A.Y - B.Y) < (A.Height + B.Height) / 2.0 + DiagramGridMetrics.NODE_GAP_PX)
                return true;
        return false;
    }

    private static double RatioDistance(double Width, double Height) =>
        Math.Abs(Math.Log((Width / Height) / (4.0 / 3.0)));

    private static int RoundGap(double Value) => (int)(Math.Ceiling(Value / DiagramGridMetrics.GRID_UNIT_PX)
        * DiagramGridMetrics.GRID_UNIT_PX);
}
