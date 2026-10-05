using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Draws SVG from adopted MSAGL node bounds, edge curves, arrowheads, and labels.
/// </summary>
public static class AdoptedMsaglSvgRenderer
{
    /// <summary>
    /// Renders a flowchart or enterprise contract with the adopted router in <paramref name="Profile"/>.
    /// </summary>
    /// <param name="Contract">Recognised diagram.</param>
    /// <param name="Profile">Router options.</param>
    /// <returns>SVG markup, or empty when layout fails.</returns>
    public static string RenderToSvg(DiagramContract Contract, AdoptedMsaglRoutingProfile Profile)
    {
        if (Contract == null || Contract.Nodes.Count == 0)
        {
            return string.Empty;
        }

        if (!AdoptedMsaglLayoutEngine.TryLayoutAndRoute(
            Contract,
            Profile,
            out GeometryGraph Graph,
            out Dictionary<string, Node> NodesById))
        {
            return string.Empty;
        }

        return RenderToSvg(Contract, Profile, Graph, NodesById);
    }

    /// <summary>
    /// Renders a previously routed graph.
    /// </summary>
    public static string RenderToSvg(
        DiagramContract Contract,
        AdoptedMsaglRoutingProfile Profile,
        GeometryGraph Graph,
        IReadOnlyDictionary<string, Node> NodesById)
    {
        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        double Padding = Options.DiagramPaddingPx;
        Rectangle GraphBox = ComputeDrawnBounds(Graph);
        double MinX = GraphBox.Left;
        double MaxY = GraphBox.Top;
        double Width = Math.Max(1.0, GraphBox.Width) + (Padding * 2.0);
        double Height = Math.Max(1.0, GraphBox.Height) + (Padding * 2.0);

        Point ToSvg(Point LayoutPoint)
        {
            return new Point(Padding + (LayoutPoint.X - MinX), Padding + (MaxY - LayoutPoint.Y));
        }

        StringBuilder Builder = new StringBuilder();
        Builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"");
        Builder.Append(Math.Ceiling(Width).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" height=\"");
        Builder.Append(Math.Ceiling(Height).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" viewBox=\"0 0 ");
        Builder.Append(Math.Ceiling(Width).ToString(CultureInfo.InvariantCulture));
        Builder.Append(' ');
        Builder.Append(Math.Ceiling(Height).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\">");
        Builder.Append("<rect x=\"0\" y=\"0\" width=\"");
        Builder.Append(Math.Ceiling(Width).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" height=\"");
        Builder.Append(Math.Ceiling(Height).ToString(CultureInfo.InvariantCulture));
        Builder.Append("\" fill=\"white\"/>");

        foreach (Cluster ClusterNode in Graph.RootCluster.AllClustersDepthFirst())
        {
            if (object.ReferenceEquals(ClusterNode, Graph.RootCluster) || ClusterNode.BoundaryCurve == null)
            {
                continue;
            }

            Rectangle Box = ClusterNode.BoundaryCurve.BoundingBox;
            string ClusterId = ResolveNodeId(ClusterNode);
            Point TopLeft = ToSvg(new Point(Box.Left, Box.Top));
            Builder.Append("<g data-id=\"");
            Builder.Append(Escape(ClusterId));
            Builder.Append("\">");
            Builder.Append("<rect x=\"");
            AppendNumber(Builder, TopLeft.X);
            Builder.Append("\" y=\"");
            AppendNumber(Builder, TopLeft.Y);
            Builder.Append("\" width=\"");
            AppendNumber(Builder, Box.Width);
            Builder.Append("\" height=\"");
            AppendNumber(Builder, Box.Height);
            Builder.Append("\" fill=\"#ffffde\" stroke=\"#aaaa33\" stroke-width=\"1\" stroke-dasharray=\"4 3\"/>");
            string Title = ResolveClusterTitle(Contract, ClusterNode);
            if (Title.Length > 0)
            {
                Point TitlePoint = ToSvg(new Point(Box.Center.X, Box.Top - 10.0));
                AppendText(Builder, TitlePoint.X, TitlePoint.Y, Title, Options.FontSizePx);
            }

            Builder.Append("</g>");
        }

        foreach (KeyValuePair<string, Node> Pair in NodesById)
        {
            if (Pair.Value is Cluster)
            {
                continue;
            }

            Rectangle Box = Pair.Value.BoundingBox;
            Point TopLeft = ToSvg(new Point(Box.Left, Box.Top));
            Builder.Append("<g data-id=\"");
            Builder.Append(Escape(Pair.Key));
            Builder.Append("\">");
            Builder.Append("<rect x=\"");
            AppendNumber(Builder, TopLeft.X);
            Builder.Append("\" y=\"");
            AppendNumber(Builder, TopLeft.Y);
            Builder.Append("\" width=\"");
            AppendNumber(Builder, Box.Width);
            Builder.Append("\" height=\"");
            AppendNumber(Builder, Box.Height);
            Builder.Append("\" fill=\"#ECECFF\" stroke=\"#9370DB\" stroke-width=\"1\"/>");
            string Label = ResolveNodeLabel(Contract, Pair.Key);
            Point Centre = ToSvg(Box.Center);
            AppendText(Builder, Centre.X, Centre.Y + 4.0, Label, Options.FontSizePx);
            Builder.Append("</g>");
        }

        foreach (Edge LayoutEdge in Graph.Edges)
        {
            if (LayoutEdge.Curve == null)
            {
                continue;
            }

            string SourceId = ResolveNodeId(LayoutEdge.Source);
            string TargetId = ResolveNodeId(LayoutEdge.Target);
            string PathData = AdoptedMsaglCurveToSvg.ToPathData(LayoutEdge.Curve, ToSvg);
            if (PathData.Length == 0)
            {
                continue;
            }

            Builder.Append("<path d=\"");
            Builder.Append(PathData);
            Builder.Append("\" fill=\"none\" stroke=\"");
            Builder.Append(Profile.LineStroke);
            Builder.Append("\" stroke-width=\"1\" data-edge-source=\"");
            Builder.Append(Escape(SourceId));
            Builder.Append("\" data-edge-target=\"");
            Builder.Append(Escape(TargetId));
            Builder.Append("\"/>");

            if (LayoutEdge.EdgeGeometry.TargetArrowhead != null)
            {
                Point Tip = ToSvg(LayoutEdge.EdgeGeometry.TargetArrowhead.TipPosition);
                Point End = ToSvg(LayoutEdge.Curve.End);
                AppendArrowPolygon(Builder, End, Tip, Profile);
            }

            if (LayoutEdge.Label != null && LayoutEdge.UserData is EdgeContract EdgeItem && !string.IsNullOrWhiteSpace(EdgeItem.Label))
            {
                Point LabelCentre = ToSvg(LayoutEdge.Label.Center);
                AppendText(Builder, LabelCentre.X, LabelCentre.Y, EdgeItem.Label, Options.EdgeLabelFontSizePx);
            }
        }

        Builder.Append("</svg>");
        return Builder.ToString();
    }

    private static Rectangle ComputeDrawnBounds(GeometryGraph Graph)
    {
        Graph.UpdateBoundingBox();
        Rectangle Bounds = Graph.BoundingBox;
        foreach (Cluster ClusterNode in Graph.RootCluster.AllClustersDepthFirst())
        {
            if (object.ReferenceEquals(ClusterNode, Graph.RootCluster) || ClusterNode.BoundaryCurve == null)
            {
                continue;
            }

            Bounds.Add(ClusterNode.BoundaryCurve.BoundingBox);
        }

        foreach (Edge LayoutEdge in Graph.Edges)
        {
            if (LayoutEdge.Curve != null)
            {
                Bounds.Add(LayoutEdge.Curve.BoundingBox);
            }

            if (LayoutEdge.Label != null)
            {
                Bounds.Add(LayoutEdge.Label.BoundingBox);
            }
        }

        if (Bounds.Width <= 0.0 || Bounds.Height <= 0.0)
        {
            Bounds = new Rectangle(0.0, 0.0, 1.0, 1.0);
        }

        return Bounds;
    }

    private static string ResolveNodeId(Node LayoutNode)
    {
        if (LayoutNode.UserData is string Id && Id.Length > 0)
        {
            return Id;
        }

        return LayoutNode.UserData?.ToString() ?? string.Empty;
    }

    private static string ResolveNodeLabel(DiagramContract Contract, string NodeId)
    {
        foreach (NodeContract NodeItem in Contract.Nodes)
        {
            if (string.Equals(NodeItem.Id, NodeId, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(NodeItem.Label) ? NodeId : NodeItem.Label;
            }
        }

        return NodeId;
    }

    private static string ResolveClusterTitle(DiagramContract Contract, Cluster ClusterNode)
    {
        string Id = ResolveNodeId(ClusterNode);
        foreach (GroupContract Group in Contract.Groups)
        {
            if (string.Equals(Group.Id, Id, StringComparison.Ordinal))
            {
                return Group.Title ?? Id;
            }
        }

        return Id;
    }

    private static void AppendArrowPolygon(
        StringBuilder Builder,
        Point Base,
        Point Tip,
        AdoptedMsaglRoutingProfile Profile)
    {
        Point Direction = Tip - Base;
        double Length = Math.Sqrt((Direction.X * Direction.X) + (Direction.Y * Direction.Y));
        if (Length < 0.001)
        {
            Direction = new Point(0.0, 1.0);
            Length = 1.0;
        }

        Point Unit = new Point(Direction.X / Length, Direction.Y / Length);
        Point Normal = new Point(-Unit.Y, Unit.X);
        double Half = Profile.ArrowheadWidthPx / 2.0;
        Point Left = Base + (Normal * Half);
        Point Right = Base - (Normal * Half);
        Builder.Append("<polygon points=\"");
        AppendNumber(Builder, Tip.X);
        Builder.Append(',');
        AppendNumber(Builder, Tip.Y);
        Builder.Append(' ');
        AppendNumber(Builder, Left.X);
        Builder.Append(',');
        AppendNumber(Builder, Left.Y);
        Builder.Append(' ');
        AppendNumber(Builder, Right.X);
        Builder.Append(',');
        AppendNumber(Builder, Right.Y);
        Builder.Append("\" fill=\"");
        Builder.Append(Profile.LineStroke);
        Builder.Append("\" stroke=\"none\"/>");
    }

    private static void AppendText(
        StringBuilder Builder,
        double X,
        double Y,
        string Text,
        double FontSizePx)
    {
        Builder.Append("<text x=\"");
        AppendNumber(Builder, X);
        Builder.Append("\" y=\"");
        AppendNumber(Builder, Y);
        Builder.Append("\" text-anchor=\"middle\" fill=\"#333\" font-family=\"&quot;trebuchet ms&quot;, verdana, arial, sans-serif\" font-size=\"");
        AppendNumber(Builder, FontSizePx);
        Builder.Append("\">");
        Builder.Append(Escape(Text));
        Builder.Append("</text>");
    }

    private static void AppendNumber(StringBuilder Builder, double Value)
    {
        Builder.Append(Value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static string Escape(string Text)
    {
        return Text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
