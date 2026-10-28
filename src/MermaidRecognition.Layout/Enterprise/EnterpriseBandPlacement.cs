using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Places semantic layer bands in authoritative cells while keeping each containment tree in a solid column block.</summary>
internal static partial class EnterpriseBandPlacement
{
    /// <summary>Observes immutable local-rank and planned-row evidence without participating in placement decisions.</summary>
    internal static Action<ElementPlacement>? PlacementObserver;

    /// <summary>One element's ranking evidence and whole-cell projection during a private band candidate.</summary>
    /// <param name="Stage">Planning boundary that produced this position.</param>
    /// <param name="Wrap">Candidate width in whole columns.</param>
    /// <param name="Id">Declared element identity.</param>
    /// <param name="Band">Semantic band index.</param>
    /// <param name="Rank">Original band-local graph rank, or null for a free element.</param>
    /// <param name="Order">Order within the original local graph rank, or null for a free element.</param>
    /// <param name="Row">Zero-based planned row within this band.</param>
    /// <param name="Region">Complete target cell reservation.</param>
    /// <param name="X">Projected page-left coordinate.</param>
    /// <param name="Y">Projected page-top coordinate.</param>
    internal sealed record ElementPlacement(string Stage, int Wrap, string Id, int Band, int? Rank, int? Order,
        int Row, FlowchartCellRegion Region, double X, double Y);

    /// <summary>Classification of one visible node; unlayered notes, locations and junctions follow their strongest neighbours.</summary>
    /// <param name="Band">Page-oriented layer index; Motivation is the first band.</param>
    /// <param name="Aspect">Local passive, behaviour, active sequence.</param>
    /// <param name="Conventional">Whether membership is a convention rather than an inferred location for an unlayered node.</param>
    /// <param name="Type">Element type, when the visible node is a typed element.</param>
    private sealed record Classification(int Band, int Aspect, bool Conventional, EnterpriseElementType? Type);

    /// <summary>One containment tree or the band's ungrouped elements, with relative whole-cell positions shared across bands.</summary>
    /// <param name="Id">Stable block identity.</param>
    /// <param name="Width">Reserved column span, including measured frame clearance.</param>
    /// <param name="Regions">Relative node regions keyed by identity.</param>
    private sealed record Block(string Id, int Width, Dictionary<string, FlowchartCellRegion> Regions);

    /// <summary>Assigns multi-layer Enterprise nodes before hints, leaving all other diagrams on their existing path.</summary>
    /// <param name="Contract">Mapped Enterprise contract carrying element-type metadata and declared containment.</param>
    /// <param name="Nodes">Complete cell-owned placement in page-down coordinates.</param>
    /// <param name="Context">Optional owning wrap-selection and diagnostic context.</param>
    /// <returns>Whether this diagram uses the new band convention.</returns>
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartRenderContext? Context = null)
        => Apply(Contract, Nodes, Context, out _);

    /// <summary>Assigns semantic bands and returns the completion that aligns partners after placement hints and measured span growth.</summary>
    /// <param name="Contract">Original declarations, memberships and honoured hints.</param>
    /// <param name="Nodes">Authoritative whole-cell nodes.</param>
    /// <param name="Context">Owning routed-wrap candidate.</param>
    /// <param name="CompletePartners">Candidate-local atomic partner completion, or null for diagrams without semantic bands.</param>
    /// <returns>Whether semantic bands own this placement.</returns>
    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartRenderContext? Context,
        out Action? CompletePartners)
    {
        CompletePartners = null;
        if (Nodes.Count == 0 || Nodes.Any(Node => Node.CellNode == null))
        {
            return false;
        }

        Dictionary<string, Classification> Classes = Contract.Nodes.Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
            .ToDictionary(Node => Node.Id, Node => Classify(Enum.Parse<EnterpriseElementType>(((RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)), StringComparer.Ordinal);
        int BandCount = Classes.Values.Where(Item => Item.Conventional).Select(Item => Item.Band).Distinct().Count();
        bool WidthConstrained = Context?.LayoutOptions.EnableWidthConstrainedLayout == true;
        if (BandCount == 0 || (BandCount <= 1 && !WidthConstrained))
        {
            return false;
        }

        foreach (DiagramGridLayout.GridNode? Node in Nodes.Where(Node => !Classes.TryGetValue(Node.Id, out Classification? Item) || !Item.Conventional))
        {
            IGrouping<int, string>? Neighbours = Contract.Edges.Where(Edge => Edge.SourceId == Node.Id || Edge.TargetId == Node.Id)
                .Select(Edge => Edge.SourceId == Node.Id ? Edge.TargetId : Edge.SourceId)
                .Where(Id => Classes.TryGetValue(Id, out Classification? Item) && Item.Conventional)
                .GroupBy(Id => Classes[Id].Band).OrderByDescending(Group => Group.Count()).ThenBy(Group => Group.Key).FirstOrDefault();
            Classification? Previous = Classes.GetValueOrDefault(Node.Id);
            Classes[Node.Id] = new(Neighbours?.Key ?? Classes.Values.Where(Item => Item.Conventional).Min(Item => Item.Band), Previous?.Aspect ?? 1, false, Previous?.Type);
        }
        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
        BandPlan Planner = new BandPlan(Contract, Nodes, Classes, Context?.UsePreviousEnterpriseBandPaddingForTests ?? false, WidthConstrained);
        Dictionary<string, FlowchartCellRegion> Targets = Planner.Build(Context?.EnterpriseBandWrapWidth ?? int.MaxValue);
        var Pins = Contract.Nodes.Where(Node => Node.PlacementHint is { IsActive: true, Kind: PlacementHintKind.At })
            .ToDictionary(Node => Node.Id, Node => new FlowchartCellRegion(Node.PlacementHint!.CellRow, Node.PlacementHint.CellColumn,
                Before.Regions[Node.Id].RowSpan, Before.Regions[Node.Id].ColumnSpan), StringComparer.Ordinal);
        if (Pins.Count > 0)
        {
            var Reserved = new Dictionary<string, FlowchartCellRegion>(Pins, StringComparer.Ordinal);
            foreach (var Pair in Targets.Where(Pair => !Pins.ContainsKey(Pair.Key)).OrderBy(Pair => Pair.Key, StringComparer.Ordinal))
            {
                var Region = Pair.Value;
                while (Reserved.Values.Any(Other => Other.Overlaps(Region)))
                    Region = new(Region.StartRow, checked(Region.StartColumn + Region.ColumnSpan), Region.RowSpan, Region.ColumnSpan);
                Reserved.Add(Pair.Key, Region);
            }
            Targets = Reserved;
        }
        FlowchartLogicalGrid.Snapshot? FixedPlacement = Context?.InitialBandPlacement;
        List<FlowchartCellGapGrowth> SelectedGaps = [];
        if (FixedPlacement != null)
        {
            Targets = FixedPlacement.Regions.ToDictionary(Pair => Pair.Key, Pair => Pair.Value, StringComparer.Ordinal);
            SelectedGaps.AddRange(FixedPlacement.ColumnGaps.Select(Pair => new FlowchartCellGapGrowth(true, Pair.Key,
                Math.Max(0, Pair.Value - Grid.Configuration.ColumnGap), DiagramGridMetrics.GRID_UNIT_PX)));
            SelectedGaps.AddRange(FixedPlacement.RowGaps.Select(Pair => new FlowchartCellGapGrowth(false, Pair.Key,
                Math.Max(0, Pair.Value - Grid.Configuration.RowGap), DiagramGridMetrics.GRID_UNIT_PX)));
        }
        var LevelRows = CaptureLevelRows(Contract, Nodes, Targets, Classes, Planner.LevelRowMembers);
        if (!Grid.TryPrepare(Targets, SelectedGaps, out FlowchartLogicalGrid.Snapshot? InitialState))
        {
            throw new InvalidOperationException("The selected initial band placement cannot preserve its cell ownership and gaps.");
        }

        IReadOnlyList<FlowchartCellGapGrowth> InitialGaps = PlanInitialRoutingCapacity(Contract, Targets, Grid, Context, InitialState);
        if (Context?.DeferInitialBandCapacity == true)
        {
            Context.InitialBandCapacityNeeded |= InitialGaps.Count > 0;
            InitialGaps = [];
        }
        if (!MatchesConvention(Contract, Classes, Targets) || !Grid.TryPrepare(Targets, SelectedGaps.Concat(InitialGaps).ToArray(), out FlowchartLogicalGrid.Snapshot? After) || !Grid.TryCommit(Before, After!))
        {
            throw new InvalidOperationException("The complete Enterprise band placement cannot reserve legal disjoint whole cells.");
        }

        Context?.HasInitialCorridorCapacity |= InitialGaps.Count > 0;
        Grid.RegionConvention = Regions => MatchesConvention(Contract, Classes, Regions)
            && (FixedPlacement == null || FixedPlacement.Regions.All(Pair => Regions[Pair.Key] == Pair.Value))
            && LevelRows.All(Row => Row.All(Id => Regions[Id].StartRow == Regions[Row[0]].StartRow));
        Grid.RegionConventionSnapshot = () =>
        {
            DiagramContract Frozen = DiagramGridLayout.CopyRoutingInputs(Contract);
            return Regions => MatchesConvention(Frozen, Classes, Regions)
                && (FixedPlacement == null || FixedPlacement.Regions.All(Pair => Regions[Pair.Key] == Pair.Value))
                && LevelRows.All(Row => Row.All(Id => Regions[Id].StartRow == Regions[Row[0]].StartRow));
        };
        CompletePartners = () =>
        {
            Planner.PublishCompletedPartnersIncrementally(Grid, Context?.BandSpaceDiagnostics);
            Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool> PriorConvention = Grid.RegionConvention;
            Func<IReadOnlyDictionary<string, FlowchartCellRegion>, bool> Alignment = Planner.CaptureSingletonAlignment(Grid.Capture().Regions);
            Grid.RegionConvention = Regions => PriorConvention?.Invoke(Regions) != false && Alignment(Regions);
            var PriorSnapshot = Grid.RegionConventionSnapshot;
            Grid.RegionConventionSnapshot = () =>
            {
                var Prior = PriorSnapshot?.Invoke();
                return Regions => Prior?.Invoke(Regions) != false && Alignment(Regions);
            };
        };
        if (Context != null)
        {
            Context.BandSpaceDiagnostics.AddRange(Planner.ConnectionClearanceDiagnostics);
            Context.EnterpriseBandWrapWidths = new[] { int.MaxValue, Planner.PreferredWidth }.Concat(Enumerable.Range(1, FlowchartLayoutRules.RepairCandidateMoves)
                .Select(Rows => (int)Math.Ceiling((double)Planner.MaximumWidth / Rows))).Distinct().Take(FlowchartLayoutRules.RepairCandidateMoves).ToArray();
            if (WidthConstrained)
            {
                double Available = Context.LayoutOptions.TargetWidthPx - (2 * MermaidRecognition.Render.FlowchartRenderOptions.FromContract(Contract).DiagramPaddingPx);
                int Columns = (int)Math.Clamp(Math.Floor((Available + Grid.Configuration.ColumnGap)
                    / (Grid.Configuration.CellWidth + Grid.Configuration.ColumnGap)), 1, Math.Max(1, Planner.MaximumWidth));
                Context.EnterpriseBandWrapWidths = new[] { int.MaxValue, Columns, Math.Max(1, Columns - 1) }
                    .Concat(Context.EnterpriseBandWrapWidths).Distinct().Take(FlowchartLayoutRules.RepairCandidateMoves).ToArray();
            }
            Context.BandSpaceDiagnostics.Add("BAND_GRAPH|blocks=" + Planner.BlockCount + "|relationships=" + Contract.Edges.Count
                + "|crossings=" + Planner.CrossingsBefore + "->" + Planner.CrossingsAfter);
        }
        return true;
    }

    /// <summary>Captures explicitly row-laid-out cohorts that repairs must move together, excluding complete active hint-dependent units and their reference peers.</summary>
    /// <param name="Contract">Original hint and containment declarations.</param>
    /// <param name="Nodes">Complete cell-owned band placement.</param>
    /// <param name="Targets">Private selected row plan.</param>
    /// <param name="Classes">Fixed band membership.</param>
    /// <param name="RowMembers">Only elements placed by row packing or timeline construction; ordinary graph ranks remain eligible for scored repairs.</param>
    /// <returns>Unhinted members sharing one planned row inside one grouping context.</returns>
    private static string[][] CaptureLevelRows(DiagramContract Contract, IReadOnlyList<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyDictionary<string, FlowchartCellRegion> Targets, IReadOnlyDictionary<string, Classification> Classes, IReadOnlySet<string> RowMembers)
    {
        HashSet<string> Hinted = HintControlledIds(Contract, Nodes);
        return [.. Nodes.Where(Node => RowMembers.Contains(Node.Id) && !Hinted.Contains(Node.Id))
            .GroupBy(Node => (Classes[Node.Id].Band, Node.GroupId, Targets[Node.Id].StartRow))
            .Where(Row => Row.Count() > 1).Select(Row => Row.Select(Node => Node.Id).ToArray())];
    }

    /// <summary>Finds complete hint-controlled units, including reference peers, so automatic partner alignment cannot change an honoured hint.</summary>
    /// <param name="Contract">Unmodified placement hints.</param>
    /// <param name="Nodes">Whole-cell layout members.</param>
    /// <returns>Every node whose position is controlled directly or transitively by an active hint.</returns>
    private static HashSet<string> HintControlledIds(DiagramContract Contract, IReadOnlyList<DiagramGridLayout.GridNode> Nodes)
    {
        HashSet<string> Hinted = new HashSet<string>(StringComparer.Ordinal);
        List<DiagramGridLayout.GridNode> Placed = [.. Nodes];
        foreach ((string Id, PlacementHintContract? Hint) Item in Contract.Nodes.Select(Node => (Node.Id, Hint: Node.PlacementHint))
            .Concat(Contract.Groups.Select(Group => (Group.Id, Hint: Group.PlacementHint)))
            .Where(Item => Item.Hint?.IsActive == true))
        {
            foreach (string Id in new[] { Item.Id, Item.Hint!.PeerId }.OfType<string>())
            {
                Hinted.UnionWith(DiagramGridLayout.CellHintDependentUnit(Contract, Placed, Id).Select(Node => Node.Id));
            }
        }
        return Hinted;
    }
    /// <summary>Maps a declared type to the page band and the local aspect sequence defined by rule 2.23.</summary>
    /// <param name="Type">Enterprise element type from recognition metadata.</param>
    /// <returns>Semantic layer, aspect and convention status.</returns>
    private static Classification Classify(EnterpriseElementType Type)
    {
        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[Type];
        int Band = Metadata.Layer switch { EnterpriseLayer.Motivation => 0, EnterpriseLayer.Strategy => 1, EnterpriseLayer.Business => 2,
            EnterpriseLayer.Application => 3, EnterpriseLayer.Technology => 4, EnterpriseLayer.Implementation => 5,
            _ => -1 };
        return new(Band, Metadata.Aspect switch { EnterpriseAspect.Passive => 0, EnterpriseAspect.Behaviour => 1, _ => 2 }, Band >= 0, Type);
    }

    /// <summary>Checks complete candidate regions so a later repair cannot undo the layer conventions.</summary>
    /// <param name="Contract">Declared triggering edges used for plateau ordering.</param>
    /// <param name="Classes">Immutable semantic membership, including inferred unlayered neighbours.</param>
    /// <param name="Regions">Complete candidate occupancy.</param>
    /// <returns>Whether layer order, triggering order still hold.</returns>
    private static bool MatchesConvention(DiagramContract Contract, IReadOnlyDictionary<string, Classification> Classes,
        IReadOnlyDictionary<string, FlowchartCellRegion> Regions)
    {
        int? End = null;
        foreach (IGrouping<int, KeyValuePair<string, Classification>>? Band in Classes.Where(Pair => Pair.Value.Conventional).GroupBy(Pair => Pair.Value.Band).OrderBy(Group => Group.Key))
        {
            if (End > Band.Min(Pair => Regions[Pair.Key].StartRow))
            {
                return false;
            }

            End = Band.Max(Pair => Regions[Pair.Key].EndRow);
        }
        // A later route repair must retain the local aspect columns reserved
        // inside each frame, even when its members occupy different ranks.
        foreach (IGrouping<(string? ParentGroupId, int Band), NodeContract> Part in Contract.Nodes.Where(Node => Node.ParentGroupId != null && Classes.ContainsKey(Node.Id))
            .GroupBy(Node => (Node.ParentGroupId, Classes[Node.Id].Band)))
        {
            int? Right = null;
            foreach (IGrouping<int, NodeContract>? Aspect in Part.GroupBy(Node => Classes[Node.Id].Aspect).OrderBy(Group => Group.Key))
            {
                if (Right > Aspect.Min(Node => Regions[Node.Id].StartColumn))
                {
                    return false;
                }

                Right = Aspect.Max(Node => Regions[Node.Id].EndColumn);
            }
        }
        return Contract.Edges.Where(IsTriggering).Where(Edge => Classes.GetValueOrDefault(Edge.SourceId)?.Type == EnterpriseElementType.Plateau
            && Classes.GetValueOrDefault(Edge.TargetId)?.Type == EnterpriseElementType.Plateau)
            .All(Edge => Regions[Edge.SourceId].EndColumn <= Regions[Edge.TargetId].StartColumn);
    }

    /// <summary>Reserves a left-to-right plateau timeline and places each related gap between its plateau endpoints.</summary>
    /// <param name="Contract">Declared relationships, with recognition creation order retained for ties.</param>
    /// <param name="Classes">Element classifications used to identify plateaus and gaps.</param>
    /// <param name="Targets">Private complete placement plan, before it is published.</param>
    private static void OrderPlateaus(DiagramContract Contract, IReadOnlyDictionary<string, Classification> Classes,
        Dictionary<string, FlowchartCellRegion> Targets)
    {
        List<string> Plateaus = [.. Classes.Where(Pair => Pair.Value.Type == EnterpriseElementType.Plateau).Select(Pair => Pair.Key)];
        if (Plateaus.Count == 0)
        {
            return;
        }

        List<string> Ordered = [];
        while (Plateaus.Count > 0)
        {
            string? Next = Plateaus.FirstOrDefault(Id => !Contract.Edges.Any(Edge => IsTriggering(Edge) && Edge.TargetId == Id && Plateaus.Contains(Edge.SourceId)));
            if (Next == null)
            {
                throw new InvalidOperationException("The declared plateau triggering order is cyclic.");
            }

            Ordered.Add(Next); Plateaus.Remove(Next);
        }
        int Row = Targets.Values.Max(Region => Region.EndRow), Column = Targets.Values.Min(Region => Region.StartColumn);
        foreach (string Id in Ordered)
        {
            FlowchartCellRegion Region = Targets[Id]; Targets[Id] = new(Row, Column, Region.RowSpan, Region.ColumnSpan); Column += Region.ColumnSpan;
            IEnumerable<string> Gaps = Classes.Where(Pair => Pair.Value.Type == EnterpriseElementType.Gap && Contract.Edges.Any(Edge =>
                ((Edge.SourceId == Pair.Key && Edge.TargetId == Id) || (Edge.TargetId == Pair.Key && Edge.SourceId == Id))))
                .Select(Pair => Pair.Key).Where(Gap => !Ordered.Take(Ordered.IndexOf(Id)).Any(Prior => Contract.Edges.Any(Edge =>
                    (Edge.SourceId == Gap && Edge.TargetId == Prior) || (Edge.TargetId == Gap && Edge.SourceId == Prior))));
            foreach (string Gap in Gaps) { FlowchartCellRegion GapRegion = Targets[Gap]; Targets[Gap] = new(Row, Column, GapRegion.RowSpan, GapRegion.ColumnSpan); Column += GapRegion.ColumnSpan; }
        }
    }

    /// <summary>Identifies declared triggering edges without inferring ordering from association or aggregation links.</summary>
    /// <param name="Edge">Mapped Enterprise relationship.</param>
    /// <returns>Whether this edge supplies a timeline constraint.</returns>
    private static bool IsTriggering(EdgeContract Edge) => Edge.Metadata.TryGetValue("enterpriseRelationshipKind", out MermaidMetadataValue? Value)
        && Value is RawMetadata { RawValue: nameof(EnterpriseRelationshipKind.Triggering) };
}
