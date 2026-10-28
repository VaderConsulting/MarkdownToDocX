using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Ranks relationships locally within a semantic band and constructs unwrapped plateau timelines.</summary>
internal static partial class EnterpriseBandPlacement
{
    /// <summary>Counts relationships crossing a proposed chain-row boundary, preserving complete work-package groups.</summary>
    /// <param name="Contract">Declared connections.</param>
    /// <param name="Order">Topological chain order.</param>
    /// <param name="Cut">First element of the following row.</param>
    /// <returns>Number of chain relationships cut by that boundary.</returns>
    private static int ChainBreakLines(DiagramContract Contract, IReadOnlyList<string> Order, int Cut)
    {
        HashSet<string> Left = Order.Take(Cut).ToHashSet(StringComparer.Ordinal);
        HashSet<string> All = Order.ToHashSet(StringComparer.Ordinal);
        return Contract.Edges.Count(Edge => All.Contains(Edge.SourceId) && All.Contains(Edge.TargetId)
            && Left.Contains(Edge.SourceId) != Left.Contains(Edge.TargetId));
    }

    /// <summary>Sweeps across implementation rows with realising work packages and deliverables moving as aligned units.</summary>
    /// <param name="Contract">Internal and cross-band relationships.</param>
    /// <param name="Classes">Semantic types used to retain plateau order.</param>
    /// <param name="Targets">Private complete plan.</param>
    /// <param name="Members">Ungrouped timeline members.</param>
    /// <param name="Deliveries">Deliverable to realising work-package mapping.</param>
    private static void RefineImplementationRows(DiagramContract Contract, IReadOnlyDictionary<string, Classification> Classes,
        Dictionary<string, FlowchartCellRegion> Targets, IReadOnlySet<string> Members, IReadOnlyDictionary<string, string> Deliveries)
    {
        Dictionary<string, string> Owner = Members.ToDictionary(Id => Id, Id => Deliveries.GetValueOrDefault(Id) ?? Id, StringComparer.Ordinal);
        DagreLayoutEngine.BandVertex[] Vertices = [.. Members.Select(Id => new DagreLayoutEngine.BandVertex(Id, Owner[Id], Targets[Id].StartRow, 0))];
        var Initial = Owner.Values.Distinct().OrderBy(Id => Targets[Id].StartColumn).ThenBy(Id => Id, StringComparer.Ordinal).ToArray();
        (string SourceId, string TargetId)[] Links = [.. Contract.Edges.Where(Edge => Members.Contains(Edge.SourceId) && Members.Contains(Edge.TargetId)).Select(Edge => (Edge.SourceId, Edge.TargetId))];
        EdgeContract[] Constraints = [.. Contract.Edges.Where(IsTriggering).Where(Edge => Members.Contains(Edge.SourceId) && Members.Contains(Edge.TargetId)
            && (Targets[Edge.SourceId].StartRow == Targets[Edge.TargetId].StartRow || Classes[Edge.SourceId].Type == EnterpriseElementType.Plateau))];
        var WorkRowOrder = Members.Where(Id => Classes[Id].Type == EnterpriseElementType.WorkPackage)
            .OrderBy(Id => Targets[Id].StartColumn).Select(Id => Owner[Id]).Distinct().ToArray();
        (IReadOnlyList<string> Order, int Before, int After) Result = DagreLayoutEngine.OrderCompleteBandBlocks(Initial, Vertices, Links,
            Order => Constraints.All(Edge => Order.ToList().IndexOf(Owner[Edge.SourceId]) < Order.ToList().IndexOf(Owner[Edge.TargetId]))
                && Enumerable.Range(1, Math.Max(0, WorkRowOrder.Length - 1)).All(Index =>
                    Order.ToList().IndexOf(WorkRowOrder[Index - 1]) < Order.ToList().IndexOf(WorkRowOrder[Index])));
        Dictionary<string, (int Top, int Bottom, int Width)> Extents = Owner.GroupBy(Pair => Pair.Value).ToDictionary(Group => Group.Key,
            Group => (Top: Group.Min(Pair => Targets[Pair.Key].StartRow), Bottom: Group.Max(Pair => Targets[Pair.Key].EndRow), Width: Group.Max(Pair => Targets[Pair.Key].ColumnSpan)));
        Dictionary<string, int> Columns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string Id in Result.Order)
        {
            Columns[Id] = Columns.Where(Pair => Extents[Pair.Key].Top < Extents[Id].Bottom && Extents[Pair.Key].Bottom > Extents[Id].Top)
                .Select(Pair => Pair.Value + Extents[Pair.Key].Width).DefaultIfEmpty(0).Max();
        }

        foreach (string Id in Members)
        {
            FlowchartCellRegion Region = Targets[Id]; Targets[Id] = new(Region.StartRow, Columns[Owner[Id]], Region.RowSpan, Region.ColumnSpan);
        }
    }

    /// <summary>Places ungrouped technology services in legal free columns nearest their host and application partners.</summary>
    /// <param name="Contract">Declared membership and serving relationships.</param>
    /// <param name="Classes">Local aspect and semantic band classifications.</param>
    /// <param name="Targets">Complete private plan before band row offsets are applied.</param>
    /// <param name="Reservations">Solid frame columns and their occupied semantic band intervals.</param>
    private static void PlaceTechnologyServices(DiagramContract Contract, IReadOnlyDictionary<string, Classification> Classes,
        Dictionary<string, FlowchartCellRegion> Targets, IReadOnlyList<(int FirstBand, int LastBand, int Left, int Right)> Reservations)
    {
        foreach (NodeContract? Node in Contract.Nodes.Where(Node => Node.ParentGroupId == null
            && Classes.GetValueOrDefault(Node.Id)?.Type == EnterpriseElementType.TechnologyService))
        {
            FlowchartCellRegion Region = Targets[Node.Id]; Classification Class = Classes[Node.Id];
            var Partners = Contract.Edges.Where(Edge => Edge.SourceId == Node.Id || Edge.TargetId == Node.Id)
                .Select(Edge => Edge.SourceId == Node.Id ? Edge.TargetId : Edge.SourceId).Distinct().ToArray();
            var PartnerColumns = Partners.SelectMany(Id => Targets.ContainsKey(Id) ? [(double)Targets[Id].StartColumn]
                : Targets.Where(Pair => DiagramGridLayout.IsInsideGroup(Contract, Pair.Key, Id)).Select(Pair => (double)Pair.Value.StartColumn)).ToArray();
            if (PartnerColumns.Length == 0)
            {
                continue;
            }

            double Pull = PartnerColumns.Average();
            int Right = Targets.Values.Max(Item => Item.EndColumn);
            KeyValuePair<string, FlowchartCellRegion>[] Other = [.. Targets.Where(Pair => Pair.Key != Node.Id && Classes[Pair.Key].Band == Class.Band)];
            int? Start = Enumerable.Range(0, Right + 1).Where(Column =>
                Reservations.Where(Frame => Frame.FirstBand <= Class.Band && Frame.LastBand >= Class.Band)
                    .All(Frame => Column + Region.ColumnSpan <= Frame.Left || Column >= Frame.Right)
                && Other.All(Pair => Region.EndRow <= Pair.Value.StartRow || Region.StartRow >= Pair.Value.EndRow
                    || Column + Region.ColumnSpan <= Pair.Value.StartColumn || Column >= Pair.Value.EndColumn)
                && Contract.Nodes.Where(Peer => Peer.ParentGroupId == null && Peer.Id != Node.Id && Classes[Peer.Id].Band == Class.Band)
                    .All(Peer => Classes[Peer.Id].Aspect == Class.Aspect
                        || (Classes[Peer.Id].Aspect < Class.Aspect ? Targets[Peer.Id].EndColumn <= Column : Column + Region.ColumnSpan <= Targets[Peer.Id].StartColumn)))
                .OrderBy(Column => Math.Abs(Column - Pull)).ThenBy(Column => Column).Cast<int?>().FirstOrDefault();
            if (Start.HasValue)
            {
                Targets[Node.Id] = new(Region.StartRow, Start.Value, Region.RowSpan, Region.ColumnSpan);
            }
        }
    }

    /// <summary>Runs the existing Dagre rank and order stages on one band's own relationships only.</summary>
    /// <param name="Contract">Source direction and relationships; cross-band links are deliberately excluded.</param>
    /// <param name="Members">The members of this band or its part inside one grouping frame.</param>
    /// <returns>Local ranks and orders, independent of every other band's rank constraints.</returns>
    private static Dictionary<string, (int Rank, int Order)> RankBand(DiagramContract Contract,
        IReadOnlyList<DiagramGridLayout.GridNode> Members)
    {
        DagreGraph Graph = new DagreGraph(Multigraph: true);
        Graph.SetGraph(new() { RankDir = Contract.Direction?.ToLowerInvariant() ?? "tb" });
        HashSet<string> Ids = Members.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        foreach (DiagramGridLayout.GridNode Node in Members)
        {
            Graph.SetNode(Node.Id, new() { Width = Node.CellNode!.Region.ColumnSpan, Height = Node.CellNode.Region.RowSpan }, true);
        }

        foreach (EdgeContract? Edge in Contract.Edges.Where(Edge => Ids.Contains(Edge.SourceId) && Ids.Contains(Edge.TargetId)))
        {
            Graph.SetEdge(Edge.SourceId, Edge.TargetId, new() { Weight = 1, Minlen = 1 }, true, Edge.Id);
        }

        DagreLayoutEngine.Layout(Graph);
        return Members.ToDictionary(Node => Node.Id, Node => (Graph.Node(Node.Id)!.Rank ?? 0, Graph.Node(Node.Id)!.Order ?? 0), StringComparer.Ordinal);
    }

    /// <summary>Builds unwrapped plateau and wrapped implementation-trigger rows within their semantic band.</summary>
    /// <param name="Contract">Declared triggering, realisation and composition relationships.</param>
    /// <param name="Classes">Semantic classifications; only ungrouped implementation members are rearranged.</param>
    /// <param name="Targets">Private complete cell assignment; no published node is moved individually.</param>
    /// <param name="WrapColumns">Existing scored wrap width, applied to the work row but never the plateau row.</param>
    private static void ArrangeImplementationTimelines(DiagramContract Contract, IReadOnlyDictionary<string, Classification> Classes,
        Dictionary<string, FlowchartCellRegion> Targets, int WrapColumns)
    {
        var Plateaus = Classes.Where(Pair => Pair.Value.Type == EnterpriseElementType.Plateau
                && Contract.Nodes.Any(Node => Node.Id == Pair.Key && Node.ParentGroupId == null)).Select(Pair => Pair.Key).ToArray();
        if (Plateaus.Length == 0)
        {
            return;
        }

        int Band = Classes[Plateaus[0]].Band;
        HashSet<string> Members = Classes.Where(Pair => Pair.Value.Band == Band
            && Contract.Nodes.Any(Node => Node.Id == Pair.Key && Node.ParentGroupId == null)).Select(Pair => Pair.Key).ToHashSet(StringComparer.Ordinal);
        EdgeContract[] ChainEdges = [.. Contract.Edges.Where(IsTriggering).Where(Edge => Members.Contains(Edge.SourceId) && Members.Contains(Edge.TargetId)
            && Classes[Edge.SourceId].Type != EnterpriseElementType.Plateau && Classes[Edge.TargetId].Type != EnterpriseElementType.Plateau)];
        List<string> Pending = [.. ChainEdges.SelectMany(Edge => new[] { Edge.SourceId, Edge.TargetId }).Distinct()];
        List<string> Ordered = [];
        while (Pending.Count > 0)
        {
            string? Next = Pending.FirstOrDefault(Id => !ChainEdges.Any(Edge => Edge.TargetId == Id && Pending.Contains(Edge.SourceId)));
            if (Next == null)
            {
                return; // Cycles retain the general band ranking rather than inventing a triggering order.
            }

            Ordered.Add(Next); Pending.Remove(Next);
        }
        if (Ordered.Count == 0)
        {
            return;
        }

        int Top = Members.Min(Id => Targets[Id].StartRow), PlateauTop = Plateaus.Min(Id => Targets[Id].StartRow);
        foreach (string Id in Plateaus.Concat(Members.Where(Id => Classes[Id].Type == EnterpriseElementType.Gap)))
        {
            Targets[Id] = FlowchartCellRepair.Translate(Targets[Id], Top - PlateauTop, 0);
        }

        int PlateauBottom = Plateaus.Concat(Members.Where(Id => Classes[Id].Type == EnterpriseElementType.Gap)).Max(Id => Targets[Id].EndRow);
        var Hubs = Members.Where(Id => Classes[Id].Type == EnterpriseElementType.WorkPackage && !Ordered.Contains(Id)
            && Contract.Edges.Any(Edge => Edge.SourceId == Id && Ordered.Contains(Edge.TargetId))).ToArray();
        Dictionary<string, string> Deliveries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string Id in Ordered)
        {
            foreach (EdgeContract? Edge in Contract.Edges.Where(Edge => Edge.SourceId == Id && Members.Contains(Edge.TargetId)
                && Classes[Edge.TargetId].Type == EnterpriseElementType.Deliverable))
            {
                Deliveries[Edge.TargetId] = Id;
            }
        }
        // A triggering timeline is a graph row, never a text-wrap run. Keep all
        // plateau tops level, then one delivery row, then the complete work row.
        foreach (string Id in Plateaus.Concat(Members.Where(Id => Classes[Id].Type == EnterpriseElementType.Gap)))
        {
            FlowchartCellRegion Region = Targets[Id]; Targets[Id] = new(Top, Region.StartColumn, Region.RowSpan, Region.ColumnSpan);
        }
        PlateauBottom = Plateaus.Concat(Members.Where(Id => Classes[Id].Type == EnterpriseElementType.Gap)).Max(Id => Targets[Id].EndRow);
        int DeliveryHeight = Deliveries.Keys.Select(Id => Targets[Id].RowSpan).DefaultIfEmpty(0).Max();
        int WorkRow = PlateauBottom + DeliveryHeight, Column = 0;
        Dictionary<int, string[]> Insertions = Hubs.GroupBy(Id =>
        {
            var Children = Ordered.Where(Child => Contract.Edges.Any(Edge => Edge.SourceId == Id && Edge.TargetId == Child)).ToArray();
            return Children.Length == 0 ? Ordered.Count / 2 : Ordered.IndexOf(Children[Children.Length / 2]);
        }).ToDictionary(Group => Group.Key, Group => Group.ToArray());
        for (int Index = 0; Index < Ordered.Count; Index++)
        {
            if (Insertions.TryGetValue(Index, out var HubIds))
            {
                foreach (string Id in HubIds)
            {
                FlowchartCellRegion Region = Targets[Id]; Targets[Id] = new(WorkRow, Column, Region.RowSpan, Region.ColumnSpan); Column += Region.ColumnSpan;
            }
            }

            string WorkId = Ordered[Index]; FlowchartCellRegion WorkRegion = Targets[WorkId];
            var DeliveryIds = Deliveries.Where(Pair => Pair.Value == WorkId).Select(Pair => Pair.Key).ToArray();
            int Width = Math.Max(WorkRegion.ColumnSpan, DeliveryIds.Sum(Id => Targets[Id].ColumnSpan));
            Targets[WorkId] = new(WorkRow, Column, WorkRegion.RowSpan, WorkRegion.ColumnSpan);
            int DeliveryColumn = Column;
            foreach (string Id in DeliveryIds)
            {
                FlowchartCellRegion Region = Targets[Id]; Targets[Id] = new(PlateauBottom, DeliveryColumn, Region.RowSpan, Region.ColumnSpan);
                DeliveryColumn += Region.ColumnSpan;
            }
            Column += Width;
        }
        HashSet<string> Placed = Plateaus.Concat(Members.Where(Id => Classes[Id].Type == EnterpriseElementType.Gap)).Concat(Ordered).Concat(Deliveries.Keys).Concat(Hubs).ToHashSet(StringComparer.Ordinal);
        int RemainingRow = Members.Where(Placed.Contains).Max(Id => Targets[Id].EndRow);
        foreach (string Id in Members.Where(Id => !Placed.Contains(Id)))
        {
            FlowchartCellRegion Region = Targets[Id]; Targets[Id] = new(RemainingRow, 0, Region.RowSpan, Region.ColumnSpan); RemainingRow += Region.RowSpan;
        }
        RefineImplementationRows(Contract, Classes, Targets, Members, Deliveries);
    }
}
