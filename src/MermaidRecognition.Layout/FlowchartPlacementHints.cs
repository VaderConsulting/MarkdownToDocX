using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Applies source-ordered node and group placement hints to grid nodes.</summary>
internal static partial class FlowchartPlacementHints
{
    private sealed record HintItem(string Id, string? ParentId, PlacementHintContract Hint, bool IsGroup);

    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyList<DiagramGridLayout.GridFrame>? CurrentFrames = null)
    {
        if (Nodes.Count == 0)
        {
            return false;
        }

        if (Nodes.Any(Node => Node.CellNode != null))
        {
            return ApplyCells(Contract, Nodes);
        }

        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        Dictionary<string, GroupContract> Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        Dictionary<string, DiagramGridLayout.GridFrame> FrameById = CurrentFrames?.ToDictionary(Frame => Frame.Id, StringComparer.Ordinal)
            ?? new Dictionary<string, DiagramGridLayout.GridFrame>(StringComparer.Ordinal);
        Dictionary<string, (Bounds Members, Bounds Frame)> InitialGroupGeometry = new(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            List<DiagramGridLayout.GridNode> Members = UnitNodes(Group.Id, true, Nodes, ById, Groups);
            if (Members.Count > 0 && FrameById.TryGetValue(Group.Id, out DiagramGridLayout.GridFrame? Frame))
            {
                InitialGroupGeometry[Group.Id] = (Measure(Members), FromFrame(Frame));
            }
        }
        List<HintItem> Hints = [.. Contract.Nodes
            .Where(Node => Node.PlacementHint?.IsActive == true && ById.ContainsKey(Node.Id))
            .Select(Node => new HintItem(Node.Id, Node.ParentGroupId, Node.PlacementHint!, false))
            .Concat(Contract.Groups
                .Where(Group => Group.PlacementHint?.IsActive == true)
                .Select(Group => new HintItem(Group.Id, Group.ParentGroupId, Group.PlacementHint!, true)))
            .OrderBy(Item => Item.Hint.SourceLine)];
        if (Hints.Count == 0)
        {
            return false;
        }

        Dictionary<string, List<string>> Attached = new(StringComparer.Ordinal);
        bool Changed = false;
        foreach (HintItem Item in Hints)
        {
            List<DiagramGridLayout.GridNode> Owner = UnitNodes(Item.Id, Item.IsGroup, Nodes, ById, Groups);
            if (Owner.Count == 0)
            {
                if (Item.IsGroup && Groups.TryGetValue(Item.Id, out GroupContract? EmptyGroup)
                    && EmptyGroup.ChildNodeIds.Count == 0 && EmptyGroup.ChildGroupIds.Count == 0)
                {
                    // Empty groups still have a visible frame and can be a
                    // placement peer or relationship endpoint. BuildFrames
                    // applies their relative position after node placement.
                    continue;
                }
                Reject(Item.Hint, "hinted group has no placeable members");
                continue;
            }

            List<DiagramGridLayout.GridNode> Moving = AttachedUnitNodes(Item.Id, Item.IsGroup, Attached, Nodes, ById, Groups);
            Bounds OwnerBounds = Measure(Moving);
            if (Item.IsGroup && InitialGroupGeometry.TryGetValue(Item.Id, out (Bounds Members, Bounds Frame) OwnerGeometry))
            {
                Bounds CurrentFrame = FollowMembers(OwnerGeometry.Members, OwnerGeometry.Frame,
                    Measure(Owner));
                OwnerBounds = Union(OwnerBounds, CurrentFrame);
            }
            if (Item.Hint.Kind == PlacementHintKind.Push)
            {
                Changed |= PushToBoundary(Item, Moving, Nodes, Groups);
                continue;
            }

            string PeerId = Item.Hint.PeerId ?? string.Empty;
            bool PeerIsGroup = Groups.ContainsKey(PeerId);
            List<DiagramGridLayout.GridNode> Peer = UnitNodes(PeerId, PeerIsGroup, Nodes, ById, Groups);
            if (Peer.Count == 0 && !FrameById.ContainsKey(PeerId))
            {
                Reject(Item.Hint, "placement hint peer has no placeable members");
                continue;
            }

            if (!Attached.TryGetValue(PeerId, out List<string>? Dependents))
            {
                Dependents = [];
                Attached[PeerId] = Dependents;
            }
            Dependents.Add(Item.Id);
            Bounds? PeerBounds = InitialGroupGeometry.TryGetValue(PeerId, out (Bounds Members, Bounds Frame) PeerGeometry)
                ? FollowMembers(PeerGeometry.Members, PeerGeometry.Frame, Measure(Peer))
                : FrameById.TryGetValue(PeerId, out DiagramGridLayout.GridFrame? PeerFrame)
                    ? FromFrame(PeerFrame) : Peer.Count > 0 ? Measure(Peer) : null;
            Changed |= PlaceRelative(Item.Hint, Moving, OwnerBounds, PeerBounds, Nodes,
                FrameById.Values, Groups);
        }
        return Changed;
    }

    /// <summary>
    /// Reapplies a node's relative hint when its peer is a fitted group frame.
    /// Group compaction can change that frame after the initial hint placement;
    /// the node's saved coordinate must not prevent the relationship from being
    /// restored against the new frame.
    /// </summary>
    internal static bool ReapplyLockedNodeHintAgainstFrame(
        DiagramContract Contract,
        DiagramGridLayout.GridNode Node,
        DiagramGridLayout.GridFrame Frame,
        List<DiagramGridLayout.GridNode> All,
        int MinimumGap)
    {
        if (Node.CellNode != null)
        {
            return ApplyCells(Contract, All);
        }

        NodeContract? ContractNode = Contract.Nodes.FirstOrDefault(Item => Item.Id == Node.Id);
        PlacementHintContract? Hint = ContractNode?.PlacementHint;
        Dictionary<string, GroupContract> Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        if (Hint?.IsActive != true || !string.Equals(Hint.PeerId, Frame.Id, StringComparison.Ordinal)
            || Hint.Kind == PlacementHintKind.Push || DiagramGridLayout.IsInsideGroup(Contract, Node.Id, Frame.Id))
        {
            return false;
        }

        bool WasLocked = Node.PlacementHintLocked;
        Node.PlacementHintLocked = false;
        try
        {
            return PlaceRelative(Hint, [Node], Measure([Node]), FromFrame(Frame), All, [Frame], Groups,
                MinimumGap);
        }
        finally
        {
            Node.PlacementHintLocked = WasLocked;
        }
    }

    internal static bool ReapplyLockedNodeHint(
        DiagramContract Contract,
        DiagramGridLayout.GridNode Node,
        List<DiagramGridLayout.GridNode> All,
        IReadOnlyList<DiagramGridLayout.GridFrame> Frames,
        int MinimumGap)
    {
        if (Node.CellNode != null)
        {
            return ApplyCells(Contract, All);
        }

        NodeContract? ContractNode = Contract.Nodes.FirstOrDefault(Item => Item.Id == Node.Id);
        PlacementHintContract? Hint = ContractNode?.PlacementHint;
        if (Hint?.IsActive != true || Hint.Kind == PlacementHintKind.Push || string.IsNullOrEmpty(Hint.PeerId))
        {
            return false;
        }

        Dictionary<string, GroupContract> Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        Dictionary<string, DiagramGridLayout.GridNode> ById = All.ToDictionary(Item => Item.Id, StringComparer.Ordinal);
        Bounds? PeerBounds = null;
        if (Groups.ContainsKey(Hint.PeerId))
        {
            DiagramGridLayout.GridFrame? PeerFrame = Frames.FirstOrDefault(Frame => Frame.Id == Hint.PeerId);
            if (PeerFrame != null)
            {
                PeerBounds = FromFrame(PeerFrame);
            }
        }
        else if (ById.TryGetValue(Hint.PeerId, out DiagramGridLayout.GridNode? PeerNode))
        {
            PeerBounds = Measure([PeerNode]);
        }

        if (PeerBounds == null)
        {
            return false;
        }

        // Preserve the hint's directional relation while allowing its other
        // coordinate to move around a fitted frame, just as initial placement
        // does. Keep the lock set: later stages must continue to respect it.
        return PlaceRelative(Hint, [Node], Measure([Node]), PeerBounds, All, Frames, Groups, MinimumGap);
    }

    private static bool PlaceRelative(
        PlacementHintContract Hint,
        List<DiagramGridLayout.GridNode> Moving,
        Bounds Owner,
        Bounds? PeerBounds,
        List<DiagramGridLayout.GridNode> All,
        IEnumerable<DiagramGridLayout.GridFrame>? Frames = null,
        IReadOnlyDictionary<string, GroupContract>? Groups = null,
        int MinimumGap = 0)
    {
        Bounds Anchor = PeerBounds ?? throw new InvalidOperationException("Placement peer has no measured bounds.");
        double TargetX = Owner.CenterX;
        double TargetY = Owner.CenterY;
        int Gap = Math.Max(DiagramGridMetrics.NODE_GAP_PX, MinimumGap);
        int Extra = Hint.Offset * DiagramGridMetrics.GRID_UNIT_PX;
        int XSign = 0, YSign = 0;
        if (Hint.IsDiagonal)
        {
            (YSign, XSign) = DiagonalSigns(Hint.Kind);
            int ColumnExtra = checked((Hint.ColumnOffset ?? Hint.Offset) * DiagramGridMetrics.GRID_UNIT_PX);
            TargetX = XSign < 0 ? Anchor.Left - Gap - ColumnExtra - Owner.Width / 2
                : Anchor.Right + Gap + ColumnExtra + Owner.Width / 2;
            TargetY = YSign < 0 ? Anchor.Top - Gap - Extra - Owner.Height / 2
                : Anchor.Bottom + Gap + Extra + Owner.Height / 2;
        }
        // Placement hints run before RouteCoordinateStage flips the layout to Y-up.
        // At this point grid Y increases down the page, so page-up means smaller Y.
        switch (Hint.Kind)
        {
            case PlacementHintKind.Above:
                TargetY = Anchor.Top - Gap - Extra - (Owner.Height / 2.0);
                TargetX = Anchor.CenterX;
                YSign = -1;
                break;
            case PlacementHintKind.Below:
                TargetY = Anchor.Bottom + Gap + Extra + (Owner.Height / 2.0);
                TargetX = Anchor.CenterX;
                YSign = 1;
                break;
            case PlacementHintKind.LeftOf:
                TargetX = Anchor.Left - Gap - Extra - (Owner.Width / 2.0);
                TargetY = Anchor.CenterY;
                XSign = -1;
                break;
            case PlacementHintKind.RightOf:
                TargetX = Anchor.Right + Gap + Extra + (Owner.Width / 2.0);
                TargetY = Anchor.CenterY;
                XSign = 1;
                break;
        }

        double Dx = SnapDelta(TargetX - Owner.CenterX);
        double Dy = SnapDelta(TargetY - Owner.CenterY);
        HashSet<string> MovingIds = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        double Unit = DiagramGridMetrics.GRID_UNIT_PX;
        for (int Steps = 0; ; Steps++)
        {
            double CandidateX = Dx + (XSign * Steps * Unit);
            double CandidateY = Dy + (YSign * Steps * Unit);
            if (!OverlapsAfterMove(Moving, All, MovingIds, CandidateX, CandidateY, Frames, Groups, Gap))
            {
                return Translate(Moving, CandidateX, CandidateY);
            }
        }
    }

    private static bool PushToBoundary(
        HintItem Item,
        List<DiagramGridLayout.GridNode> Moving,
        List<DiagramGridLayout.GridNode> All,
        IReadOnlyDictionary<string, GroupContract> Groups)
    {
        HashSet<string> MovingIds = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        List<DiagramGridLayout.GridNode> Siblings = [.. All.Where(Node => !MovingIds.Contains(Node.Id)
            && string.Equals(ImmediateParent(Node, Groups), Item.ParentId, StringComparison.Ordinal))];
        if (Siblings.Count == 0)
        {
            return false;
        }

        Bounds Owner = Measure(Moving);
        Bounds Other = Measure(Siblings);
        int Gap = DiagramGridMetrics.NODE_GAP_PX;
        double Dx = 0.0, Dy = 0.0;
        // Push hints also run in the pre-flip, page-down coordinate system.
        switch (Item.Hint.Direction)
        {
            case LayoutHintDirection.North: Dy = Other.Top - Gap - Owner.Bottom; break;
            case LayoutHintDirection.South: Dy = Other.Bottom + Gap - Owner.Top; break;
            case LayoutHintDirection.East: Dx = Other.Right + Gap - Owner.Left; break;
            case LayoutHintDirection.West: Dx = Other.Left - Gap - Owner.Right; break;
        }
        Dx = SnapDelta(Dx);
        Dy = SnapDelta(Dy);
        return Translate(Moving, Dx, Dy);
    }

    private static string? ImmediateParent(
        DiagramGridLayout.GridNode Node,
        IReadOnlyDictionary<string, GroupContract> Groups) => Node.GroupId;

    private static List<DiagramGridLayout.GridNode> AttachedUnitNodes(
        string Id,
        bool IsGroup,
        IReadOnlyDictionary<string, List<string>> Attached,
        List<DiagramGridLayout.GridNode> All,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> ById,
        IReadOnlyDictionary<string, GroupContract> Groups)
    {
        HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal);
        Stack<string> Pending = new Stack<string>();
        Pending.Push(Id);
        while (Pending.Count > 0)
        {
            string Current = Pending.Pop();
            if (!Ids.Add(Current))
            {
                continue;
            }

            if (Attached.TryGetValue(Current, out List<string>? Children))
            {
                foreach (string Child in Children)
                {
                    Pending.Push(Child);
                }
            }
        }

        List<DiagramGridLayout.GridNode> Result = [];
        foreach (string UnitId in Ids)
        {
            bool UnitIsGroup = Groups.ContainsKey(UnitId) || (UnitId == Id && IsGroup);
            foreach (DiagramGridLayout.GridNode Node in UnitNodes(UnitId, UnitIsGroup, All, ById, Groups))
            {
                if (!Result.Contains(Node))
                {
                    Result.Add(Node);
                }
            }
        }
        return Result;
    }

    private static List<DiagramGridLayout.GridNode> UnitNodes(
        string Id,
        bool IsGroup,
        List<DiagramGridLayout.GridNode> All,
        IReadOnlyDictionary<string, DiagramGridLayout.GridNode> ById,
        IReadOnlyDictionary<string, GroupContract> Groups)
    {
        if (!IsGroup)
        {
            return ById.TryGetValue(Id, out DiagramGridLayout.GridNode? Node)
                ? [Node] : [];
        }

        HashSet<string> DeclaredMembers = new(StringComparer.Ordinal);
        HashSet<string> VisitedGroups = new(StringComparer.Ordinal);
        Stack<string> PendingGroups = new();
        PendingGroups.Push(Id);
        while (PendingGroups.Count > 0)
        {
            string Current = PendingGroups.Pop();
            if (!VisitedGroups.Add(Current) || !Groups.TryGetValue(Current, out GroupContract? Group))
            {
                continue;
            }

            foreach (string ChildId in Group.ChildNodeIds)
            {
                DeclaredMembers.Add(ChildId);
            }

            foreach (GroupContract Child in Groups.Values.Where(Child =>
                string.Equals(Child.ParentGroupId, Current, StringComparison.Ordinal)))
            {
                PendingGroups.Push(Child.Id);
            }

            foreach (string ChildGroupId in Group.ChildGroupIds)
            {
                PendingGroups.Push(ChildGroupId);
            }
        }

        return [.. All.Where(Node => DeclaredMembers.Contains(Node.Id) || IsInGroup(Node, Id, Groups))];
    }

    private static bool IsInGroup(
        DiagramGridLayout.GridNode Node,
        string GroupId,
        IReadOnlyDictionary<string, GroupContract> Groups)
    {
        string? Current = Node.GroupId;
        while (Current != null)
        {
            if (string.Equals(Current, GroupId, StringComparison.Ordinal))
            {
                return true;
            }

            Current = Groups.TryGetValue(Current, out GroupContract? Group) ? Group.ParentGroupId : null;
        }
        return false;
    }

    private static bool OverlapsAfterMove(
        IEnumerable<DiagramGridLayout.GridNode> Moving,
        IEnumerable<DiagramGridLayout.GridNode> All,
        HashSet<string> MovingIds,
        double Dx,
        double Dy,
        IEnumerable<DiagramGridLayout.GridFrame>? Frames,
        IReadOnlyDictionary<string, GroupContract>? Groups,
        int FrameClearance)
    {
        foreach (DiagramGridLayout.GridNode A in Moving)
        {
            foreach (DiagramGridLayout.GridNode B in All)
        {
            if (MovingIds.Contains(B.Id))
                {
                    continue;
                }

                if (Math.Abs((A.X + Dx) - B.X) < ((A.Width + B.Width) / 2.0) + DiagramGridMetrics.NODE_GAP_PX
                && Math.Abs((A.Y + Dy) - B.Y) < ((A.Height + B.Height) / 2.0) + DiagramGridMetrics.NODE_GAP_PX)
                {
                    return true;
                }
            }
        }

        if (Frames != null)
        {
            foreach (DiagramGridLayout.GridFrame Frame in Frames)
        {
            if (Groups != null && Moving.All(Node => IsInGroup(Node, Frame.Id, Groups)))
                {
                    continue;
                }

                foreach (DiagramGridLayout.GridNode Node in Moving)
                {
                    if (Math.Abs((Node.X + Dx) - Frame.CenterX()) < (Node.Width / 2.0) + ((Frame.Right - Frame.Left) / 2.0) + FrameClearance
                    && Math.Abs((Node.Y + Dy) - Frame.CenterY()) < (Node.Height / 2.0) + ((Frame.Top - Frame.Bottom) / 2.0) + FrameClearance)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool Translate(IEnumerable<DiagramGridLayout.GridNode> Nodes, double Dx, double Dy)
    {
        if (Math.Abs(Dx) < 0.1 && Math.Abs(Dy) < 0.1)
        {
            return false;
        }

        foreach (DiagramGridLayout.GridNode Node in Nodes)
        {
            Node.X += Dx;
            Node.Y += Dy;
        }
        return true;
    }

    private static int SnapDelta(double Value) =>
        (int)(Math.Round(Value / DiagramGridMetrics.GRID_UNIT_PX) * DiagramGridMetrics.GRID_UNIT_PX);

    private static void Reject(PlacementHintContract Hint, string Reason)
    {
        Hint.IsActive = false;
        Hint.UnhonouredReason = Reason;
    }

    private readonly record struct Bounds(double Left, double Top, double Right, double Bottom)
    {
        internal double Width => Right - Left;
        internal double Height => Bottom - Top;
        internal double CenterX => (Left + Right) / 2.0;
        internal double CenterY => (Top + Bottom) / 2.0;
    }

    private static Bounds Measure(IEnumerable<DiagramGridLayout.GridNode> Nodes)
    {
        DiagramGridLayout.GridNode[] Items = [.. Nodes];
        return new Bounds(
            Items.Min(Node => Node.X - (Node.Width / 2.0)),
            Items.Min(Node => Node.Y - (Node.Height / 2.0)),
            Items.Max(Node => Node.X + (Node.Width / 2.0)),
            Items.Max(Node => Node.Y + (Node.Height / 2.0)));
    }

    private static Bounds FromFrame(DiagramGridLayout.GridFrame Frame) =>
        new Bounds(Frame.Left, Frame.Top, Frame.Right, Frame.Bottom);

    private static Bounds FollowMembers(Bounds InitialMembers, Bounds InitialFrame, Bounds CurrentMembers) =>
        new Bounds(
            CurrentMembers.Left + InitialFrame.Left - InitialMembers.Left,
            CurrentMembers.Top + InitialFrame.Top - InitialMembers.Top,
            CurrentMembers.Right + InitialFrame.Right - InitialMembers.Right,
            CurrentMembers.Bottom + InitialFrame.Bottom - InitialMembers.Bottom);

    private static Bounds Union(Bounds Left, Bounds Right) => new(
        Math.Min(Left.Left, Right.Left), Math.Min(Left.Top, Right.Top),
        Math.Max(Left.Right, Right.Right), Math.Max(Left.Bottom, Right.Bottom));
}
