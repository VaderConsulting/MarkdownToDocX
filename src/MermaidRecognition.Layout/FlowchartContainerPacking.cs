using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Packs intact containers according to their relationship neighbourhoods.</summary>
internal static class FlowchartContainerPacking
{
    internal sealed record Unit(DiagramGridLayout.GridFrame Frame, List<DiagramGridLayout.GridNode> Members);
    private readonly record struct Position(double X, double Y);
    private sealed record Link(int Source, int Target, string SourceId, string TargetId,
        Position SourceOffset, Position TargetOffset);

    internal static void Apply(DiagramContract Contract, List<Unit> Units)
    {
        int Gap = 3 * DiagramGridMetrics.GRID_UNIT_PX;
        int RowWidth = DiagramGridMetrics.CeilToGrid(Math.Sqrt(Units.Sum(Unit =>
            (double)(Unit.Frame.Right - Unit.Frame.Left + Gap) * (Unit.Frame.Bottom - Unit.Frame.Top + Gap))));
        var Owner = new Dictionary<string, int>(StringComparer.Ordinal);
        var Offsets = new Dictionary<string, Position>(StringComparer.Ordinal);
        for (int Index = 0; Index < Units.Count; Index++)
        {
            var Unit = Units[Index];
            Owner[Unit.Frame.Id] = Index;
            Offsets[Unit.Frame.Id] = new((Unit.Frame.Right - Unit.Frame.Left) / 2.0,
                (Unit.Frame.Bottom - Unit.Frame.Top) / 2.0);
            foreach (var Member in Unit.Members)
            {
                Owner[Member.Id] = Index;
                Offsets[Member.Id] = new(Member.X - Unit.Frame.Left, Member.Y - Unit.Frame.Top);
            }
        }
        var Links = Contract.Edges.Where(Edge => Owner.ContainsKey(Edge.SourceId) && Owner.ContainsKey(Edge.TargetId)
            && Owner[Edge.SourceId] != Owner[Edge.TargetId]
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .Select(Edge => new Link(Owner[Edge.SourceId], Owner[Edge.TargetId], Edge.SourceId, Edge.TargetId,
                Offsets[Edge.SourceId], Offsets[Edge.TargetId])).ToList();
        var Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        int Sign = Direction is FlowchartDirection.BottomToTop or FlowchartDirection.RightToLeft ? -1 : 1;

        Position[] Place(int[] Order)
        {
            var Positions = new Position[Units.Count];
            int X = 0, Y = 0, RowHeight = 0;
            foreach (int Index in Order)
            {
                var Frame = Units[Index].Frame;
                int Width = DiagramGridMetrics.CeilToGrid(Frame.Right - Frame.Left);
                int Height = DiagramGridMetrics.CeilToGrid(Frame.Bottom - Frame.Top);
                if (X > 0 && X + Width > RowWidth)
                {
                    X = 0; Y += RowHeight + Gap; RowHeight = 0;
                }
                Positions[Index] = new(X, Y);
                X += Width + Gap;
                RowHeight = Math.Max(RowHeight, Height);
            }
            return Positions;
        }

        double Cost(Position[] Positions)
        {
            double Result = 0;
            var Runs = new List<(Position A, Position B, Link Link)>();
            foreach (var Link in Links)
            {
                var A = new Position(Positions[Link.Source].X + Link.SourceOffset.X,
                    Positions[Link.Source].Y + Link.SourceOffset.Y);
                var B = new Position(Positions[Link.Target].X + Link.TargetOffset.X,
                    Positions[Link.Target].Y + Link.TargetOffset.Y);
                Result += Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
                double MainDistance = Horizontal ? B.X - A.X : B.Y - A.Y;
                if (MainDistance * Sign <= 0) Result += 2 * Gap;
                var FirstBend = Horizontal ? new Position((A.X + B.X) / 2, A.Y) : new Position(A.X, (A.Y + B.Y) / 2);
                var SecondBend = Horizontal ? new Position(FirstBend.X, B.Y) : new Position(B.X, FirstBend.Y);
                Runs.Add((A, FirstBend, Link)); Runs.Add((FirstBend, SecondBend, Link)); Runs.Add((SecondBend, B, Link));
            }
            for (int First = 0; First < Runs.Count; First++)
            for (int Second = First + 1; Second < Runs.Count; Second++)
            {
                var A = Runs[First]; var B = Runs[Second];
                if (A.Link == B.Link || A.Link.SourceId == B.Link.SourceId || A.Link.TargetId == B.Link.TargetId) continue;
                bool AH = A.A.Y == A.B.Y, BH = B.A.Y == B.B.Y;
                if (AH == BH) continue;
                var H = AH ? A : B; var V = AH ? B : A;
                if (V.A.X > Math.Min(H.A.X, H.B.X) && V.A.X < Math.Max(H.A.X, H.B.X)
                    && H.A.Y > Math.Min(V.A.Y, V.B.Y) && H.A.Y < Math.Max(V.A.Y, V.B.Y))
                    Result += 10 * Gap;
            }
            return Result;
        }

        int[] Order = Enumerable.Range(0, Units.Count).ToArray();
        double Best = Cost(Place(Order));
        for (int Pass = 0; Pass < Units.Count; Pass++)
        {
            bool Changed = false;
            for (int First = 0; First < Order.Length; First++)
            for (int Second = First + 1; Second < Order.Length; Second++)
            {
                (Order[First], Order[Second]) = (Order[Second], Order[First]);
                double Candidate = Cost(Place(Order));
                if (Candidate < Best - 0.01) { Best = Candidate; Changed = true; }
                else (Order[First], Order[Second]) = (Order[Second], Order[First]);
            }
            if (!Changed) break;
        }
        var Final = Place(Order);
        for (int Index = 0; Index < Units.Count; Index++)
        {
            var Unit = Units[Index];
            double Snap(double Delta) => Math.Sign(Delta) * DiagramGridMetrics.CeilToGrid(Math.Abs(Delta));
            double Dx = Snap(Final[Index].X - Unit.Frame.Left), Dy = Snap(Final[Index].Y - Unit.Frame.Top);
            foreach (var Member in Unit.Members) { Member.X += Dx; Member.Y += Dy; }
        }
    }
}
