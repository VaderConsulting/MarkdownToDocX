using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Checks connection clearance while constructing a private initial band assignment.</summary>
internal static partial class EnterpriseBandPlacement
{
    /// <summary>Owns complete initial cell candidates before their atomic publication.</summary>
    private sealed partial class BandPlan
    {
        /// <summary>Unresolved direct-connection blockers retained when fixed placement constraints rule out the enumerated free columns.</summary>
        internal List<string> ConnectionClearanceDiagnostics { get; } = [];

        /// <summary>Rejects occupied connection columns when a free, convention-preserving column exists in the same band row.</summary>
        /// <param name="Targets">Complete unpublished regions; rows, groups, hints and frame bounds remain fixed.</param>
        private void AvoidBlockedBandColumns(Dictionary<string, FlowchartCellRegion> Targets)
        {
            var Hinted = HintControlledIds(Contract, Nodes);
            var Before = BlockedConnections(Targets);
            if (Before.Count == 0) return;
            int Crossings = ConnectionCrossings(Targets);
            foreach (var Node in Nodes.Where(Node => Node.GroupId == null && !Hinted.Contains(Node.Id)))
            {
                if (!Before.Any(Item => Item.Blocker == Node.Id)) continue;
                var OriginalRegion = Targets[Node.Id];
                var Occupied = Targets.Keys.Where(Id => Id != Node.Id).ToHashSet(StringComparer.Ordinal);
                int Right = checked(Targets.Values.Max(Region => Region.EndColumn) + OriginalRegion.ColumnSpan);
                var ExistingFrames = ProjectedFrameFaults(Targets);
                foreach (int Column in Enumerable.Range(0, Right - OriginalRegion.ColumnSpan + 1)
                    .OrderBy(Column => Math.Abs(Column - OriginalRegion.StartColumn)).ThenBy(Column => Column))
                {
                    if (Column == OriginalRegion.StartColumn) continue;
                    var Candidate = new FlowchartCellRegion(OriginalRegion.StartRow, Column,
                        OriginalRegion.RowSpan, OriginalRegion.ColumnSpan);
                    if (!FreeFor(Node.Id, Candidate, Targets, Occupied) || !KeepsLocalOrder(Node.Id, Candidate, Targets)) continue;
                    var Proposed = new Dictionary<string, FlowchartCellRegion>(Targets, StringComparer.Ordinal) { [Node.Id] = Candidate };
                    var After = BlockedConnections(Proposed);
                    int ProposedCrossings = ConnectionCrossings(Proposed);
                    if (After.Count >= Before.Count || After.Any(Item => !Before.Contains(Item))
                        || !PreservesAlignedPartners(Targets, Proposed)
                        || ProposedCrossings > Crossings
                        || !MatchesConvention(Contract, Classes, Proposed)
                        || !Nodes[0].CellNode!.Grid.TryPrepare(Proposed, [], out _)
                        || ProjectedFrameFaults(Proposed).Any(Fault => !ExistingFrames.Contains(Fault))) continue;
                    Targets[Node.Id] = Candidate;
                    Before = After;
                    Crossings = ProposedCrossings;
                    break;
                }
            }
            foreach (var Item in Before.OrderBy(Item => Item.Source, StringComparer.Ordinal)
                .ThenBy(Item => Item.Target, StringComparer.Ordinal).ThenBy(Item => Item.Blocker, StringComparer.Ordinal))
                ConnectionClearanceDiagnostics.Add($"BAND_CONNECTION_BLOCKED|{Item.Source}->{Item.Target}|blocker={Item.Blocker}"
                    + $"|hintControlled={Hinted.Contains(Item.Blocker)}|reason=no-valid-enumerated-column");
        }

        /// <summary>Keeps every already attained cross-band partner column while choosing a same-band connection-clearance column.</summary>
        /// <param name="Before">Existing private placement, including completed partner alignment.</param>
        /// <param name="After">One proposed alternative column.</param>
        /// <returns>False if the proposal would undo a straight cross-band partner alignment.</returns>
        private bool PreservesAlignedPartners(IReadOnlyDictionary<string, FlowchartCellRegion> Before,
            IReadOnlyDictionary<string, FlowchartCellRegion> After)
            => Contract.Edges.Where(Edge => Before.ContainsKey(Edge.SourceId) && Before.ContainsKey(Edge.TargetId)
                && Classes[Edge.SourceId].Band != Classes[Edge.TargetId].Band)
                .All(Edge => Before[Edge.SourceId].StartColumn != Before[Edge.TargetId].StartColumn
                    || After[Edge.SourceId].StartColumn == After[Edge.TargetId].StartColumn);

        /// <summary>Uses the existing Dagre adjacent-row counter to avoid trading column clearance for a worse connection ordering.</summary>
        /// <param name="Targets">Complete private cell positions; no routed geometry is estimated or published.</param>
        /// <returns>Fixed-coordinate crossing count over all declared relationships.</returns>
        private int ConnectionCrossings(IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            var Vertices = Nodes.Select(Node => new DagreLayoutEngine.BandVertex(Node.Id, "grid",
                Targets[Node.Id].StartRow, Targets[Node.Id].StartColumn)).ToList();
            foreach (var Group in Contract.Groups)
            {
                var Members = Nodes.Where(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Group.Id)).ToArray();
                if (Members.Length == 0) continue;
                Vertices.Add(new(Group.Id, "grid", (int)Math.Round(Members.Average(Node => Targets[Node.Id].StartRow)),
                    Members.Average(Node => (double)Targets[Node.Id].StartColumn)));
            }
            var Ids = Vertices.Select(Vertex => Vertex.Id).ToHashSet(StringComparer.Ordinal);
            return DagreLayoutEngine.CountBandCoordinateCrossings(Vertices, Contract.Edges
                .Where(Edge => Ids.Contains(Edge.SourceId) && Ids.Contains(Edge.TargetId))
                .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToArray());
        }

        /// <summary>Finds third nodes occupying the open column interval of a direct same-band connection.</summary>
        /// <param name="Targets">Complete private whole-cell geometry.</param>
        /// <returns>Stable endpoint and blocker identities; endpoint regions themselves are excluded.</returns>
        private HashSet<(string Source, string Target, string Blocker)> BlockedConnections(
            IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            var Result = new HashSet<(string Source, string Target, string Blocker)>();
            foreach (var Edge in Contract.Edges)
            {
                if (Edge.SourceId == Edge.TargetId || !Targets.TryGetValue(Edge.SourceId, out var Source)
                    || !Targets.TryGetValue(Edge.TargetId, out var Target)
                    || Classes[Edge.SourceId].Band != Classes[Edge.TargetId].Band
                    || Source.StartColumn != Target.StartColumn || Source.ColumnSpan != Target.ColumnSpan) continue;
                int First = Math.Min(Source.EndRow, Target.EndRow), Last = Math.Max(Source.StartRow, Target.StartRow);
                foreach (var Node in Nodes.Where(Node => Node.Id != Edge.SourceId && Node.Id != Edge.TargetId
                    && Classes[Node.Id].Band == Classes[Edge.SourceId].Band))
                {
                    var Region = Targets[Node.Id];
                    if (Region.StartRow < Last && Region.EndRow > First
                        && Region.StartColumn < Source.EndColumn && Region.EndColumn > Source.StartColumn)
                        Result.Add((Edge.SourceId, Edge.TargetId, Node.Id));
                }
            }
            return Result;
        }
    }
}
