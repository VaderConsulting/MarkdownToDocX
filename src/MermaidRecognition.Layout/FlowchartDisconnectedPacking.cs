using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Packs disconnected top-level pieces in declared order using measured bounds.</summary>
internal static partial class FlowchartDisconnectedPacking
{
    private sealed record Piece(List<DiagramGridLayout.GridNode> Nodes, double Left, double Top,
        double Width, double Height, int Order);
    private readonly record struct Position(double X, double Y);

    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes)
    {
        if (Nodes.Count < 2)
        {
            return false;
        }

        if (Nodes.Any(Node => Node.CellNode != null))
        {
            return ApplyCells(Contract, Nodes);
        }

        Dictionary<string, int> Declared = Contract.Nodes.Select((Node, Index) => (Node.Id, Index))
            .GroupBy(Item => Item.Id, StringComparer.Ordinal)
            .ToDictionary(Group => Group.Key, Group => Group.First().Index, StringComparer.Ordinal);
        List<Piece> Pieces = [.. FindComponents(Contract, Nodes)
            .Select(Members => CreatePiece(Members, Declared)).OrderBy(Piece => Piece.Order)];
        if (Pieces.Count < 2)
        {
            return false;
        }

        int Gap = DiagramGridMetrics.NODE_GAP_PX;
        Position[] Best = [.. Enumerable.Range(0, Pieces.Count).Select(Index => new Position(Pieces[Index].Left, Pieces[Index].Top))];
        double BestScore = Score(Best, Pieces, Gap);
        string BestKey = Key(Best);
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal) { BestKey };
        for (int MaximumPerRow = 1; MaximumPerRow <= Pieces.Count; MaximumPerRow++)
        {
            Position[] Candidate = Place(Pieces, MaximumPerRow, Gap);
            string CandidateKey = Key(Candidate);
            if (!Seen.Add(CandidateKey))
            {
                continue;
            }

            double CandidateScore = Score(Candidate, Pieces, Gap);
            if (CandidateScore < BestScore - 0.0001)
            {
                Best = Candidate;
                BestScore = CandidateScore;
                BestKey = CandidateKey;
            }
        }
        Position[] Original = [.. Enumerable.Range(0, Pieces.Count).Select(Index => new Position(Pieces[Index].Left, Pieces[Index].Top))];
        if (BestKey == Key(Original))
        {
            return false;
        }

        for (int Index = 0; Index < Pieces.Count; Index++)
        {
            double Dx = Best[Index].X - Pieces[Index].Left;
            double Dy = Best[Index].Y - Pieces[Index].Top;
            foreach (DiagramGridLayout.GridNode Node in Pieces[Index].Nodes)
            {
                Node.X += Dx;
                Node.Y += Dy;
            }
        }
        return true;
    }

    /// <summary>Finds graph-connected top-level units independently of drawing or cell geometry.</summary>
    /// <param name="Contract">Visible graph and nested group ownership.</param>
    /// <param name="Nodes">Present layout nodes.</param>
    /// <returns>Complete member sets; root group members always stay together.</returns>
    private static List<List<DiagramGridLayout.GridNode>> FindComponents(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes)
    {
        Dictionary<string, string> Roots = RootGroups(Contract, Nodes);
        Dictionary<string, string> Owner = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Owner[Node.Id] = Roots.TryGetValue(Node.Id, out string? Root) ? "g:" + Root : "n:" + Node.Id;
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            Owner[Group.Id] = "g:" + RootGroup(Contract, Group.Id);
        }

        Dictionary<string, string> Parent = Owner.Values.Distinct(StringComparer.Ordinal)
            .ToDictionary(Key => Key, Key => Key, StringComparer.Ordinal);
        string Find(string Key)
        {
            while (!string.Equals(Parent[Key], Key, StringComparison.Ordinal))
            {
                Parent[Key] = Parent[Parent[Key]];
                Key = Parent[Key];
            }
            return Key;
        }
        void Union(string A, string B)
        {
            A = Find(A); B = Find(B);
            if (!string.Equals(A, B, StringComparison.Ordinal))
            {
                Parent[B] = A;
            }
        }
        foreach (EdgeContract Edge in Contract.Edges.Where(Edge =>
            !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
        {
            if (Owner.TryGetValue(Edge.SourceId, out string? Source)
                && Owner.TryGetValue(Edge.TargetId, out string? Target))
            {
                Union(Source, Target);
            }
        }

        return [.. Nodes.GroupBy(Node => Find(Owner[Node.Id]), StringComparer.Ordinal).Select(Group => Group.ToList())];
    }

    private static Piece CreatePiece(List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, int> Declared)
    {
        double Left = Nodes.Min(Node => Node.X - (Node.Width / 2.0));
        double Right = Nodes.Max(Node => Node.X + (Node.Width / 2.0));
        double Top = Nodes.Max(Node => Node.Y + (Node.Height / 2.0));
        double Bottom = Nodes.Min(Node => Node.Y - (Node.Height / 2.0));
        int Order = Nodes.Min(Node => Declared.GetValueOrDefault(Node.Id, int.MaxValue));
        return new(Nodes, Left, Top, Right - Left, Top - Bottom, Order);
    }

    private static Position[] Place(IReadOnlyList<Piece> Pieces, int MaximumPerRow, int Gap)
    {
        Position[] Result = new Position[Pieces.Count];
        double X = 0.0, Top = 0.0, RowHeight = 0.0;
        for (int Index = 0; Index < Pieces.Count; Index++)
        {
            if (Index > 0 && Index % MaximumPerRow == 0)
            {
                X = 0.0;
                Top -= RowHeight + Gap;
                RowHeight = 0.0;
            }
            Result[Index] = new(X, Top);
            X += Pieces[Index].Width + Gap;
            RowHeight = Math.Max(RowHeight, Pieces[Index].Height);
        }
        return Result;
    }

    private static double Score(Position[] Positions, IReadOnlyList<Piece> Pieces, int Gap)
    {
        double Left = Positions.Min(Position => Position.X);
        double Right = Enumerable.Range(0, Pieces.Count).Max(Index => Positions[Index].X + Pieces[Index].Width);
        double Top = Positions.Max(Position => Position.Y);
        double Bottom = Enumerable.Range(0, Pieces.Count).Min(Index => Positions[Index].Y - Pieces[Index].Height);
        double Width = Right - Left, Height = Top - Bottom;
        double Used = Pieces.Sum(Piece => (Piece.Width + Gap) * (Piece.Height + Gap));
        double Empty = Math.Max(0.0, 1.0 - (Used / Math.Max(1.0, Width * Height)));
        return FlowchartLayoutScore.Calculate(Width, Height, 0, Empty);
    }

    private static string Key(IEnumerable<Position> Positions) => string.Join(";",
        Positions.Select(Position => $"{Position.X:0.###},{Position.Y:0.###}"));

    private static Dictionary<string, string> RootGroups(DiagramContract Contract,
        IEnumerable<DiagramGridLayout.GridNode> Nodes) => Nodes.Where(Node => Node.GroupId != null)
        .ToDictionary(Node => Node.Id, Node => RootGroup(Contract, Node.GroupId!), StringComparer.Ordinal);

    private static string RootGroup(DiagramContract Contract, string GroupId)
    {
        string Current = GroupId;
        while (true)
        {
            GroupContract? Group = Contract.Groups.FirstOrDefault(Item => Item.Id == Current);
            if (Group?.ParentGroupId == null)
            {
                return Current;
            }

            Current = Group.ParentGroupId;
        }
    }
}
