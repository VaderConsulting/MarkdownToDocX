using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

internal static partial class PreviewNodePlacement
{
    internal sealed record SnappedPlacement(string? PeerId, string Direction, int Offset, PointF Center, int? ColumnOffset = null,
        int? CellRow = null, int? CellColumn = null);

    /// <summary>Projects hint-representable whole-cell moves without placement or routing.</summary>
    internal sealed class GridSnap(string Source, IReadOnlyList<PreviewObject> Items, FlowchartLayoutResult Layout)
    {
        private readonly EnterpriseDocument Document = EnterpriseValidator.Analyze(Source).Document;
        private readonly Dictionary<string, FlowchartCellNode> Cells = (Layout.CellNodes ?? [])
            .Where(Node => Node.CellNode != null).ToDictionary(Node => Node.Id, Node => Node.CellNode!, StringComparer.Ordinal);

        internal SnappedPlacement? Snap(string NodeId, PointF Drop)
        {
            if (!float.IsFinite(Drop.X) || !float.IsFinite(Drop.Y) || !Cells.TryGetValue(NodeId, out var Moving)
                || !Document.Elements.TryGetValue(NodeId, out var Element)) return null;
            var Shown = Items.FirstOrDefault(Item => Item.Kind == PreviewObjectKind.Node && Item.Id == NodeId);
            if (Shown == null) return null;
            var Before = Moving.Bounds(Moving.Region, PageCoordinates: true);
            SnappedPlacement? Best = null;
            double BestDistance = double.PositiveInfinity;
            foreach (var Peer in Items.Where(Item => Item.Kind == PreviewObjectKind.Node && Item.Id != NodeId)
                .OrderBy(Item => Item.Id, StringComparer.Ordinal))
            {
                if (!Cells.TryGetValue(Peer.Id, out var PeerCell) || !Document.Elements.TryGetValue(Peer.Id, out var Other)
                    || Document.Groups.ContainsKey(Peer.Id) || Other.ParentId != Element.ParentId
                    || EnterpriseElementCatalog.Elements[Other.ElementType].Layer != EnterpriseElementCatalog.Elements[Element.ElementType].Layer
                    || DependsOn(Document, Peer.Id, NodeId)) continue;
                foreach (string Direction in new[] { "left-of", "right-of", "above", "below", "NW", "NE", "SE", "SW" })
                {
                    bool Diagonal = Direction.Length == 2;
                    PointF Project(int Offset, int? ColumnOffset = null)
                    {
                        var Owner = Moving.Region; var Anchor = PeerCell.Region;
                        int Row = Direction switch { "above" or "NW" or "NE" => checked(Anchor.StartRow - Owner.RowSpan - Offset),
                            "below" or "SE" or "SW" => checked(Anchor.EndRow + Offset), _ => Anchor.StartRow };
                        int Column = Direction switch { "left-of" or "NW" or "SW" => checked(Anchor.StartColumn - Owner.ColumnSpan - (ColumnOffset ?? Offset)),
                            "right-of" or "NE" or "SE" => checked(Anchor.EndColumn + (ColumnOffset ?? Offset)), _ => Anchor.StartColumn };
                        var Bounds = Moving.Bounds(new(Row, Column, Owner.RowSpan, Owner.ColumnSpan), PageCoordinates: true);
                        return new((float)((Shown.Left + Shown.Right) / 2 + Bounds.CenterX - Before.CenterX),
                            (float)((Shown.Top + Shown.Bottom) / 2 + Bounds.CenterY - Before.CenterY));
                    }
                    int Nearest(bool Horizontal, int Sign, Func<int, PointF> Projection)
                    {
                        double Axis(PointF Point) => Horizontal ? Point.X : Point.Y;
                        int Low = 0, High = 10000;
                        // Invert each authoritative axis, including expanded gaps.
                        while (Low < High)
                        {
                            int Middle = (Low + High) / 2;
                            if (Sign * Axis(Projection(Middle)) < Sign * Axis(Drop)) Low = Middle + 1;
                            else High = Middle;
                        }
                        int Previous = Math.Max(0, Low - 1);
                        return Math.Abs(Axis(Projection(Previous)) - Axis(Drop)) <= Math.Abs(Axis(Projection(Low)) - Axis(Drop))
                            ? Previous : Low;
                    }
                    int Offset = Diagonal ? Nearest(false, Direction[0] == 'N' ? -1 : 1, Value => Project(Value, 0))
                        : Nearest(Direction is "left-of" or "right-of", Direction is "left-of" or "above" ? -1 : 1, Value => Project(Value));
                    int? HorizontalOffset = Diagonal ? Nearest(true, Direction[1] == 'W' ? -1 : 1, Value => Project(0, Value)) : null;
                    PointF Center = Project(Offset, HorizontalOffset);
                    double Distance = Math.Pow(Center.X - Drop.X, 2) + Math.Pow(Center.Y - Drop.Y, 2);
                    if (Distance < BestDistance)
                    {
                        BestDistance = Distance;
                        Best = new(Peer.Id, Direction, Offset, Center, HorizontalOffset == Offset ? null : HorizontalOffset);
                    }
                }
            }
            return Best ?? SnapAbsolute(Moving, Shown, Drop);
        }

        private static SnappedPlacement SnapAbsolute(FlowchartCellNode Moving, PreviewObject Shown, PointF Drop)
        {
            var Region = Moving.Region;
            var Before = Moving.Bounds(Region, PageCoordinates: true);
            PointF Project(int Row, int Column)
            {
                var Box = Moving.Bounds(new(Row, Column, Region.RowSpan, Region.ColumnSpan), PageCoordinates: true);
                return new((float)((Shown.Left + Shown.Right) / 2 + Box.CenterX - Before.CenterX),
                    (float)((Shown.Top + Shown.Bottom) / 2 + Box.CenterY - Before.CenterY));
            }
            int Nearest(bool Horizontal)
            {
                int Start = Horizontal ? Region.StartColumn : Region.StartRow;
                int Span = Horizontal ? Region.ColumnSpan : Region.RowSpan;
                int Minimum = (int)Math.Max(int.MinValue, (long)Start - 10000);
                int Low = Minimum, High = (int)Math.Min((long)int.MaxValue - Span, (long)Start + 10000);
                double Axis(int Value) => Horizontal ? Project(Region.StartRow, Value).X : Project(Value, Region.StartColumn).Y;
                double Target = Horizontal ? Drop.X : Drop.Y;
                while (Low < High)
                {
                    int Middle = Low + (High - Low) / 2;
                    if (Axis(Middle) < Target) Low = Middle + 1; else High = Middle;
                }
                int Previous = Low > Minimum ? Low - 1 : Low;
                return Math.Abs(Axis(Previous) - Target) <= Math.Abs(Axis(Low) - Target) ? Previous : Low;
            }
            int Row = Nearest(false), Column = Nearest(true);
            return new(null, "at", 0, Project(Row, Column), CellRow: Row, CellColumn: Column);
        }
    }
}
