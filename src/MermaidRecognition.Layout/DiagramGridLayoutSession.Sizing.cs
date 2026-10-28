using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    internal sealed partial class RetainedRoutingState
    {
        private ImmutableDictionary<string, (int Rows, int Columns)> NodeSizeHistory = ImmutableDictionary<string, (int Rows, int Columns)>.Empty;

        private static string SizeRequest(NodeContract Node) => JsonSerializer.Serialize(new
            { Node.Id, Node.Shape, Node.Label, Node.LabelType, Node.LayoutWidth, Node.LayoutHeight });

        private string? PlanSourceNodeSizes(DiagramContract Model, IReadOnlyDictionary<string, FlowchartCellRegion> Regions,
            out Dictionary<string, FlowchartCellRegion> Result)
        {
            Result = new(Regions, StringComparer.Ordinal);
            var Config = Grid.Configuration;
            foreach (var Node in Model.Nodes)
            {
                if (!double.IsFinite(Node.LayoutWidth) || !double.IsFinite(Node.LayoutHeight) || Node.LayoutWidth < 0 || Node.LayoutHeight < 0)
                    return "A resized node requires finite nonnegative measured dimensions.";
                if (!State.Nodes.TryGetValue(Node.Id, out var Old) || SizeRequest(Node) == SizeRequest(Old)) continue;
                var Region = Regions[Node.Id];
                NodeSizeHistory = NodeSizeHistory.SetItem(SizeRequest(Old), (Region.RowSpan, Region.ColumnSpan));
                int Columns = Math.Max(1, checked((int)Math.Ceiling((Node.LayoutWidth + Config.ColumnGap) / (Config.CellWidth + Config.ColumnGap))));
                int Rows = Math.Max(1, checked((int)Math.Ceiling((Node.LayoutHeight + Config.RowGap) / (Config.CellHeight + Config.RowGap))));
                int Grow(bool Horizontal, double Needed, int Upper)
                {
                    int Lower = Horizontal ? Region.ColumnSpan : Region.RowSpan;
                    double Extent(int Span)
                    {
                        var Size = Grid.Measure(new(Region.StartRow, Region.StartColumn,
                            Horizontal ? Region.RowSpan : Span, Horizontal ? Span : Region.ColumnSpan));
                        return Horizontal ? Size.Width : Size.Height;
                    }
                    if (Extent(Lower) >= Needed) return Lower;
                    Upper = Math.Max(Upper, checked(Lower + 1));
                    while (Lower + 1 < Upper)
                    {
                        int Middle = Lower + (Upper - Lower) / 2;
                        if (Extent(Middle) >= Needed) Upper = Middle; else Lower = Middle;
                    }
                    return Upper;
                }
                Columns = Grow(true, Node.LayoutWidth, Columns);
                Rows = Grow(false, Node.LayoutHeight, Rows);
                if (FlowchartDagrePresentation.IsDiamondShape(Node.Shape)) Rows = Math.Max(Rows, checked(Columns * 2));
                var Span = NodeSizeHistory.TryGetValue(SizeRequest(Node), out var Previous) ? Previous
                    : (Rows: Math.Max(Rows, Region.RowSpan), Columns: Math.Max(Columns, Region.ColumnSpan));
                var Size = Grid.Measure(new(Region.StartRow, Region.StartColumn, Span.Rows, Span.Columns));
                if (Size.Width < Node.LayoutWidth || Size.Height < Node.LayoutHeight)
                    return "The prior cell reservation cannot contain the new measured node.";
                if (FlowchartDagrePresentation.IsDiamondShape(Node.Shape))
                {
                    var Owner = State.Placed.Find(Owner => Owner.Id == Node.Id)!;
                    var Body = Owner.CellNode!.Bounds(new(Region.StartRow, Region.StartColumn, Span.Rows, Span.Columns), Node.GeometryProvider);
                    if (Body.Width < Node.LayoutWidth || Body.Height < Node.LayoutHeight)
                        return "The measured node cannot fit its proportional silhouette in the selected cells.";
                }
                Result[Node.Id] = new(Region.StartRow, Region.StartColumn, Span.Rows, Span.Columns);
            }
            return null;
        }
    }
}
