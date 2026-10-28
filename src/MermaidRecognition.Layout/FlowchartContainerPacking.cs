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
    private sealed record Link(int Source, int Target, string SourceId, string TargetId, Position SourceOffset, Position TargetOffset);
    private readonly record struct Run(Position A, Position B, Link Link);

    private static int CountCrossings(IReadOnlyList<Run> runs)
    {
        // Orthogonal sweep: insert horizontal runs only after vertical queries at
        // their left endpoint and remove them before queries at their right
        // endpoint. This preserves the strict-interior crossing test below.
        List<(double X, int Kind, int Index)> events = [];
        SortedSet<(double Y, int Index)> active = [];
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
            if (compare != 0)
            {
                return compare;
            }

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
                if (y <= minY || y >= maxY)
                {
                    continue;
                }

                Link horizontalLink = runs[horizontalIndex].Link;
                Link verticalLink = run.Link;
                if (horizontalLink == verticalLink
                    || horizontalLink.SourceId == verticalLink.SourceId
                    || horizontalLink.TargetId == verticalLink.TargetId)
                {
                    continue;
                }

                crossings++;
            }
        }
        return crossings;
    }

    internal static void Apply(DiagramContract Contract, List<Unit> Units)
    {
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: start {Units.Count} units");
        int Gap = 3 * DiagramGridMetrics.GRID_UNIT_PX;
        Dictionary<string, int> Owner = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, Position> Offsets = new Dictionary<string, Position>(StringComparer.Ordinal);
        for (int Index = 0; Index < Units.Count; Index++)
        {
            Unit Unit = Units[Index];
            Owner[Unit.Frame.Id] = Index;
            Offsets[Unit.Frame.Id] = new((Unit.Frame.Right - Unit.Frame.Left) / 2.0,
                (Unit.Frame.Bottom - Unit.Frame.Top) / 2.0);
            foreach (DiagramGridLayout.GridNode Member in Unit.Members)
            {
                Owner[Member.Id] = Index;
                Offsets[Member.Id] = new(Member.X - Unit.Frame.Left, Member.Y - Unit.Frame.Top);
            }
        }
        List<Link> Links = [.. Contract.Edges.Where(Edge => Owner.ContainsKey(Edge.SourceId) && Owner.ContainsKey(Edge.TargetId)
            && Owner[Edge.SourceId] != Owner[Edge.TargetId]
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .Select(Edge => new Link(Owner[Edge.SourceId], Owner[Edge.TargetId], Edge.SourceId, Edge.TargetId,
                Offsets[Edge.SourceId], Offsets[Edge.TargetId]))];
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: indexed {Links.Count} links");
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        bool Horizontal = Direction is FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft;
        int Sign = Direction is FlowchartDirection.BottomToTop or FlowchartDirection.RightToLeft ? -1 : 1;

        Position[] Place(int[] Order)
        {
            double UsableWidth = FlowchartLayoutRules.DiagramUsableWidthPx;
            List<(Position[] Positions, double WidthOverflow, double Area)> Candidates = [];
            HashSet<int> CandidateWidths = [];
            int OriginalRowWidth = DiagramGridMetrics.CeilToGrid(Math.Sqrt(Units.Sum(Unit =>
                (double)(Unit.Frame.Right - Unit.Frame.Left + Gap) * (Unit.Frame.Bottom - Unit.Frame.Top + Gap))));
            CandidateWidths.Add(OriginalRowWidth);
            int RunningWidth = 0;
            foreach (int Index in Order)
            {
                DiagramGridLayout.GridFrame Frame = Units[Index].Frame;
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
                Position[] Positions = new Position[Units.Count];
                int X = 0, Y = 0, RowHeight = 0, UsedWidth = 0;
                foreach (int Index in Order)
                {
                    DiagramGridLayout.GridFrame Frame = Units[Index].Frame;
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

        //double WidthOf(int Index) => DiagramGridMetrics.CeilToGrid(Units[Index].Frame.Right - Units[Index].Frame.Left);
        //double HeightOf(int Index) => DiagramGridMetrics.CeilToGrid(Units[Index].Frame.Bottom - Units[Index].Frame.Top);

        int EstimatedCrossings(Position[] Positions)
        {
            List<Run> Runs = [];
            foreach (Link? Link in Links)
            {
                Position A = new Position(Positions[Link.Source].X + Link.SourceOffset.X, Positions[Link.Source].Y + Link.SourceOffset.Y);
                Position B = new Position(Positions[Link.Target].X + Link.TargetOffset.X, Positions[Link.Target].Y + Link.TargetOffset.Y);
                Position FirstBend = Horizontal ? new Position((A.X + B.X) / 2, A.Y) : new Position(A.X, (A.Y + B.Y) / 2);
                Position SecondBend = Horizontal ? new Position(FirstBend.X, B.Y) : new Position(B.X, FirstBend.Y);
                Runs.Add(new(A, FirstBend, Link)); Runs.Add(new(FirstBend, SecondBend, Link)); Runs.Add(new(SecondBend, B, Link));
            }
            return CountCrossings(Runs);
        }

        double Cost(Position[] Positions)
        {
            double Result = 0;
            List<Run> Runs = [];
            foreach (Link? Link in Links)
            {
                Position A = new Position(Positions[Link.Source].X + Link.SourceOffset.X, Positions[Link.Source].Y + Link.SourceOffset.Y);
                Position B = new Position(Positions[Link.Target].X + Link.TargetOffset.X, Positions[Link.Target].Y + Link.TargetOffset.Y);
                Result += Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
                double MainDistance = Horizontal ? B.X - A.X : B.Y - A.Y;
                if (MainDistance * Sign <= 0)
                {
                    Result += 2 * Gap;
                }

                Position FirstBend = Horizontal ? new Position((A.X + B.X) / 2, A.Y) : new Position(A.X, (A.Y + B.Y) / 2);
                Position SecondBend = Horizontal ? new Position(FirstBend.X, B.Y) : new Position(B.X, FirstBend.Y);
                Runs.Add(new(A, FirstBend, Link)); Runs.Add(new(FirstBend, SecondBend, Link)); Runs.Add(new(SecondBend, B, Link));
            }
            Result += 1000000d * Gap * CountCrossings(Runs);
            return Result;
        }

        int[] Order = [.. Enumerable.Range(0, Units.Count)];
        int TransposeCandidates = 0;
        double Best = Cost(Place(Order));
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: initial placement scored");
        for (int Pass = 0; Pass < Units.Count; Pass++)
        {
            DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: transpose pass {Pass + 1} start");
            bool Changed = false;
            for (int First = 0; First < Order.Length; First++)
            {
                for (int Second = First + 1; Second < Order.Length; Second++)
                {
                    (Order[First], Order[Second]) = (Order[Second], Order[First]);
                    double Candidate = Cost(Place(Order));
                    TransposeCandidates++;
                    if (TransposeCandidates % 25 == 0)
                    {
                        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"container packing: scored {TransposeCandidates} transpose candidates");
                    }

                    if (Candidate < Best - 0.01) { Best = Candidate; Changed = true; }
                    else
                    {
                        (Order[First], Order[Second]) = (Order[Second], Order[First]);
                    }
                }
            }

            if (!Changed)
            {
                break;
            }
        }
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: transpose search complete");
        Position[] Final = ImproveShape(Place(Order));
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: shape search complete");
        for (int Index = 0; Index < Units.Count; Index++)
        {
            Unit Unit = Units[Index];
            double Snap(double Delta) => Math.Sign(Delta) * DiagramGridMetrics.CeilToGrid(Math.Abs(Delta));
            double Dx = Snap(Final[Index].X - Unit.Frame.Left), Dy = Snap(Final[Index].Y - Unit.Frame.Top);
            foreach (DiagramGridLayout.GridNode Member in Unit.Members) { Member.X += Dx; Member.Y += Dy; }
        }
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("container packing: positions applied");
    }

    private static bool PreservesAlignedLinks(Position[] Before, Position[] After, List<Link> Links, bool Horizontal, int Sign)
    {
        foreach (Link Link in Links)
        {
            double MainBefore;
            if (Horizontal)
            {
                MainBefore = (double)(Before[Link.Target].X + Link.TargetOffset.X - Before[Link.Source].X - Link.SourceOffset.X);
            }
            else
            {
                MainBefore = (double)(Before[Link.Target].Y + Link.TargetOffset.Y - Before[Link.Source].Y - Link.SourceOffset.Y);
            }

            double MainAfter = Horizontal
                ? After[Link.Target].X + Link.TargetOffset.X - After[Link.Source].X - Link.SourceOffset.X
                : After[Link.Target].Y + Link.TargetOffset.Y - After[Link.Source].Y - Link.SourceOffset.Y;
            if (MainBefore * Sign > 0.01 && MainAfter * Sign <= 0.01)
            {
                return false;
            }

            double SourceBefore;
            if (Horizontal)
            {
                SourceBefore = (double)(Before[Link.Source].Y + Link.SourceOffset.Y);
            }
            else
            {
                SourceBefore = (double)(Before[Link.Source].X + Link.SourceOffset.X);
            }

            double TargetBefore;
            if (Horizontal)
            {
                TargetBefore = (double)(Before[Link.Target].Y + Link.TargetOffset.Y);
            }
            else
            {
                TargetBefore = (double)(Before[Link.Target].X + Link.TargetOffset.X);
            }

            if (Math.Abs(SourceBefore - TargetBefore) > 0.01)
            {
                continue;
            }

            double SourceAfter;
            if (Horizontal)
            {
                SourceAfter = (double)(After[Link.Source].Y + Link.SourceOffset.Y);
            }
            else
            {
                SourceAfter = (double)(After[Link.Source].X + Link.SourceOffset.X);
            }

            double TargetAfter;
            if (Horizontal)
            {
                TargetAfter = (double)(After[Link.Target].Y + Link.TargetOffset.Y);
            }
            else
            {
                TargetAfter = (double)(After[Link.Target].X + Link.TargetOffset.X);
            }

            if (Math.Abs(SourceAfter - TargetAfter) > 0.01)
            {
                return false;
            }
        }
        return true;
    }

    //private static bool Overlaps(Position[] Positions, List<Unit> Units, int Gap)
    //{
    //    for (int A = 0; A < Units.Count; A++)
    //    {
    //        for (int B = A + 1; B < Units.Count; B++)
    //        {
    //            if (Positions[A].X < Positions[B].X + WidthOf(B) + Gap
    //            && Positions[A].X + WidthOf(A) + Gap > Positions[B].X
    //            && Positions[A].Y < Positions[B].Y + HeightOf(B) + Gap
    //            && Positions[A].Y + HeightOf(A) + Gap > Positions[B].Y)
    //            {
    //                return true;
    //            }
    //        }
    //    }

    //    return false;
    //}

    //private static void NormalizeRows(Position[] Positions, List<Unit> Units, int Gap)
    //{
    //    double NextY = 0;
    //    foreach (double OldY in Positions.Select(Position => Position.Y).Distinct().OrderBy(Y => Y).ToList())
    //    {
    //        List<int> Row = [.. Enumerable.Range(0, Units.Count).Where(Index => Math.Abs(Positions[Index].Y - OldY) < 0.01)];
    //        foreach (int Index in Row)
    //        {
    //            Positions[Index] = Positions[Index] with { Y = NextY };
    //        }

    //        NextY += Row.Max(HeightOf) + Gap;
    //    }
    //}

    //private static double RowBalance(Position[] Positions, List<Unit> Units) => Positions.Select((Position Position) => Position.Y).Distinct()
    //            .Sum(double (double Y) => Math.Pow(Enumerable.Range(0, Units.Count).Where((int Index) => Math.Abs(Positions[Index].Y - Y) < 0.01)
    //                .Max<int, double>((int Index) => Positions[Index].X + DiagramGridMetrics.CeilToGrid(Units[Index].Frame.Right - Units[Index].Frame.Left)), 2));

    //private static (double Width, double Height) Extents(Position[] Positions, List<Unit> Units) =>
    //            (Enumerable.Range(0, Units.Count).Max((int Index) => Positions[Index].X + DiagramGridMetrics.CeilToGrid(Units[Index].Frame.Right - Units[Index].Frame.Left)),
    //             Enumerable.Range(0, Units.Count).Max((int Index) => Positions[Index].Y + DiagramGridMetrics.CeilToGrid(Units[Index].Frame.Bottom - Units[Index].Frame.Top)));
}
