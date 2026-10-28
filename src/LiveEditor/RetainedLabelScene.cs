using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;

namespace LiveEditor;

internal sealed record RetainedLabelScene(RetainedRouteCheckpoint Routes, EnterpriseLabelSnapshot Labels)
{
    internal static string Key(DiagramSourceUpdateService.Snapshot Snapshot, string Presentation, FlowchartSvgCoordinateFrame Frame)
    {
        static object? Hint(PlacementHintContract? Hint) => Hint == null ? null : new
            { Hint.Kind, Hint.PeerId, Hint.Offset, Hint.ColumnOffset, Hint.Direction, Hint.CellRow, Hint.CellColumn, Hint.IsActive };
        static object? Ports(RoutingHintContract? Hint) => Hint == null ? null : new
            { Hint.Leave, Hint.Enter, Hint.Travel, Hint.LeavePoint, Hint.EnterPoint, Hint.IsActive };
        string Json = JsonSerializer.Serialize(new
        {
            Presentation, Snapshot.Layout.RenderContext.LayoutOptions.GeometryCacheKey,
            Bounds = RetainedRouteCheckpoint.GeometryKey(Snapshot.Layout), Frame.MinLeft, Frame.MaxTop, Frame.DiagramPaddingPx,
            Nodes = Snapshot.Model.Nodes.OrderBy(Node => Node.Id, StringComparer.Ordinal).Select(Node => new
                { Node.Id, Node.Label, Node.LabelType, Node.Shape, Node.LayoutWidth, Node.LayoutHeight, Placement = Hint(Node.PlacementHint) }),
            Edges = Snapshot.Model.Edges.OrderBy(Edge => Edge.Id, StringComparer.Ordinal).Select(Edge => new
                { Edge.Id, Edge.SourceId, Edge.TargetId, Edge.Label, Edge.LabelType, Edge.MinLength, Edge.MarkerStart, Edge.MarkerEnd,
                    Edge.Stroke, Edge.Curve, Routing = Ports(Edge.RoutingHint), Metadata = Edge.Metadata.OrderBy(Pair => Pair.Key, StringComparer.Ordinal)
                        .Select(Pair => new { Pair.Key, Value = JsonSerializer.Serialize(Pair.Value, Pair.Value.GetType()) }) })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json)));
    }
}
