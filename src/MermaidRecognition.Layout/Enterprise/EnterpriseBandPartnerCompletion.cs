using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Completes partner placement after semantic timeline rows and placement hints have settled.</summary>
internal static partial class EnterpriseBandPlacement
{
    /// <summary>Owns private, whole-cell partner proposals and their repair-time alignment constraints.</summary>
    private sealed partial class BandPlan
    {
        /// <summary>Removes excess whole columns between sibling frame blocks while retaining their existing order, complete membership and measured frame clearance.</summary>
        /// <param name="Targets">Complete private initial plan, before any port assignment or route construction.</param>
        internal void CompactFrameNeighbours(Dictionary<string, FlowchartCellRegion> Targets)
        {
            FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            foreach (IGrouping<string?, GroupContract> Family in Contract.Groups.GroupBy(Group => Group.ParentGroupId))
            {
                (string Id, string[] Members)[] Units = [.. Family.Select(Group => (Group.Id, Members: Nodes.Where(Node =>
                    DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Group.Id)).Select(Node => Node.Id).ToArray()))
                    .Where(Unit => Unit.Members.Length > 0)
                    .OrderBy(Unit => Unit.Members.Min(Id => Targets[Id].StartColumn))];
                for (int Index = 1; Index < Units.Length; Index++)
                {
                    (string Id, string[] Members) Unit = Units[Index];
                    if (Unit.Members.Any(Hinted.Contains))
                    {
                        continue;
                    }

                    var Earlier = Units.Take(Index).SelectMany(Peer => Peer.Members).Where(Id =>
                        Classes[Id].Band >= Unit.Members.Min(Member => Classes[Member].Band)
                        && Classes[Id].Band <= Unit.Members.Max(Member => Classes[Member].Band)).ToArray();
                    if (Earlier.Length == 0)
                    {
                        continue;
                    }

                    int Left = Unit.Members.Min(Id => Targets[Id].StartColumn);
                    int NeighbourEnd = Earlier.Max(Id => Targets[Id].EndColumn);
                    HashSet<string> InitialFaults = ProjectedFrameFaults(Targets);
                    // Whole blocks retain their order. Try the closest cell
                    // first; nested title/padding geometry decides how many
                    // empty columns really have to remain between members.
                    for (int Column = NeighbourEnd; Column < Left; Column++)
                    {
                        Dictionary<string, FlowchartCellRegion> Proposed = new Dictionary<string, FlowchartCellRegion>(Targets, StringComparer.Ordinal);
                        foreach (string Id in Unit.Members)
                        {
                            Proposed[Id] = FlowchartCellRepair.Translate(Targets[Id], 0, Column - Left);
                        }

                        if (!MatchesConvention(Contract, Classes, Proposed) || !Grid.TryPrepare(Proposed, [], out _)
                            || ProjectedFrameFaults(Proposed).Any(Fault => !InitialFaults.Contains(Fault)))
                        {
                            continue;
                        }

                        foreach (string Id in Unit.Members)
                        {
                            Targets[Id] = Proposed[Id];
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>Checks exact node and nested-frame geometry for a private complete assignment without publishing cells or mutating any route.</summary>
        /// <param name="Targets">Complete candidate regions.</param>
        /// <returns>The existing hard frame-conflict identities for that candidate.</returns>
        private HashSet<string> ProjectedFrameFaults(IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
            FlowchartLogicalGrid.Snapshot Current = Grid.Capture();
            FlowchartLogicalGrid.Snapshot State = new FlowchartLogicalGrid.Snapshot(Grid, Targets, Current.ColumnGaps, Current.RowGaps);
            Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Nodes.ToDictionary(Node => Node.Id, Node => Node.CellNode!.Bounds(Targets[Node.Id], Node.GeometryProvider, State));
            return DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes,
                DiagramGridLayout.ProjectCellFrames(Contract, Nodes, State), Bounds);
        }

        /// <summary>Revisits singleton and mixed bands using the final positions of their partners, without moving timeline rows or hinted units.</summary>
        /// <param name="Targets">Complete private cell assignment; publication remains atomic in the caller.</param>
        internal void AlignCompletedPartners(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            HashSet<int> TimelineBands = [.. Classes.Values.Where(Item => Item.Type == MermaidRecognition.Kinds.Enterprise.EnterpriseElementType.Plateau).Select(Item => Item.Band)];
            for (int Sweep = 0; Sweep < DagreLayoutEngine.OrderSweepPatience; Sweep++)
            {
                bool Changed = false;
                IGrouping<int, DiagramGridLayout.GridNode>[] Bands = [.. Nodes.GroupBy(Node => Classes[Node.Id].Band).OrderBy(Band => Band.Key)];
                if (Sweep % 2 != 0)
                {
                    Array.Reverse(Bands);
                }

                foreach (IGrouping<int, DiagramGridLayout.GridNode>? Band in Bands)
                {
                    if (TimelineBands.Contains(Band.Key) || (Band.Count() > 1 && !Band.Any(Node => Node.GroupId != null)))
                    {
                        continue;
                    }

                    foreach (DiagramGridLayout.GridNode? Node in Band.Where(Node => Node.GroupId == null && !Hinted.Contains(Node.Id)))
                    {
                        var Partners = CrossBandMembers(Node.Id, Targets).ToArray();
                        double Pull = Band.Count() == 1 && Partners.Length > 0
                            ? MedianColumn(Partners, Targets) : PartnerMedian(Node.Id, Targets);
                        FlowchartCellRegion Region = Targets[Node.Id];
                        HashSet<string> Occupied = Band.Where(Peer => Peer.Id != Node.Id).Select(Peer => Peer.Id).ToHashSet(StringComparer.Ordinal);
                        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
                        FlowchartLogicalGrid.Snapshot Current = Grid.Capture();
                        FlowchartLogicalGrid.Snapshot Geometry = new FlowchartLogicalGrid.Snapshot(Grid, Targets, Current.ColumnGaps, Current.RowGaps);
                        List<DiagramGridLayout.GridFrame>? Frames = Contract.Groups.Count == 0 ? null : DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Geometry);
                        Dictionary<string, FlowchartLayoutNodeBounds>? Bounds = Frames == null ? null : Nodes.ToDictionary(Member => Member.Id,
                            Member => Member.CellNode!.Bounds(Targets[Member.Id], Member.GeometryProvider, Geometry));
                        HashSet<string>? ExistingFaults = Frames == null ? null : DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, Frames, Bounds!);
                        // A wrap width cannot truncate an already reserved solid
                        // frame. Search the actual diagram and the partner pull,
                        // including a free cell beyond its measured right edge.
                        int Right = checked(Math.Max(Targets.Values.Max(Item => Item.EndColumn), (int)Math.Ceiling(Pull)) + Region.ColumnSpan);
                        foreach (int Column in Enumerable.Range(0, Right - Region.ColumnSpan + 1)
                            .OrderBy(Column => Math.Abs(Column - Pull)).ThenBy(Column => Column))
                        {
                            FlowchartCellRegion Candidate = new FlowchartCellRegion(Region.StartRow, Column, Region.RowSpan, Region.ColumnSpan);
                            if (!FreeForOccupancyAndAspect(Node.Id, Candidate, Targets, Occupied)
                                || !KeepsLocalOrder(Node.Id, Candidate, Targets))
                            {
                                continue;
                            }

                            if (Frames != null)
                            {
                                // A frame occupies its drawn rectangle, not an
                                // infinitely tall column through its whole band.
                                // Ungrouped nodes do not change member-derived
                                // frames, so reuse those exact bounds in trials.
                                Bounds![Node.Id] = Node.CellNode!.Bounds(Candidate, Node.GeometryProvider, Geometry);
                                if (DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, Frames, Bounds)
                                    .Any(Fault => !ExistingFaults!.Contains(Fault)))
                                {
                                    continue;
                                }
                            }
                            Changed |= Region != Candidate;
                            Targets[Node.Id] = Candidate;
                            break;
                        }
                    }
                }
                if (!Changed)
                {
                    break;
                }
            }
        }

        /// <summary>Preserves an unhinted singleton's attained proximity to its partners through later complete repair and rollback candidates.</summary>
        /// <param name="Targets">Authoritative assignment after hints and partner completion.</param>
        /// <returns>A pure complete-region predicate; no route, gap, region or hint is mutated by validation.</returns>
        internal Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool> CaptureSingletonAlignment(
            IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            (string Id, string[] Partners, double Maximum)[] Constraints = [.. Nodes.GroupBy(Node => Classes[Node.Id].Band).Where(Band => Band.Count() == 1)
                .Select(Band => Band.Single()).Where(Node => Node.GroupId == null && !Hinted.Contains(Node.Id))
                .Select(Node => (Node.Id, Partners: CrossBandMembers(Node.Id, Targets).ToArray()))
                .Where(Item => Item.Partners.Length > 0 && !Item.Partners.Any(Hinted.Contains))
                .Select(Item => (Item.Id, Item.Partners, Maximum: Math.Max(1,
                    Math.Abs(Targets[Item.Id].StartColumn - MedianColumn(Item.Partners, Targets)))))];
            return Regions => Constraints.Where(Item => Regions.ContainsKey(Item.Id) && Item.Partners.All(Regions.ContainsKey))
                .All(Item => Math.Abs(Regions[Item.Id].StartColumn - MedianColumn(Item.Partners, Regions)) <= Item.Maximum);
        }

        /// <summary>Resolves cross-band relationships to actual member identities, including declared frame endpoints.</summary>
        /// <param name="Id">Element being aligned.</param>
        /// <param name="Targets">Complete region set.</param>
        /// <returns>Distinct partners in other semantic bands.</returns>
        private IEnumerable<string> CrossBandMembers(string Id, IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
            => PartnerIds(Id).SelectMany(Partner => Targets.ContainsKey(Partner) ? [Partner]
                : Targets.Keys.Where(Member => DiagramGridLayout.IsInsideGroup(Contract, Member, Partner)))
                .Where(Partner => Classes[Partner].Band != Classes[Id].Band).Distinct(StringComparer.Ordinal);

        /// <summary>Measures an actual partner median from the supplied complete candidate, never from an earlier wrap or timeline plan.</summary>
        /// <param name="Partners">Nonempty resolved partner identities.</param>
        /// <param name="Targets">Current candidate regions.</param>
        /// <returns>Median starting column, with the usual average for an even-sized set.</returns>
        private static double MedianColumn(IReadOnlyList<string> Partners, IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            var Columns = Partners.Select(Id => Targets[Id].StartColumn).Order().ToArray();
            return ((double)Columns[(Columns.Length - 1) / 2] + Columns[Columns.Length / 2]) / 2;
        }

        /// <summary>Retains the declared horizontal local rank order while partner pulls choose legal columns.</summary>
        /// <param name="Id">Moving ungrouped member.</param>
        /// <param name="Candidate">Proposed same-row region.</param>
        /// <param name="Targets">Complete private assignment.</param>
        /// <returns>Whether the move leaves every earlier and later local rank on its required side.</returns>
        private bool KeepsLocalOrder(string Id, FlowchartCellRegion Candidate, IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
            => !HorizontalRanks.TryGetValue(Id, out int Rank) || !HorizontalRanks.Any(Pair => Pair.Key != Id
                && Classes[Pair.Key].Band == Classes[Id].Band && ((Pair.Value < Rank && Targets[Pair.Key].EndColumn > Candidate.StartColumn)
                    || (Pair.Value > Rank && Targets[Pair.Key].StartColumn < Candidate.EndColumn)));
    }
}
