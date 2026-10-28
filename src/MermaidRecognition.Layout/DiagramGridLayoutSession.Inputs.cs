using System;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    // Deliberately a routing-input copy, not a replacement for the editor's parsed document.
    // Source declarations, interactions and presentation styles stay with that document.
    internal static DiagramContract CopyRoutingInputs(DiagramContract Source)
    {
        DiagramContract Copy = new()
        {
            Kind = Source.Kind, Header = Source.Header, Direction = Source.Direction,
            LayoutMode = Source.LayoutMode, SourceHash = Source.SourceHash
        };
        foreach (var Pair in Source.Configuration.EffectiveValues) { Copy.Configuration.EffectiveValuesMutable.Add(Pair.Key, Pair.Value); }
        foreach (NodeContract Node in Source.Nodes) { Copy.NodesMutable.Add(CopyRoutingNode(Node)); }
        foreach (EdgeContract Edge in Source.Edges) { Copy.EdgesMutable.Add(CopyRoutingEdge(Edge)); }
        foreach (GroupContract Group in Source.Groups)
        {
            GroupContract Item = new()
            {
                Id = Group.Id, Title = Group.Title, LabelType = Group.LabelType, Direction = Group.Direction,
                ParentGroupId = Group.ParentGroupId, PlacementHint = CopyPlacementHint(Group.PlacementHint)
            };
            Item.ChildNodeIdsMutable.AddRange(Group.ChildNodeIds);
            Item.ChildGroupIdsMutable.AddRange(Group.ChildGroupIds);
            Copy.GroupsMutable.Add(Item);
        }
        return Copy;
    }

    private static NodeContract CopyRoutingNode(NodeContract Node)
    {
        NodeContract Item = new()
        {
            Id = Node.Id, Label = Node.Label, LabelType = Node.LabelType, Shape = Node.Shape,
            ShapeSource = Node.ShapeSource, ParentGroupId = Node.ParentGroupId,
            LayoutWidth = Node.LayoutWidth, LayoutHeight = Node.LayoutHeight,
            GeometryProvider = Node.GeometryProvider, PlacementHint = CopyPlacementHint(Node.PlacementHint)
        };
        Item.ClassesMutable.AddRange(Node.Classes);
        foreach (var Pair in Node.Metadata) { Item.MetadataMutable.Add(Pair.Key, Pair.Value); }
        return Item;
    }

    private static EdgeContract CopyRoutingEdge(EdgeContract Edge)
    {
        EdgeContract Item = new()
        {
            Id = Edge.Id, IsUserDefinedId = Edge.IsUserDefinedId, SourceId = Edge.SourceId,
            TargetId = Edge.TargetId, MarkerStart = Edge.MarkerStart, MarkerEnd = Edge.MarkerEnd,
            Stroke = Edge.Stroke, MinLength = Edge.MinLength, Label = Edge.Label, LabelType = Edge.LabelType,
            Curve = Edge.Curve, CreationIndex = Edge.CreationIndex,
            RoutingHint = Edge.RoutingHint is { } Hint && !(Hint.IsActive && Hint.Leave == null && Hint.Enter == null
                && Hint.Travel == null && Hint.LeavePoint == null && Hint.EnterPoint == null) ? new()
            {
                Leave = Hint.Leave, Enter = Hint.Enter, Travel = Hint.Travel, SourceLine = Hint.SourceLine,
                LeavePoint = Hint.LeavePoint, EnterPoint = Hint.EnterPoint,
                IsActive = Hint.IsActive, UnhonouredReason = Hint.UnhonouredReason
            } : null
        };
        Item.ClassesMutable.AddRange(Edge.Classes);
        foreach (var Pair in Edge.Metadata) { Item.MetadataMutable.Add(Pair.Key, Pair.Value); }
        return Item;
    }

    private static PlacementHintContract? CopyPlacementHint(PlacementHintContract? Hint) => Hint == null ? null : new()
    {
        Kind = Hint.Kind, PeerId = Hint.PeerId, Offset = Hint.Offset, ColumnOffset = Hint.ColumnOffset, Direction = Hint.Direction,
        CellRow = Hint.CellRow, CellColumn = Hint.CellColumn,
        SourceLine = Hint.SourceLine, IsActive = Hint.IsActive, UnhonouredReason = Hint.UnhonouredReason
    };
}
