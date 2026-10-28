using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;

namespace LiveEditor;

internal static partial class PreviewNodePlacement
{
    internal sealed record SnappedPlacement(string PeerId, string Direction, int Offset, PointF Center);

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
                foreach (string Direction in new[] { "left-of", "right-of", "above", "below" })
                {
                    PointF Project(int Offset)
                    {
                        var Owner = Moving.Region; var Anchor = PeerCell.Region;
                        int Row = Direction switch { "above" => Anchor.StartRow - Owner.RowSpan - Offset,
                            "below" => Anchor.EndRow + Offset, _ => Anchor.StartRow };
                        int Column = Direction switch { "left-of" => Anchor.StartColumn - Owner.ColumnSpan - Offset,
                            "right-of" => Anchor.EndColumn + Offset, _ => Anchor.StartColumn };
                        var Bounds = Moving.Bounds(new(Row, Column, Owner.RowSpan, Owner.ColumnSpan), PageCoordinates: true);
                        return new((float)((Shown.Left + Shown.Right) / 2 + Bounds.CenterX - Before.CenterX),
                            (float)((Shown.Top + Shown.Bottom) / 2 + Bounds.CenterY - Before.CenterY));
                    }
                    bool Horizontal = Direction is "left-of" or "right-of";
                    double Axis(PointF Point) => Horizontal ? Point.X : Point.Y;
                    int Sign = Direction is "left-of" or "above" ? -1 : 1;
                    int Low = 0, High = 10000;
                    // Invert authoritative projection, including expanded gaps.
                    while (Low < High)
                    {
                        int Middle = (Low + High) / 2;
                        if (Sign * Axis(Project(Middle)) < Sign * Axis(Drop)) Low = Middle + 1;
                        else High = Middle;
                    }
                    foreach (int Offset in new[] { Math.Max(0, Low - 1), Low })
                    {
                        PointF Center = Project(Offset);
                        double Distance = Math.Pow(Center.X - Drop.X, 2) + Math.Pow(Center.Y - Drop.Y, 2);
                        if (Distance < BestDistance)
                        {
                            BestDistance = Distance;
                            Best = new(Peer.Id, Direction, Offset, Center);
                        }
                    }
                }
            }
            return Best;
        }
    }
}
