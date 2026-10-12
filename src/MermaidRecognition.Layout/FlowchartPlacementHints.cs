using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Applies source-ordered node and group placement hints to grid nodes.</summary>
internal static class FlowchartPlacementHints
{
    private sealed record HintItem(string Id, string? ParentId, PlacementHintContract Hint, bool IsGroup);

    internal static bool Apply(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes,
        IReadOnlyList<DiagramGridLayout.GridFrame>? CurrentFrames = null)
    {
        if (Nodes.Count == 0) return false;

        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        Dictionary<string, GroupContract> Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        Dictionary<string, DiagramGridLayout.GridFrame> FrameById = CurrentFrames?.ToDictionary(Frame => Frame.Id, StringComparer.Ordinal)
            ?? new Dictionary<string, DiagramGridLayout.GridFrame>(StringComparer.Ordinal);
        Dictionary<string, (Bounds Members, Bounds Frame)> InitialGroupGeometry = new(StringComparer.Ordinal);
        foreach (GroupContract Group in Contract.Groups)
        {
            List<DiagramGridLayout.GridNode> Members = UnitNodes(Group.Id, true, Nodes, ById, Groups);
            if (Members.Count > 0 && FrameById.TryGetValue(Group.Id, out DiagramGridLayout.GridFrame? Frame))
                InitialGroupGeometry[Group.Id] = (Measure(Members), FromFrame(Frame));
        }
        List<HintItem> Hints = Contract.Nodes
            .Where(Node => Node.PlacementHint?.IsActive == true && ById.ContainsKey(Node.Id))
            .Select(Node => new HintItem(Node.Id, Node.ParentGroupId, Node.PlacementHint!, false))
            .Concat(Contract.Groups
                .Where(Group => Group.PlacementHint?.IsActive == true)
                .Select(Group => new HintItem(Group.Id, Group.ParentGroupId, Group.PlacementHint!, true)))
            .OrderBy(Item => Item.Hint.SourceLine)
            .ToList();
        if (Hints.Count == 0) return false;

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
            if (Item.IsGroup && InitialGroupGeometry.TryGetValue(Item.Id, out var OwnerGeometry))
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
                Dependents = new List<string>();
                Attached[PeerId] = Dependents;
            }
            Dependents.Add(Item.Id);
            Bounds? PeerBounds = InitialGroupGeometry.TryGetValue(PeerId, out var PeerGeometry)
                ? FollowMembers(PeerGeometry.Members, PeerGeometry.Frame, Measure(Peer))
                : FrameById.TryGetValue(PeerId, out DiagramGridLayout.GridFrame? PeerFrame)
                    ? FromFrame(PeerFrame) : Peer.Count > 0 ? Measure(Peer) : null;
            Changed |= PlaceRelative(Item.Hint, Moving, OwnerBounds, PeerBounds, Nodes);
        }
        return Changed;
    }

    private static bool PlaceRelative(
        PlacementHintContract Hint,
        List<DiagramGridLayout.GridNode> Moving,
        Bounds Owner,
        Bounds? PeerBounds,
        List<DiagramGridLayout.GridNode> All)
    {
        Bounds Anchor = PeerBounds ?? throw new InvalidOperationException("Placement peer has no measured bounds.");
        double TargetX = Owner.CenterX;
        double TargetY = Owner.CenterY;
        int Gap = DiagramGridMetrics.NODE_GAP_PX;
        int Extra = Hint.Offset * DiagramGridMetrics.GRID_UNIT_PX;
        int XSign = 0, YSign = 0;
        switch (Hint.Kind)
        {
            case PlacementHintKind.Above:
                TargetX = Anchor.CenterX;
                TargetY = Anchor.Top - Gap - Extra - Owner.Height / 2.0;
                YSign = -1;
                break;
            case PlacementHintKind.Below:
                TargetX = Anchor.CenterX;
                TargetY = Anchor.Bottom + Gap + Extra + Owner.Height / 2.0;
                YSign = 1;
                break;
            case PlacementHintKind.LeftOf:
                TargetX = Anchor.Left - Gap - Extra - Owner.Width / 2.0;
                TargetY = Anchor.CenterY;
                XSign = -1;
                break;
            case PlacementHintKind.RightOf:
                TargetX = Anchor.Right + Gap + Extra + Owner.Width / 2.0;
                TargetY = Anchor.CenterY;
                XSign = 1;
                break;
        }

        double Dx = SnapDelta(TargetX - Owner.CenterX);
        double Dy = SnapDelta(TargetY - Owner.CenterY);
        HashSet<string> MovingIds = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        while (OverlapsAfterMove(Moving, All, MovingIds, Dx, Dy))
        {
            Dx += XSign * DiagramGridMetrics.GRID_UNIT_PX;
            Dy += YSign * DiagramGridMetrics.GRID_UNIT_PX;
        }
        return Translate(Moving, Dx, Dy);
    }

    private static bool PushToBoundary(
        HintItem Item,
        List<DiagramGridLayout.GridNode> Moving,
        List<DiagramGridLayout.GridNode> All,
        IReadOnlyDictionary<string, GroupContract> Groups)
    {
        HashSet<string> MovingIds = Moving.Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
        List<DiagramGridLayout.GridNode> Siblings = All.Where(Node => !MovingIds.Contains(Node.Id)
            && string.Equals(ImmediateParent(Node, Groups), Item.ParentId, StringComparison.Ordinal)).ToList();
        if (Siblings.Count == 0) return false;

        Bounds Owner = Measure(Moving);
        Bounds Other = Measure(Siblings);
        int Gap = DiagramGridMetrics.NODE_GAP_PX;
        double Dx = 0.0, Dy = 0.0;
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
        var Ids = new HashSet<string>(StringComparer.Ordinal);
        var Pending = new Stack<string>();
        Pending.Push(Id);
        while (Pending.Count > 0)
        {
            string Current = Pending.Pop();
            if (!Ids.Add(Current)) continue;
            if (Attached.TryGetValue(Current, out List<string>? Children))
                foreach (string Child in Children) Pending.Push(Child);
        }

        var Result = new List<DiagramGridLayout.GridNode>();
        foreach (string UnitId in Ids)
        {
            bool UnitIsGroup = Groups.ContainsKey(UnitId) || (UnitId == Id && IsGroup);
            foreach (DiagramGridLayout.GridNode Node in UnitNodes(UnitId, UnitIsGroup, All, ById, Groups))
                if (!Result.Contains(Node)) Result.Add(Node);
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
            return ById.TryGetValue(Id, out DiagramGridLayout.GridNode? Node)
                ? new List<DiagramGridLayout.GridNode> { Node } : new List<DiagramGridLayout.GridNode>();

        HashSet<string> DeclaredMembers = new(StringComparer.Ordinal);
        HashSet<string> VisitedGroups = new(StringComparer.Ordinal);
        Stack<string> PendingGroups = new();
        PendingGroups.Push(Id);
        while (PendingGroups.Count > 0)
        {
            string Current = PendingGroups.Pop();
            if (!VisitedGroups.Add(Current) || !Groups.TryGetValue(Current, out GroupContract? Group)) continue;
            foreach (string ChildId in Group.ChildNodeIds) DeclaredMembers.Add(ChildId);
            foreach (GroupContract Child in Groups.Values.Where(Child =>
                string.Equals(Child.ParentGroupId, Current, StringComparison.Ordinal)))
                PendingGroups.Push(Child.Id);
            foreach (string ChildGroupId in Group.ChildGroupIds) PendingGroups.Push(ChildGroupId);
        }

        return All.Where(Node => DeclaredMembers.Contains(Node.Id) || IsInGroup(Node, Id, Groups)).ToList();
    }

    private static bool IsInGroup(
        DiagramGridLayout.GridNode Node,
        string GroupId,
        IReadOnlyDictionary<string, GroupContract> Groups)
    {
        string? Current = Node.GroupId;
        while (Current != null)
        {
            if (string.Equals(Current, GroupId, StringComparison.Ordinal)) return true;
            Current = Groups.TryGetValue(Current, out GroupContract? Group) ? Group.ParentGroupId : null;
        }
        return false;
    }

    private static bool OverlapsAfterMove(
        IEnumerable<DiagramGridLayout.GridNode> Moving,
        IEnumerable<DiagramGridLayout.GridNode> All,
        HashSet<string> MovingIds,
        double Dx,
        double Dy)
    {
        foreach (DiagramGridLayout.GridNode A in Moving)
        foreach (DiagramGridLayout.GridNode B in All)
        {
            if (MovingIds.Contains(B.Id)) continue;
            if (Math.Abs((A.X + Dx) - B.X) < (A.Width + B.Width) / 2.0 + DiagramGridMetrics.NODE_GAP_PX
                && Math.Abs((A.Y + Dy) - B.Y) < (A.Height + B.Height) / 2.0 + DiagramGridMetrics.NODE_GAP_PX)
                return true;
        }
        return false;
    }

    private static bool Translate(IEnumerable<DiagramGridLayout.GridNode> Nodes, double Dx, double Dy)
    {
        if (Math.Abs(Dx) < 0.1 && Math.Abs(Dy) < 0.1) return false;
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
        DiagramGridLayout.GridNode[] Items = Nodes.ToArray();
        return new Bounds(
            Items.Min(Node => Node.X - Node.Width / 2.0),
            Items.Min(Node => Node.Y - Node.Height / 2.0),
            Items.Max(Node => Node.X + Node.Width / 2.0),
            Items.Max(Node => Node.Y + Node.Height / 2.0));
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
