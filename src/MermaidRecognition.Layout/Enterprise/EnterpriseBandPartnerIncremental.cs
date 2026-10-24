using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Enterprise;

internal static partial class EnterpriseBandPlacement
{
    private sealed partial class BandPlan
    {
        /// <summary>Publishes completed-partner column pulls one legal move at a time on the live grid.</summary>
        /// <param name="Grid">Authoritative cell grid after initial band publication.</param>
        /// <param name="Diagnostics">Optional layout log sink.</param>
        internal void PublishCompletedPartnersIncrementally(FlowchartLogicalGrid Grid, IList<string>? Diagnostics)
        {
            HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
            HashSet<int> TimelineBands = Classes.Values
                .Where(Item => Item.Type == MermaidRecognition.Kinds.Enterprise.EnterpriseElementType.Plateau)
                .Select(Item => Item.Band)
                .ToHashSet();
            HashSet<int> LoggedBandSkips = [];
            for (int Sweep = 0; Sweep < DagreLayoutEngine.OrderSweepPatience; Sweep++)
            {
                bool Changed = false;
                IGrouping<int, DiagramGridLayout.GridNode>[] Bands = Nodes.GroupBy(Node => Classes[Node.Id].Band)
                    .OrderBy(Band => Band.Key)
                    .ToArray();
                if (Sweep % 2 != 0)
                {
                    Array.Reverse(Bands);
                }

                Dictionary<string, FlowchartCellRegion> Targets = Grid.Capture().Regions.ToDictionary(
                    Pair => Pair.Key,
                    Pair => Pair.Value,
                    StringComparer.Ordinal);
                foreach (IGrouping<int, DiagramGridLayout.GridNode> Band in Bands)
                {
                    if (TimelineBands.Contains(Band.Key))
                    {
                        if (LoggedBandSkips.Add(Band.Key))
                        {
                            Log(Diagnostics, "skipped", "band:" + Band.Key, "timeline-plateau-band", null, null);
                        }

                        continue;
                    }

                    if (Band.Count() > 1 && !Band.Any(Node => Node.GroupId != null))
                    {
                        if (LoggedBandSkips.Add(Band.Key))
                        {
                            Log(Diagnostics, "skipped", "band:" + Band.Key, "multi-node-ungrouped-band", null, null);
                        }

                        continue;
                    }

                    foreach (DiagramGridLayout.GridNode Node in Band.Where(Node => Node.GroupId == null && !Hinted.Contains(Node.Id)))
                    {
                        string[] Partners = CrossBandMembers(Node.Id, Targets).ToArray();
                        double Pull = Band.Count() == 1 && Partners.Length > 0
                            ? MedianColumn(Partners, Targets)
                            : PartnerMedian(Node.Id, Targets);
                        FlowchartCellRegion Region = Targets[Node.Id];
                        if (Region.StartColumn == (int)Math.Round(Pull, MidpointRounding.AwayFromZero)
                            && Math.Abs(Region.StartColumn - Pull) < 0.001)
                        {
                            continue;
                        }

                        HashSet<string> Occupied = Band.Where(Peer => Peer.Id != Node.Id)
                            .Select(Peer => Peer.Id)
                            .ToHashSet(StringComparer.Ordinal);
                        FlowchartLogicalGrid.Snapshot Current = Grid.Capture();
                        FlowchartLogicalGrid.Snapshot Geometry = new(
                            Grid,
                            Targets,
                            Current.ColumnGaps,
                            Current.RowGaps);
                        Dictionary<string, FlowchartLayoutNodeBounds>? Bounds = null;
                        List<DiagramGridLayout.GridFrame>? Frames = null;
                        HashSet<string>? ExistingFaults = null;
                        if (Contract.Groups.Count > 0)
                        {
                            Frames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Geometry);
                            Bounds = Nodes.ToDictionary(
                                Member => Member.Id,
                                Member => Member.CellNode!.Bounds(Targets[Member.Id], Member.GeometryProvider, Geometry));
                            ExistingFaults = DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, Frames, Bounds);
                        }

                        int Right = checked(Math.Max(Targets.Values.Max(Item => Item.EndColumn), (int)Math.Ceiling(Pull))
                            + Region.ColumnSpan);
                        bool Moved = false;
                        foreach (int Column in Enumerable.Range(0, Right - Region.ColumnSpan + 1)
                            .OrderBy(Column => Math.Abs(Column - Pull))
                            .ThenBy(Column => Column))
                        {
                            FlowchartCellRegion Candidate = new(Region.StartRow, Column, Region.RowSpan, Region.ColumnSpan);
                            if (Candidate == Region)
                            {
                                continue;
                            }

                            double CurrentDistance = Math.Abs(Region.StartColumn - Pull);
                            double CandidateDistance = Math.Abs(Column - Pull);
                            if (CandidateDistance + 1e-9 >= CurrentDistance)
                            {
                                continue;
                            }

                            if (!FreeForOccupancyAndAspect(Node.Id, Candidate, Targets, Occupied))
                            {
                                Log(Diagnostics, "rejected", Node.Id, "occupancy-or-aspect-order", Region, Candidate);
                                continue;
                            }

                            if (!KeepsLocalOrder(Node.Id, Candidate, Targets))
                            {
                                Log(Diagnostics, "rejected", Node.Id, "local-rank-order", Region, Candidate);
                                continue;
                            }

                            if (Frames != null && Bounds != null)
                            {
                                Bounds[Node.Id] = Node.CellNode!.Bounds(Candidate, Node.GeometryProvider, Geometry);
                                if (DiagramGridLayout.ProjectedCellFrameConflictKeys(Contract, Nodes, Frames, Bounds)
                                    .Any(Fault => !ExistingFaults!.Contains(Fault)))
                                {
                                    Log(Diagnostics, "rejected", Node.Id, "frame-conflict", Region, Candidate);
                                    continue;
                                }
                            }

                            Dictionary<string, FlowchartCellRegion> Change = new(StringComparer.Ordinal)
                            {
                                [Node.Id] = Candidate,
                            };
                            if (!Grid.TryPrepare(Change, [], out FlowchartLogicalGrid.Snapshot? After))
                            {
                                string Reason = Grid.DescribePrepareFailure(Change) ?? "grid-prepare";
                                Log(Diagnostics, "rejected", Node.Id, Reason, Region, Candidate);
                                continue;
                            }

                            FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
                            if (!Grid.TryCommit(Before, After!))
                            {
                                Log(Diagnostics, "rejected", Node.Id, "stale-grid-commit", Region, Candidate);
                                continue;
                            }

                            Log(Diagnostics, "applied", Node.Id, "completed-partner-column", Region, Candidate);
                            Targets[Node.Id] = Candidate;
                            Changed = true;
                            Moved = true;
                            break;
                        }

                        if (!Moved && Partners.Length > 0)
                        {
                            Log(Diagnostics, "rejected", Node.Id, "no-legal-column-toward-partner", Region, null);
                        }
                    }
                }

                if (!Changed)
                {
                    break;
                }
            }
        }

        private static void Log(
            IList<string>? Diagnostics,
            string Outcome,
            string Subject,
            string Reason,
            FlowchartCellRegion? From,
            FlowchartCellRegion? To)
        {
            if (Diagnostics == null)
            {
                return;
            }

            string FromText = From == null ? "-" : From.StartRow + "," + From.StartColumn;
            string ToText = To == null ? "-" : To.StartRow + "," + To.StartColumn;
            Diagnostics.Add(
                "PARTNER_ALIGN|" + Outcome + "|" + Subject + "|" + Reason + "|from=" + FromText + "|to=" + ToText);
        }
    }
}
