using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Rule 2.24: stacks declared relationship chains in one whole-cell column.</summary>
internal static partial class EnterpriseBandPlacement
{
    private sealed partial class BandPlan
    {
        /// <summary>Aligns every longest declared chain to the anchor column without changing rows or spans.</summary>
        /// <param name="Targets">Complete private assignment.</param>
        /// <summary>Stacks downstream partners in the same column when they sit in a lower semantic band.</summary>
        /// <param name="Targets">Complete private assignment.</param>
        internal void AlignFlowStackColumns(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
                !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
            for (int Sweep = 0; Sweep < Nodes.Count; Sweep++)
            {
                bool Changed = false;
                foreach (EdgeContract Edge in Visible)
                {
                    if (!TryLayoutEdgeEndpoints(Edge, Hinted, Targets, Classes, out string SourceId, out string TargetId))
                    {
                        continue;
                    }

                    int SourceBand = Classes[SourceId].Band;
                    int TargetBand = Classes[TargetId].Band;
                    if (SourceBand == TargetBand)
                    {
                        continue;
                    }

                    FlowchartCellRegion Source = Targets[SourceId];
                    FlowchartCellRegion Target = Targets[TargetId];
                    if (TargetBand > SourceBand)
                    {
                        if (Target.StartColumn == Source.StartColumn)
                        {
                            continue;
                        }

                        FlowchartCellRegion Candidate = new(Target.StartRow, Source.StartColumn, Target.RowSpan, Target.ColumnSpan);
                        HashSet<string> Occupied = RowOccupants(Targets, Candidate.StartRow, TargetId);
                        if (!FreeForOccupancyAndAspect(TargetId, Candidate, Targets, Occupied))
                        {
                            continue;
                        }

                        Targets[TargetId] = Candidate;
                        Changed = true;
                    }
                    else if (SourceBand > TargetBand)
                    {
                        if (Source.StartColumn == Target.StartColumn)
                        {
                            continue;
                        }

                        FlowchartCellRegion Candidate = new(Source.StartRow, Target.StartColumn, Source.RowSpan, Source.ColumnSpan);
                        HashSet<string> Occupied = RowOccupants(Targets, Candidate.StartRow, SourceId);
                        if (!FreeForOccupancyAndAspect(SourceId, Candidate, Targets, Occupied))
                        {
                            continue;
                        }

                        Targets[SourceId] = Candidate;
                        Changed = true;
                    }
                }

                if (!Changed)
                {
                    break;
                }
            }
        }

        /// <summary>Places secondary same-band branches on the hub row to the right of the hub.</summary>
        /// <param name="Targets">Complete private assignment.</param>
        internal void AlignHubBranchRows(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
                !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
            Dictionary<string, List<EdgeContract>> Outgoing = Visible
                .GroupBy(Edge => Edge.SourceId, StringComparer.Ordinal)
                .ToDictionary(Group => Group.Key, Group => Group.OrderBy(Edge => Edge.CreationIndex).ToList(), StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<EdgeContract>> Pair in Outgoing)
            {
                if (Pair.Value.Count < 2 || Hinted.Contains(Pair.Key) || !Targets.ContainsKey(Pair.Key)
                    || !Classes.ContainsKey(Pair.Key))
                {
                    continue;
                }

                FlowchartCellRegion Hub = Targets[Pair.Key];
                List<EdgeContract> SameBand = Pair.Value
                    .Where(Edge => TryLayoutEdgeEndpoints(Edge, Hinted, Targets, Classes, out string _, out string TargetId)
                        && Classes[TargetId].Band == Classes[Pair.Key].Band)
                    .OrderBy(Edge => Math.Abs(Targets[Edge.TargetId].StartColumn - Hub.StartColumn))
                    .ThenBy(Edge => Edge.CreationIndex)
                    .ToList();
                if (SameBand.Count < 2)
                {
                    continue;
                }

                EdgeContract Primary = SameBand[0];
                if (!Hinted.Contains(Primary.TargetId) && Targets.TryGetValue(Primary.TargetId, out FlowchartCellRegion PrimaryTarget))
                {
                    FlowchartCellRegion PrimaryCandidate = new(
                        Hub.EndRow,
                        Hub.StartColumn,
                        PrimaryTarget.RowSpan,
                        PrimaryTarget.ColumnSpan);
                    HashSet<string> PrimaryOccupied = Nodes
                        .Where(Node => Node.Id != Primary.TargetId && Targets[Node.Id].StartRow == PrimaryCandidate.StartRow)
                        .Select(Node => Node.Id)
                        .ToHashSet(StringComparer.Ordinal);
                    if (FreeForOccupancyAndAspect(Primary.TargetId, PrimaryCandidate, Targets, PrimaryOccupied))
                    {
                        Targets[Primary.TargetId] = PrimaryCandidate;
                    }
                }

                int Column = Hub.EndColumn;
                foreach (EdgeContract Edge in SameBand.Skip(1))
                {
                    if (Hinted.Contains(Edge.TargetId))
                    {
                        continue;
                    }

                    FlowchartCellRegion Target = Targets[Edge.TargetId];
                    FlowchartCellRegion Candidate = new(Hub.StartRow, Column, Target.RowSpan, Target.ColumnSpan);
                    HashSet<string> Occupied = Nodes
                        .Where(Node => Node.Id != Edge.TargetId && Targets[Node.Id].StartRow == Candidate.StartRow)
                        .Select(Node => Node.Id)
                        .ToHashSet(StringComparer.Ordinal);
                    if (!FreeForOccupancyAndAspect(Edge.TargetId, Candidate, Targets, Occupied))
                    {
                        continue;
                    }

                    Targets[Edge.TargetId] = Candidate;
                    Column = checked(Column + Target.ColumnSpan);
                }
            }
        }

        /// <summary>Within one semantic band, pull downstream nodes into the source column to form a readable spine.</summary>
        /// <param name="Targets">Complete private assignment.</param>
        internal void AlignSameBandSpineColumns(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
                !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
            for (int Sweep = 0; Sweep < Nodes.Count; Sweep++)
            {
                bool Changed = false;
                foreach (EdgeContract Edge in Visible)
                {
                    if (!TryLayoutEdgeEndpoints(Edge, Hinted, Targets, Classes, out string SourceId, out string TargetId))
                    {
                        continue;
                    }

                    if (Classes[SourceId].Band != Classes[TargetId].Band)
                    {
                        continue;
                    }

                    FlowchartCellRegion Source = Targets[SourceId];
                    FlowchartCellRegion Target = Targets[TargetId];
                    if (Target.StartRow < Source.StartRow || Target.StartColumn == Source.StartColumn)
                    {
                        continue;
                    }

                    FlowchartCellRegion Candidate = new(Target.StartRow, Source.StartColumn, Target.RowSpan, Target.ColumnSpan);
                    HashSet<string> Occupied = RowOccupants(Targets, Candidate.StartRow, TargetId);
                    if (!FreeForOccupancyAndAspect(TargetId, Candidate, Targets, Occupied))
                    {
                        continue;
                    }

                    Targets[TargetId] = Candidate;
                    Changed = true;
                }

                if (!Changed)
                {
                    break;
                }
            }
        }

        internal void AlignEnterpriseChainColumns(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            List<List<string>> Chains = BuildEnterpriseColumnChains();
            foreach (List<string> Chain in Chains)
            {
                if (Chain.Count < 2)
                {
                    continue;
                }

                string AnchorId = Chain[0];
                if (!Targets.TryGetValue(AnchorId, out FlowchartCellRegion AnchorRegion))
                {
                    continue;
                }

                int Column = AnchorRegion.StartColumn;
                foreach (string Id in Chain)
                {
                    if (Hinted.Contains(Id) || !Targets.TryGetValue(Id, out FlowchartCellRegion Region))
                    {
                        continue;
                    }

                    if (Region.StartColumn == Column)
                    {
                        continue;
                    }

                    FlowchartCellRegion Candidate = new(Region.StartRow, Column, Region.RowSpan, Region.ColumnSpan);
                    HashSet<string> Occupied = Nodes
                        .Where(Node => Node.Id != Id && Classes[Node.Id].Band == Classes[Id].Band
                            && Targets[Node.Id].StartRow == Candidate.StartRow)
                        .Select(Node => Node.Id)
                        .ToHashSet(StringComparer.Ordinal);
                    if (!FreeForOccupancyAndAspect(Id, Candidate, Targets, Occupied))
                    {
                        continue;
                    }

                    Targets[Id] = Candidate;
                }
            }
        }

        /// <summary>Rule 2.25: a same-band neighbour pair with one local edge shares a row for a clear straight chord.</summary>
        /// <param name="Targets">Complete private assignment.</param>
        /// <summary>Rule 2.25: nodes with a single declared partner share that partner's column.</summary>
        /// <param name="Targets">Complete private assignment.</param>
        internal void AlignLonelyNeighbourColumns(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            Dictionary<string, HashSet<string>> Neighbours = new(StringComparer.Ordinal);
            foreach (EdgeContract Edge in Contract.Edges.Where(Edge =>
                !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
            {
                AddNeighbour(Neighbours, Edge.SourceId, Edge.TargetId);
                AddNeighbour(Neighbours, Edge.TargetId, Edge.SourceId);
            }

            for (int Sweep = 0; Sweep < Nodes.Count; Sweep++)
            {
                bool Changed = false;
                foreach (DiagramGridLayout.GridNode Node in Nodes)
                {
                    if (Hinted.Contains(Node.Id) || !Neighbours.TryGetValue(Node.Id, out HashSet<string>? Set) || Set.Count != 1)
                    {
                        continue;
                    }

                    string PeerId = Set.First();
                    if (!Targets.TryGetValue(PeerId, out FlowchartCellRegion Peer) || !Targets.TryGetValue(Node.Id, out FlowchartCellRegion Region))
                    {
                        continue;
                    }

                    if (Region.StartColumn == Peer.StartColumn)
                    {
                        continue;
                    }

                    FlowchartCellRegion Candidate = new(Region.StartRow, Peer.StartColumn, Region.RowSpan, Region.ColumnSpan);
                    HashSet<string> Occupied = Nodes
                        .Where(Other => Other.Id != Node.Id && Targets[Other.Id].StartRow == Candidate.StartRow)
                        .Select(Other => Other.Id)
                        .ToHashSet(StringComparer.Ordinal);
                    if (!FreeForOccupancyAndAspect(Node.Id, Candidate, Targets, Occupied))
                    {
                        continue;
                    }

                    Targets[Node.Id] = Candidate;
                    Changed = true;
                }

                if (!Changed)
                {
                    break;
                }
            }
        }

        private static void AddNeighbour(Dictionary<string, HashSet<string>> Neighbours, string Left, string Right)
        {
            if (!Neighbours.TryGetValue(Left, out HashSet<string>? Set))
            {
                Set = new HashSet<string>(StringComparer.Ordinal);
                Neighbours[Left] = Set;
            }

            Set.Add(Right);
        }

        internal void AlignSameBandChordRows(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
                !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (EdgeContract Edge in Visible)
            {
                if (!TryLayoutEdgeEndpoints(Edge, Hinted, Targets, Classes, out string SourceId, out string TargetId))
                {
                    continue;
                }

                if (Classes[SourceId].Band != Classes[TargetId].Band)
                {
                    continue;
                }

                FlowchartCellRegion Source = Targets[SourceId];
                FlowchartCellRegion Target = Targets[TargetId];
                int SharedRow = Math.Min(Source.StartRow, Target.StartRow);
                int SourceColumn = Math.Min(Source.StartColumn, Target.StartColumn);
                int TargetColumn = Source.StartColumn == Target.StartColumn
                    ? checked(Source.StartColumn + Source.ColumnSpan)
                    : Math.Max(Source.StartColumn, Target.StartColumn);

                FlowchartCellRegion SourceCandidate = new(SharedRow, SourceColumn, Source.RowSpan, Source.ColumnSpan);
                FlowchartCellRegion TargetCandidate = new(SharedRow, TargetColumn, Target.RowSpan, Target.ColumnSpan);
                HashSet<string> Occupied = RowOccupants(Targets, SharedRow, SourceId, TargetId);
                if (!FreeFor(SourceId, SourceCandidate, Targets, Occupied)
                    || !FreeFor(TargetId, TargetCandidate, Targets, Occupied.Union([SourceId]).ToHashSet(StringComparer.Ordinal)))
                {
                    continue;
                }

                Targets[SourceId] = SourceCandidate;
                Targets[TargetId] = TargetCandidate;
            }
        }

        /// <summary>Stacks accessors above passive objects on the object's column.</summary>
        /// <param name="Targets">Complete private assignment.</param>
        internal void AlignAccessorObjectColumns(Dictionary<string, FlowchartCellRegion> Targets)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            foreach (EdgeContract Edge in Contract.Edges.Where(Edge =>
                !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
            {
                if (Hinted.Contains(Edge.SourceId) || Hinted.Contains(Edge.TargetId)
                    || !Targets.ContainsKey(Edge.SourceId) || !Targets.ContainsKey(Edge.TargetId))
                {
                    continue;
                }

                bool AccessLike = string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase)
                    || (Edge.Label?.Contains("access", StringComparison.OrdinalIgnoreCase) ?? false);
                if (!AccessLike)
                {
                    continue;
                }

                FlowchartCellRegion ObjectRegion = Targets[Edge.TargetId];
                FlowchartCellRegion ProcessRegion = Targets[Edge.SourceId];
                int ProcessRow = checked(ObjectRegion.StartRow - ProcessRegion.RowSpan);
                if (ProcessRow < 0)
                {
                    ProcessRow = ObjectRegion.StartRow;
                }

                FlowchartCellRegion Candidate = new(ProcessRow, ObjectRegion.StartColumn, ProcessRegion.RowSpan, ProcessRegion.ColumnSpan);
                HashSet<string> Occupied = Nodes
                    .Where(Node => Node.Id != Edge.SourceId && Targets[Node.Id].StartRow == Candidate.StartRow)
                    .Select(Node => Node.Id)
                    .ToHashSet(StringComparer.Ordinal);
                if (!FreeForOccupancyAndAspect(Edge.SourceId, Candidate, Targets, Occupied))
                {
                    continue;
                }

                Targets[Edge.SourceId] = Candidate;
            }
        }

        private static HashSet<string> RowOccupants(
            IReadOnlyDictionary<string, FlowchartCellRegion> Targets,
            int Row,
            params string[] Exclude)
        {
            HashSet<string> ExcludeSet = new(Exclude, StringComparer.Ordinal);
            return Targets.Where(Pair => Pair.Value.StartRow == Row && !ExcludeSet.Contains(Pair.Key))
                .Select(Pair => Pair.Key)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static bool TryLayoutEdgeEndpoints(
            EdgeContract Edge,
            HashSet<string> Hinted,
            IReadOnlyDictionary<string, FlowchartCellRegion> Targets,
            IReadOnlyDictionary<string, Classification> Classes,
            out string SourceId,
            out string TargetId)
        {
            SourceId = Edge.SourceId;
            TargetId = Edge.TargetId;
            if (Hinted.Contains(SourceId) || Hinted.Contains(TargetId))
            {
                return false;
            }

            if (!Classes.ContainsKey(SourceId) || !Classes.ContainsKey(TargetId))
            {
                return false;
            }

            return Targets.ContainsKey(SourceId) && Targets.ContainsKey(TargetId);
        }

        private List<List<string>> BuildEnterpriseColumnChains()
        {
            List<EdgeContract> Visible = Contract.Edges.Where(Edge =>
                !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                && Classes.ContainsKey(Edge.SourceId) && Classes.ContainsKey(Edge.TargetId)
                && Classes[Edge.SourceId].Band == Classes[Edge.TargetId].Band).ToList();
            HashSet<string> NodeIds = Contract.Nodes.Select(Node => Node.Id)
                .Where(Id => Classes.ContainsKey(Id))
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, List<string>> Outgoing = NodeIds.ToDictionary(
                Id => Id,
                Id => Visible.Where(Edge => Edge.SourceId == Id).Select(Edge => Edge.TargetId).Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);
            Dictionary<string, int> Indegree = NodeIds.ToDictionary(Id => Id, _ => 0, StringComparer.Ordinal);
            foreach (EdgeContract Edge in Visible)
            {
                if (NodeIds.Contains(Edge.TargetId))
                {
                    Indegree[Edge.TargetId]++;
                }
            }

            Dictionary<string, int> Order = Contract.Nodes
                .Select((Node, Index) => (Node.Id, Index))
                .ToDictionary(Item => Item.Id, Item => Item.Index, StringComparer.Ordinal);
            List<string> Starts = NodeIds.Where(Id => Indegree[Id] == 0)
                .OrderBy(Id => Order.GetValueOrDefault(Id, int.MaxValue))
                .ToList();
            if (Starts.Count == 0)
            {
                Starts = NodeIds.OrderBy(Id => Classes[Id].Band).ThenBy(Id => Order.GetValueOrDefault(Id, int.MaxValue)).ToList();
            }

            List<List<string>> Chains = [];
            HashSet<string> Visited = new(StringComparer.Ordinal);
            foreach (string Start in Starts)
            {
                if (Visited.Contains(Start))
                {
                    continue;
                }

                List<string> Path = WalkSimpleChain(Start, Outgoing, Indegree, Visited);
                if (Path.Count >= 2)
                {
                    Chains.Add(Path);
                }
            }

            foreach (string Id in NodeIds.Where(Id => !Visited.Contains(Id)).OrderBy(Id => Order.GetValueOrDefault(Id, int.MaxValue)))
            {
                List<string> Path = WalkSimpleChain(Id, Outgoing, Indegree, Visited);
                if (Path.Count >= 2)
                {
                    Chains.Add(Path);
                }
            }

            return Chains;
        }

        private static List<string> WalkSimpleChain(
            string Start,
            IReadOnlyDictionary<string, List<string>> Outgoing,
            IReadOnlyDictionary<string, int> Indegree,
            HashSet<string> Visited)
        {
            List<string> Path = [Start];
            Visited.Add(Start);
            string Current = Start;
            while (Outgoing.TryGetValue(Current, out List<string>? NextIds) && NextIds.Count == 1)
            {
                string Next = NextIds[0];
                if (Visited.Contains(Next) || Indegree.GetValueOrDefault(Next) != 1)
                {
                    break;
                }

                Path.Add(Next);
                Visited.Add(Next);
                Current = Next;
            }

            return Path;
        }

    }
}
