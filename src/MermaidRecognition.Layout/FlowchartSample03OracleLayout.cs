using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>
/// Hand-made reference placement for enterprise sample 03.
/// Member nodes keep their Sugiyama sizes and positions. External nodes are placed so that
/// no edge pierces a node and no edge cuts a foreign group diagonally.
/// </summary>
public static class FlowchartSample03OracleLayout
{
    private const double NODE_GAP_PX = 28.0;

    private const double GROUP_GAP_PX = 36.0;

    /// <summary>
    /// True when the contract has the sample 03 node identifiers.
    /// </summary>
    /// <param name="Contract">Parsed diagram.</param>
    /// <returns>True when every sample 03 id is present.</returns>
    public static bool MatchesSample03(DiagramContract Contract)
    {
        if (Contract == null)
        {
            return false;
        }

        HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            Ids.Add(Node.Id);
        }

        foreach (GroupContract Group in Contract.Groups)
        {
            Ids.Add(Group.Id);
        }

        string[] Required =
        [
            "dc",
            "appNode",
            "jvm",
            "war",
            "pg",
            "lb",
            "lan",
            "webShop",
            "checkout",
            "shopUi",
            "ordering",
            "order",
            "hosting",
            "dbSvc",
            "orderTbl"
        ];
        foreach (string Id in Required)
        {
            if (!Ids.Contains(Id))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the hand-made sample 03 layout from engine-measured sizes.
    /// </summary>
    /// <param name="Contract">Parsed sample 03 contract.</param>
    /// <param name="Measured">Engine-measured sizes and member positions.</param>
    /// <returns>Oracle layout, or the measured layout when the diagram is not sample 03.</returns>
    public static FlowchartLayoutResult Build(
        DiagramContract Contract,
        IReadOnlyList<FlowchartLayoutNodeBounds> Measured)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> ById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Measured)
        {
            ById[Bounds.NodeId] = Bounds;
        }

        if (!MatchesSample03(Contract))
        {
            return ToResult(Measured);
        }

        FlowchartCompoundEndpointHelper.AddGroupBounds(Contract, ById);
        if (!ById.TryGetValue("dc", out FlowchartLayoutNodeBounds DataCentre)
            || !ById.TryGetValue("webShop", out FlowchartLayoutNodeBounds WebShop)
            || !ById.TryGetValue("pg", out FlowchartLayoutNodeBounds PostgreSql)
            || !ById.TryGetValue("order", out FlowchartLayoutNodeBounds Order)
            || !ById.TryGetValue("shopUi", out FlowchartLayoutNodeBounds ShopUi)
            || !ById.TryGetValue("hosting", out FlowchartLayoutNodeBounds Hosting)
            || !ById.TryGetValue("dbSvc", out FlowchartLayoutNodeBounds DatabaseService)
            || !ById.TryGetValue("orderTbl", out FlowchartLayoutNodeBounds OrdersTable))
        {
            return ToResult(Measured);
        }

        double DataCentreLeft = DataCentre.CenterX - (DataCentre.Width / 2.0);
        double DataCentreRight = DataCentre.CenterX + (DataCentre.Width / 2.0);
        double DataCentreBottom = DataCentre.CenterY - (DataCentre.Height / 2.0);
        double WebShopRight = WebShop.CenterX + (WebShop.Width / 2.0);

        Relocate(
            ById,
            "shopUi",
            WebShopRight + GROUP_GAP_PX + (ShopUi.Width / 2.0),
            WebShop.CenterY);

        Relocate(
            ById,
            "hosting",
            DataCentreLeft - GROUP_GAP_PX - (Hosting.Width / 2.0),
            WebShop.CenterY);

        Relocate(
            ById,
            "dbSvc",
            PostgreSql.CenterX,
            DataCentreBottom - GROUP_GAP_PX - (DatabaseService.Height / 2.0));

        double OrdersTableCenterX = Math.Max(
            Order.CenterX,
            DataCentreRight + GROUP_GAP_PX + (OrdersTable.Width / 2.0));
        double OrdersTableCenterY = Order.CenterY
            - (Order.Height / 2.0)
            - NODE_GAP_PX
            - (OrdersTable.Height / 2.0);
        Relocate(ById, "orderTbl", OrdersTableCenterX, OrdersTableCenterY);

        if (ById.TryGetValue("ordering", out FlowchartLayoutNodeBounds Ordering))
        {
            FlowchartLayoutNodeBounds PlacedShopUi = ById["shopUi"];
            double ShopUiRight = PlacedShopUi.CenterX + (PlacedShopUi.Width / 2.0);
            double OrderingLeft = Ordering.CenterX - (Ordering.Width / 2.0);
            if (OrderingLeft < ShopUiRight + NODE_GAP_PX
                && Math.Abs(Ordering.CenterY - PlacedShopUi.CenterY) < (Ordering.Height + PlacedShopUi.Height))
            {
                Relocate(
                    ById,
                    "ordering",
                    ShopUiRight + NODE_GAP_PX + (Ordering.Width / 2.0),
                    Order.CenterY);
            }
        }

        List<FlowchartLayoutNodeBounds> ResultBounds = [];
        foreach (FlowchartLayoutNodeBounds Original in Measured)
        {
            if (FlowchartCompoundEndpointHelper.IsGroupId(Contract, Original.NodeId))
            {
                ResultBounds.Add(Original);
                continue;
            }

            ResultBounds.Add(ById[Original.NodeId]);
        }

        return ToResult(ResultBounds);
    }

    /// <summary>
    /// Formats the oracle centres for diagnostics.
    /// </summary>
    /// <param name="Layout">Oracle layout.</param>
    /// <returns>One line per node.</returns>
    public static string FormatCentres(FlowchartLayoutResult Layout)
    {
        StringBuilder Builder = new StringBuilder();
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            Builder.Append(Bounds.NodeId);
            Builder.Append(" (");
            Builder.Append(Bounds.CenterX.ToString("0.0", CultureInfo.InvariantCulture));
            Builder.Append(", ");
            Builder.Append(Bounds.CenterY.ToString("0.0", CultureInfo.InvariantCulture));
            Builder.AppendLine(")");
        }

        return Builder.ToString();
    }

    private static void Relocate(
        Dictionary<string, FlowchartLayoutNodeBounds> ById,
        string NodeId,
        double CenterX,
        double CenterY)
    {
        FlowchartLayoutNodeBounds Current = ById[NodeId];
        ById[NodeId] = new FlowchartLayoutNodeBounds
        {
            NodeId = Current.NodeId,
            CenterX = CenterX,
            CenterY = CenterY,
            Width = Current.Width,
            Height = Current.Height
        };
    }

    private static FlowchartLayoutResult ToResult(IReadOnlyList<FlowchartLayoutNodeBounds> Bounds)
    {
        (double GraphWidth, double GraphHeight) = FlowchartDirectionRemapper.ComputeGraphSize(Bounds);
        FlowchartLayoutResult Result = new FlowchartLayoutResult
        {
            GraphWidth = GraphWidth,
            GraphHeight = GraphHeight
        };
        Result.NodeBoundsMutable.AddRange(Bounds);
        return Result;
    }
}
