using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Group placement and frame construction helpers for the group-frame pipeline stage.</summary>
public static partial class DiagramGridLayout
{    private static void CenterLoneNestedGroups(DiagramContract Contract, List<GridNode> Placed, bool Horizontal)
    {
        foreach (GroupContract Parent in Contract.Groups)
        {
            if (Parent.ChildGroupIds.Count != 1)
            {
                continue;
            }

            string ChildGroupId = Parent.ChildGroupIds[0];
            List<GridNode> Members = new List<GridNode>();
            foreach (GridNode Node in Placed)
            {
                if (IsInsideGroup(Contract, Node.Id, ChildGroupId))
                {
                    Members.Add(Node);
                }
            }

            if (Members.Count == 0)
            {
                continue;
            }

            double ContentLeft = double.PositiveInfinity;
            double ContentRight = double.NegativeInfinity;
            foreach (GridNode Node in Members)
            {
                double Centre = Cross(Node, Horizontal, Old: false);
                double Half = CrossHalf(Node, Horizontal, Leading: true);
                ContentLeft = Math.Min(ContentLeft, Centre - Half);
                ContentRight = Math.Max(ContentRight, Centre + Half);
            }

            List<GridNode> ParentRank = new List<GridNode>();
            foreach (GridNode Node in Placed)
            {
                if (string.Equals(GroupOf(Contract, Node.Id), Parent.Id, StringComparison.Ordinal)
                    && !IsInsideGroup(Contract, Node.Id, ChildGroupId))
                {
                    ParentRank.Add(Node);
                }
            }

            double ParentLeft = double.PositiveInfinity;
            double ParentRight = double.NegativeInfinity;
            foreach (GridNode Node in ParentRank)
            {
                double Centre = Cross(Node, Horizontal, Old: false);
                double Half = CrossHalf(Node, Horizontal, Leading: true);
                ParentLeft = Math.Min(ParentLeft, Centre - Half);
                ParentRight = Math.Max(ParentRight, Centre + Half);
            }

            if (ParentRank.Count == 0)
            {
                continue;
            }

            double ContentCentre = (ContentLeft + ContentRight) / 2.0;
            double ParentCentre = (ParentLeft + ParentRight) / 2.0;
            int Shift = (int)Math.Round(ParentCentre - ContentCentre);
            if (Shift == 0)
            {
                continue;
            }

            foreach (GridNode Node in Members)
            {
                SetCross(Node, (int)Math.Round(Cross(Node, Horizontal, Old: false)) + Shift, Horizontal);
            }
        }
    }

    private static void CompactOverlappingRootGroups(DiagramContract Contract, List<GridNode> Placed,
        List<GridFrame> Frames, bool Horizontal, FlowchartRenderContext RenderContext)
    {
        Frames.Clear();
        BuildFrames(Contract, Placed, Frames);
        var Roots = Frames.Where(Frame => ParentGroup(Contract, Frame.Id) == null).ToList();
        bool Overlapping = Roots.Any(First => Roots.Any(Second => First != Second
            && First.Left < Second.Right && First.Right > Second.Left
            && First.Top < Second.Bottom && First.Bottom > Second.Top));
        if (!Overlapping) return;
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"compact root groups: {Roots.Count} overlapping root frames");

        // Global ranks can scatter a container's members between other containers.
        // Close those empty strips locally, then move each container as one unit.
        foreach (var Root in Roots)
        {
            var Members = Placed.Where(Node => IsInsideGroup(Contract, Node.Id, Root.Id)).ToList();
            DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"compact root {Root.Id}: begin reflow ({Members.Count} members)");
            var Local = new DiagramContract { Kind = Contract.Kind, Header = Contract.Header,
                Direction = Contract.Direction, LayoutMode = Contract.LayoutMode };
            var Ids = Members.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
            List<NodeContract> LocalNodes = Contract.Nodes.Where(Node => Ids.Contains(Node.Id)).ToList();
            Local.NodesMutable.AddRange(LocalNodes);
            List<GroupContract> LocalGroups = Contract.Groups.Where(Group => Group.Id == Root.Id
                || Members.Any(Node => IsInsideGroup(Contract, Node.Id, Group.Id))
                    && IsInsideGroup(Contract, Group.Id, Root.Id)).ToList();
            var Endpoints = Ids.Concat(LocalGroups.Select(Group => Group.Id)).ToHashSet(StringComparer.Ordinal);
            foreach (GroupContract Group in LocalGroups)
                Local.GroupsMutable.Add(CloneGroupForLocalLayout(Group, Endpoints));
            Local.EdgesMutable.AddRange(Contract.Edges.Where(Edge => Endpoints.Contains(Edge.SourceId)
                && Endpoints.Contains(Edge.TargetId)));
            PlacementHintContract?[] OriginalHints = LocalNodes.Select(Node => Node.PlacementHint).ToArray();
            for (int Index = 0; Index < LocalNodes.Count; Index++)
                LocalNodes[Index].PlacementHint = CloneHint(OriginalHints[Index]);
            try
            {
                Stopwatch ReflowTimer = Stopwatch.StartNew();
                if (FlowchartDagrePlacement.TryPlace(Local, out var LocalRaw))
                {
                    var LocalLayout = ApplyForGroupReflow(Local, LocalRaw, RenderContext);
                    foreach (var Bounds in LocalLayout.NodeBounds)
                    {
                        var Member = Members.Find(Node => Node.Id == Bounds.NodeId)!;
                        Member.X = Bounds.CenterX;
                        Member.Y = -Bounds.CenterY;
                    }
                    ReflowTimer.Stop();
                    DiagramGridLayout.GroupFrameFitPassObserver?.Invoke(
                        $"compact root {Root.Id}: reflow complete ({ReflowTimer.Elapsed.TotalMilliseconds:0.0} ms; nodes={Local.Nodes.Count}; edges={Local.Edges.Count})");
                }
                else
                {
                    ReflowTimer.Stop();
                    DiagramGridLayout.GroupFrameFitPassObserver?.Invoke(
                        $"compact root {Root.Id}: local placement failed ({ReflowTimer.Elapsed.TotalMilliseconds:0.0} ms)");
                }
            }
            finally
            {
                for (int Index = 0; Index < LocalNodes.Count; Index++)
                    LocalNodes[Index].PlacementHint = OriginalHints[Index];
            }
            FlowchartGridCompaction.Apply(Members, Horizontal, 2 * DiagramGridMetrics.GRID_UNIT_PX);
            FlowchartGridCompaction.Apply(Members, !Horizontal, 2 * DiagramGridMetrics.GRID_UNIT_PX);
            foreach (var Member in Members) { Member.OldX = Member.X; Member.OldY = Member.Y; }
            AssignRanks(Members, Horizontal);
            SeparateOverlaps(Members, Horizontal);
            DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"compact root {Root.Id}: local packing complete");
        }
        Frames.Clear();
        BuildFrames(Contract, Placed, Frames);
        Roots = Contract.Groups.Where(Group => Group.ParentGroupId == null)
            .Select(Group => Frames.Find(Frame => Frame.Id == Group.Id)).Where(Frame => Frame != null)
            .Cast<GridFrame>().ToList();
        foreach (var Node in Placed.Where(Node => Node.GroupId == null))
        {
            var Neighbour = Roots.Select(Frame => (Frame, Links: Contract.Edges.Count(Edge =>
                Edge.SourceId == Node.Id && IsInsideGroup(Contract, Edge.TargetId, Frame.Id)
                || Edge.TargetId == Node.Id && IsInsideGroup(Contract, Edge.SourceId, Frame.Id))))
                .OrderByDescending(Item => Item.Links).FirstOrDefault();
            var Unit = new GridFrame { Id = Node.Id,
                Left = (int)Math.Floor(Node.X - Node.Width / 2.0),
                Right = (int)Math.Ceiling(Node.X + Node.Width / 2.0),
                Top = (int)Math.Floor(Node.Y - Node.Height / 2.0),
                Bottom = (int)Math.Ceiling(Node.Y + Node.Height / 2.0) };
            if (Neighbour.Links > 0) Roots.Insert(Roots.IndexOf(Neighbour.Frame) + 1, Unit);
            else Roots.Add(Unit);
        }
        FlowchartContainerPacking.Apply(Contract, Roots.Select(Root => new FlowchartContainerPacking.Unit(Root,
            Placed.Where(Node => Node.Id == Root.Id || IsInsideGroup(Contract, Node.Id, Root.Id)).ToList())).ToList());
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke("compact root groups: container packing complete");
        foreach (var Node in Placed) { Node.OldX = Node.X; Node.OldY = Node.Y; }
        AssignRanks(Placed, Horizontal);
        Frames.Clear();
    }

    internal static void FitFrames(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames,
        bool Horizontal, string Trigger)
    {
        HashSet<string> SeenStates = new(StringComparer.Ordinal) { PositionSignature(Placed) };
        Dictionary<string, string> FittedMembers = new(StringComparer.Ordinal);
        Dictionary<string, string> PendingCauses = Contract.Groups.ToDictionary(Group => Group.Id,
            _ => Trigger, StringComparer.Ordinal);
        List<string> Adjustments = [];
        int Refit = 0;
        string StopReason = "stable frame fit";
        while (true)
        {
            Frames.Clear();
            BuildFrames(Contract, Placed, Frames); // BuildFrames is bottom-up.
            (double X, double Y)[] BeforeUnrelatedFrameSeparation = Placed.Select(Node => (Node.X, Node.Y)).ToArray();
            if (SeparateUnrelatedFrames(Contract, Placed, Frames))
            {
                EnqueueChangedMemberFrames(Contract, ChangedNodeIds(Placed, BeforeUnrelatedFrameSeparation), PendingCauses,
                    "rule 7.8 unrelated-frame separation");
                Adjustments.Add("separate unrelated group frames");
                if (!SeenStates.Add(PositionSignature(Placed)))
                {
                    StopReason = "repeated placement state after unrelated-frame separation";
                    break;
                }
                continue;
            }
            // Keep sibling containers intact before clearing individual outsiders.
            // Run the global sibling pass once per dirty batch, then refit only changed descendants.
            (double X, double Y)[] BeforeSiblingSeparation = Placed.Select(Node => (Node.X, Node.Y)).ToArray();
            if (SeparateSiblingFrames(Contract, Placed, Frames, Horizontal) && PositionsDiffer(Placed, BeforeSiblingSeparation))
            {
                EnqueueChangedMemberFrames(Contract, ChangedNodeIds(Placed, BeforeSiblingSeparation),
                    PendingCauses, "sibling-frame separation");
                Adjustments.Add("separate overlapping sibling frames");
                if (!SeenStates.Add(PositionSignature(Placed)))
                {
                    StopReason = "repeated placement state after sibling separation";
                    break;
                }
                continue;
            }

            Dictionary<string, string> CausesThisBatch = new(PendingCauses, StringComparer.Ordinal);
            HashSet<string> DirtyThisBatch = CausesThisBatch.Keys.ToHashSet(StringComparer.Ordinal);
            PendingCauses.Clear();
            HashSet<string> ProcessedThisBatch = new(StringComparer.Ordinal);
            foreach (string FrameId in Frames.Select(Frame => Frame.Id).Where(DirtyThisBatch.Contains).ToArray())
            {
                // Earlier frame fits may have moved members, so refresh descendant bounds before
                // each parent is considered. The id order remains the bottom-up BuildFrames order.
                Frames.Clear();
                BuildFrames(Contract, Placed, Frames);
                GridFrame? FrameToFit = Frames.FirstOrDefault(Frame => Frame.Id == FrameId);
                if (FrameToFit == null) continue;
                string Signature = FrameMemberSignature(Contract, Placed, FrameId);
                if (FittedMembers.TryGetValue(FrameId, out string? Fitted) && Fitted == Signature) continue;
                string FrameCause = CausesThisBatch.GetValueOrDefault(FrameId, "changed nested members");
                Refit++;
                DiagramGridLayout.GroupFrameFitPassObserver?.Invoke($"refit frame {FrameId}: {FrameCause}");
                FittedMembers[FrameId] = Signature;
                List<GridNode> TrialPlaced = Placed.Select(CloneForFrameFit).ToList();
                List<string> FrameAdjustments = [];
                GridFrame Frame = FrameToFit;
                for (int NodeIndex = 0; NodeIndex < TrialPlaced.Count; NodeIndex++)
                {
                    GridNode Node = TrialPlaced[NodeIndex];
                    if (IsInsideGroup(Contract, Node.Id, Frame.Id)) continue;

                    int Clearance = Contract.Edges.Any(Edge =>
                        (Edge.SourceId == Node.Id && (Edge.TargetId == Frame.Id || IsInsideGroup(Contract, Edge.TargetId, Frame.Id)))
                        || (Edge.TargetId == Node.Id && (Edge.SourceId == Frame.Id || IsInsideGroup(Contract, Edge.SourceId, Frame.Id))))
                        ? 2 * DiagramGridMetrics.GRID_UNIT_PX : DiagramGridMetrics.GRID_UNIT_PX;
                    string? ParentFrame = ParentGroup(Contract, Frame.Id);
                    if (ParentFrame != null && IsInsideGroup(Contract, Node.Id, ParentFrame))
                        Clearance = Math.Max(Clearance, DiagramGridMetrics.CeilToGrid(
                            FlowchartSubgraphGapThresholds.ResolveDirectMemberNestedGapPx(FlowchartRenderOptions.FromContract(Contract))));
                    if (!Overlaps(Node, Frame, Clearance))
                    {
                        continue;
                    }

                    (double X, double Y)[] BeforeNodeMove = TrialPlaced.Select(Node => (Node.X, Node.Y)).ToArray();
                    if (FlowchartPlacementHints.ReapplyLockedNodeHintAgainstFrame(
                            Contract, Node, Frame, TrialPlaced, Clearance))
                    {
                        FrameAdjustments.Add($"refresh {Node.Id} hint against frame {Frame.Id}");
                        continue;
                    }

                    if (Node.PlacementHintLocked && FlowchartPlacementHints.ReapplyLockedNodeHint(
                            Contract, Node, TrialPlaced, Frames, Clearance)
                        && PositionsDiffer(TrialPlaced, BeforeNodeMove))
                    {
                        FrameAdjustments.Add($"move {Node.Id} within its directional hint to clear frame {Frame.Id}");
                        continue;
                    }

                    bool Evicted = TryEvictNodeFromFrame(Contract, TrialPlaced, Node, Frame, Clearance);
                    if (Evicted && PositionsDiffer(TrialPlaced, BeforeNodeMove))
                    {
                        List<GridFrame> UpdatedFrames = [];
                        BuildFrames(Contract, TrialPlaced, UpdatedFrames);
                        GridFrame? UpdatedFrame = UpdatedFrames.Find(Item => Item.Id == Frame.Id);
                        if (UpdatedFrame == null || !Overlaps(Node, UpdatedFrame, Clearance))
                        {
                            FrameAdjustments.Add($"evict {Node.Id} from frame {Frame.Id}");
                            continue;
                        }
                    }

                    int Push = DiagramGridMetrics.GRID_UNIT_PX;
                    bool MovedConflictSide = Node.PlacementHintLocked
                        && MoveFrameAwayFromLockedNode(Contract, TrialPlaced, Node, Frame, Horizontal, Push);
                    if (!MovedConflictSide && Main(Node, Horizontal, Old: false) >= Frame.CenterMain(Horizontal))
                    {
                        foreach (GridNode Later in TrialPlaced)
                        {
                            if (Later.Rank >= Node.Rank)
                            {
                                SetMain(Later, (int)Math.Round(Main(Later, Horizontal, Old: false)) + Push, Horizontal);
                            }
                        }
                    }
                    else if (!MovedConflictSide)
                    {
                        foreach (GridNode Member in TrialPlaced)
                        {
                            if (IsInsideGroup(Contract, Member.Id, Frame.Id) || Member.Rank > Node.Rank)
                            {
                                SetMain(Member, (int)Math.Round(Main(Member, Horizontal, Old: false)) + Push, Horizontal);
                            }
                        }
                    }
                    if (PositionsDiffer(TrialPlaced, BeforeNodeMove))
                        FrameAdjustments.Add(MovedConflictSide
                            ? $"move unhinted members of frame {Frame.Id} away from locked node {Node.Id}"
                            : $"push rank around frame {Frame.Id} for node {Node.Id}");
                    else if (Overlaps(Node, Frame, Clearance))
                    {
                        string Conflict = $"unresolved group-frame conflict: node {Node.Id} remains inside frame {Frame.Id}; "
                            + $"placement hint locked={Node.PlacementHintLocked}";
                        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke(Conflict);
                        throw new InvalidOperationException(Conflict);
                    }
                }

                if (PositionsDiffer(TrialPlaced, Placed))
                {
                    (double X, double Y)[] BeforeMove = Placed.Select(Node => (Node.X, Node.Y)).ToArray();
                    for (int Index = 0; Index < Placed.Count; Index++)
                        Placed[Index].SetPositionFromFrameFit(TrialPlaced[Index].X, TrialPlaced[Index].Y);
                    HashSet<string> ChangedNodes = ChangedNodeIds(Placed, BeforeMove);
                    Dictionary<string, string> Affected = new(StringComparer.Ordinal);
                    EnqueueChangedMemberFrames(Contract, ChangedNodes, Affected,
                        $"members changed by frame {Frame.Id}");
                    foreach ((string Id, string Cause) in Affected)
                    {
                        // A later frame in this bottom-up batch will see the new geometry on its turn.
                        // Only already-fitted frames need to be scheduled for another batch.
                        if (ProcessedThisBatch.Contains(Id)) PendingCauses[Id] = Cause;
                    }
                    Adjustments.AddRange(FrameAdjustments);
                    if (!SeenStates.Add(PositionSignature(Placed)))
                    {
                        StopReason = "repeated placement state";
                        break;
                    }
                }
                else
                {
                    FittedMembers[FrameId] = Signature;
                }
                ProcessedThisBatch.Add(FrameId);
            }

            if (StopReason == "repeated placement state") break;
            if (PendingCauses.Count == 0) break;
        }
        DiagramGridLayout.GroupFrameRefitObserver?.Invoke(
            $"refit for {Trigger}; {Refit} fitting passes; stop={StopReason}; adjustments: {string.Join(" -> ", Adjustments)}");
    }

    private static string FrameMemberSignature(DiagramContract Contract, IReadOnlyList<GridNode> Placed, string FrameId) =>
        string.Join("|", Placed.Where(Node => IsInsideGroup(Contract, Node.Id, FrameId))
            .OrderBy(Node => Node.Id, StringComparer.Ordinal).Select(Node =>
                $"{Node.Id}:{BitConverter.DoubleToInt64Bits(Node.X):X16},{BitConverter.DoubleToInt64Bits(Node.Y):X16},{Node.Width},{Node.Height}"));

    private static HashSet<string> ChangedNodeIds(IReadOnlyList<GridNode> Nodes,
        IReadOnlyList<(double X, double Y)> Before)
    {
        HashSet<string> Changed = new(StringComparer.Ordinal);
        for (int Index = 0; Index < Nodes.Count; Index++)
            if (Nodes[Index].X != Before[Index].X || Nodes[Index].Y != Before[Index].Y)
                Changed.Add(Nodes[Index].Id);
        return Changed;
    }

    private static void EnqueueChangedMemberFrames(DiagramContract Contract, IEnumerable<string> ChangedNodes,
        Dictionary<string, string> Pending, string Cause)
    {
        foreach (string NodeId in ChangedNodes)
        foreach (GroupContract Group in Contract.Groups)
            if (IsInsideGroup(Contract, NodeId, Group.Id)) Pending[Group.Id] = Cause;
    }

    private static GridNode CloneForFrameFit(GridNode Node) => new()
    {
        Id = Node.Id,
        Shape = Node.Shape,
        GroupId = Node.GroupId,
        OldX = Node.OldX,
        OldY = Node.OldY,
        X = Node.X,
        Y = Node.Y,
        Width = Node.Width,
        Height = Node.Height,
        Rank = Node.Rank,
        Column = Node.Column,
        IsDiamond = Node.IsDiamond,
        PlacementHintLocked = Node.PlacementHintLocked
    };

    private static bool PositionsDiffer(IReadOnlyList<GridNode> First, IReadOnlyList<GridNode> Second)
    {
        if (First.Count != Second.Count) return true;
        for (int Index = 0; Index < First.Count; Index++)
            if (First[Index].X != Second[Index].X || First[Index].Y != Second[Index].Y) return true;
        return false;
    }

    private static bool PositionsDiffer(IReadOnlyList<GridNode> Nodes, IReadOnlyList<(double X, double Y)> Positions)
    {
        if (Nodes.Count != Positions.Count) return true;
        for (int Index = 0; Index < Nodes.Count; Index++)
            if (Nodes[Index].X != Positions[Index].X || Nodes[Index].Y != Positions[Index].Y) return true;
        return false;
    }

    private static string GroupMemberPositionSignature(DiagramContract Contract, IEnumerable<GridNode> Placed)
    {
        HashSet<string> Grouped = Placed.Where(Node => Node.GroupId != null
                || Contract.Groups.Any(Group => IsInsideGroup(Contract, Node.Id, Group.Id)))
            .Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        return PositionSignature(Placed.Where(Node => Grouped.Contains(Node.Id)));
    }

    private static GroupContract CloneGroupForLocalLayout(
        GroupContract Group,
        IReadOnlySet<string> LocalEndpoints)
    {
        PlacementHintContract? Hint = CloneHint(Group.PlacementHint);
        if (Hint != null && Hint.Kind != PlacementHintKind.Push
            && (Hint.PeerId == null || !LocalEndpoints.Contains(Hint.PeerId)))
            Hint = null;

        GroupContract Copy = new GroupContract
        {
            Id = Group.Id,
            Title = Group.Title,
            LabelType = Group.LabelType,
            ParentGroupId = Group.ParentGroupId,
            Direction = Group.Direction,
            PlacementHint = Hint
        };
        Copy.ChildNodeIdsMutable.AddRange(Group.ChildNodeIds);
        Copy.ChildGroupIdsMutable.AddRange(Group.ChildGroupIds);
        return Copy;
    }

    private static PlacementHintContract? CloneHint(PlacementHintContract? Hint) => Hint == null ? null : new PlacementHintContract
    {
        Kind = Hint.Kind,
        PeerId = Hint.PeerId,
        Offset = Hint.Offset,
        Direction = Hint.Direction,
        SourceLine = Hint.SourceLine,
        IsActive = Hint.IsActive,
        UnhonouredReason = Hint.UnhonouredReason
    };

    private static bool SeparateSiblingFrames(
        DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames, bool Horizontal)
    {
        bool Ancestor(string Parent, string Child)
        {
            string? Current = ParentGroup(Contract, Child);
            while (Current != null)
            {
                if (Current == Parent) return true;
                Current = ParentGroup(Contract, Current);
            }
            return false;
        }
        for (int First = 0; First < Frames.Count; First++)
        for (int Second = First + 1; Second < Frames.Count; Second++)
        {
            var A = Frames[First];
            var B = Frames[Second];
            if (Ancestor(A.Id, B.Id) || Ancestor(B.Id, A.Id)) continue;
            double MainMin(GridFrame Frame) => Horizontal ? Frame.Left : Frame.Top;
            double MainMax(GridFrame Frame) => Horizontal ? Frame.Right : Frame.Bottom;
            double CrossMin(GridFrame Frame) => Horizontal ? Frame.Top : Frame.Left;
            double CrossMax(GridFrame Frame) => Horizontal ? Frame.Bottom : Frame.Right;
            double MainGap = Math.Max(MainMin(A), MainMin(B)) - Math.Min(MainMax(A), MainMax(B));
            double CrossGap = Math.Max(CrossMin(A), CrossMin(B)) - Math.Min(CrossMax(A), CrossMax(B));
            var AMembers = Placed.Where(Node => IsInsideGroup(Contract, Node.Id, A.Id)).ToArray();
            var BMembers = Placed.Where(Node => IsInsideGroup(Contract, Node.Id, B.Id)).ToArray();
            bool Sequential = AMembers.Length > 0 && BMembers.Length > 0
                && (AMembers.Max(Node => Node.Rank) < BMembers.Min(Node => Node.Rank)
                    || BMembers.Max(Node => Node.Rank) < AMembers.Min(Node => Node.Rank));
            bool AlongMain = CrossGap <= 0 && (MainGap >= 0 || Sequential);
            double Needed = AlongMain ? 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + DiagramGridMetrics.GRID_UNIT_PX
                : DiagramGridMetrics.GRID_UNIT_PX;
            double Gap = AlongMain ? MainGap : CrossGap;
            if (Gap >= Needed || (!AlongMain && MainGap >= 0)) continue;
            var Moving = AlongMain ? (MainMin(A) > MainMin(B) ? A : B)
                : (CrossMin(A) < CrossMin(B) ? A : B);
            int Shift = DiagramGridMetrics.CeilToGrid(Needed - Gap) * (AlongMain ? 1 : -1);
            var Members = Placed.Where(Node => IsInsideGroup(Contract, Node.Id, Moving.Id)).ToHashSet();
            var Visible = Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)).ToList();
            bool Added;
            do
            {
                Added = false;
                foreach (var Node in Placed.Where(Node => Node.GroupId == null && !Members.Contains(Node)))
                {
                    if (Visible.Count(Edge => Edge.SourceId == Node.Id) <= 1
                        && Visible.Count(Edge => Edge.TargetId == Node.Id) <= 1
                        && Visible.Any(Edge => (Edge.SourceId == Node.Id && Members.Any(Member => Member.Id == Edge.TargetId))
                            || (Edge.TargetId == Node.Id && Members.Any(Member => Member.Id == Edge.SourceId))))
                        Added |= Members.Add(Node);
                }
            } while (Added);
            foreach (var Node in Members)
            {
                if (AlongMain) SetMain(Node, (int)Math.Round(Main(Node, Horizontal, Old: false)) + Shift, Horizontal);
                else SetCross(Node, (int)Math.Round(Cross(Node, Horizontal, Old: false)) + Shift, Horizontal);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Keeps unrelated frames disjoint by translating a group's members as one
    /// unit. The next bottom-up frame build grows the moved member's frame around
    /// its contents. Hinted units and units referenced by an external hint are
    /// fixed, so this repair cannot silently undo a placement hint.
    /// </summary>
    private static bool SeparateUnrelatedFrames(DiagramContract Contract, List<GridNode> Placed,
        List<GridFrame> Frames)
    {
        static bool Ancestor(DiagramContract Contract, string PossibleAncestor, string Child)
        {
            string? Parent = ParentGroup(Contract, Child);
            while (Parent != null)
            {
                if (string.Equals(Parent, PossibleAncestor, StringComparison.Ordinal)) return true;
                Parent = ParentGroup(Contract, Parent);
            }
            return false;
        }

        static double MinY(GridFrame Frame) => Math.Min(Frame.Top, Frame.Bottom);
        static double MaxY(GridFrame Frame) => Math.Max(Frame.Top, Frame.Bottom);
        static bool Intersects(GridFrame A, GridFrame B) =>
            Math.Min(A.Right, B.Right) > Math.Max(A.Left, B.Left)
            && Math.Min(MaxY(A), MaxY(B)) > Math.Max(MinY(A), MinY(B));

        static string PairKey(string A, string B) => string.CompareOrdinal(A, B) < 0
            ? $"{A}\u001f{B}" : $"{B}\u001f{A}";

        HashSet<string> Existing = [];
        for (int I = 0; I < Frames.Count; I++)
        for (int J = I + 1; J < Frames.Count; J++)
        {
            GridFrame A = Frames[I], B = Frames[J];
            if (!Ancestor(Contract, A.Id, B.Id) && !Ancestor(Contract, B.Id, A.Id)
                && Intersects(A, B)) Existing.Add(PairKey(A.Id, B.Id));
        }
        if (Existing.Count == 0) return false;

        Dictionary<string, NodeContract> NodeContracts = Contract.Nodes
            .ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        Dictionary<string, GroupContract> Groups = Contract.Groups
            .ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        var FramePairs = (from A in Frames
                          from B in Frames
                          where string.CompareOrdinal(A.Id, B.Id) < 0
                              && !Ancestor(Contract, A.Id, B.Id)
                              && !Ancestor(Contract, B.Id, A.Id)
                              && Intersects(A, B)
                          orderby A.Id, B.Id
                          select (A, B)).ToArray();

        (int Count, int Distance, string Pair, string Group, double Dx, double Dy, List<GridNode> Moved)? Best = null;
        foreach ((GridFrame A, GridFrame B) in FramePairs)
        foreach ((GridFrame MovingFrame, GridFrame FixedFrame) in new[] { (A, B), (B, A) })
        {
            HashSet<string> MovingGroupIds = Groups.Keys.Where(Id => Id == MovingFrame.Id
                || IsInsideGroup(Contract, Id, MovingFrame.Id)).ToHashSet(StringComparer.Ordinal);
            HashSet<string> MovingIds = Placed.Where(Node => IsInsideGroup(Contract, Node.Id, MovingFrame.Id))
                .Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
            bool AddedDependency;
            do
            {
                AddedDependency = false;
                foreach (GroupContract Dependent in Groups.Values.Where(Group => !MovingGroupIds.Contains(Group.Id)
                             && Group.PlacementHint?.IsActive == true
                             && (MovingGroupIds.Contains(Group.PlacementHint.PeerId ?? string.Empty)
                                 || MovingIds.Contains(Group.PlacementHint.PeerId ?? string.Empty))))
                {
                    MovingGroupIds.Add(Dependent.Id);
                    foreach (string Descendant in Groups.Keys.Where(Id => Id == Dependent.Id
                                 || Ancestor(Contract, Dependent.Id, Id)))
                        MovingGroupIds.Add(Descendant);
                    MovingIds.UnionWith(Placed.Where(Node => IsInsideGroup(Contract, Node.Id, Dependent.Id))
                        .Select(Node => Node.Id));
                    AddedDependency = true;
                }
                foreach (NodeContract Dependent in NodeContracts.Values.Where(Node => !MovingIds.Contains(Node.Id)
                             && Node.PlacementHint?.IsActive == true
                             && (MovingGroupIds.Contains(Node.PlacementHint.PeerId ?? string.Empty)
                                 || MovingIds.Contains(Node.PlacementHint.PeerId ?? string.Empty))))
                {
                    MovingIds.Add(Dependent.Id);
                    AddedDependency = true;
                }
            } while (AddedDependency);
            List<GridNode> Members = Placed.Where(Node => MovingIds.Contains(Node.Id)).ToList();
            if (Members.Count == 0) continue;
            bool HasExternalHint(PlacementHintContract? Hint) => Hint?.IsActive == true
                && (Hint.Kind == PlacementHintKind.Push || Hint.PeerId == null
                    || !MovingIds.Contains(Hint.PeerId) && !MovingGroupIds.Contains(Hint.PeerId));
            PlacementHintContract? FrameHint = Groups.TryGetValue(MovingFrame.Id, out GroupContract? MovingGroup)
                && MovingGroup.PlacementHint?.IsActive == true ? MovingGroup.PlacementHint : null;
            bool DirectionalFrameHint = FrameHint?.Kind is PlacementHintKind.RightOf or PlacementHintKind.LeftOf
                or PlacementHintKind.Above or PlacementHintKind.Below;
            if (Members.Any(Member => NodeContracts.TryGetValue(Member.Id, out NodeContract? Node)
                    && HasExternalHint(Node.PlacementHint))
                || Groups.Values.Any(Group => MovingGroupIds.Contains(Group.Id)
                    && (Group.Id != MovingFrame.Id || !DirectionalFrameHint)
                    && HasExternalHint(Group.PlacementHint))) continue;
            bool HasExternalDependent = Contract.Nodes.Any(Node => !MovingIds.Contains(Node.Id)
                    && Node.PlacementHint?.IsActive == true
                    && (MovingIds.Contains(Node.PlacementHint.PeerId ?? string.Empty)
                        || MovingGroupIds.Contains(Node.PlacementHint.PeerId ?? string.Empty)))
                || Groups.Values.Any(Group => !MovingGroupIds.Contains(Group.Id)
                    && Group.PlacementHint?.IsActive == true
                    && (MovingGroupIds.Contains(Group.PlacementHint.PeerId ?? string.Empty)
                        || MovingIds.Contains(Group.PlacementHint.PeerId ?? string.Empty)));
            if (HasExternalDependent) continue;

            int Step = DiagramGridMetrics.GRID_UNIT_PX;
            static double GridDelta(double Delta) => Math.Sign(Delta)
                * DiagramGridMetrics.CeilToGrid(Math.Abs(Delta));
            GridFrame[] UnrelatedFrames = Frames.Where(Other => Other.Id != MovingFrame.Id
                && !Ancestor(Contract, MovingFrame.Id, Other.Id)
                && !Ancestor(Contract, Other.Id, MovingFrame.Id)).ToArray();
            GridFrame[] HorizontalConflicts = UnrelatedFrames.Where(Other =>
                MovingFrame.Left < Other.Right && MovingFrame.Right > Other.Left).ToArray();
            GridFrame[] VerticalConflicts = UnrelatedFrames.Where(Other =>
                MaxY(MovingFrame) > MinY(Other) && MinY(MovingFrame) < MaxY(Other)).ToArray();
            (double Dx, double Dy)[] Shifts = DirectionalFrameHint
                ? FrameHint!.Kind switch
                {
                    // Move only far enough to clear frames whose projection can overlap
                    // the moving frame. Using every unrelated frame's extreme edge can
                    // send a hinted group far beyond the actual conflict.
                    PlacementHintKind.RightOf =>
                        [(GridDelta(VerticalConflicts.Max(Other => Other.Right) + Step - MovingFrame.Left), 0)],
                    PlacementHintKind.LeftOf =>
                        [(GridDelta(VerticalConflicts.Min(Other => Other.Left) - Step - MovingFrame.Right), 0)],
                    PlacementHintKind.Below =>
                        [(0, GridDelta(HorizontalConflicts.Max(MaxY) + Step - MinY(MovingFrame)))],
                    PlacementHintKind.Above =>
                        [(0, GridDelta(HorizontalConflicts.Min(MinY) - Step - MaxY(MovingFrame)))],
                    _ => []
                }
                : [
                    (GridDelta(FixedFrame.Left - Step - MovingFrame.Right), 0),
                    (GridDelta(FixedFrame.Right + Step - MovingFrame.Left), 0),
                    (0, GridDelta(MinY(FixedFrame) - Step - MaxY(MovingFrame))),
                    (0, GridDelta(MaxY(FixedFrame) + Step - MinY(MovingFrame)))
                ];
            foreach ((double Dx, double Dy) in Shifts)
            {
                if (Dx == 0 && Dy == 0) continue;
                double TrialDx = Dx, TrialDy = Dy;
                HashSet<string> SeenTranslations = [];
                while (SeenTranslations.Add($"{TrialDx:0.##},{TrialDy:0.##}"))
                {
                    bool Collision = false;
                    double RequiredDelta = TrialDx != 0 ? TrialDx : TrialDy;
                    foreach (GridNode Member in Members)
                    foreach (GridNode Other in Placed.Where(Node => !MovingIds.Contains(Node.Id)))
                    {
                        double MovedX = Member.X + TrialDx, MovedY = Member.Y + TrialDy;
                        bool OverlapX = Math.Abs(MovedX - Other.X) * 2 < Member.Width + Other.Width;
                        bool OverlapY = Math.Abs(MovedY - Other.Y) * 2 < Member.Height + Other.Height;
                        if (!OverlapX || !OverlapY) continue;
                        Collision = true;
                        if (TrialDx < 0)
                            RequiredDelta = Math.Min(RequiredDelta,
                                Other.X - Other.Width / 2.0 - Step - (Member.X + Member.Width / 2.0));
                        else if (TrialDx > 0)
                            RequiredDelta = Math.Max(RequiredDelta,
                                Other.X + Other.Width / 2.0 + Step - (Member.X - Member.Width / 2.0));
                        else if (TrialDy < 0)
                            RequiredDelta = Math.Min(RequiredDelta,
                                Other.Y - Other.Height / 2.0 - Step - (Member.Y + Member.Height / 2.0));
                        else
                            RequiredDelta = Math.Max(RequiredDelta,
                                Other.Y + Other.Height / 2.0 + Step - (Member.Y - Member.Height / 2.0));
                    }
                    if (!Collision) break;
                    double Snapped = GridDelta(RequiredDelta);
                    if (TrialDx != 0) TrialDx = Snapped;
                    else TrialDy = Snapped;
                }
                if (!SeenTranslations.Contains($"{TrialDx:0.##},{TrialDy:0.##}")) continue;
                List<GridNode> Trial = Placed.Select(CloneForFrameFit).ToList();
                HashSet<string> TrialIds = Trial.Where(Node => MovingIds.Contains(Node.Id))
                    .Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                foreach (GridNode Member in Trial.Where(Node => TrialIds.Contains(Node.Id)))
                    Member.SetPositionFromFrameFit(Member.X + TrialDx, Member.Y + TrialDy);

                bool NodeCollision = false;
                GridNode[] Moved = Trial.Where(Node => TrialIds.Contains(Node.Id)).ToArray();
                foreach (GridNode MovedNode in Moved)
                foreach (GridNode Other in Trial.Where(Node => !TrialIds.Contains(Node.Id)))
                {
                    if (Math.Abs(MovedNode.X - Other.X) * 2 < MovedNode.Width + Other.Width
                        && Math.Abs(MovedNode.Y - Other.Y) * 2 < MovedNode.Height + Other.Height)
                    {
                        NodeCollision = true;
                        break;
                    }
                }
                if (NodeCollision) continue;

                List<GridFrame> TrialFrames = [];
                BuildFrames(Contract, Trial, TrialFrames);
                HashSet<string> After = [];
                for (int I = 0; I < TrialFrames.Count; I++)
                for (int J = I + 1; J < TrialFrames.Count; J++)
                {
                    GridFrame First = TrialFrames[I], Second = TrialFrames[J];
                    if (!Ancestor(Contract, First.Id, Second.Id) && !Ancestor(Contract, Second.Id, First.Id)
                        && Intersects(First, Second)) After.Add(PairKey(First.Id, Second.Id));
                }
                // A rigid translation can remove one conflict while exposing a
                // different pair. Accept only a strict reduction in total frame
                // conflicts; the next fit pass resolves any remaining pair.
                if (After.Count >= Existing.Count) continue;
                int Distance = (int)(Math.Abs(TrialDx) + Math.Abs(TrialDy));
                var Candidate = (Count: After.Count, Distance, Pair: PairKey(A.Id, B.Id),
                    Group: MovingFrame.Id, Dx: TrialDx, Dy: TrialDy, Moved: Members);
                DiagramGridLayout.GroupFrameCandidateObserver?.Invoke(
                    $"rule 7.8 candidate {MovingFrame.Id} by ({TrialDx:0},{TrialDy:0}) "
                    + $"clears {Candidate.Pair}; remaining overlaps={Candidate.Count}; distance={Distance}");
                if (Best == null || Candidate.Count < Best.Value.Count
                    || Candidate.Count == Best.Value.Count && (Candidate.Distance < Best.Value.Distance
                        || Candidate.Distance == Best.Value.Distance
                            && string.CompareOrdinal(Candidate.Pair + Candidate.Group,
                                Best.Value.Pair + Best.Value.Group) < 0))
                    Best = Candidate;
            }
        }

        if (Best == null)
        {
            DiagramGridLayout.GroupFrameFitPassObserver?.Invoke(
                $"rule 7.8 unresolved unrelated frame overlaps: {string.Join(",", Existing.OrderBy(Key => Key, StringComparer.Ordinal))}");
            return false;
        }
        foreach (GridNode Member in Best.Value.Moved)
            Member.SetPositionFromFrameFit(Member.X + Best.Value.Dx, Member.Y + Best.Value.Dy);
        DiagramGridLayout.GroupFrameFitPassObserver?.Invoke(
            $"rule 7.8 move members of {Best.Value.Group} by ({Best.Value.Dx:0},{Best.Value.Dy:0}) to clear frame {Best.Value.Pair}");
        return true;
    }

    internal static int CountUnrelatedFrameOverlaps(DiagramContract Contract, IReadOnlyList<GridFrame> Frames)
    {
        bool IsAncestor(string PossibleAncestor, string Child)
        {
            string? Parent = ParentGroup(Contract, Child);
            while (Parent != null)
            {
                if (string.Equals(Parent, PossibleAncestor, StringComparison.Ordinal)) return true;
                Parent = ParentGroup(Contract, Parent);
            }
            return false;
        }

        int Count = 0;
        for (int First = 0; First < Frames.Count; First++)
        for (int Second = First + 1; Second < Frames.Count; Second++)
        {
            GridFrame A = Frames[First], B = Frames[Second];
            if (IsAncestor(A.Id, B.Id) || IsAncestor(B.Id, A.Id)) continue;
            if (Math.Min(A.Right, B.Right) > Math.Max(A.Left, B.Left)
                && Math.Min(Math.Max(A.Top, A.Bottom), Math.Max(B.Top, B.Bottom))
                    > Math.Max(Math.Min(A.Top, A.Bottom), Math.Min(B.Top, B.Bottom))) Count++;
        }
        return Count;
    }

    /// <summary>
    /// Moves a non-member node just outside a group frame with at least one grid step of clearance (rulebook 7.3).
    /// </summary>
    internal static bool TryEvictNodeFromFrame(DiagramContract Contract, List<GridNode> Placed, GridNode Node, GridFrame Frame,
        int Gap = DiagramGridMetrics.GRID_UNIT_PX)
    {
        if (!Overlaps(Node, Frame, Gap))
        {
            return false;
        }

        double ShiftUp = Frame.Top - Gap - (Node.Height / 2.0) - Node.Y;
        double ShiftDown = Frame.Bottom + Gap + (Node.Height / 2.0) - Node.Y;
        double ShiftLeft = Frame.Left - Gap - (Node.Width / 2.0) - Node.X;
        double ShiftRight = Frame.Right + Gap + (Node.Width / 2.0) - Node.X;
        double Best = double.MaxValue;
        double ChosenX = 0.0;
        double ChosenY = 0.0;
        var Members = Placed.Where(Member => IsInsideGroup(Contract, Member.Id, Frame.Id)).ToList();
        double Snap(double Delta) => Math.Sign(Delta) * DiagramGridMetrics.CeilToGrid(Math.Abs(Delta));
        if (Members.All(Member => Member.Y > Node.Y)) ConsiderEviction(0.0, Snap(ShiftUp), ref Best, ref ChosenX, ref ChosenY);
        if (Members.All(Member => Member.Y < Node.Y)) ConsiderEviction(0.0, Snap(ShiftDown), ref Best, ref ChosenX, ref ChosenY);
        if (Members.All(Member => Member.X > Node.X)) ConsiderEviction(Snap(ShiftLeft), 0.0, ref Best, ref ChosenX, ref ChosenY);
        if (Members.All(Member => Member.X < Node.X)) ConsiderEviction(Snap(ShiftRight), 0.0, ref Best, ref ChosenX, ref ChosenY);
        if (Best == double.MaxValue)
        {
            // A non-member inside the members' extent needs a separate grid
            // line. Move its aligned outside column or row together; members
            // keep their existing grid positions and the frame is refitted.
            ConsiderEviction(0.0, Snap(ShiftUp), ref Best, ref ChosenX, ref ChosenY);
            ConsiderEviction(0.0, Snap(ShiftDown), ref Best, ref ChosenX, ref ChosenY);
            ConsiderEviction(Snap(ShiftLeft), 0.0, ref Best, ref ChosenX, ref ChosenY);
            ConsiderEviction(Snap(ShiftRight), 0.0, ref Best, ref ChosenX, ref ChosenY);
            double OutsideColumn = Node.X, OutsideRow = Node.Y;
            foreach (var Other in Placed.Where(Other => !Members.Contains(Other)))
            {
                if (ChosenX != 0 && Other.X == OutsideColumn) Other.X += (int)ChosenX;
                if (ChosenY != 0 && Other.Y == OutsideRow) Other.Y += (int)ChosenY;
            }
            return true;
        }

        double CutX = Node.X, CutY = Node.Y;
        foreach (var Other in Placed)
        {
            if ((ChosenX < 0 && Other.X <= CutX) || (ChosenX > 0 && Other.X >= CutX)) Other.X += (int)ChosenX;
            if ((ChosenY < 0 && Other.Y <= CutY) || (ChosenY > 0 && Other.Y >= CutY)) Other.Y += (int)ChosenY;
        }
        return true;
    }

    internal static bool MoveFrameAwayFromLockedNode(
        DiagramContract Contract,
        List<GridNode> Placed,
        GridNode LockedNode,
        GridFrame Frame,
        bool Horizontal,
        int Distance)
    {
        if (!LockedNode.PlacementHintLocked) return false;
        List<GridNode> Members = Placed.Where(Member => IsInsideGroup(Contract, Member.Id, Frame.Id)).ToList();
        if (Members.Count == 0 || Members.Any(Member => Member.PlacementHintLocked
            && !HasRelativeHintWithinFrame(Contract, Member, Frame.Id))) return false;

        int Direction = Main(LockedNode, Horizontal, Old: false) >= Frame.CenterMain(Horizontal) ? -1 : 1;
        foreach (GridNode Member in Members)
        {
            int Delta = Direction * Distance;
            Member.SetPositionFromFrameFit(Horizontal ? Member.X + Delta : Member.X,
                Horizontal ? Member.Y : Member.Y + Delta);
        }
        return true;
    }

    private static bool HasRelativeHintWithinFrame(DiagramContract Contract, GridNode Member, string FrameId)
    {
        NodeContract? Source = Contract.Nodes.FirstOrDefault(Node => Node.Id == Member.Id);
        PlacementHintContract? Hint = Source?.PlacementHint;
        return Hint?.IsActive != true || Hint.Kind != PlacementHintKind.Push && Hint.PeerId != null
            && IsInsideGroup(Contract, Hint.PeerId, FrameId);
    }

    private static void ConsiderEviction(
        double ShiftX,
        double ShiftY,
        ref double Best,
        ref double ChosenX,
        ref double ChosenY)
    {
        if (ShiftX == 0.0 && ShiftY == 0.0)
        {
            return;
        }

        double Distance = Math.Sqrt((ShiftX * ShiftX) + (ShiftY * ShiftY));
        if (Distance < Best)
        {
            Best = Distance;
            ChosenX = ShiftX;
            ChosenY = ShiftY;
        }
    }

    /// <summary>Fits frames during route repair without reintroducing placement's downward Y axis.</summary>
    /// <param name="Contract">Group membership, title measurements and frame hints.</param>
    /// <param name="Placed">Final node geometry in upward Y coordinates.</param>
    /// <param name="Frames">Receives the fitted frames in the same upward Y coordinates.</param>
    private static void BuildFramesYUp(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames)
    {
        var Mirrored = Placed.Select(Node => new GridNode { Id = Node.Id, Shape = Node.Shape,
            GroupId = Node.GroupId, X = Node.X, Y = -Node.Y, OldX = Node.OldX, OldY = -Node.OldY,
            Width = Node.Width, Height = Node.Height, Rank = Node.Rank, Column = Node.Column,
            IsDiamond = Node.IsDiamond }).ToList();
        BuildFrames(Contract, Mirrored, Frames);
        foreach (var Frame in Frames)
        {
            Frame.Top = -Frame.Top;
            Frame.Bottom = -Frame.Bottom;
        }
    }

    private static void BuildFrames(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        List<GroupContract> Ordered = Contract.Groups.Select((Group, Index) => (Group, Index))
            .OrderByDescending(Item => DepthOf(Contract, Item.Group.Id))
            .ThenBy(Item => Item.Index)
            .Select(Item => Item.Group).ToList();

        Dictionary<string, GridFrame> FrameById = new Dictionary<string, GridFrame>(StringComparer.Ordinal);
        foreach (GroupContract Group in Ordered)
        {
            double Left = double.PositiveInfinity;
            double Right = double.NegativeInfinity;
            double Top = double.PositiveInfinity;
            double Bottom = double.NegativeInfinity;
            bool Any = false;
            foreach (string ChildId in Group.ChildNodeIds)
            {
                if (ById.TryGetValue(ChildId, out GridNode? Child))
                {
                    IncludeNode(Child, ref Left, ref Right, ref Top, ref Bottom, ref Any);
                }
            }

            foreach (NodeContract Node in Contract.Nodes)
            {
                if (string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal)
                    && ById.TryGetValue(Node.Id, out GridNode? Child))
                {
                    IncludeNode(Child, ref Left, ref Right, ref Top, ref Bottom, ref Any);
                }
            }

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                if (FrameById.TryGetValue(ChildGroupId, out GridFrame? ChildFrame))
                {
                    IncludeRect(ChildFrame.Left, ChildFrame.Top, ChildFrame.Right, ChildFrame.Bottom, ref Left, ref Right, ref Top, ref Bottom, ref Any);
                }
            }

            if (!Any)
            {
                GridFrame EmptyFrame = BuildEmptyGroupFrame(Group, Contract, ById, FrameById, Options);
                Frames.Add(EmptyFrame);
                FrameById[Group.Id] = EmptyFrame;
                continue;
            }

            string Title = string.IsNullOrWhiteSpace(Group.Title) ? Group.Id : Group.Title;
            double TitleWidth = Title.Split('\n').Max(Line => FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Line, Options.FontSizePx));
            int Strip = DiagramGridMetrics.CeilToGrid(Math.Max(
                DiagramGridMetrics.GRID_UNIT_PX,
                FlowchartSubgraphSvgRenderer.RequiredTitleBandPx(Options.FontSizePx, Title.Split('\n').Length)));
            int Padding = DiagramGridMetrics.NODE_GAP_PX;
            if (Group.ChildGroupIds.Count > 0)
                Padding = Math.Max(Padding, DiagramGridMetrics.CeilToGrid(
                    FlowchartSubgraphGapThresholds.ResolveNestedFlowAxisGapPx(Options)));
            int InternalReturns = FlowchartBackEdgeHelper.FindBackEdges(Contract).Count(Edge =>
                IsInsideGroup(Contract, Edge.SourceId, Group.Id) && IsInsideGroup(Contract, Edge.TargetId, Group.Id));
            if (InternalReturns > 0)
                Padding += DiagramGridMetrics.CeilToGrid(DiagramGridRouteBuilder.CORNER_RADIUS_PX + 6 * InternalReturns);
            int BoundaryPadding = FlowchartCompoundEndpointHelper.InternalBoundaryPadding(Contract, Group.Id);
            int Unit = DiagramGridMetrics.GRID_UNIT_PX;
            int HorizontalPadding = Math.Max(Padding, BoundaryPadding);
            int VerticalPadding = Math.Max(Padding,
                FlowchartCompoundEndpointHelper.InternalBoundaryPadding(Contract, Group.Id));
            int FrameLeft = FloorCoordinateToGrid(Left - HorizontalPadding);
            int FrameRight = CeilCoordinateToGrid(Right + HorizontalPadding);
            int FrameTop = FloorCoordinateToGrid(Top - Padding - Strip);
            int FrameBottom = CeilCoordinateToGrid(Bottom + VerticalPadding);
            int NeededWidth = DiagramGridMetrics.CeilToGrid(TitleWidth + (2.0 * Padding));
            int HaveWidth = FrameRight - FrameLeft;
            if (NeededWidth > HaveWidth)
            {
                int ExtraUnits = (int)Math.Ceiling((NeededWidth - HaveWidth) / (double)Unit);
                int LeftUnits = ExtraUnits / 2;
                FrameLeft -= LeftUnits * Unit;
                FrameRight += (ExtraUnits - LeftUnits) * Unit;
            }

            GridFrame Frame = new GridFrame
            {
                Id = Group.Id,
                Left = FrameLeft,
                Top = FrameTop,
                Right = FrameRight,
                Bottom = FrameBottom,
                TitleStrip = Strip,
                Padding = Padding
            };
            Frames.Add(Frame);
            FrameById[Group.Id] = Frame;
        }

        ResolveEmptyGroupFrameOverlaps(Contract, Ordered, Frames, FrameById);
    }

    private static void ResolveEmptyGroupFrameOverlaps(DiagramContract Contract,
        IReadOnlyList<GroupContract> Ordered, List<GridFrame> Frames,
        IReadOnlyDictionary<string, GridFrame> FrameById)
    {
        bool IsAncestor(string First, string Second)
        {
            string? Parent = ParentGroup(Contract, Second);
            while (Parent != null)
            {
                if (string.Equals(Parent, First, StringComparison.Ordinal)) return true;
                Parent = ParentGroup(Contract, Parent);
            }
            return false;
        }

        bool IsEmpty(GroupContract Group) => Group.ChildNodeIds.Count == 0
            && Group.ChildGroupIds.Count == 0
            && !Contract.Nodes.Any(Node => string.Equals(Node.ParentGroupId, Group.Id, StringComparison.Ordinal));

        bool Overlaps(GridFrame A, GridFrame B) =>
            Math.Min(A.Right, B.Right) > Math.Max(A.Left, B.Left)
            && Math.Min(A.Bottom, B.Bottom) > Math.Max(A.Top, B.Top);

        foreach (GroupContract Group in Ordered.Where(IsEmpty)
                     .OrderBy(Group => Group.PlacementHint?.SourceLine ?? int.MaxValue))
        {
            if (!FrameById.TryGetValue(Group.Id, out GridFrame? Frame)) continue;
            PlacementHintContract? Hint = Group.PlacementHint is { IsActive: true } ? Group.PlacementHint : null;
            int StepX = Hint?.Kind switch
            {
                PlacementHintKind.RightOf => 1,
                PlacementHintKind.LeftOf => -1,
                _ => 0
            };
            int StepY = Hint?.Kind switch
            {
                PlacementHintKind.Below => 1,
                PlacementHintKind.Above => -1,
                _ => 0
            };
            if (StepX == 0 && StepY == 0) StepX = 1;

            while (Frames.Any(Other => Other != Frame
                       && !IsAncestor(Group.Id, Other.Id) && !IsAncestor(Other.Id, Group.Id)
                       && Overlaps(Frame, Other)))
            {
                Frame.Left += StepX * DiagramGridMetrics.GRID_UNIT_PX;
                Frame.Right += StepX * DiagramGridMetrics.GRID_UNIT_PX;
                Frame.Top += StepY * DiagramGridMetrics.GRID_UNIT_PX;
                Frame.Bottom += StepY * DiagramGridMetrics.GRID_UNIT_PX;
            }
        }
    }

    private static GridFrame BuildEmptyGroupFrame(
        GroupContract Group,
        DiagramContract Contract,
        IReadOnlyDictionary<string, GridNode> Nodes,
        IReadOnlyDictionary<string, GridFrame> Frames,
        FlowchartRenderOptions Options)
    {
        string Title = string.IsNullOrWhiteSpace(Group.Title) ? Group.Id : Group.Title;
        string[] Lines = Title.Split('\n');
        double TitleWidth = Lines.Max(Line => FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Line, Options.FontSizePx));
        int Strip = DiagramGridMetrics.CeilToGrid(Math.Max(
            DiagramGridMetrics.GRID_UNIT_PX,
            FlowchartSubgraphSvgRenderer.RequiredTitleBandPx(Options.FontSizePx, Lines.Length)));
        int Padding = DiagramGridMetrics.NODE_GAP_PX;
        int Width = DiagramGridMetrics.CeilToGrid(TitleWidth + 2.0 * Padding);
        int Height = DiagramGridMetrics.CeilToGrid(Strip + 2.0 * Padding);
        double Left = 0.0;
        double Top = 0.0;

        PlacementHintContract? Hint = Group.PlacementHint?.IsActive == true ? Group.PlacementHint : null;
        if (Hint != null && !string.IsNullOrEmpty(Hint.PeerId))
        {
            double PeerLeft, PeerTop, PeerRight, PeerBottom;
            if (Frames.TryGetValue(Hint.PeerId, out GridFrame? PeerFrame))
            {
                PeerLeft = PeerFrame.Left; PeerTop = PeerFrame.Top;
                PeerRight = PeerFrame.Right; PeerBottom = PeerFrame.Bottom;
            }
            else if (Nodes.TryGetValue(Hint.PeerId, out GridNode? PeerNode))
            {
                PeerLeft = PeerNode.X - PeerNode.Width / 2.0;
                PeerTop = PeerNode.Y - PeerNode.Height / 2.0;
                PeerRight = PeerNode.X + PeerNode.Width / 2.0;
                PeerBottom = PeerNode.Y + PeerNode.Height / 2.0;
            }
            else
            {
                PeerLeft = PeerTop = PeerRight = PeerBottom = 0.0;
            }

            int Gap = DiagramGridMetrics.NODE_GAP_PX + Hint.Offset * DiagramGridMetrics.GRID_UNIT_PX;
            switch (Hint.Kind)
            {
                case PlacementHintKind.Above:
                    Left = (PeerLeft + PeerRight - Width) / 2.0;
                    Top = PeerTop - Gap - Height;
                    break;
                case PlacementHintKind.Below:
                    Left = (PeerLeft + PeerRight - Width) / 2.0;
                    Top = PeerBottom + Gap;
                    break;
                case PlacementHintKind.LeftOf:
                    Left = PeerLeft - Gap - Width;
                    Top = (PeerTop + PeerBottom - Height) / 2.0;
                    break;
                case PlacementHintKind.RightOf:
                    Left = PeerRight + Gap;
                    Top = (PeerTop + PeerBottom - Height) / 2.0;
                    break;
                default:
                    Left = PeerLeft + (PeerRight - PeerLeft - Width) / 2.0;
                    Top = PeerTop + (PeerBottom - PeerTop - Height) / 2.0;
                    break;
            }
        }

        GridFrame Candidate = new()
        {
            Id = Group.Id,
            Left = DiagramGridMetrics.RoundToGrid(Left),
            Top = DiagramGridMetrics.RoundToGrid(Top),
            Right = DiagramGridMetrics.RoundToGrid(Left) + Width,
            Bottom = DiagramGridMetrics.RoundToGrid(Top) + Height,
            TitleStrip = Strip,
            Padding = Padding
        };

        return Candidate;
    }

    private static int FloorCoordinateToGrid(double Coordinate) =>
        (int)(Math.Floor(Coordinate / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX);

    private static int CeilCoordinateToGrid(double Coordinate) =>
        (int)(Math.Ceiling(Coordinate / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX);

    private static void IncludeNode(
        GridNode Child,
        ref double Left,
        ref double Right,
        ref double Top,
        ref double Bottom,
        ref bool Any)
    {
        IncludeRect(
            Child.X - (Child.Width / 2.0),
            Child.Y - (Child.Height / 2.0),
            Child.X + (Child.Width / 2.0),
            Child.Y + (Child.Height / 2.0),
            ref Left,
            ref Right,
            ref Top,
            ref Bottom,
            ref Any);
    }

    private static void IncludeRect(
        double RectLeft,
        double RectTop,
        double RectRight,
        double RectBottom,
        ref double Left,
        ref double Right,
        ref double Top,
        ref double Bottom,
        ref bool Any)
    {
        if (!Any)
        {
            Any = true;
            Left = RectLeft;
            Right = RectRight;
            Top = RectTop;
            Bottom = RectBottom;
            return;
        }

        Left = Math.Min(Left, RectLeft);
        Right = Math.Max(Right, RectRight);
        Top = Math.Min(Top, RectTop);
        Bottom = Math.Max(Bottom, RectBottom);
    }

    private static bool Overlaps(GridNode Node, GridFrame Frame, int Clearance = 0)
    {
        double Left = Node.X - (Node.Width / 2.0);
        double Right = Node.X + (Node.Width / 2.0);
        double Top = Node.Y - (Node.Height / 2.0);
        double Bottom = Node.Y + (Node.Height / 2.0);
        return Right > Frame.Left - Clearance && Left < Frame.Right + Clearance
            && Bottom > Frame.Top - Clearance && Top < Frame.Bottom + Clearance;
    }

    private static void RewriteRoutes(FlowchartLayoutResult Copy, List<GridNode> Placed, List<GridFrame> Frames)
    {
        Dictionary<string, GridNode> ById = IndexPlaced(Placed);
        Dictionary<string, GridFrame> FrameById = new Dictionary<string, GridFrame>(StringComparer.Ordinal);
        foreach (GridFrame Frame in Frames)
        {
            FrameById[Frame.Id] = Frame;
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            if (Route.Points.Count < 2)
            {
                continue;
            }

            bool HasSource = TryCentre(Route.SourceId, ById, FrameById, out double OldSourceX, out double OldSourceY, out double NewSourceX, out double NewSourceY);
            bool HasTarget = TryCentre(Route.TargetId, ById, FrameById, out double OldTargetX, out double OldTargetY, out double NewTargetX, out double NewTargetY);
            if (!HasSource || !HasTarget)
            {
                continue;
            }

            double SourceDeltaX = NewSourceX - OldSourceX;
            double SourceDeltaY = NewSourceY - OldSourceY;
            double TargetDeltaX = NewTargetX - OldTargetX;
            double TargetDeltaY = NewTargetY - OldTargetY;
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                double SourceDistance = Math.Abs(Point.X - OldSourceX) + Math.Abs(Point.Y - OldSourceY);
                double TargetDistance = Math.Abs(Point.X - OldTargetX) + Math.Abs(Point.Y - OldTargetY);
                double Denominator = SourceDistance + TargetDistance;
                double TowardTarget = Denominator < 1e-6 ? 0.5 : TargetDistance / Denominator;
                Point.X = Math.Round(Point.X + (TowardTarget * SourceDeltaX) + ((1.0 - TowardTarget) * TargetDeltaX));
                Point.Y = Math.Round(Point.Y + (TowardTarget * SourceDeltaY) + ((1.0 - TowardTarget) * TargetDeltaY));
            }
        }
    }

    private static bool TryCentre(
        string Id,
        Dictionary<string, GridNode> ById,
        Dictionary<string, GridFrame> FrameById,
        out double OldX,
        out double OldY,
        out double NewX,
        out double NewY)
    {
        if (ById.TryGetValue(Id, out GridNode? Node))
        {
            OldX = Node.OldX;
            OldY = Node.OldY;
            NewX = Node.X;
            NewY = Node.Y;
            return true;
        }

        if (FrameById.TryGetValue(Id, out GridFrame? Frame))
        {
            OldX = Frame.CenterX();
            OldY = Frame.CenterY();
            NewX = OldX;
            NewY = OldY;
            return true;
        }

        OldX = 0;
        OldY = 0;
        NewX = 0;
        NewY = 0;
        return false;
    }

    private static void FlipToYUp(List<GridNode> Placed, List<GridFrame> Frames, FlowchartLayoutResult Copy)
    {
        double MaxY = 0.0;
        foreach (GridNode Node in Placed)
        {
            MaxY = Math.Max(MaxY, Node.Y + (Node.Height / 2.0));
        }

        foreach (GridFrame Frame in Frames)
        {
            MaxY = Math.Max(MaxY, Frame.Bottom);
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                MaxY = Math.Max(MaxY, Point.Y);
            }
        }

        int Flip = DiagramGridMetrics.CeilToGrid(MaxY);
        if (Flip < MaxY)
        {
            Flip += DiagramGridMetrics.GRID_UNIT_PX;
        }

        foreach (GridNode Node in Placed)
        {
            Node.SetYAfterCoordinateTransform(Flip - Node.Y);
            Node.OldY = Flip - Node.OldY;
        }

        foreach (GridFrame Frame in Frames)
        {
            int OldTop = Frame.Top;
            int OldBottom = Frame.Bottom;
            Frame.Top = Flip - OldTop;
            Frame.Bottom = Flip - OldBottom;
        }

        foreach (FlowchartLayoutEdgeRoute Route in Copy.EdgeRoutes)
        {
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                Point.Y = Flip - Point.Y;
            }
        }
    }

}
