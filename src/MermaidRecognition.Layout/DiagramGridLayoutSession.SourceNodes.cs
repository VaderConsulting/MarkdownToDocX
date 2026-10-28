using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    internal sealed partial class RetainedRoutingState
    {
        private string? AddSourceNodes(DiagramContract Model)
        {
            var Config = Grid.Configuration;
            foreach (var Node in Model.Nodes.Where(Node => !State.Nodes.ContainsKey(Node.Id)).OrderBy(Node => Node.Id, StringComparer.Ordinal))
            {
                if (!string.IsNullOrEmpty(Node.ParentGroupId) || Model.Groups.Count != 0)
                    return "Grouped node insertion requires a containment construction operation.";
                if (!double.IsFinite(Node.LayoutWidth) || !double.IsFinite(Node.LayoutHeight) || Node.LayoutWidth < 0 || Node.LayoutHeight < 0)
                    return "The added node requires finite nonnegative measured dimensions.";
                int Columns = Math.Max(1, checked((int)Math.Ceiling((Node.LayoutWidth + Config.ColumnGap) / (Config.CellWidth + Config.ColumnGap))));
                int Rows = Math.Max(1, checked((int)Math.Ceiling((Node.LayoutHeight + Config.RowGap) / (Config.CellHeight + Config.RowGap))));
                bool Diamond = FlowchartDagrePresentation.IsDiamondShape(Node.Shape);
                if (Diamond) Rows = Math.Max(Rows, checked(2 * Columns));
                var Regions = Grid.Capture().Regions;
                int Right = Regions.Count == 0 ? 0 : checked(Regions.Values.Max(Region => Region.EndColumn) + 1);
                var RowCandidates = Regions.Values.Select(Region => Region.StartRow)
                    .Concat(Regions.Values.Select(Region => checked(Region.EndRow + 1)))
                    .Prepend(Regions.Count == 0 ? 0 : checked(Regions.Values.Max(Region => Region.EndRow) + 1))
                    .Prepend(Regions.Count == 0 ? 0 : checked(Regions.Values.Min(Region => Region.StartRow) - Rows - 1))
                    .Prepend(Node.PlacementHint is { IsActive: true, Kind: PlacementHintKind.At } Pin ? Pin.CellRow : 0)
                    .Distinct().Take(64);
                FlowchartCellRegion? Selected = null;
                foreach (int Row in RowCandidates)
                {
                    var Region = new FlowchartCellRegion(Row, Right, Rows, Columns);
                    var Proposal = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal) { [Node.Id] = Region };
                    if (!Enterprise.EnterpriseBandPlacement.RetainedAssignmentIsLegal(Model, Proposal) || !Grid.TryPlace(Node.Id, Region)) continue;
                    Selected = Region; break;
                }
                if (Selected == null) return "No legal local cell reservation was found for the added node within the construction budget.";
                var Owner = new GridNode { Id = Node.Id, Shape = Node.Shape, IsDiamond = Diamond, GeometryProvider = Node.GeometryProvider };
                Owner.BindCells(Grid, Layout.RenderContext);
                Owner.CellNode!.UseRoutingCoordinates(State.Placed.FirstOrDefault()?.CellNode?.RoutingOrigin ?? 0);
                State.Placed.Add(Owner);
                Layout.NodeBoundsMutable.Add(Owner.CellNode.Bounds(Owner.GeometryProvider));
            }
            return null;
        }
    }
}
