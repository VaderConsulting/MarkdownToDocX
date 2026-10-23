using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout.Dagre;

namespace MermaidRecognition.Layout.Enterprise;

internal static partial class EnterpriseBandPlacement
{
    /// <summary>Builds complete cell plans from local layer graphs and a shared ordering of solid frames and individual ungrouped elements.</summary>
    private sealed partial class BandPlan
    {
        private readonly DiagramContract Contract;
        private readonly List<DiagramGridLayout.GridNode> Nodes;
        private readonly IReadOnlyDictionary<string, Classification> Classes;
        private readonly Dictionary<string, FlowchartCellRegion> Original;
        private readonly Dictionary<string, int> Rows = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> Keys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> HorizontalRanks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (int Rank, int Order)> LocalRanks = new(StringComparer.Ordinal);
        private readonly HashSet<string> Free = new(StringComparer.Ordinal);
        private readonly HashSet<string> RowMembers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> Owners = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> Offsets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> Widths = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string?> Parents;
        private readonly int Inset;
        private readonly bool PreviousBandPadding;

        /// <summary>Largest measured horizontal run; used only to enumerate whole-cell wrapping candidates.</summary>
        internal int MaximumWidth { get; private set; }
        /// <summary>Root frames plus every individual ungrouped element.</summary>
        internal int BlockCount => Widths.Count;
        /// <summary>Complete adjacent-row crossing estimate before the alternating sweeps.</summary>
        internal int CrossingsBefore { get; private set; }
        /// <summary>Complete adjacent-row crossing estimate after strict sweeps and adjacent exchanges.</summary>
        internal int CrossingsAfter { get; private set; }
        /// <summary>Elements explicitly laid out in level rows, excluding ordinary layered-graph ranks.</summary>
        internal IReadOnlySet<string> LevelRowMembers => RowMembers;

        /// <summary>Ranks each band independently, using cross-band connections only in the subsequent ordering graph.</summary>
        /// <param name="Contract">Complete typed contract.</param>
        /// <param name="Nodes">Cell-owned initial nodes.</param>
        /// <param name="Classes">Semantic membership, including inferred unlayered neighbours.</param>
        /// <param name="PreviousBandPadding">Diagnostic replay only; reproduces the old empty-cell separator with every other algorithm unchanged.</param>
        internal BandPlan(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, IReadOnlyDictionary<string, Classification> Classes, bool PreviousBandPadding)
        {
            this.Contract = Contract; this.Nodes = Nodes; this.Classes = Classes;
            this.PreviousBandPadding = PreviousBandPadding;
            Original = Nodes[0].CellNode!.Grid.Capture().Regions.ToDictionary(Pair => Pair.Key, Pair => Pair.Value, StringComparer.Ordinal);
            Parents = Contract.Groups.ToDictionary(Group => Group.Id, Group => Group.ParentGroupId, StringComparer.Ordinal);
            Inset = (int)Math.Ceiling(DiagramGridMetrics.NODE_GAP_PX / Nodes[0].CellNode!.Grid.Configuration.CellWidth);
            bool Horizontal = Contract.Direction is "LR" or "RL";
            foreach (var Band in Nodes.GroupBy(Node => Classes[Node.Id].Band))
            {
                var Ids = Band.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                var Linked = Contract.Edges.Where(Edge => Ids.Contains(Edge.SourceId) && Ids.Contains(Edge.TargetId))
                    .SelectMany(Edge => new[] { Edge.SourceId, Edge.TargetId }).ToHashSet(StringComparer.Ordinal);
                Free.UnionWith(Ids.Except(Linked));
                var Local = Linked.Count == 0 ? new Dictionary<string, (int Rank, int Order)>()
                    : RankBand(Contract, Band.Where(Node => Linked.Contains(Node.Id)).ToArray());
                var RankKeys = Local.Values.Select(Value => Value.Rank).Distinct().Order().ToArray();
                if (Contract.Direction is "BT" or "RL") Array.Reverse(RankKeys);
                foreach (var Node in Band)
                {
                    if (!Local.TryGetValue(Node.Id, out var Value))
                    { Rows[Node.Id] = 0; Keys[Node.Id] = 0; continue; }
                    int Rank = Array.IndexOf(RankKeys, Value.Rank);
                    LocalRanks[Node.Id] = Value;
                    Rows[Node.Id] = Horizontal ? Value.Order : Rank;
                    Keys[Node.Id] = Horizontal ? Rank * Nodes.Count + Value.Order : Value.Order;
                    if (Horizontal && Node.GroupId == null) HorizontalRanks[Node.Id] = Rank;
                }
            }
            foreach (var Node in Nodes)
            {
                string Owner = Node.Id;
                for (string? Parent = Node.GroupId; Parent != null; Parent = Parents[Parent]) Owner = Parent;
                Owners[Node.Id] = Owner;
            }
        }

        /// <summary>Creates one complete initial layout; no placement score selects a wrap here.</summary>
        /// <param name="Wrap">Maximum run span, or Int32.MaxValue for the unwrapped candidate.</param>
        /// <returns>Complete disjoint candidate regions, ready for one atomic publication.</returns>
        internal Dictionary<string, FlowchartCellRegion> Build(int Wrap)
        {
            foreach (var Group in Contract.Groups.Where(Group => Group.ParentGroupId == null)) Widths[Group.Id] = SizeFrame(Group.Id, 0);
            foreach (var Node in Nodes.Where(Node => Node.GroupId == null)) { Widths[Node.Id] = Original[Node.Id].ColumnSpan; Offsets[Node.Id] = 0; }
            MaximumWidth = Nodes.GroupBy(Node => (Classes[Node.Id].Band, Rows[Node.Id])).Max(Group => Group.Sum(Node => Original[Node.Id].ColumnSpan));
            var Targets = Original.ToDictionary(Pair => Pair.Key, Pair => new FlowchartCellRegion(Rows[Pair.Key], Offsets[Pair.Key], Pair.Value.RowSpan, Pair.Value.ColumnSpan), StringComparer.Ordinal);
            ObservePlacement("local-ranks", Wrap, Targets);
            StackBands(Targets);
            var Order = Widths.Keys.OrderBy(Id => InitialKey(Id)).ToArray();
            var Vertices = CreateVertices(Targets);
            var Links = Contract.Edges.Where(Edge => Vertices.Any(Vertex => Vertex.Id == Edge.SourceId) && Vertices.Any(Vertex => Vertex.Id == Edge.TargetId))
                .Select(Edge => (Edge.SourceId, Edge.TargetId)).ToArray();
            var Ordered = DagreLayoutEngine.OrderCompleteBandBlocks(Order, Vertices, Links, PreservesHorizontalRanks);
            CrossingsBefore = Ordered.Before; CrossingsAfter = Ordered.After;
            PackColumns(Targets, Ordered.Order);
            PlacePartnerElements(Targets, Ordered.Order, Wrap);
            OrderPlateaus(Contract, Classes, Targets);
            ArrangeImplementationTimelines(Contract, Classes, Targets, Wrap);
            var TimelineBands = Nodes.Where(Node => Node.GroupId == null
                && Classes[Node.Id].Type == MermaidRecognition.Kinds.Enterprise.EnterpriseElementType.Plateau)
                .Select(Node => Classes[Node.Id].Band).ToHashSet();
            RowMembers.UnionWith(Nodes.Where(Node => Node.GroupId == null && TimelineBands.Contains(Classes[Node.Id].Band)).Select(Node => Node.Id));
            RestackLevelBands(Targets);
            CompactFrameNeighbours(Targets);
            AvoidBlockedBandColumns(Targets);
            AlignEnterpriseChainColumns(Targets);
            AlignSameBandSpineColumns(Targets);
            AlignFlowStackColumns(Targets);
            AlignLonelyNeighbourColumns(Targets);
            AlignHubBranchRows(Targets);
            AlignAccessorObjectColumns(Targets);
            AlignCompletedPartners(Targets);
            AlignSameBandChordRows(Targets);
            ObservePlacement("final-band-plan", Wrap, Targets);
            return Targets;
        }

        /// <summary>Emits rank, order, row and pixel evidence only when a diagnostic subscriber requests it.</summary>
        /// <param name="Stage">Exact private planning boundary.</param>
        /// <param name="Wrap">Candidate's whole-column capacity.</param>
        /// <param name="Targets">Complete private occupancy, which is never changed by observation.</param>
        private void ObservePlacement(string Stage, int Wrap, IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            if (PlacementObserver == null) return;
            var Grid = Nodes[0].CellNode!.Grid;
            foreach (var Band in Nodes.GroupBy(Node => Classes[Node.Id].Band))
            {
                var Starts = Band.Select(Node => Targets[Node.Id].StartRow).Distinct().Order().ToArray();
                foreach (var Node in Band)
                {
                    var Region = Targets[Node.Id];
                    var Origin = Grid.Origin(Region);
                    bool Ranked = LocalRanks.TryGetValue(Node.Id, out var Local);
                    PlacementObserver(new(Stage, Wrap, Node.Id, Band.Key, Ranked ? Local.Rank : null,
                        Ranked ? Local.Order : null, Array.IndexOf(Starts, Region.StartRow), Region, Origin.X, Origin.Y));
                }
            }
        }

        /// <summary>Measures one root or nested frame's fixed local aspect columns across all of its band parts.</summary>
        /// <param name="Id">Declared grouping identity.</param>
        /// <param name="Start">Its relative starting column inside the root block.</param>
        /// <returns>Whole columns reserved by that solid block.</returns>
        private int SizeFrame(string Id, int Start)
        {
            int Column = Start + Inset;
            foreach (var Child in Contract.Groups.Where(Group => Group.ParentGroupId == Id)) Column += SizeFrame(Child.Id, Column);
            foreach (int Aspect in Nodes.Where(Node => Node.GroupId == Id).Select(Node => Classes[Node.Id].Aspect).Distinct().Order())
            {
                var Members = Nodes.Where(Node => Node.GroupId == Id && Classes[Node.Id].Aspect == Aspect).ToArray();
                int Width = 0;
                foreach (var Row in Members.GroupBy(Node => (Classes[Node.Id].Band, Rows[Node.Id])))
                {
                    int Offset = 0;
                    foreach (var Node in Row.OrderBy(Node => Keys[Node.Id])) { Offsets[Node.Id] = Column + Offset; Offset += Original[Node.Id].ColumnSpan; }
                    Width = Math.Max(Width, Offset);
                }
                Column += Width;
            }
            return Column - Start + Inset;
        }

        /// <summary>Stacks logical rank rows with the normal configured rank gap; no empty padding row is inserted between bands.</summary>
        /// <param name="Targets">Private relative regions.</param>
        private void StackBands(Dictionary<string, FlowchartCellRegion> Targets)
        {
            int Next = 0;
            foreach (var Band in Targets.Keys.GroupBy(Id => Classes[Id].Band).OrderBy(Group => Group.Key))
            {
            foreach (var Row in Band.GroupBy(Id => Targets[Id].StartRow).OrderBy(Group => Group.Key).ToArray())
            {
                int Height = Row.Max(Id => Original[Id].RowSpan);
                foreach (string Id in Row.ToArray()) Targets[Id] = new(Next, Targets[Id].StartColumn, Original[Id].RowSpan, Original[Id].ColumnSpan);
                Next += Height;
            }
                if (PreviousBandPadding) Next += (int)Math.Ceiling(DiagramGridMetrics.NODE_GAP_PX / Nodes[0].CellNode!.Grid.Configuration.CellHeight);
            }
        }

        /// <summary>Creates leaf and group-endpoint vertices so every declared relationship participates in the ordering graph.</summary>
        /// <param name="Targets">Current whole-cell rank coordinates.</param>
        /// <returns>Vertices with fixed within-frame columns and explicit owner blocks.</returns>
        private List<DagreLayoutEngine.BandVertex> CreateVertices(IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
        {
            var Result = Nodes.Select(Node => new DagreLayoutEngine.BandVertex(Node.Id, Owners[Node.Id], Targets[Node.Id].StartRow, Offsets[Node.Id])).ToList();
            foreach (var Group in Contract.Groups)
            {
                var Members = Nodes.Where(Node => DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Group.Id)).ToArray();
                if (Members.Length == 0) continue;
                Result.Add(new(Group.Id, Owners[Members[0].Id], (int)Math.Round(Members.Average(Node => (double)Targets[Node.Id].StartRow)),
                    Members.Average(Node => (double)Offsets[Node.Id])));
            }
            return Result;
        }

        /// <summary>Packs each row by a common block permutation, sharing frame columns across all spanned rows.</summary>
        /// <param name="Targets">Private node regions.</param>
        /// <param name="Order">Strictly improved shared block order.</param>
        private void PackColumns(Dictionary<string, FlowchartCellRegion> Targets, IReadOnlyList<string> Order)
        {
            var Extents = Owners.GroupBy(Pair => Pair.Value).ToDictionary(Group => Group.Key,
                Group => (First: Group.Min(Pair => Targets[Pair.Key].StartRow), Last: Group.Max(Pair => Targets[Pair.Key].EndRow)));
            foreach (string Id in Widths.Keys) Extents.TryAdd(Id, (0, 0));
            var Starts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string Id in Order)
            {
                var Span = Extents[Id];
                Starts[Id] = Starts.Where(Pair => Extents[Pair.Key].First < Span.Last && Extents[Pair.Key].Last > Span.First
                        || HorizontalRanks.TryGetValue(Id, out int Rank) && HorizontalRanks.TryGetValue(Pair.Key, out int PreviousRank)
                            && Classes[Id].Band == Classes[Pair.Key].Band && PreviousRank < Rank)
                    .Select(Pair => Pair.Value + Widths[Pair.Key]).DefaultIfEmpty(0).Max();
            }
            foreach (var Node in Nodes) Targets[Node.Id] = FlowchartCellRepair.Translate(Targets[Node.Id], 0, Starts[Owners[Node.Id]]);
        }

        /// <summary>Wraps only sparse ungrouped runs; dense bands retain their own layered ranks instead of text-like wrapping.</summary>
        /// <param name="Targets">Packed complete plan.</param>
        /// <param name="Order">Shared block order.</param>
        /// <param name="Wrap">Measured whole-cell run capacity.</param>
        private void WrapUngrouped(Dictionary<string, FlowchartCellRegion> Targets, IReadOnlyList<string> Order, int Wrap)
        {
            foreach (var Band in Nodes.GroupBy(Node => Classes[Node.Id].Band).OrderByDescending(Group => Group.Key))
            {
                if (Band.Any(Node => Node.GroupId != null) || Band.Select(Node => Rows[Node.Id]).Distinct().Count() > 1) continue;
                int Top = Band.Min(Node => Targets[Node.Id].StartRow), Row = Top, Column = 0, Height = 0;
                foreach (string Id in Order.Where(Id => Band.Any(Node => Node.Id == Id)))
                {
                    var Region = Targets[Id];
                    if (Column > 0 && Column + Region.ColumnSpan > Wrap) { Row += Height; Column = 0; Height = 0; }
                    Targets[Id] = new(Row, Column, Region.RowSpan, Region.ColumnSpan); Column += Region.ColumnSpan; Height = Math.Max(Height, Region.RowSpan);
                }
                int Added = Row + Height - Band.Max(Node => Original[Node.Id].RowSpan) - Top;
                foreach (string Id in Targets.Keys.Where(Id => Classes[Id].Band > Band.Key).ToArray()) Targets[Id] = FlowchartCellRepair.Translate(Targets[Id], Added, 0);
            }
        }

        /// <summary>Uses local ranking for initial order; cross-band relationships do not constrain these ranks.</summary>
        /// <param name="Block">Root frame or ungrouped element.</param>
        /// <returns>A deterministic local order key.</returns>
        private int InitialKey(string Block) => Nodes.Where(Node => Owners[Node.Id] == Block).Select(Node => Keys[Node.Id]).DefaultIfEmpty(0).Min();

        /// <summary>Preserves the main-axis ranks of a dense horizontal band while the common graph orders its connections.</summary>
        /// <param name="Order">Proposed root-block permutation.</param>
        /// <returns>Whether no later local rank would precede an earlier one.</returns>
        private bool PreservesHorizontalRanks(IReadOnlyList<string> Order)
        {
            var Previous = new Dictionary<int, int>();
            foreach (string Id in Order)
            {
                if (!HorizontalRanks.TryGetValue(Id, out int Rank)) continue;
                int Band = Classes[Id].Band;
                if (Previous.TryGetValue(Band, out int Last) && Last > Rank) return false;
                Previous[Band] = Rank;
            }
            return true;
        }
    }
}
