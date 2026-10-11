using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Wraps long tails beneath a wide branch while preserving the branch order and its existing routes.</summary>
internal static class FlowchartComponentWrapping
{
    private const double TargetRatio = 4.0 / 3.0;

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

        int NodeWidth = Nodes.Max(Node => Node.Width);
        int NodeHeight = Nodes.Max(Node => Node.Height);
        int ColumnPitch = NodeWidth + DiagramGridMetrics.NODE_GAP_PX;
        int RowPitch = NodeHeight + DiagramGridMetrics.NODE_GAP_PX;
        double ContinuationY = Root.Y - RowPitch;
        double HeadLeft = HeadIds.Select(Id => ById[Id].X - ById[Id].Width / 2.0).Min();
        double HeadRight = HeadIds.Select(Id => ById[Id].X + ById[Id].Width / 2.0).Max();
        double HeadTop = Root.Y + Root.Height / 2.0;
        double HeadBottom = ContinuationY - Continuation.Height / 2.0;
        int HeadColumns = Math.Max(1, (int)Math.Ceiling((HeadRight - HeadLeft + DiagramGridMetrics.NODE_GAP_PX) / ColumnPitch));
        Plan Best = Choose(Contract, Components, Main, Tail, HeadColumns, HeadTop - HeadBottom,
            NodeWidth, NodeHeight, ColumnPitch, RowPitch);
        double OldWidth = Span(Nodes, true), OldHeight = Span(Nodes, false);
        if (Distance(Best.Width, Best.Height) >= Distance(OldWidth, OldHeight) - 1e-9) return false;

        Continuation.Y = ContinuationY;
        double TailTop = HeadBottom - DiagramGridMetrics.NODE_GAP_PX;
        PlaceColumnMajor(Tail, Continuation.X + ColumnPitch, TailTop, Best.Run, 1, NodeWidth, NodeHeight, ColumnPitch, RowPitch);

        double NextX = HeadLeft - DiagramGridMetrics.NODE_GAP_PX - NodeWidth / 2.0;
        foreach (List<DiagramGridLayout.GridNode> Component in Components.Where(Component => Component != Main))
        {
            PlaceColumnMajor(Component, NextX, TailTop, Best.Run, -1, NodeWidth, NodeHeight, ColumnPitch, RowPitch);
            NextX -= (DivideRoundUp(Component.Count, Best.Run) + 1) * ColumnPitch;
        }
        double TerminalY = Nodes.Where(Node => !Fan.Contains(Node)).Min(Node => Node.Y - Node.Height / 2.0)
            - DiagramGridMetrics.NODE_GAP_PX - NodeHeight / 2.0;
        var RelationalLeaves = Fan.Where(Node => Incoming[Node.Id].Any(Id => Tail.Contains(ById[Id]))).ToList();
        foreach (DiagramGridLayout.GridNode Child in Fan.Where(Node => Outgoing[Node.Id].Count == 0 && !RelationalLeaves.Contains(Node)))
            Child.Y = TerminalY;
        double ExteriorX = Nodes.Where(Node => !RelationalLeaves.Contains(Node)).Max(Node => Node.X + Node.Width / 2.0)
            + DiagramGridMetrics.NODE_GAP_PX + NodeWidth / 2.0;
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
        List<DiagramGridLayout.GridNode> Main, List<DiagramGridLayout.GridNode> Tail, int HeadColumns, double HeadHeight,
        int NodeWidth, int NodeHeight, int ColumnPitch, int RowPitch)
    {
        var Candidates = new List<(Plan Plan, double Distance, int Alignment)>();
        var TailIndex = Tail.Select((Node, Index) => (Node.Id, Index)).ToDictionary(Item => Item.Id, Item => Item.Index, StringComparer.Ordinal);
        int Maximum = Math.Max(Tail.Count, Components.Where(Component => Component != Main).DefaultIfEmpty([]).Max(Component => Component.Count));
        for (int Run = FlowchartWrapPlan.MinimumRun; Run <= Maximum; Run++)
        {
            int Columns = HeadColumns;
            int PackedColumns = DivideRoundUp(Tail.Count, Run);
            foreach (List<DiagramGridLayout.GridNode> Component in Components.Where(Component => Component != Main))
                PackedColumns += 1 + DivideRoundUp(Component.Count, Run);
            Columns = Math.Max(Columns, PackedColumns);
            int Rows = Run;
            int Width = Columns * NodeWidth + Math.Max(0, Columns - 1) * (ColumnPitch - NodeWidth);
            int Height = (int)Math.Ceiling(HeadHeight) + DiagramGridMetrics.NODE_GAP_PX
                + Rows * NodeHeight + Math.Max(0, Rows - 1) * (RowPitch - NodeHeight);
            int Alignment = 0;
            foreach (EdgeContract Edge in Contract.Edges)
            {
                if (!TailIndex.TryGetValue(Edge.SourceId, out int Source) || !TailIndex.TryGetValue(Edge.TargetId, out int Target)
                    || Math.Abs(Source - Target) < FlowchartWrapPlan.MinimumRun + 1) continue;
                (int SourceColumn, int SourceRow) = Cell(Source, Run);
                (int TargetColumn, int TargetRow) = Cell(Target, Run);
                if (SourceColumn != TargetColumn && SourceRow != TargetRow)
                    Alignment += Math.Abs(SourceRow - TargetRow);
            }
            var Candidate = new Plan(Run, Width, Height);
            Candidates.Add((Candidate, Distance(Width, Height), Alignment));
        }
        double Closest = Candidates.Min(Candidate => Candidate.Distance);
        return Candidates.Where(Candidate => Candidate.Distance <= Closest + 0.15)
            .OrderBy(Candidate => Candidate.Alignment)
            .ThenBy(Candidate => Candidate.Distance)
            .ThenBy(Candidate => Candidate.Plan.Width * (long)Candidate.Plan.Height)
            .First().Plan;
    }

    private static void PlaceColumnMajor(List<DiagramGridLayout.GridNode> Nodes, double FirstX, double Top,
        int Rows, int ColumnDirection, int NodeWidth, int NodeHeight, int ColumnPitch, int RowPitch)
    {
        for (int Index = 0; Index < Nodes.Count; Index++)
        {
            (int Column, int Row) = Cell(Index, Rows);
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
    private static double Distance(double Width, double Height) => Math.Abs(Math.Log((Width / Height) / TargetRatio));
    private sealed record Plan(int Run, int Width, int Height);
}
