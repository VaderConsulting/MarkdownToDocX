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
    private readonly record struct Run(Position A, Position B, Link Link);

    private static int CountCrossings(IReadOnlyList<Run> runs)
    {
        // Orthogonal sweep: insert horizontal runs only after vertical queries at
        // their left endpoint and remove them before queries at their right
        // endpoint. This preserves the strict-interior crossing test below.
        var events = new List<(double X, int Kind, int Index)>();
        var active = new SortedSet<(double Y, int Index)>();
        for (int index = 0; index < runs.Count; index++)
        {
            Run run = runs[index];
            bool horizontal = run.A.Y == run.B.Y;
            bool vertical = run.A.X == run.B.X;
            if (horizontal && run.A.X != run.B.X)
            {
                events.Add((Math.Min(run.A.X, run.B.X), 2, index)); // add after endpoint queries
                events.Add((Math.Max(run.A.X, run.B.X), 0, index)); // remove before endpoint queries
            }
            else if (vertical && run.A.Y != run.B.Y)
            {
                events.Add((run.A.X, 1, index));
            }
        }

        events.Sort((left, right) =>
        {
            int compare = left.X.CompareTo(right.X);
            if (compare != 0) return compare;
            compare = left.Kind.CompareTo(right.Kind);
            return compare != 0 ? compare : left.Index.CompareTo(right.Index);
        });

        int crossings = 0;
        foreach ((double _, int kind, int index) in events)
        {
            Run run = runs[index];
            if (kind == 0)
            {
                active.Remove((run.A.Y, index));
                continue;
            }
            if (kind == 2)
            {
                active.Add((run.A.Y, index));
                continue;
            }

            double minY = Math.Min(run.A.Y, run.B.Y);
            double maxY = Math.Max(run.A.Y, run.B.Y);
            foreach ((double y, int horizontalIndex) in active.GetViewBetween(
                         (minY, int.MinValue), (maxY, int.MaxValue)))
            {
                if (y <= minY || y >= maxY) continue;
                Link horizontalLink = runs[horizontalIndex].Link;
                Link verticalLink = run.Link;
                if (horizontalLink == verticalLink
                    || horizontalLink.SourceId == verticalLink.SourceId
                    || horizontalLink.TargetId == verticalLink.TargetId) continue;
                crossings++;
            }
        }
        return crossings;
    }

    internal static void Apply(DiagramContract Contract, List<Unit> Units)
    {
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: start {Units.Count} units");
        int Gap = 3 * DiagramGridMetrics.GRID_UNIT_PX;
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
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: indexed {Links.Count} links");
        var Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        int Sign = Direction is FlowchartDirection.BottomToTop or FlowchartDirection.RightToLeft ? -1 : 1;

        Position[] Place(int[] Order)
        {
            double UsableWidth = FlowchartLayoutRules.DiagramUsableWidthPx;
            var Candidates = new List<(Position[] Positions, double WidthOverflow, double Area)>();
            var CandidateWidths = new HashSet<int>();
            int OriginalRowWidth = DiagramGridMetrics.CeilToGrid(Math.Sqrt(Units.Sum(Unit =>
                (double)(Unit.Frame.Right - Unit.Frame.Left + Gap) * (Unit.Frame.Bottom - Unit.Frame.Top + Gap))));
            CandidateWidths.Add(OriginalRowWidth);
            int RunningWidth = 0;
            foreach (int Index in Order)
            {
                var Frame = Units[Index].Frame;
                int Width = DiagramGridMetrics.CeilToGrid(Frame.Right - Frame.Left);
                RunningWidth += (RunningWidth == 0 ? 0 : Gap) + Width;
                CandidateWidths.Add(RunningWidth);
                CandidateWidths.Add(Width);
            }
            if (Contract.Groups.Count(Group => string.IsNullOrEmpty(Group.ParentGroupId)) < FlowchartWrapPlan.MinimumRun)
            {
                CandidateWidths.Clear();
                CandidateWidths.Add(OriginalRowWidth);
            }
            foreach (int RowWidth in CandidateWidths.OrderBy(Value => Value))
            {
                var Positions = new Position[Units.Count];
                int X = 0, Y = 0, RowHeight = 0, UsedWidth = 0;
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
                    UsedWidth = Math.Max(UsedWidth, X - Gap);
                    RowHeight = Math.Max(RowHeight, Height);
                }
                int UsedHeight = Y + RowHeight;
                double WidthOverflow = Math.Max(0, UsedWidth - UsableWidth);
                double Area = UsedWidth * (double)UsedHeight;
                Candidates.Add((Positions, WidthOverflow, Area));
            }
            return Candidates
                .Select(Candidate => (Candidate.Positions, Candidate.WidthOverflow, Candidate.Area,
                    Crossings: EstimatedCrossings(Candidate.Positions)))
                .OrderBy(Candidate => Candidate.WidthOverflow)
                .ThenBy(Candidate => Candidate.Crossings)
                .ThenBy(Candidate => Candidate.Area)
                .First().Positions;
        }

        Position[] ImproveShape(Position[] Initial) => Initial;

        double WidthOf(int Index) => DiagramGridMetrics.CeilToGrid(Units[Index].Frame.Right - Units[Index].Frame.Left);
        double HeightOf(int Index) => DiagramGridMetrics.CeilToGrid(Units[Index].Frame.Bottom - Units[Index].Frame.Top);
        (double Width, double Height) Extents(Position[] Positions) =>
            (Enumerable.Range(0, Units.Count).Max(Index => Positions[Index].X + WidthOf(Index)),
             Enumerable.Range(0, Units.Count).Max(Index => Positions[Index].Y + HeightOf(Index)));
        double RowBalance(Position[] Positions) => Positions.Select(Position => Position.Y).Distinct()
            .Sum(Y => Math.Pow(Enumerable.Range(0, Units.Count).Where(Index => Math.Abs(Positions[Index].Y - Y) < 0.01)
                .Max(Index => Positions[Index].X + WidthOf(Index)), 2));
        void NormalizeRows(Position[] Positions)
        {
            double NextY = 0;
            foreach (double OldY in Positions.Select(Position => Position.Y).Distinct().OrderBy(Y => Y).ToList())
            {
                List<int> Row = Enumerable.Range(0, Units.Count)
                    .Where(Index => Math.Abs(Positions[Index].Y - OldY) < 0.01).ToList();
                foreach (int Index in Row) Positions[Index] = Positions[Index] with { Y = NextY };
                NextY += Row.Max(HeightOf) + Gap;
            }
        }
        bool Overlaps(Position[] Positions)
        {
            for (int A = 0; A < Units.Count; A++)
            for (int B = A + 1; B < Units.Count; B++)
                if (Positions[A].X < Positions[B].X + WidthOf(B) + Gap
                    && Positions[A].X + WidthOf(A) + Gap > Positions[B].X
                    && Positions[A].Y < Positions[B].Y + HeightOf(B) + Gap
                    && Positions[A].Y + HeightOf(A) + Gap > Positions[B].Y) return true;
            return false;
        }
        bool PreservesAlignedLinks(Position[] Before, Position[] After)
        {
            foreach (Link Link in Links)
            {
                double MainBefore = Horizontal
                    ? Before[Link.Target].X + Link.TargetOffset.X - Before[Link.Source].X - Link.SourceOffset.X
                    : Before[Link.Target].Y + Link.TargetOffset.Y - Before[Link.Source].Y - Link.SourceOffset.Y;
                double MainAfter = Horizontal
                    ? After[Link.Target].X + Link.TargetOffset.X - After[Link.Source].X - Link.SourceOffset.X
                    : After[Link.Target].Y + Link.TargetOffset.Y - After[Link.Source].Y - Link.SourceOffset.Y;
                if (MainBefore * Sign > 0.01 && MainAfter * Sign <= 0.01) return false;
                double SourceBefore = Horizontal
                    ? Before[Link.Source].Y + Link.SourceOffset.Y : Before[Link.Source].X + Link.SourceOffset.X;
                double TargetBefore = Horizontal
                    ? Before[Link.Target].Y + Link.TargetOffset.Y : Before[Link.Target].X + Link.TargetOffset.X;
                if (Math.Abs(SourceBefore - TargetBefore) > 0.01) continue;
                double SourceAfter = Horizontal
                    ? After[Link.Source].Y + Link.SourceOffset.Y : After[Link.Source].X + Link.SourceOffset.X;
                double TargetAfter = Horizontal
                    ? After[Link.Target].Y + Link.TargetOffset.Y : After[Link.Target].X + Link.TargetOffset.X;
                if (Math.Abs(SourceAfter - TargetAfter) > 0.01) return false;
            }
            return true;
        }

        int EstimatedCrossings(Position[] Positions)
        {
            var Runs = new List<Run>();
            foreach (var Link in Links)
            {
                var A = new Position(Positions[Link.Source].X + Link.SourceOffset.X,
                    Positions[Link.Source].Y + Link.SourceOffset.Y);
                var B = new Position(Positions[Link.Target].X + Link.TargetOffset.X,
                    Positions[Link.Target].Y + Link.TargetOffset.Y);
                var FirstBend = Horizontal ? new Position((A.X + B.X) / 2, A.Y) : new Position(A.X, (A.Y + B.Y) / 2);
                var SecondBend = Horizontal ? new Position(FirstBend.X, B.Y) : new Position(B.X, FirstBend.Y);
                Runs.Add(new(A, FirstBend, Link)); Runs.Add(new(FirstBend, SecondBend, Link)); Runs.Add(new(SecondBend, B, Link));
            }
            return CountCrossings(Runs);
        }

        double Cost(Position[] Positions)
        {
            double Result = 0;
            var Runs = new List<Run>();
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
                Runs.Add(new(A, FirstBend, Link)); Runs.Add(new(FirstBend, SecondBend, Link)); Runs.Add(new(SecondBend, B, Link));
            }
            Result += 1000000d * Gap * CountCrossings(Runs);
            return Result;
        }

        int[] Order = Enumerable.Range(0, Units.Count).ToArray();
        int TransposeCandidates = 0;
        double Best = Cost(Place(Order));
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: initial placement scored");
        for (int Pass = 0; Pass < Units.Count; Pass++)
        {
            DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: transpose pass {Pass + 1} start");
            bool Changed = false;
            for (int First = 0; First < Order.Length; First++)
            for (int Second = First + 1; Second < Order.Length; Second++)
            {
                (Order[First], Order[Second]) = (Order[Second], Order[First]);
                double Candidate = Cost(Place(Order));
                TransposeCandidates++;
                if (TransposeCandidates % 25 == 0)
                    DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: scored {TransposeCandidates} transpose candidates");
                if (Candidate < Best - 0.01) { Best = Candidate; Changed = true; }
                else (Order[First], Order[Second]) = (Order[Second], Order[First]);
            }
            if (!Changed) break;
        }
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: transpose search complete");
        var Final = ImproveShape(Place(Order));
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: shape search complete");
        for (int Index = 0; Index < Units.Count; Index++)
        {
            var Unit = Units[Index];
            double Snap(double Delta) => Math.Sign(Delta) * DiagramGridMetrics.CeilToGrid(Math.Abs(Delta));
            double Dx = Snap(Final[Index].X - Unit.Frame.Left), Dy = Snap(Final[Index].Y - Unit.Frame.Top);
            foreach (var Member in Unit.Members) { Member.X += Dx; Member.Y += Dy; }
        }
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: positions applied");
    }
}
