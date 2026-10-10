using System;
using System.Collections.Generic;
using System.Globalization;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Builds a real markdown2docx flowchart of the same graph as an enterprise
/// sample and compares node rectangles and edge paths.
/// </summary>
public static class EnterpriseParity
{
    /// <summary>
    /// Writes equivalent Mermaid flowchart source for an enterprise diagram.
    /// </summary>
    /// <param name="EnterpriseSource">Enterprise source.</param>
    /// <returns>Flowchart source.</returns>
    public static string WriteFlowchartSource(string EnterpriseSource)
    {
        EnterpriseDocument Document = RequireDocument(EnterpriseSource);
        return EnterpriseFlowchartSourceWriter.Write(Document);
    }

    /// <summary>
    /// Renders the equivalent flowchart through the native markdown2docx pipeline,
    /// using enterprise leaf sizes so the layout boxes match the ArchiMate boxes.
    /// </summary>
    /// <param name="EnterpriseSource">Enterprise source.</param>
    /// <returns>Flowchart SVG.</returns>
    public static string RenderFlowchartSvg(string EnterpriseSource)
    {
        EnterpriseDocument Document = RequireDocument(EnterpriseSource);
        string FlowchartSource = EnterpriseFlowchartSourceWriter.Write(Document);
        DiagramContract Parsed = MermaidRecognizer.Parse(FlowchartSource);
        AlignParsedContract(Parsed, EnterpriseFlowchartContractMapper.Build(Document), Document);
        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Parsed);
        if (string.IsNullOrEmpty(Svg))
        {
            throw new InvalidOperationException("Equivalent flowchart did not render.");
        }

        return Svg;
    }

    /// <summary>
    /// Returns mismatches between enterprise and flowchart node rectangles and
    /// edge path data. Empty when they match within <paramref name="TolerancePx"/>.
    /// </summary>
    /// <param name="EnterpriseSource">Enterprise source.</param>
    /// <param name="TolerancePx">Allowed centre and size difference in pixels.</param>
    /// <returns>Diagnostic strings.</returns>
    public static IReadOnlyList<string> FindGeometryMismatches(string EnterpriseSource, double TolerancePx)
    {
        EnterpriseDocument Document = RequireDocument(EnterpriseSource);
        DiagramContract EnterpriseContract = EnterpriseFlowchartContractMapper.Build(Document);
        if (!FlowchartSugiyamaLayoutEngine.TryLayout(EnterpriseContract, out FlowchartLayoutResult EnterpriseLayout)
            || !FlowchartSvgCoordinateFrameResolver.TryResolve(EnterpriseContract, EnterpriseLayout, out FlowchartSvgCoordinateFrame EnterpriseFrame))
        {
            return new List<string> { "enterprise contract failed flowchart layout" };
        }

        string FlowchartSource = EnterpriseFlowchartSourceWriter.Write(Document);
        DiagramContract Parsed = MermaidRecognizer.Parse(FlowchartSource);
        AlignParsedContract(Parsed, EnterpriseContract, Document);
        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Parsed, out FlowchartLayoutResult FlowchartLayout)
            || !FlowchartSvgCoordinateFrameResolver.TryResolve(Parsed, FlowchartLayout, out FlowchartSvgCoordinateFrame FlowchartFrame))
        {
            return new List<string> { "parsed flowchart failed layout" };
        }

        Dictionary<string, EnterpriseSvgRect> EnterpriseRects = CollectSvgRects(EnterpriseContract, EnterpriseLayout, EnterpriseFrame);
        Dictionary<string, EnterpriseSvgRect> FlowchartRects = CollectSvgRects(Parsed, FlowchartLayout, FlowchartFrame);
        List<string> Mismatches = new List<string>();
        foreach (KeyValuePair<string, EnterpriseSvgRect> Entry in FlowchartRects)
        {
            if (!EnterpriseRects.TryGetValue(Entry.Key, out EnterpriseSvgRect? EnterpriseRect))
            {
                Mismatches.Add(Entry.Key + " is in the flowchart layout but missing from enterprise");
                continue;
            }

            if (!RectsMatch(Entry.Value, EnterpriseRect, TolerancePx))
            {
                Mismatches.Add(
                    Entry.Key
                    + " rect differs: flowchart "
                    + FormatRect(Entry.Value)
                    + " enterprise "
                    + FormatRect(EnterpriseRect));
            }
        }

        foreach (string EnterpriseId in EnterpriseRects.Keys)
        {
            if (!FlowchartRects.ContainsKey(EnterpriseId))
            {
                Mismatches.Add(EnterpriseId + " is in the enterprise layout but missing from flowchart");
            }
        }

        List<EnterpriseFlowchartPathFact> EnterprisePaths = ResolvePaths(EnterpriseContract, EnterpriseLayout, EnterpriseFrame);
        List<EnterpriseFlowchartPathFact> FlowchartPaths = ResolvePaths(Parsed, FlowchartLayout, FlowchartFrame);
        if (EnterprisePaths.Count != FlowchartPaths.Count)
        {
            Mismatches.Add(
                "edge count differs: flowchart "
                + FlowchartPaths.Count.ToString(CultureInfo.InvariantCulture)
                + " enterprise "
                + EnterprisePaths.Count.ToString(CultureInfo.InvariantCulture));
        }

        int PairCount = Math.Min(EnterprisePaths.Count, FlowchartPaths.Count);
        for (int Index = 0; Index < PairCount; Index++)
        {
            EnterpriseFlowchartPathFact Left = FlowchartPaths[Index];
            EnterpriseFlowchartPathFact Right = EnterprisePaths[Index];
            if (!string.Equals(Left.SourceId, Right.SourceId, StringComparison.Ordinal)
                || !string.Equals(Left.TargetId, Right.TargetId, StringComparison.Ordinal)
                || !string.Equals(Left.PathData, Right.PathData, StringComparison.Ordinal))
            {
                Mismatches.Add(
                    Left.SourceId + "->" + Left.TargetId
                    + " path differs from enterprise " + Right.SourceId + "->" + Right.TargetId);
            }
        }

        return Mismatches;
    }

    internal static void AlignParsedContract(
        DiagramContract Parsed,
        DiagramContract EnterpriseContract,
        EnterpriseDocument Document)
    {
        ApplyEnterpriseSizes(Parsed, Document);
        HashSet<string> GroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (GroupContract Group in Parsed.Groups)
        {
            GroupIds.Add(Group.Id);
        }

        Parsed.NodesMutable.RemoveAll(Node => GroupIds.Contains(Node.Id));
        Dictionary<string, EdgeContract> EnterpriseEdges = new Dictionary<string, EdgeContract>(StringComparer.Ordinal);
        foreach (EdgeContract Edge in EnterpriseContract.Edges)
        {
            string Key = Edge.SourceId + "->" + Edge.TargetId;
            if (!EnterpriseEdges.ContainsKey(Key))
            {
                EnterpriseEdges[Key] = Edge;
            }
        }

        foreach (EdgeContract Edge in Parsed.Edges)
        {
            string Key = Edge.SourceId + "->" + Edge.TargetId;
            if (!EnterpriseEdges.TryGetValue(Key, out EdgeContract? EnterpriseEdge))
            {
                continue;
            }

            Edge.Stroke = EnterpriseEdge.Stroke;
            Edge.CreationIndex = EnterpriseEdge.CreationIndex;
            Edge.Label = EnterpriseEdge.Label;
        }
    }

    internal static void ApplyEnterpriseSizes(DiagramContract Contract, EnterpriseDocument Document)
    {
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!Document.Elements.TryGetValue(Node.Id, out EnterpriseElement? Element))
            {
                continue;
            }

            (double Width, double Height) Size = EnterpriseNodeSizer.MeasureElement(Element);
            Node.LayoutWidth = Size.Width;
            Node.LayoutHeight = Size.Height;
            if (EnterpriseElementCatalog.Elements[Element.ElementType].BoxKind == EnterpriseBoxKind.Ellipse)
            {
                Node.Shape = EnterpriseFlowchartContractMapper.RoutingShape(Element);
            }
            if (Element.ElementType == EnterpriseElementType.Junction)
            {
                Node.Label = " ";
                Node.Shape = "circle";
            }
        }
    }

    private static EnterpriseDocument RequireDocument(string EnterpriseSource)
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(EnterpriseSource);
        if (Analysis.HasErrors)
        {
            throw new InvalidOperationException("Enterprise diagram has validation errors.");
        }

        return Analysis.Document;
    }

    private static Dictionary<string, EnterpriseSvgRect> CollectSvgRects(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartSvgCoordinateFrame Frame)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        Dictionary<string, EnterpriseSvgRect> Rects = new Dictionary<string, EnterpriseSvgRect>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            Rects[Bounds.NodeId] = EnterpriseSvgRect.FromNodeBounds(Bounds, Frame);
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (FlowchartSubgraphLayoutRect Rect in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(
                     Contract,
                     BoundsById,
                     FlowchartSubgraphBoundsRequest.FromContract(Contract, Options)))
        {
            Rects[Rect.GroupId] = EnterpriseSvgRect.FromSubgraph(Rect, Frame);
        }

        return Rects;
    }

    private static List<EnterpriseFlowchartPathFact> ResolvePaths(
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartSvgCoordinateFrame Frame)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById =
            new Dictionary<string, FlowchartLayoutNodeBounds>(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Bounds in Layout.NodeBounds)
        {
            BoundsById[Bounds.NodeId] = Bounds;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        foreach (FlowchartSubgraphLayoutRect Rect in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(
                     Contract,
                     BoundsById,
                     FlowchartSubgraphBoundsRequest.FromContract(Contract, Options)))
        {
            BoundsById[Rect.GroupId] = new FlowchartLayoutNodeBounds
            {
                NodeId = Rect.GroupId,
                CenterX = (Rect.Left + Rect.Right) / 2.0,
                CenterY = (Rect.Bottom + Rect.Top) / 2.0,
                Width = Math.Max(1.0, Rect.Right - Rect.Left),
                Height = Math.Max(1.0, Rect.Top - Rect.Bottom)
            };
        }

        Dictionary<string, NodeContract> NodeById = new Dictionary<string, NodeContract>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            NodeById[Node.Id] = Node;
        }

        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<EnterpriseFlowchartPathFact> Facts = new List<EnterpriseFlowchartPathFact>();
        foreach (EdgeContract Edge in Contract.Edges)
        {
            if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> LayoutPoints,
                    out FlowchartEdgeRouteKind RouteKind)
                || LayoutPoints.Count < 2)
            {
                continue;
            }

            List<(double X, double Y)> SvgPoints = new List<(double X, double Y)>();
            foreach ((double LayoutX, double LayoutY) in LayoutPoints)
            {
                SvgPoints.Add(Frame.LayoutToSvg(LayoutX, LayoutY));
            }

            string PathData = FlowchartCurvePathBuilder.BuildRoundedCornerPathData(SvgPoints, DiagramGridRouteBuilder.CORNER_RADIUS_PX);
            if (string.IsNullOrWhiteSpace(PathData))
            {
                PathData = FlowchartCurvePathBuilder.BuildPathDataFromGuidePoints(SvgPoints, Options.Curve);
            }

            Facts.Add(new EnterpriseFlowchartPathFact(Edge.SourceId, Edge.TargetId, PathData));
        }

        return Facts;
    }

    private static bool RectsMatch(EnterpriseSvgRect Left, EnterpriseSvgRect Right, double TolerancePx)
    {
        return Math.Abs(Left.Left - Right.Left) <= TolerancePx
            && Math.Abs(Left.Top - Right.Top) <= TolerancePx
            && Math.Abs(Left.Width - Right.Width) <= TolerancePx
            && Math.Abs(Left.Height - Right.Height) <= TolerancePx;
    }

    private static string FormatRect(EnterpriseSvgRect Rect)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[x={0:0.##} y={1:0.##} w={2:0.##} h={3:0.##}]",
            Rect.Left,
            Rect.Top,
            Rect.Width,
            Rect.Height);
    }
}
