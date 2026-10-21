using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Enterprise;

internal static partial class EnterpriseBandPlacement
{
    private sealed partial class BandPlan
    {
        /// <summary>Measured candidate width from linked bands and the natural cell footprint.</summary>
        internal int PreferredWidth { get; private set; }

        /// <summary>Places elements without local relationships by cross-band medians, keeping frames and local ranks intact.</summary>
        /// <param name="Targets">Private complete assignment; no live cell is changed during this search.</param>
        /// <param name="Order">Existing crossing-reduced order used for stable equal-median ties.</param>
        /// <param name="Wrap">Whole-column candidate capacity, or no wrapping for the reference candidate.</param>
        private void PlacePartnerElements(Dictionary<string, FlowchartCellRegion> Targets, IReadOnlyList<string> Order, int Wrap)
        {
            var Grid = Nodes[0].CellNode!.Grid;
            double Area = Original.Values.Sum(Region => (double)Region.RowSpan * Region.ColumnSpan);
            int NaturalWidth = (int)Math.Ceiling(Math.Sqrt(Area * Grid.Configuration.CellHeight / Grid.Configuration.CellWidth));
            int LinkedWidth = Nodes.Where(Node => !Free.Contains(Node.Id)).GroupBy(Node => Classes[Node.Id].Band)
                .Select(Band => Band.Max(Node => Targets[Node.Id].EndColumn) - Band.Min(Node => Targets[Node.Id].StartColumn))
                .DefaultIfEmpty(0).Max();
            PreferredWidth = Math.Max(Original.Values.Max(Region => Region.ColumnSpan), Math.Max(NaturalWidth, LinkedWidth));
            var Tie = Order.Select((Id, Index) => (Id, Index)).ToDictionary(Item => Item.Id, Item => Item.Index);
            // Cross-band links supply pulls only. Reusing the existing finite
            // sweep allowance settles medians without inventing rank edges.
            for (int Sweep = 0; Sweep < DagreLayoutEngine.OrderSweepPatience; Sweep++)
            {
                bool Changed = false;
                var Bands = Nodes.GroupBy(Node => Classes[Node.Id].Band).OrderBy(Band => Band.Key).ToArray();
                if (Sweep % 2 != 0) Array.Reverse(Bands);
                foreach (var Band in Bands)
                {
                    var Movable = Band.Where(Node => Free.Contains(Node.Id) && Node.GroupId == null).ToArray();
                    int Top = Band.Min(Node => Targets[Node.Id].StartRow);
                    int Height = Band.Max(Node => Targets[Node.Id].RowSpan);
                    var Occupied = Band.Where(Node => !Movable.Contains(Node)).Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                    var Pulls = Movable.ToDictionary(Node => Node.Id, Node => PartnerMedian(Node.Id, Targets));
                    bool FreeRowBand = Movable.Length > 1 && Movable.Length == Band.Count()
                        && !Contract.Groups.Any(Frame =>
                        {
                            var Parts = Nodes.Where(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Frame.Id)).ToArray();
                            return Parts.Length > 0 && Parts.Min(Node => Classes[Node.Id].Band) <= Band.Key
                                && Parts.Max(Node => Classes[Node.Id].Band) >= Band.Key;
                        });
                    if (FreeRowBand && Wrap != int.MaxValue)
                    {
                        Changed |= PlaceFreeBandRows(Targets, Movable, Pulls, Tie, Wrap, Top, Height);
                        continue;
                    }
                    foreach (var Node in Movable.OrderBy(Node => Pulls[Node.Id]).ThenBy(Node => Classes[Node.Id].Aspect)
                        .ThenBy(Node => Tie.GetValueOrDefault(Owners[Node.Id])))
                    {
                        var Region = Targets[Node.Id];
                        int Capacity = Wrap == int.MaxValue ? Math.Max(PreferredWidth, Targets.Values.Max(Item => Item.EndColumn))
                            + Movable.Sum(Item => Targets[Item.Id].ColumnSpan) : Math.Max(Wrap, Region.ColumnSpan);
                        FlowchartCellRegion? Best = null;
                        int Rows = Band.Max(Item => Targets[Item.Id].EndRow) - Top + Movable.Sum(Item => Targets[Item.Id].RowSpan);
                        for (int Row = Top; Row <= Top + Rows && Best == null; Row += Height)
                            foreach (int Column in Enumerable.Range(0, Capacity - Region.ColumnSpan + 1)
                                .OrderBy(Column => Math.Abs(Column - Pulls[Node.Id])).ThenBy(Column => Column))
                            {
                                var Candidate = new FlowchartCellRegion(Row, Column, Region.RowSpan, Region.ColumnSpan);
                                if (!FreeFor(Node.Id, Candidate, Targets, Occupied)) continue;
                                Best = Candidate; break;
                            }
                        if (Best == null) continue; // The original complete plan remains the safe candidate.
                        Changed |= Targets[Node.Id] != Best;
                        Targets[Node.Id] = Best;
                        Occupied.Add(Node.Id);
                    }
                    // The unwrapped option keeps its established median order
                    // and port/track nesting. It still declares its level rows,
                    // but does not need a new wrap origin or a new permutation.
                    if (FreeRowBand) RowMembers.UnionWith(Movable.Select(Node => Node.Id));
                    foreach (var Node in Band.Where(Node => Node.GroupId == null && !Free.Contains(Node.Id)))
                    {
                        var Partners = PartnerIds(Node.Id).Where(Id => Classes.TryGetValue(Id, out var Class)
                            && Math.Abs(Class.Band - Band.Key) == 1 && Targets.ContainsKey(Id)).Distinct().ToArray();
                        if (Partners.Length != 1) continue;
                        var Region = Targets[Node.Id];
                        var Candidate = new FlowchartCellRegion(Region.StartRow, Targets[Partners[0]].StartColumn, Region.RowSpan, Region.ColumnSpan);
                        if (!FreeFor(Node.Id, Candidate, Targets, Band.Select(Item => Item.Id).Where(Id => Id != Node.Id).ToHashSet())) continue;
                        if (HorizontalRanks.TryGetValue(Node.Id, out int Rank) && HorizontalRanks.Any(Pair => Pair.Key != Node.Id
                            && Classes[Pair.Key].Band == Band.Key && (Pair.Value < Rank && Targets[Pair.Key].EndColumn > Candidate.StartColumn
                                || Pair.Value > Rank && Targets[Pair.Key].StartColumn < Candidate.EndColumn))) continue;
                        Changed |= Region != Candidate; Targets[Node.Id] = Candidate;
                    }
                }
                if (!Changed) break;
            }
        }

        /// <summary>Packs an unconstrained wrapped free band as explicit rows, using partner medians for order rather than carrying their coordinates into a new row.</summary>
        /// <param name="Targets">Complete private assignment, changed only after every row position has been built.</param>
        /// <param name="Members">All members of a band with no local links or spanning frame obstacles.</param>
        /// <param name="Pulls">Current cross-band median columns.</param>
        /// <param name="Tie">Stable crossing-reduced block order.</param>
        /// <param name="Wrap">Whole-cell capacity, including the unwrapped candidate.</param>
        /// <param name="Top">Band's first row.</param>
        /// <param name="Height">Common row pitch measured from member spans.</param>
        /// <returns>Whether any private region changed.</returns>
        private bool PlaceFreeBandRows(Dictionary<string, FlowchartCellRegion> Targets, IReadOnlyList<DiagramGridLayout.GridNode> Members,
            IReadOnlyDictionary<string, double> Pulls, IReadOnlyDictionary<string, int> Tie, int Wrap, int Top, int Height)
        {
            var Plan = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
            int RowIndex = 0, Position = 0;
            foreach (var Node in Members.OrderBy(Node => Classes[Node.Id].Aspect).ThenBy(Node => Pulls[Node.Id])
                .ThenBy(Node => Tie.GetValueOrDefault(Owners[Node.Id])))
            {
                var Region = Targets[Node.Id];
                if (Position > 0 && (long)Position + Region.ColumnSpan > Wrap)
                {
                    RowIndex++;
                    Position = 0;
                }
                // The next row has its own origin. Graph rank and the previous
                // element's median never contribute to its page-top coordinate.
                Plan.Add(Node.Id, new(checked(Top + RowIndex * Height), Position, Region.RowSpan, Region.ColumnSpan));
                Position = checked(Position + Region.ColumnSpan);
            }
            bool Changed = Plan.Any(Pair => Targets[Pair.Key] != Pair.Value);
            RowMembers.UnionWith(Plan.Keys);
            foreach (var Pair in Plan) Targets[Pair.Key] = Pair.Value;
            return Changed;
        }

        /// <summary>Returns declared opposite endpoints without converting relationships into local rank constraints.</summary>
        /// <param name="Id">Element whose cross-band partners are needed.</param>
        /// <returns>Stable distinct partner identities, including frames.</returns>
        private IEnumerable<string> PartnerIds(string Id) => Contract.Edges.Where(Edge => Edge.SourceId == Id || Edge.TargetId == Id)
            .Select(Edge => Edge.SourceId == Id ? Edge.TargetId : Edge.SourceId).Distinct(StringComparer.Ordinal);

        /// <summary>Measures the median starting column of partners, resolving a frame to its contained cell regions.</summary>
        /// <param name="Id">Free element being placed.</param>
        /// <param name="Targets">Current complete candidate.</param>
        /// <returns>A starting-column pull; no-partner elements retain their existing column.</returns>
        private double PartnerMedian(string Id, IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            var Values = PartnerIds(Id).SelectMany(Partner => Targets.TryGetValue(Partner, out var Region)
                ? new[] { (double)Region.StartColumn }
                : Targets.Where(Pair => DiagramGridLayout.IsInsideGroup(Contract, Pair.Key, Partner)).Select(Pair => (double)Pair.Value.StartColumn))
                .Order().ToArray();
            return Values.Length == 0 ? Targets[Id].StartColumn
                : (Values[(Values.Length - 1) / 2] + Values[Values.Length / 2]) / 2;
        }

        /// <summary>Checks node occupancy, solid frame columns and the local aspect order before a private placement is selected.</summary>
        /// <param name="Id">Ungrouped moving node.</param>
        /// <param name="Candidate">Whole-cell target.</param>
        /// <param name="Targets">Complete tentative assignment.</param>
        /// <param name="Occupied">Peers already placed in this semantic band.</param>
        /// <returns>Whether the region preserves every fixed reservation.</returns>
        private bool FreeFor(string Id, FlowchartCellRegion Candidate, IReadOnlyDictionary<string, FlowchartCellRegion> Targets, IReadOnlySet<string> Occupied)
        {
            if (!FreeForOccupancyAndAspect(Id, Candidate, Targets, Occupied)) return false;
            foreach (var Frame in Contract.Groups.Where(Group => Group.ParentGroupId == null))
            {
                var Members = Nodes.Where(Node => Owners[Node.Id] == Frame.Id).ToArray();
                if (Members.Length == 0 || Members.All(Node => Classes[Node.Id].Band > Classes[Id].Band)
                    || Members.All(Node => Classes[Node.Id].Band < Classes[Id].Band)) continue;
                int Left = Members.Min(Node => Targets[Node.Id].StartColumn - Offsets[Node.Id]);
                if (Candidate.StartColumn < Left + Widths[Frame.Id] && Candidate.EndColumn > Left) return false;
            }
            return true;
        }

        /// <summary>Checks exclusive cells and the local ungrouped aspect order independently of frame geometry.</summary>
        /// <param name="Id">Moving ungrouped member.</param>
        /// <param name="Candidate">Proposed whole-cell region.</param>
        /// <param name="Targets">Complete private assignment.</param>
        /// <param name="Occupied">Other members fixed in this proposal.</param>
        /// <returns>Whether occupancy and same-row aspect ordering remain legal.</returns>
        private bool FreeForOccupancyAndAspect(string Id, FlowchartCellRegion Candidate,
            IReadOnlyDictionary<string, FlowchartCellRegion> Targets, IReadOnlySet<string> Occupied)
        {
            if (Occupied.Any(Peer => Targets[Peer].Overlaps(Candidate))) return false;
            return Occupied.Where(Peer => Owners[Peer] == Peer && Targets[Peer].StartRow == Candidate.StartRow)
                .All(Peer => Classes[Peer].Aspect == Classes[Id].Aspect || (Classes[Peer].Aspect < Classes[Id].Aspect
                    ? Targets[Peer].EndColumn <= Candidate.StartColumn : Candidate.EndColumn <= Targets[Peer].StartColumn));
        }

        /// <summary>Shares one top edge for every logical row and stacks complete band extents without moving elements between bands.</summary>
        /// <param name="Targets">Complete private plan after partner placement and timeline construction.</param>
        private void RestackLevelBands(Dictionary<string, FlowchartCellRegion> Targets)
        {
            int Next = 0;
            foreach (var Band in Nodes.GroupBy(Node => Classes[Node.Id].Band).OrderBy(Band => Band.Key))
            {
                foreach (var Row in Band.GroupBy(Node => Targets[Node.Id].StartRow).OrderBy(Row => Row.Key).ToArray())
                {
                    int Height = Row.Max(Node => Targets[Node.Id].RowSpan);
                    foreach (var Node in Row)
                    {
                        var Region = Targets[Node.Id]; Targets[Node.Id] = new(Next, Region.StartColumn, Region.RowSpan, Region.ColumnSpan);
                    }
                    Next += Height;
                }
                if (PreviousBandPadding) Next += Inset;
            }
        }
    }
}
