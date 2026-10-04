using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Maps between MSAGL layout coordinates and rendered SVG coordinates for one laid-out diagram.
/// </summary>
public sealed class FlowchartSvgLayoutCoordinateMapper
{
    private readonly double _MinLeft;

    private readonly double _MaxTop;

    private readonly double _DiagramPaddingPx;

    /// <summary>
    /// Builds a mapper using the same bounds and padding as <see cref="FlowchartSugiyamaSvgRenderer"/>.
    /// </summary>
    public static bool TryCreate(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        out FlowchartSvgLayoutCoordinateMapper? Mapper)
    {
        Mapper = null;

        if (Layout.NodeBounds.Count == 0)
        {
            return false;
        }

        Dictionary<string, NodeContract> NodeById = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
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

        FlowchartRenderOptions RenderOptions = FlowchartRenderOptions.FromContract(Contract);
        Mapper = new FlowchartSvgLayoutCoordinateMapper(MinLeft, MaxTop, RenderOptions.DiagramPaddingPx);
        return true;
    }

    private FlowchartSvgLayoutCoordinateMapper(double MinLeft, double MaxTop, double DiagramPaddingPx)
    {
        _MinLeft = MinLeft;
        _MaxTop = MaxTop;
        _DiagramPaddingPx = DiagramPaddingPx;
    }

    /// <summary>
    /// Converts one layout point to SVG coordinates.
    /// </summary>
    public (double X, double Y) LayoutToSvg(double LayoutX, double LayoutY)
    {
        double X = _DiagramPaddingPx + (LayoutX - _MinLeft);
        double Y = _DiagramPaddingPx + (_MaxTop - LayoutY);
        return (X, Y);
    }

    /// <summary>
    /// Converts one SVG point to layout coordinates.
    /// </summary>
    public (double X, double Y) SvgToLayout(double SvgX, double SvgY)
    {
        double LayoutX = _MinLeft + (SvgX - _DiagramPaddingPx);
        double LayoutY = _MaxTop - (SvgY - _DiagramPaddingPx);
        return (LayoutX, LayoutY);
    }

    /// <summary>
    /// Converts flattened SVG samples to layout coordinates.
    /// </summary>
    public List<(double X, double Y)> ConvertSamplesToLayout(IReadOnlyList<(double X, double Y)> SvgSamples)
    {
        List<(double X, double Y)> LayoutSamples = [];
        foreach ((double SvgX, double SvgY) in SvgSamples)
        {
            LayoutSamples.Add(SvgToLayout(SvgX, SvgY));
        }

        return LayoutSamples;
    }
}
