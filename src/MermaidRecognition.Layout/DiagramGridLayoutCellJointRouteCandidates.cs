using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Offers complete fan and chain assignments without fixing centres or mutating live regions.</summary>
public static partial class DiagramGridLayout
{
    /// <summary>One complete validated joint assignment waiting for final-route scoring.</summary>
    /// <param name="Key">Family and deterministic target identity.</param>
    /// <param name="Subject">Original root or chain endpoint.</param>
    /// <param name="Assignment">Immutable whole-cell targets and unchanged shared gaps.</param>
    internal sealed record CellJointRouteCandidate(string Key, string Subject, FlowchartCellRepair.Assignment Assignment);

    /// <summary>Builds complete side, fan, chain and hint-directed cell assignments before any target is applied.</summary>
    /// <param name="Contract">Original visible topology, groups and hints.</param>
    /// <param name="Placed">All authoritative nodes sharing the same cell grid.</param>
    /// <param name="Horizontal">Whether rows are the cross-flow axis.</param>
    /// <param name="Nominated">Endpoints of bent or missing lines.</param>
    /// <returns>Round-robin unpublished joint alternatives under the existing candidate bound.</returns>
    internal static IEnumerable<CellJointRouteCandidate> CellJointRouteAssignments(DiagramContract Contract,
        List<GridNode> Placed, bool Horizontal, IReadOnlySet<string> Nominated)
    {
        FlowchartLogicalGrid.Snapshot Before = FlowchartCellRepair.Capture(Placed);
        IReadOnlySet<EdgeContract> Back = FlowchartBackEdgeHelper.FindBackEdges(Contract);
        EdgeContract[] Edges = [.. Contract.Edges.Where(Edge => !Back.Contains(Edge)
            && !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))];
        // Recognition retains a node-shaped alias when a link names a group.
        // Dagre deliberately gives that identity only a cluster, not a placed
        // leaf. Build leaf units from actual cell owners, then add each group
        // envelope once. Keep ToDictionary strict for real ownership defects.
        Dictionary<string, GridNode[]> Units = Placed.Select(Node => Node.Id).Concat(Contract.Groups.Select(Group => Group.Id))
            .ToDictionary(Id => Id, Id => Placed.Where(Node => Node.Id == Id || IsInsideGroup(Contract, Node.Id, Id)).ToArray(), StringComparer.Ordinal);
        Dictionary<string, string[]> Incoming = Edges.GroupBy(Edge => Edge.TargetId).ToDictionary(Group => Group.Key,
            Group => Group.Select(Edge => Edge.SourceId).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        Dictionary<string, string[]> Outgoing = Edges.GroupBy(Edge => Edge.SourceId).ToDictionary(Group => Group.Key,
            Group => Group.Select(Edge => Edge.TargetId).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        List<List<CellJointRouteCandidate>> Families = [];
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, FlowchartCellRegion> UnitRegions = Units.Where(Pair => Pair.Value.Length != 0).ToDictionary(Pair => Pair.Key,
            Pair => CellRouteEnvelope(Pair.Value.Select(Node => Before.Regions[Node.Id]).ToArray()), StringComparer.Ordinal);
        foreach (string Root in Units.Keys.Where(Id => Units[Id].Length != 0 && Nominated.Contains(Id))
            .OrderBy(Id => CellSourceOrder(Contract, UnitRegions, Id, Horizontal)))
        {
            var Arms = Outgoing.GetValueOrDefault(Root, []);
            AddSidePlacements(Root);
            List<CellJointRouteCandidate>? FanTranslations = null;
            if (Arms.Length > 1)
            {
                HashSet<string> Fan = new HashSet<string>(StringComparer.Ordinal) { Root };
                foreach (string Arm in Arms)
                {
                    Fan.UnionWith(JointLinearPath(Arm, Incoming, Outgoing, Units));
                }

                GridNode[] Members = [.. Fan.Where(Units.ContainsKey).SelectMany(Id => Units[Id]).Distinct()];
                List<CellJointRouteCandidate> Options = [];
                FlowchartCellRegion RootRegion = CellRouteEnvelope(Units[Root].Select(Node => Before.Regions[Node.Id]).ToArray());
                FlowchartCellRegion FanRegion = CellRouteEnvelope(Members.Select(Node => Before.Regions[Node.Id]).ToArray());
                HashSet<string> MemberIds = Members.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                bool InteractsOutside = Edges.Any(Edge => Fan.Contains(Edge.SourceId) != Fan.Contains(Edge.TargetId))
                    || Placed.Any(Node => !MemberIds.Contains(Node.Id) && FanRegion.Overlaps(Before.Regions[Node.Id]));
                List<(int Rows, int Columns)> Deltas = [];
                foreach (EdgeContract? Edge in Edges.Where(Edge => Fan.Contains(Edge.SourceId) != Fan.Contains(Edge.TargetId)))
                {
                    string Peer = Fan.Contains(Edge.SourceId) ? Edge.TargetId : Edge.SourceId;
                    if (!Units.TryGetValue(Peer, out GridNode[]? PeerNodes) || PeerNodes.Length == 0)
                    {
                        continue;
                    }

                    FlowchartCellRegion PeerRegion = CellRouteEnvelope(PeerNodes.Select(Node => Before.Regions[Node.Id]).ToArray());
                    int Delta = Horizontal ? PeerRegion.StartRow - RootRegion.StartRow : PeerRegion.StartColumn - RootRegion.StartColumn;
                    Deltas.Add(Horizontal ? (Delta, 0) : (0, Delta));
                }
                for (int Distance = 1; Distance <= FlowchartLayoutRules.RepairCandidateMoves; Distance++)
                {
                    Deltas.AddRange([(-Distance, 0), (Distance, 0), (0, -Distance), (0, Distance)]);
                }
                // Translating an isolated, unobstructed whole component cannot
                // straighten any relative terminal. Keep budget for assignments
                // that can change its routes instead of compacting it by origin.
                foreach ((int Rows, int Columns) Delta in Deltas.Where(Delta => InteractsOutside && Delta != (0, 0)).Distinct())
                {
                    Dictionary<string, FlowchartCellRegion> Targets = Members.ToDictionary(Node => Node.Id, Node => FlowchartCellRepair.Translate(
                        Before.Regions[Node.Id], Delta.Rows, Delta.Columns), StringComparer.Ordinal);
                    CellJointRouteCandidate? Option = PrepareJointCellOption("fan", Root, Targets, Contract, Placed, Before, Seen);
                    if (Option != null)
                    {
                        Options.Add(Option);
                    }

                    if (Options.Count >= FlowchartLayoutRules.RepairCandidateMoves)
                    {
                        break;
                    }
                }
                if (Options.Count != 0)
                {
                    FanTranslations = Options;
                }
            }

            // A nominated middle node extends to its unique predecessor as well
            // as its output tail. Parallel links do not split a logical chain.
            HashSet<string> Prefix = new HashSet<string>(StringComparer.Ordinal) { Root };
            string First = Root;
            while (Incoming.TryGetValue(First, out var Parents) && Parents.Length == 1
                && Outgoing.TryGetValue(Parents[0], out var Children) && Children.Length == 1
                && Units.ContainsKey(Parents[0]) && Prefix.Add(Parents[0]))
            {
                First = Parents[0];
            }

            IReadOnlyList<string> Chain = JointLinearPath(First, Incoming, Outgoing, Units);
            AddHintedChain(Root, Chain);
            AddAlignments("chain", Root, Chain);
            if (Chain.Count > 1 && First != Root)
            {
                // The straight prefix was not nominated by its own line. Keep
                // it with the bent tail instead of moving that tail in isolation.
                GridNode[] Members = [.. Chain.SelectMany(Id => Units[Id]).Distinct()];
                HashSet<string> ChainIds = Chain.ToHashSet(StringComparer.Ordinal);
                List<(int Rows, int Columns)> Deltas = [];
                foreach (EdgeContract? Edge in Edges.Where(Edge => ChainIds.Contains(Edge.SourceId) != ChainIds.Contains(Edge.TargetId)))
                {
                    string Inside = ChainIds.Contains(Edge.SourceId) ? Edge.SourceId : Edge.TargetId;
                    string Outside = ChainIds.Contains(Edge.SourceId) ? Edge.TargetId : Edge.SourceId;
                    if (!UnitRegions.TryGetValue(Inside, out FlowchartCellRegion? From) || !UnitRegions.TryGetValue(Outside, out FlowchartCellRegion? To))
                    {
                        continue;
                    }

                    int Delta = Horizontal ? To.StartRow - From.StartRow : To.StartColumn - From.StartColumn;
                    Deltas.Add(Horizontal ? (Delta, 0) : (0, Delta));
                }
                if (Deltas.Count != 0)
                {
                    for (int Distance = 1; Distance <= FlowchartLayoutRules.RepairCandidateMoves; Distance++)
                    {
                        Deltas.AddRange([(-Distance, 0), (Distance, 0), (0, -Distance), (0, Distance)]);
                    }

                    List<CellJointRouteCandidate> Translations = [];
                    foreach ((int Rows, int Columns) Delta in Deltas.Where(Delta => Delta != (0, 0)).Distinct())
                    {
                        Dictionary<string, FlowchartCellRegion> Targets = Members.ToDictionary(Node => Node.Id,
                            Node => FlowchartCellRepair.Translate(Before.Regions[Node.Id], Delta.Rows, Delta.Columns), StringComparer.Ordinal);
                        CellJointRouteCandidate? Option = PrepareJointCellOption("chain-move", Root, Targets, Contract, Placed, Before, Seen);
                        if (Option != null)
                        {
                            Translations.Add(Option);
                        }

                        if (Translations.Count >= FlowchartLayoutRules.RepairCandidateMoves)
                        {
                            break;
                        }
                    }
                    if (Translations.Count != 0)
                    {
                        Families.Add(Translations);
                    }
                }
            }
            // A fork's root and one whole arm may move together without forcing
            // every sibling onto one column. The final score includes siblings.
            foreach (string Arm in Arms)
            {
                AddAlignments("fan-arm", Root, new[] { Root }.Concat(JointLinearPath(Arm, Incoming, Outgoing, Units)).ToArray());
            }

            if (FanTranslations != null)
            {
                Families.Add(FanTranslations);
            }
        }
        for (int Index = 0; Index < FlowchartLayoutRules.RepairCandidateMoves; Index++)
        {
            foreach (List<CellJointRouteCandidate> Family in Families)
            {
                if (Index < Family.Count)
                {
                    yield return Family[Index];
                }
            }
        }

        void AddHintedChain(string Root, IReadOnlyList<string> Path)
        {
            if (Path.Count < 2)
            {
                return;
            }

            Dictionary<string, (int Row, int Column)> Relative = new Dictionary<string, (int Row, int Column)>(StringComparer.Ordinal) { [Path[0]] = (0, 0) };
            for (int Index = 1; Index < Path.Count; Index++)
            {
                EdgeContract[] Links = [.. Edges.Where(Edge => Edge.SourceId == Path[Index - 1] && Edge.TargetId == Path[Index])];
                if (Links.Length != 1 || Links[0].RoutingHint is not { IsActive: true, Travel: null } Hint
                    || (!Hint.Leave.HasValue && !Hint.Enter.HasValue))
                {
                    return;
                }

                string Enter = Hint.Enter.HasValue ? HintSide(Hint.Enter.Value) : string.Empty;
                string Side = Hint.Leave.HasValue ? HintSide(Hint.Leave.Value)
                    : Enter switch { "left" => "right", "right" => "left", "top" => "bottom", _ => "top" };
                if (Enter.Length != 0 && Enter != (Side switch { "left" => "right", "right" => "left", "top" => "bottom", _ => "top" }))
                {
                    return;
                }

                (int Row, int Column) Previous = Relative[Path[Index - 1]]; FlowchartCellRegion Source = UnitRegions[Path[Index - 1]]; FlowchartCellRegion Target = UnitRegions[Path[Index]];
                Relative[Path[Index]] = Side switch
                {
                    "top" => (Previous.Row - Target.RowSpan, Previous.Column),
                    "bottom" => (Previous.Row + Source.RowSpan, Previous.Column),
                    "left" => (Previous.Row, Previous.Column - Target.ColumnSpan),
                    _ => (Previous.Row, Previous.Column + Source.ColumnSpan)
                };
            }
            List<CellJointRouteCandidate> Options = [];
            // Every anchor is an existing starting cell. This proposes the
            // complete chain in its declared terminal directions, never a
            // centre nudge or an edited routing/placement hint.
            foreach (string Anchor in Path)
            {
                int Rows = UnitRegions[Anchor].StartRow - Relative[Anchor].Row;
                int Columns = UnitRegions[Anchor].StartColumn - Relative[Anchor].Column;
                Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal); bool Consistent = true;
                foreach (string Id in Path)
                {
                    foreach (GridNode? Node in Units[Id])
                {
                        FlowchartCellRegion Target = FlowchartCellRepair.Translate(Before.Regions[Node.Id], Relative[Id].Row + Rows - UnitRegions[Id].StartRow,
                        Relative[Id].Column + Columns - UnitRegions[Id].StartColumn);
                    if (Targets.TryGetValue(Node.Id, out FlowchartCellRegion? Existing) && Existing != Target)
                        {
                            Consistent = false;
                        }

                        Targets[Node.Id] = Target;
                }
                }

                if (!Consistent)
                {
                    continue;
                }

                CellJointRouteCandidate? Option = PrepareJointCellOption("hint-chain", Root, Targets, Contract, Placed, Before, Seen);
                if (Option != null)
                {
                    Options.Add(Option);
                }

                if (Options.Count >= FlowchartLayoutRules.RepairCandidateMoves)
                {
                    break;
                }
            }
            if (Options.Count != 0)
            {
                Families.Add(Options);
            }
        }

        // Chosen sides are private search state. Construct every neighbour and
        // whole-arm target before preparing one immutable grid assignment.
        void AddSidePlacements(string Root)
        {
            var Parents = Incoming.GetValueOrDefault(Root, []);
            var Peers = Parents.Concat(Outgoing.GetValueOrDefault(Root, [])).Where(Id => Id != Root && UnitRegions.ContainsKey(Id))
                .Distinct(StringComparer.Ordinal).ToArray();
            string[] Sides = ["left", "right", "top", "bottom"];
            if (Peers.Length < 2 || Peers.Length > Sides.Length)
            {
                return;
            }

            if (Peers.Any(Peer => Units[Peer].Intersect(Units[Root]).Any()))
            {
                return;
            }

            List<CellJointRouteCandidate> Options = [];
            Dictionary<string, string[]> Preferred = new Dictionary<string, string[]>(StringComparer.Ordinal);
            bool Reverse = FlowchartDirectionNormalizer.Normalize(Contract.Direction) is FlowchartDirection.RightToLeft or FlowchartDirection.BottomToTop;
            foreach (string Peer in Peers)
            {
                bool IncomingPeer = Parents.Contains(Peer, StringComparer.Ordinal);
                string Primary = Horizontal ? IncomingPeer != Reverse ? "left" : "right" : IncomingPeer != Reverse ? "top" : "bottom";
                string Secondary = Horizontal ? IncomingPeer ? "top" : "bottom" : IncomingPeer ? "left" : "right";
                Preferred[Peer] = [.. new[] { Primary, Secondary }.Concat(Sides).Distinct(StringComparer.Ordinal)];
            }
            Dictionary<string, string> Chosen = new Dictionary<string, string>(StringComparer.Ordinal);
            Visit(0);
            if (Options.Count != 0)
            {
                Families.Add(Options);
            }

            void Visit(int Index)
            {
                if (Options.Count >= FlowchartLayoutRules.RepairCandidateMoves)
                {
                    return;
                }

                if (Index != Peers.Length)
                {
                    string Peer = Peers[Index];
                    foreach (string Side in Preferred[Peer].Where(Side => !Chosen.Values.Contains(Side, StringComparer.Ordinal)))
                    {
                        Chosen[Peer] = Side; Visit(Index + 1); Chosen.Remove(Peer);
                    }
                    return;
                }
                Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
                foreach (string Peer in Peers)
                {
                    FlowchartCellRegion Hub = UnitRegions[Root]; FlowchartCellRegion Region = UnitRegions[Peer];
                    int Row = Chosen[Peer] switch { "top" => Hub.StartRow - Region.RowSpan, "bottom" => Hub.EndRow, _ => Hub.StartRow };
                    int Column = Chosen[Peer] switch { "left" => Hub.StartColumn - Region.ColumnSpan, "right" => Hub.EndColumn, _ => Hub.StartColumn };
                    HashSet<string> Members = new HashSet<string>(StringComparer.Ordinal) { Peer };
                    if (Parents.Contains(Peer, StringComparer.Ordinal))
                    {
                        string Current = Peer;
                        while (Incoming.TryGetValue(Current, out var Upstream) && Upstream.Length == 1 && Upstream[0] != Root
                            && Outgoing.GetValueOrDefault(Upstream[0], []).Length == 1 && Members.Add(Upstream[0]))
                        {
                            Current = Upstream[0];
                        }
                    }
                    else
                    {
                        Members.UnionWith(JointLinearPath(Peer, Incoming, Outgoing, Units).Where(Id => Id != Root));
                    }

                    foreach (GridNode? Node in Members.Where(Units.ContainsKey).SelectMany(Id => Units[Id]).Distinct())
                    {
                        FlowchartCellRegion Target = FlowchartCellRepair.Translate(Before.Regions[Node.Id], Row - Region.StartRow, Column - Region.StartColumn);
                        if (Targets.TryGetValue(Node.Id, out FlowchartCellRegion? Existing) && Existing != Target)
                        {
                            return;
                        }

                        Targets[Node.Id] = Target;
                    }
                }
                CellJointRouteCandidate? Option = PrepareJointCellOption("fan-sides", Root, Targets, Contract, Placed, Before, Seen);
                if (Option != null)
                {
                    Options.Add(Option);
                }
            }
        }

        // Anchors are existing logical starts. Every member is projected from
        // the same original snapshot; no centre or live pixel is an input.
        void AddAlignments(string Kind, string Root, IReadOnlyList<string> Path)
        {
            if (Path.Count < 2)
            {
                return;
            }

            var PathUnits = Path.Where(Id => Units.TryGetValue(Id, out GridNode[]? Nodes) && Nodes.Length != 0).ToArray();
            Dictionary<string, FlowchartCellRegion> Regions = PathUnits.ToDictionary(Id => Id,
                Id => CellRouteEnvelope(Units[Id].Select(Node => Before.Regions[Node.Id]).ToArray()), StringComparer.Ordinal);
            List<CellJointRouteCandidate> Options = [];
            foreach (int Anchor in Regions.Values.Select(Region => Horizontal ? Region.StartRow : Region.StartColumn).Distinct())
            {
                Dictionary<string, FlowchartCellRegion> Targets = new Dictionary<string, FlowchartCellRegion>(StringComparer.Ordinal);
                bool Consistent = true;
                foreach (string Id in PathUnits)
                {
                    int Delta = Anchor - (Horizontal ? Regions[Id].StartRow : Regions[Id].StartColumn);
                    foreach (GridNode? Node in Units[Id])
                    {
                        FlowchartCellRegion Target = FlowchartCellRepair.Translate(Before.Regions[Node.Id], Horizontal ? Delta : 0, Horizontal ? 0 : Delta);
                        if (Targets.TryGetValue(Node.Id, out FlowchartCellRegion? Existing) && Existing != Target)
                        {
                            Consistent = false;
                        }

                        Targets[Node.Id] = Target;
                    }
                }
                if (!Consistent)
                {
                    continue;
                }

                CellJointRouteCandidate? Option = PrepareJointCellOption(Kind, Root, Targets, Contract, Placed, Before, Seen);
                if (Option != null)
                {
                    Options.Add(Option);
                }

                if (Options.Count >= FlowchartLayoutRules.RepairCandidateMoves)
                {
                    break;
                }
            }
            if (Options.Count != 0)
            {
                Families.Add(Options);
            }
        }
    }

    /// <summary>Traverses a sole-successor arm without shape exceptions, crossing a merge or looping forever.</summary>
    /// <param name="Start">First endpoint included in the returned path.</param>
    /// <param name="Incoming">Distinct incoming neighbours.</param>
    /// <param name="Outgoing">Distinct outgoing neighbours.</param>
    /// <param name="Units">Existing node/group membership.</param>
    /// <returns>Ordered complete logical units, stopping before a multi-input merge.</returns>
    private static IReadOnlyList<string> JointLinearPath(string Start, IReadOnlyDictionary<string, string[]> Incoming,
        IReadOnlyDictionary<string, string[]> Outgoing, IReadOnlyDictionary<string, GridNode[]> Units)
    {
        List<string> Path = []; HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal); string Current = Start;
        while (Units.TryGetValue(Current, out GridNode[]? Nodes) && Nodes.Length != 0 && Seen.Add(Current))
        {
            Path.Add(Current);
            if (!Outgoing.TryGetValue(Current, out var Next) || Next.Length != 1
                || Incoming.GetValueOrDefault(Next[0], []).Length != 1)
            {
                break;
            }

            Current = Next[0];
        }
        return Path;
    }

    /// <summary>Closes complete targets over hint dependents and validates shared occupancy before offering an atomic assignment.</summary>
    /// <param name="Kind">Joint candidate family.</param>
    /// <param name="Root">Original nominated endpoint.</param>
    /// <param name="Targets">Complete targets built without touching live regions.</param>
    /// <param name="Contract">Original active hint relationships.</param>
    /// <param name="Placed">Complete diagram membership.</param>
    /// <param name="Before">Exact original grid and gaps.</param>
    /// <param name="Seen">Target identities already offered by any joint family.</param>
    /// <returns>A legal immutable assignment or null; no live state is changed.</returns>
    private static CellJointRouteCandidate? PrepareJointCellOption(string Kind, string Root,
        Dictionary<string, FlowchartCellRegion> Targets, DiagramContract Contract, List<GridNode> Placed,
        FlowchartLogicalGrid.Snapshot Before, HashSet<string> Seen)
    {
        // These are the same immutable eligibility checks used by the route
        // search. Do not build/validate a whole fan that it must reject for a
        // locked frame or the existing coupled-line bound.
        if (!CanExamineJointTargets(Contract, Placed, Before, Targets))
        {
            return null;
        }

        foreach (KeyValuePair<string, FlowchartCellRegion> Pair in Targets.ToArray())
        {
            FlowchartCellRegion Original = Before.Regions[Pair.Key];
            int Rows = Pair.Value.StartRow - Original.StartRow, Columns = Pair.Value.StartColumn - Original.StartColumn;
            if (Rows == 0 && Columns == 0)
            {
                continue;
            }

            foreach (GridNode Dependent in CellHintDependentUnit(Contract, Placed, Pair.Key))
            {
                FlowchartCellRegion Target = FlowchartCellRepair.Translate(Before.Regions[Dependent.Id], Rows, Columns);
                if (Targets.TryGetValue(Dependent.Id, out FlowchartCellRegion? Existing) && Existing != Target)
                {
                    return null;
                }

                Targets[Dependent.Id] = Target;
            }
        }
        Dictionary<string, FlowchartCellRegion> Changes = Targets.Where(Pair => Pair.Value != Before.Regions[Pair.Key])
            .ToDictionary(Pair => Pair.Key, Pair => Pair.Value, StringComparer.Ordinal);
        if (Changes.Count == 0)
        {
            return null;
        }

        GridNode[] Nodes = [.. Placed.Where(Node => Changes.ContainsKey(Node.Id))];
        // Single-node proposals already have their own complete candidate
        // family. Joint alternatives must actually change several regions.
        if (Nodes.Length < 2)
        {
            return null;
        }

        if (!CanExamineJointTargets(Contract, Placed, Before, Changes)
            || HasExternalCellHintDependent(Contract, Root, Nodes))
        {
            return null;
        }

        string Signature = string.Join(";", Changes.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
            .Select(Pair => Pair.Key + ":" + Pair.Value.StartRow + "," + Pair.Value.StartColumn));
        if (Seen.Contains(Signature))
        {
            return null;
        }

        FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Nodes, Changes);
        if (Plan == null)
        {
            return null;
        }

        Seen.Add(Signature);
        return new(Kind + "|" + Signature, Root, Plan);
    }

    /// <summary>Rejects targets that cannot reach scoring under the unchanged hint locks and affected-line limit.</summary>
    /// <param name="Contract">Original visible links and hinted group hierarchy.</param>
    /// <param name="Placed">All authoritative node identities and hint locks.</param>
    /// <param name="Before">Unchanged source ownership.</param>
    /// <param name="Targets">Complete targets before or after hint-dependent closure.</param>
    /// <returns>Whether the route search can examine this assignment; no state is modified.</returns>
    private static bool CanExamineJointTargets(DiagramContract Contract, List<GridNode> Placed,
        FlowchartLogicalGrid.Snapshot Before, IReadOnlyDictionary<string, FlowchartCellRegion> Targets)
    {
        HashSet<string> Ids = Targets.Where(Pair => Pair.Value != Before.Regions[Pair.Key]).Select(Pair => Pair.Key).ToHashSet(StringComparer.Ordinal);
        return Ids.Count != 0 && !Placed.Any(Node => Ids.Contains(Node.Id)
                && (Node.PlacementHintLocked || RouteRepairStage.HasHintedContainingGroup(Contract, Node.Id)))
            && Contract.Edges.Count(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                && (Ids.Contains(Edge.SourceId) || Ids.Contains(Edge.TargetId))) <= FlowchartLayoutRules.RepairChainLines;
    }
}
