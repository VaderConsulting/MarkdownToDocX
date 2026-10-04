using System;
using System.Collections.Generic;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Layout-to-SVG coordinate frame used by <see cref="FlowchartSugiyamaSvgRenderer"/>.
/// </summary>
public sealed class FlowchartSvgCoordinateFrame
{
    public FlowchartLayoutResult Layout { get; init; } = new FlowchartLayoutResult();

    public double MinLeft { get; init; }

    public double MaxTop { get; init; }

    public double DiagramPaddingPx { get; init; }

    /// <summary>
    /// Converts one layout-space point to SVG user coordinates.
    /// </summary>
    public (double X, double Y) LayoutToSvg(double LayoutX, double LayoutY)
    {
        double SvgX = DiagramPaddingPx + (LayoutX - MinLeft);
        double SvgY = DiagramPaddingPx + (MaxTop - LayoutY);
        return (SvgX, SvgY);
    }
}

/// <summary>
/// Resolves the SVG coordinate frame for a laid-out flowchart diagram.
/// </summary>
public static class FlowchartSvgCoordinateFrameResolver
{
    /// <summary>
    /// Builds the same MinLeft/MaxTop frame used when rendering SVG output.
    /// </summary>
    public static bool TryResolve(DiagramContract Contract, out FlowchartSvgCoordinateFrame Frame)
    {
        Frame = new FlowchartSvgCoordinateFrame();

        if (Contract.Nodes.Count == 0)
        {
            return false;
        }

        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
        {
            return false;
        }

        return TryResolve(Contract, Layout, out Frame);
    }

    /// <summary>
    /// Builds the same MinLeft/MaxTop frame used when rendering SVG output, from a layout the caller already has.
    /// </summary>
    /// <param name="Contract">The diagram that was laid out.</param>
    /// <param name="Layout">The layout result for <paramref name="Contract"/>.</param>
    /// <param name="Frame">The resolved frame, or an empty frame when the layout has no nodes.</param>
    /// <returns>True when a frame was built.</returns>
    public static bool TryResolve(DiagramContract Contract, FlowchartLayoutResult Layout, out FlowchartSvgCoordinateFrame Frame)
    {
        Frame = new FlowchartSvgCoordinateFrame();

        if (Layout.NodeBounds.Count == 0)
        {
            return false;
        }

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        double DiagramPaddingPx = RenderOptions.DiagramPaddingPx;

        Dictionary<string, NodeContract> NodeById =
            new Dictionary<string, NodeContract>(StringComparer.Ordinal);

        foreach (NodeContract NodeEntry in Contract.Nodes)
        {
            NodeById[NodeEntry.Id] = NodeEntry;
        }

        double MinLeft = double.MaxValue;
        double MinBottom = double.MaxValue;
        double MaxRight = double.MinValue;
        double MaxTop = double.MinValue;

        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            string? Shape = NodeById.TryGetValue(Bounds.NodeId, out NodeContract? BoundsNode) ? BoundsNode.Shape : null;
            double LeftExtension = FlowchartShapeLayoutHelper.GetLeftExtensionPx(Shape, Bounds.Height);
            double RightExtension = FlowchartShapeLayoutHelper.GetRightExtensionPx(Shape, Bounds.Height);
            double Left = Bounds.CenterX - (Bounds.Width / 2.0) - LeftExtension;
            double Right = Bounds.CenterX + (Bounds.Width / 2.0) + RightExtension;
            double Bottom = Bounds.CenterY - (Bounds.Height / 2.0);
            double Top = Bounds.CenterY + (Bounds.Height / 2.0);

            if (Left < MinLeft)
            {
                MinLeft = Left;
            }

            if (Right > MaxRight)
            {
                MaxRight = Right;
            }

            if (Bottom < MinBottom)
            {
                MinBottom = Bottom;
            }

            if (Top > MaxTop)
            {
                MaxTop = Top;
            }
        }

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);

        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Dictionary<string, string> RouteClassByEdgeKey =
            FlowchartEdgeRoutingIntentAnalyzer.BuildRouteClassNamesByEdgeKey(Contract, Layout, Direction);

        FlowchartSugiyamaSvgRenderer.ExpandLayoutBoundsForEdgeLabels(
            Contract,
            RenderOptions,
            BoundsById,
            RouteClassByEdgeKey,
            Direction,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);

        FlowchartSugiyamaSvgRenderer.ExpandLayoutBoundsForRetryLoops(
            Contract,
            Direction,
            Layout.NodeBounds,
            NodeById,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);

        FlowchartSubgraphBoundsRequest BoundsRequest =
            FlowchartSubgraphBoundsRequest.FromContract(Contract, RenderOptions);
        List<FlowchartSubgraphLayoutRect> SubgraphRects =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById, BoundsRequest);
        FlowchartSubgraphBoundsBuilder.ExpandLayoutExtents(
            SubgraphRects,
            ref MinLeft,
            ref MinBottom,
            ref MaxRight,
            ref MaxTop);

        Frame = new FlowchartSvgCoordinateFrame
        {
            Layout = Layout,
            MinLeft = MinLeft,
            MaxTop = MaxTop,
            DiagramPaddingPx = DiagramPaddingPx
        };

        return true;
    }
}
