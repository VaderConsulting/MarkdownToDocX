using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Wraps long tails beneath a wide branch while preserving the branch order and its existing routes.</summary>
internal static class FlowchartComponentWrapping
{
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, bool Horizontal)
    {
        if (Horizontal || Nodes.Any(Node => Node.GroupId != null)) return false;
        var ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        var Outgoing = Nodes.ToDictionary(Node => Node.Id, _ => new List<string>(), StringComparer.Ordinal);
        var Incoming = Nodes.ToDictionary(Node => Node.Id, _ => new List<string>(), StringComparer.Ordinal);
        var Adjacent = Nodes.ToDictionary(Node => Node.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (EdgeContract Edge in Contract.Edges.Where(Edge => ById.ContainsKey(Edge.SourceId)
            && ById.ContainsKey(Edge.TargetId) && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
        {
            Outgoing[Edge.SourceId].Add(Edge.TargetId);
            Incoming[Edge.TargetId].Add(Edge.SourceId);
            Adjacent[Edge.SourceId].Add(Edge.TargetId);
            Adjacent[Edge.TargetId].Add(Edge.SourceId);
        }

        DiagramGridLayout.GridNode? Root = Nodes
            .Where(Node => Outgoing[Node.Id].Distinct(StringComparer.Ordinal).Count() >= FlowchartWrapPlan.MinimumRun)
            .OrderByDescending(Node => Outgoing[Node.Id].Distinct(StringComparer.Ordinal).Count()).FirstOrDefault();
        if (Root == null) return false;

        var Order = Contract.Nodes.Select((Node, Index) => (Node.Id, Index))
            .GroupBy(Item => Item.Id, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First().Index, StringComparer.Ordinal);
        List<List<DiagramGridLayout.GridNode>> Components = Collect(Nodes, ById, Adjacent, Order);
        if (Components.Count < 2) return false;
        List<DiagramGridLayout.GridNode> Main = Components.Single(Component => Component.Contains(Root));
        var Fan = Outgoing[Root.Id].Distinct(StringComparer.Ordinal).Where(ById.ContainsKey).Select(Id => ById[Id]).ToList();
        DiagramGridLayout.GridNode Continuation = Fan.FirstOrDefault(Node => Outgoing[Node.Id].Count > 0) ?? Fan[^1];
        var HeadIds = Fan.Select(Node => Node.Id).Append(Root.Id).ToHashSet(StringComparer.Ordinal);
        List<DiagramGridLayout.GridNode> Tail = Main.Where(Node => !HeadIds.Contains(Node.Id))
            .OrderBy(Node => Order.GetValueOrDefault(Node.Id, int.MaxValue)).ToList();
        if (Tail.Count < FlowchartWrapPlan.MinimumRun) return false;
        var RelationalLeaves = Fan.Where(Node => Incoming[Node.Id].Any(Id => Tail.Contains(ById[Id]))).ToList();
        List<DiagramGridLayout.GridNode> FanGrid = Fan.Where(Node => Node != Continuation && !RelationalLeaves.Contains(Node))
            .OrderBy(Node => Order.GetValueOrDefault(Node.Id, int.MaxValue)).ToList();

        int NodeWidth = Nodes.Max(Node => Node.Width);
        int NodeHeight = Nodes.Max(Node => Node.Height);
        int ColumnPitch = NodeWidth + DiagramGridMetrics.NODE_GAP_PX;
        int RowPitch = NodeHeight + DiagramGridMetrics.NODE_GAP_PX;
        Plan Best = Choose(Contract, Components, Main, Tail, FanGrid.Count, RelationalLeaves.Count,
            NodeWidth, NodeHeight, ColumnPitch, RowPitch);
        double OldWidth = Span(Nodes, true);
        double Usable = FlowchartLayoutRules.DiagramUsableWidthPx;
        if (OldWidth <= Usable + 1e-9 || Best.Width >= OldWidth - 1e-9) return false;

        double Left = Nodes.Min(Node => Node.X - Node.Width / 2.0);
        double Top = Nodes.Max(Node => Node.Y + Node.Height / 2.0);
        double NextX = Left + NodeWidth / 2.0;
        foreach (List<DiagramGridLayout.GridNode> Component in Components.Where(Component => Component != Main))
        {
            PlaceColumnMajor(Component, NextX, Top, Best.Run, 1, NodeWidth, NodeHeight, ColumnPitch, RowPitch);
            NextX += (DivideRoundUp(Component.Count, Best.Run) + 1) * ColumnPitch;
        }
        double FanLeft = NextX;
        Root.X = FanLeft + ((Best.FanColumns - 1) / 2.0) * ColumnPitch;
        Root.Y = Top - NodeHeight / 2.0;
        Root.Width = Math.Max(Root.Width,
            Best.FanColumns * NodeWidth + Math.Max(0, Best.FanColumns - 1) * (ColumnPitch - NodeWidth));
        for (int Index = 0; Index < FanGrid.Count; Index++)
        {
            FanGrid[Index].X = FanLeft + (Index % Best.FanColumns) * ColumnPitch;
            FanGrid[Index].Y = Top - NodeHeight / 2.0 - (1 + Index / Best.FanColumns) * RowPitch;
        }
        double TailX = FanLeft + (Best.FanColumns + 1) * ColumnPitch;
        Continuation.X = TailX;
        Continuation.Y = Top - NodeHeight / 2.0;
        double TailTop = Continuation.Y - Continuation.Height / 2.0 - DiagramGridMetrics.NODE_GAP_PX;
        PlaceColumnMajor(Tail, TailX, TailTop, Best.Run, 1, NodeWidth, NodeHeight, ColumnPitch, RowPitch, Best.Offset);

        double ExteriorX = TailX + (DivideRoundUp(Tail.Count + Best.Offset, Best.Run) + 1) * ColumnPitch;
        foreach (DiagramGridLayout.GridNode Child in RelationalLeaves)
        {
            DiagramGridLayout.GridNode Parent = Incoming[Child.Id].Where(Id => Tail.Contains(ById[Id]))
                .Select(Id => ById[Id]).First();
            Child.X = ExteriorX;
            Child.Y = Parent.Y;
            ExteriorX += ColumnPitch;
        }
        return true;
    }

    private static List<List<DiagramGridLayout.GridNode>> Collect(List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> ById,
        IReadOnlyDictionary<string, List<string>> Adjacent,
        IReadOnlyDictionary<string, int> Order)
    {
        var Result = new List<List<DiagramGridLayout.GridNode>>();
        var Seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Seed in Nodes.OrderBy(Node => Order.GetValueOrDefault(Node.Id, int.MaxValue)))
        {
            if (!Seen.Add(Seed.Id)) continue;
            var Component = new List<DiagramGridLayout.GridNode>();
            var Pending = new Queue<string>();
            Pending.Enqueue(Seed.Id);
            while (Pending.Count > 0)
            {
                string Id = Pending.Dequeue();
                Component.Add(ById[Id]);
                foreach (string Other in Adjacent[Id].OrderBy(Id => Order.GetValueOrDefault(Id, int.MaxValue)))
                    if (Seen.Add(Other)) Pending.Enqueue(Other);
            }
            Component.Sort((A, B) => Order.GetValueOrDefault(A.Id, int.MaxValue).CompareTo(Order.GetValueOrDefault(B.Id, int.MaxValue)));
            Result.Add(Component);
        }
        return Result;
    }

    private static Plan Choose(DiagramContract Contract, List<List<DiagramGridLayout.GridNode>> Components,
        List<DiagramGridLayout.GridNode> Main, List<DiagramGridLayout.GridNode> Tail, int FanCount, int RelationalLeafCount,
        int NodeWidth, int NodeHeight, int ColumnPitch, int RowPitch)
    {
        double Usable = FlowchartLayoutRules.DiagramUsableWidthPx;
        var Candidates = new List<(Plan Plan, double WidthOverflow, int Alignment, int EndBalance)>();
        var TailIndex = Tail.Select((Node, Index) => (Node.Id, Index)).ToDictionary(Item => Item.Id, Item => Item.Index, StringComparer.Ordinal);
        int Maximum = Math.Max(Tail.Count, Components.Where(Component => Component != Main).DefaultIfEmpty([]).Max(Component => Component.Count));
        int MinimumFanColumns = Math.Max(Math.Min(FanCount, FlowchartWrapPlan.MinimumRun),
            DivideRoundUp(FanCount, DiagramGridMetrics.TRACKS_PER_GAP + 1));
        for (int Run = FlowchartWrapPlan.MinimumRun; Run <= Maximum; Run++)
        for (int FanColumns = MinimumFanColumns; FanColumns <= Math.Max(1, FanCount); FanColumns++)
        for (int Offset = 0; Offset < Run; Offset++)
        {
            int PackedColumns = FanColumns + 1 + DivideRoundUp(Tail.Count + Offset, Run) + RelationalLeafCount;
            foreach (List<DiagramGridLayout.GridNode> Component in Components.Where(Component => Component != Main))
                PackedColumns += 1 + DivideRoundUp(Component.Count, Run);
            int Columns = PackedColumns;
            int Rows = Math.Max(1 + DivideRoundUp(FanCount, FanColumns), Run);
            int Width = Columns * NodeWidth + Math.Max(0, Columns - 1) * (ColumnPitch - NodeWidth);
            int Height = Rows * NodeHeight + Math.Max(0, Rows - 1) * (RowPitch - NodeHeight);
            int Alignment = 0;
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (!TailIndex.TryGetValue(Edge.SourceId, out int Source) || !TailIndex.TryGetValue(Edge.TargetId, out int Target)
                    || Math.Abs(Source - Target) < FlowchartWrapPlan.MinimumRun + 1) continue;
                (int SourceColumn, int SourceRow) = Cell(Source + Offset, Run);
                (int TargetColumn, int TargetRow) = Cell(Target + Offset, Run);
                if (SourceColumn != TargetColumn && SourceRow != TargetRow)
                    Alignment += Math.Abs(SourceRow - TargetRow);
            }
            var Candidate = new Plan(Run, FanColumns, Offset, Width, Height);
            int Capacity = DivideRoundUp(Tail.Count + Offset, Run) * Run;
            int Trailing = Capacity - Tail.Count - Offset;
            double Overflow = Math.Max(0, Width - Usable);
            Candidates.Add((Candidate, Overflow, Alignment, Math.Abs(Offset - Trailing)));
        }
        return Candidates
            .OrderBy(Candidate => Candidate.WidthOverflow)
            .ThenBy(Candidate => Candidate.Plan.Width * (long)Candidate.Plan.Height)
            .ThenBy(Candidate => Candidate.EndBalance)
            .ThenBy(Candidate => Candidate.Alignment)
            .First().Plan;
    }

    private static void PlaceColumnMajor(List<DiagramGridLayout.GridNode> Nodes, double FirstX, double Top,
        int Rows, int ColumnDirection, int NodeWidth, int NodeHeight, int ColumnPitch, int RowPitch, int Offset = 0)
    {
        for (int Index = 0; Index < Nodes.Count; Index++)
        {
            (int Column, int Row) = Cell(Index + Offset, Rows);
            Nodes[Index].X = FirstX + ColumnDirection * Column * ColumnPitch;
            Nodes[Index].Y = Top - NodeHeight / 2.0 - Row * RowPitch;
        }
    }

    private static (int Column, int Row) Cell(int Index, int Rows)
    {
        int Column = Index / Rows;
        int WithinColumn = Index % Rows;
        return (Column, Column % 2 == 0 ? WithinColumn : Rows - 1 - WithinColumn);
    }

    private static int DivideRoundUp(int Value, int Divisor) => (Value + Divisor - 1) / Divisor;
    private static double Span(IEnumerable<DiagramGridLayout.GridNode> Nodes, bool X) => X
        ? Nodes.Max(Node => Node.X + Node.Width / 2.0) - Nodes.Min(Node => Node.X - Node.Width / 2.0)
        : Nodes.Max(Node => Node.Y + Node.Height / 2.0) - Nodes.Min(Node => Node.Y - Node.Height / 2.0);
    private sealed record Plan(int Run, int FanColumns, int Offset, int Width, int Height);
}
