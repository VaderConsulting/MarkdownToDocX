using System;
using System.Collections.Generic;
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
        List<GridFrame> Frames, bool Horizontal)
    {
        BuildFrames(Contract, Placed, Frames);
        var Roots = Frames.Where(Frame => ParentGroup(Contract, Frame.Id) == null).ToList();
        bool Overlapping = Roots.Any(First => Roots.Any(Second => First != Second
            && First.Left < Second.Right && First.Right > Second.Left
            && First.Top < Second.Bottom && First.Bottom > Second.Top));
        if (!Overlapping) return;

        // Global ranks can scatter a container's members between other containers.
        // Close those empty strips locally, then move each container as one unit.
        foreach (var Root in Roots)
        {
            var Members = Placed.Where(Node => IsInsideGroup(Contract, Node.Id, Root.Id)).ToList();
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
                if (FlowchartDagrePlacement.TryPlace(Local, out var LocalRaw))
                {
                    var LocalLayout = ApplyForGroupReflow(Local, LocalRaw);
                    foreach (var Bounds in LocalLayout.NodeBounds)
                    {
                        var Member = Members.Find(Node => Node.Id == Bounds.NodeId)!;
                        Member.X = Bounds.CenterX;
                        Member.Y = -Bounds.CenterY;
                    }
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
            FitFrames(Contract, Members, new List<GridFrame>(), Horizontal);
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
        foreach (var Node in Placed) { Node.OldX = Node.X; Node.OldY = Node.Y; }
        AssignRanks(Placed, Horizontal);
        Frames.Clear();
    }

    private static void FitFrames(DiagramContract Contract, List<GridNode> Placed, List<GridFrame> Frames, bool Horizontal)
    {
        HashSet<string> SeenStates = new(StringComparer.Ordinal) { PositionSignature(Placed) };
        while (true)
        {
            Frames.Clear();
            BuildFrames(Contract, Placed, Frames);
            // Keep sibling containers intact before clearing individual outsiders.
            // Evicting their members first can stretch both frames repeatedly.
            if (SeparateSiblingFrames(Contract, Placed, Frames, Horizontal))
            {
                if (SeenStates.Add(PositionSignature(Placed))) continue;
                break;
            }
            bool Moved = false;
            foreach (GridFrame Frame in Frames)
            {
                foreach (GridNode Node in Placed)
                {
                    if (IsInsideGroup(Contract, Node.Id, Frame.Id))
                    {
                        continue;
                    }

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

                    if (TryEvictNodeFromFrame(Contract, Placed, Node, Frame, Clearance))
                    {
                        Moved = true;
                        break;
                    }

                    int Push = DiagramGridMetrics.GRID_UNIT_PX;
                    if (Main(Node, Horizontal, Old: false) >= Frame.CenterMain(Horizontal))
                    {
                        foreach (GridNode Later in Placed)
                        {
                            if (Later.Rank >= Node.Rank)
                            {
                                SetMain(Later, (int)Math.Round(Main(Later, Horizontal, Old: false)) + Push, Horizontal);
                            }
                        }
                    }
                    else
                    {
                        foreach (GridNode Member in Placed)
                        {
                            if (IsInsideGroup(Contract, Member.Id, Frame.Id) || Member.Rank > Node.Rank)
                            {
                                SetMain(Member, (int)Math.Round(Main(Member, Horizontal, Old: false)) + Push, Horizontal);
                            }
                        }
                    }

                    Moved = true;
                    break;
                }

                if (Moved)
                {
                    break;
                }
            }

            bool SeparatedSiblingFrames = !Moved && SeparateSiblingFrames(Contract, Placed, Frames, Horizontal);
            if ((!Moved && !SeparatedSiblingFrames) || !SeenStates.Add(PositionSignature(Placed))) break;
        }
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
            int Strip = DiagramGridMetrics.GRID_UNIT_PX;
            double LineHeight = Options.FontSizePx * DiagramGridMetrics.TEXT_LINE_HEIGHT_EM * Title.Split('\n').Length;
            if (LineHeight > Strip)
            {
                Strip = DiagramGridMetrics.CeilToGrid(LineHeight);
            }
            int Padding = DiagramGridMetrics.NODE_GAP_PX;
            if (Group.ChildGroupIds.Count > 0)
                Padding = Math.Max(Padding, DiagramGridMetrics.CeilToGrid(
                    FlowchartSubgraphGapThresholds.ResolveNestedFlowAxisGapPx(Options)));
            int InternalReturns = FlowchartBackEdgeHelper.FindBackEdges(Contract).Count(Edge =>
                IsInsideGroup(Contract, Edge.SourceId, Group.Id) && IsInsideGroup(Contract, Edge.TargetId, Group.Id));
            if (InternalReturns > 0)
                Padding += DiagramGridMetrics.CeilToGrid(DiagramGridRouteBuilder.CORNER_RADIUS_PX + 6 * InternalReturns);
            int BoundaryPadding = FlowchartCompoundEndpointHelper.InternalBoundaryPadding(Contract, Group.Id);
            int FrameLeft = (int)Math.Floor(Left) - Math.Max(Padding, BoundaryPadding);
            int FrameRight = (int)Math.Ceiling(Right) + Math.Max(Padding, BoundaryPadding);
            int FrameTop = (int)Math.Floor(Top) - Padding - Strip;
            int FrameBottom = (int)Math.Ceiling(Bottom) + Math.Max(Padding,
                FlowchartCompoundEndpointHelper.InternalBoundaryPadding(Contract, Group.Id));
            int NeededWidth = DiagramGridMetrics.CeilToGrid(TitleWidth + (2.0 * Padding));
            int HaveWidth = FrameRight - FrameLeft;
            if (NeededWidth > HaveWidth)
            {
                int Extra = NeededWidth - HaveWidth;
                FrameLeft -= Extra / 2;
                FrameRight += Extra - (Extra / 2);
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
        int Strip = Math.Max(DiagramGridMetrics.GRID_UNIT_PX,
            DiagramGridMetrics.CeilToGrid(Options.FontSizePx * DiagramGridMetrics.TEXT_LINE_HEIGHT_EM * Lines.Length));
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

        return new GridFrame
        {
            Id = Group.Id,
            Left = DiagramGridMetrics.RoundToGrid(Left),
            Top = DiagramGridMetrics.RoundToGrid(Top),
            Right = DiagramGridMetrics.RoundToGrid(Left) + Width,
            Bottom = DiagramGridMetrics.RoundToGrid(Top) + Height,
            TitleStrip = Strip,
            Padding = Padding
        };
    }

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
