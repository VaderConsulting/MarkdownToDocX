using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Verifies rendered SVG node and edge labels are centred on their owning geometry.
/// </summary>
public static class FlowchartSvgLabelAlignmentHelper
{
    private const double ALIGNMENT_TOLERANCE_PX = 2.0;

    private const double FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR = 1.5;

    /// <summary>
    /// Updates node and edge label alignment facts from rendered SVG output.
    /// </summary>
    public static void Apply(DiagramActualResult Diagram, DiagramContract Contract, string Svg)
    {
        if (string.IsNullOrWhiteSpace(Svg) || !Diagram.LayoutSucceeded)
        {
            return;
        }

        if (!FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, out FlowchartSvgCoordinateFrame CoordinateFrame))
        {
            return;
        }

        FlowchartRenderOptions Options = FlowchartRenderOptions.FromContract(Contract);
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        List<RenderedTextElement> TextElements = ParseRenderedTextElements(Svg, Options);
        ApplyNodeLabelAlignmentFacts(Diagram, TextElements, CoordinateFrame);
        ApplyEdgeLabelAlignmentFacts(
            Diagram,
            Contract,
            Svg,
            Options,
            Direction,
            CoordinateFrame);
    }

    private static void ApplyNodeLabelAlignmentFacts(
        DiagramActualResult Diagram,
        List<RenderedTextElement> TextElements,
        FlowchartSvgCoordinateFrame CoordinateFrame)
    {
        foreach (NodeActualResult Node in Diagram.Nodes)
        {
            if (!Node.IsRendered || string.IsNullOrWhiteSpace(Node.Label))
            {
                continue;
            }

            (double ExpectedCentreSvgX, double ExpectedCentreSvgY) = CoordinateFrame.LayoutToSvg(
                Node.Bounds.CentreX,
                Node.Bounds.CentreY);

            RenderedTextElement? TextElement = FindTextElementForLabel(
                TextElements,
                Node.Label,
                ExpectedCentreSvgX,
                ExpectedCentreSvgY);
            if (TextElement is null)
            {
                Node.Facts["Label.CentredHorizontally"] = false;
                Node.Facts["Label.CentredVertically"] = false;
                continue;
            }

            Node.Facts["Label.CentredHorizontally"] =
                Math.Abs(TextElement.CentreX - ExpectedCentreSvgX) <= ALIGNMENT_TOLERANCE_PX;
            Node.Facts["Label.CentredVertically"] =
                Math.Abs(TextElement.CentreY - ExpectedCentreSvgY) <= ALIGNMENT_TOLERANCE_PX;
        }
    }

    private static void ApplyEdgeLabelAlignmentFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        string Svg,
        FlowchartRenderOptions Options,
        FlowchartDirection Direction,
        FlowchartSvgCoordinateFrame CoordinateFrame)
    {
        bool VerticalFlow = Direction == FlowchartDirection.TopToBottom
            || Direction == FlowchartDirection.BottomToTop;

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = CoordinateFrame.Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds, StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds Cluster in CoordinateFrame.Layout.ClusterBounds)
        {
            BoundsById[Cluster.NodeId] = Cluster;
        }
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node, StringComparer.Ordinal);
        Dictionary<string, string> RouteClassByEdgeKey =
            FlowchartEdgeRoutingIntentAnalyzer.BuildRouteClassNamesByEdgeKey(
                Contract,
                CoordinateFrame.Layout,
                Direction);

        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            if (!Edge.IsVisible || string.IsNullOrWhiteSpace(Edge.Label))
            {
                continue;
            }

            EdgeContract? EdgeContract = TryResolveEdgeContract(Contract, Edge);
            if (EdgeContract is null)
            {
                SetEdgeLabelAlignmentFactsFalse(Edge);
                continue;
            }

            if (!BoundsById.TryGetValue(Edge.SourceNodeId, out FlowchartLayoutNodeBounds? SourceBounds)
                || !BoundsById.TryGetValue(Edge.TargetNodeId, out FlowchartLayoutNodeBounds? TargetBounds))
            {
                SetEdgeLabelAlignmentFactsFalse(Edge);
                continue;
            }

            string? SourceShape = NodeById.TryGetValue(Edge.SourceNodeId, out NodeContract? SourceNode)
                ? SourceNode.Shape
                : null;
            string? RouteClass = TryResolveRouteClassName(EdgeContract, RouteClassByEdgeKey) ?? Edge.RouteClass;

            List<(double X, double Y)> LayoutPoints = [];
            if (CoordinateFrame.Layout.TryCopyDrawnRoutePoints(EdgeContract, out List<(double X, double Y)> DrawnPoints))
            {
                LayoutPoints = DrawnPoints;
            }
            else if (!FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    EdgeContract,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> ResolvedGuidePoints,
                    out FlowchartEdgeRouteKind _))
            {
                if (Edge.Path.Points.Count < 2)
                {
                    SetEdgeLabelAlignmentFactsFalse(Edge);
                    continue;
                }

                LayoutPoints = Edge.Path.Points
                    .Select(Point => (Point.X, Point.Y))
                    .ToList();
            }
            else
            {
                LayoutPoints = ResolvedGuidePoints;
            }

            if (FlowchartCurvePathBuilder.TryCollapseAxisAlignedEndpoints(
                    LayoutPoints,
                    out List<(double X, double Y)> StraightSegment))
            {
                LayoutPoints = StraightSegment;
            }

            string LabelForPlacement = string.IsNullOrWhiteSpace(EdgeContract.Label)
                ? Edge.Label ?? string.Empty
                : EdgeContract.Label;

            (double PlacementLayoutX, double PlacementLayoutY) =
                FlowchartEdgeLabelPlacementHelper.ResolveEdgeLabelPosition(
                    SourceBounds,
                    TargetBounds,
                    LayoutPoints,
                    Direction,
                    SourceShape,
                    LabelForPlacement,
                    EdgeContract,
                    Contract,
                    BoundsById,
                    RouteClass);
            (double ExpectedLayoutX, double ExpectedLayoutY) = FlowchartSugiyamaSvgRenderer.ResolveDrawnEdgeLabelCentre(
                EdgeContract,
                Contract,
                BoundsById,
                SourceBounds,
                TargetBounds,
                SourceShape,
                LayoutPoints,
                Direction,
                RouteClass,
                Options);

            (double PlacementCentreSvgX, double PlacementCentreSvgY) = CoordinateFrame.LayoutToSvg(
                PlacementLayoutX,
                PlacementLayoutY);
            (double ExpectedCentreSvgX, double ExpectedCentreSvgY) = CoordinateFrame.LayoutToSvg(
                ExpectedLayoutX,
                ExpectedLayoutY);

            if (!TryMeasureEdgeLabelCentreInSvg(
                    Svg,
                    LabelForPlacement,
                    Options,
                    ExpectedCentreSvgX,
                    ExpectedCentreSvgY,
                    out double MeasuredCentreSvgX,
                    out double MeasuredCentreSvgY))
            {
                SetEdgeLabelAlignmentFactsFalse(Edge);
                continue;
            }

            bool CentredHorizontally =
                Math.Abs(MeasuredCentreSvgX - ExpectedCentreSvgX) <= ALIGNMENT_TOLERANCE_PX;
            bool CentredVertically =
                Math.Abs(MeasuredCentreSvgY - ExpectedCentreSvgY) <= ALIGNMENT_TOLERANCE_PX;

            bool IsLrLabelledRectFanOut = FlowchartFanOutPortAllocator.IsLrLabelledRectFanOutEdge(
                EdgeContract,
                Contract,
                BoundsById,
                SourceShape,
                Direction);

            bool StraightConnector = LayoutPoints.Count >= 2
                && FlowchartEdgeConnectionGeometry.ShouldRenderStraightConnector(
                    LayoutPoints[0].X,
                    LayoutPoints[0].Y,
                    LayoutPoints[LayoutPoints.Count - 1].X,
                    LayoutPoints[LayoutPoints.Count - 1].Y);

            bool HasExteriorCorridorDetour =
                FlowchartEdgeLabelPlacementHelper.HasExteriorCorridorDetourForValidation(LayoutPoints, Direction);

            bool CentredAlongConnector = VerticalFlow
                ? Math.Abs(MeasuredCentreSvgY - PlacementCentreSvgY) <= ALIGNMENT_TOLERANCE_PX
                : Math.Abs(MeasuredCentreSvgX - PlacementCentreSvgX) <= ALIGNMENT_TOLERANCE_PX;

            Edge.LabelFacts["CentredOnConnectorHorizontally"] = CentredHorizontally;
            Edge.LabelFacts["CentredOnConnectorVertically"] = CentredVertically;
            Edge.LabelFacts["CentredOnConnectorSpine"] = EvaluateCentredOnConnectorSpine(
                CentredHorizontally,
                CentredVertically,
                CentredAlongConnector,
                IsLrLabelledRectFanOut,
                Edge.RouteClass,
                StraightConnector,
                VerticalFlow,
                HasExteriorCorridorDetour);
        }
    }

    /// <summary>
    /// Decides whether a label sits where the spine rules want it. Labels on straight connectors are drawn
    /// beside the line, not on it (spec §15.0C.8), so they must be centred along the connector at its midspan
    /// and drawn at the renderer's cleared position across the flow.
    /// </summary>
    private static bool EvaluateCentredOnConnectorSpine(
        bool CentredHorizontally,
        bool CentredVertically,
        bool CentredAlongConnector,
        bool IsLrLabelledRectFanOut,
        string RouteClass,
        bool StraightConnector,
        bool VerticalFlow,
        bool HasExteriorCorridorDetour)
    {
        if (IsLrLabelledRectFanOut)
        {
            return CentredHorizontally && CentredVertically;
        }

        if (string.Equals(RouteClass, "LocalDecisionBranch", StringComparison.Ordinal))
        {
            if (HasExteriorCorridorDetour)
            {
                return true;
            }

            if (VerticalFlow)
            {
                return CentredHorizontally;
            }

            return true;
        }

        if (StraightConnector)
        {
            return CentredAlongConnector && CentredHorizontally && CentredVertically;
        }

        return true;
    }

    private static string? TryResolveRouteClassName(
        EdgeContract Edge,
        IReadOnlyDictionary<string, string> RouteClassByEdgeKey)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Id)
            && RouteClassByEdgeKey.TryGetValue(Edge.Id, out string? RouteClassById))
        {
            return RouteClassById;
        }

        string SourceTargetKey = Edge.SourceId + "->" + Edge.TargetId;
        if (RouteClassByEdgeKey.TryGetValue(SourceTargetKey, out string? RouteClassByEndpoints))
        {
            return RouteClassByEndpoints;
        }

        return null;
    }

    private static EdgeContract? TryResolveEdgeContract(DiagramContract Contract, EdgeActualResult Edge)
    {
        List<EdgeContract> Matches = Contract.Edges
            .Where(Candidate =>
                string.Equals(Candidate.SourceId, Edge.SourceNodeId, StringComparison.Ordinal)
                && string.Equals(Candidate.TargetId, Edge.TargetNodeId, StringComparison.Ordinal))
            .ToList();

        if (Matches.Count == 0)
        {
            return null;
        }

        if (Matches.Count == 1)
        {
            return Matches[0];
        }

        EdgeContract? IndexedMatch = Matches.FirstOrDefault(Candidate => Candidate.CreationIndex == Edge.CreationIndex);
        return IndexedMatch ?? Matches[0];
    }

    private static void SetEdgeLabelAlignmentFactsFalse(EdgeActualResult Edge)
    {
        Edge.LabelFacts["CentredOnConnectorHorizontally"] = false;
        Edge.LabelFacts["CentredOnConnectorVertically"] = false;
        Edge.LabelFacts["CentredOnConnectorSpine"] = false;
    }

    private static bool TryMeasureEdgeLabelCentreInSvg(
        string Svg,
        string Label,
        FlowchartRenderOptions Options,
        double ExpectedCentreSvgX,
        double ExpectedCentreSvgY,
        out double CentreX,
        out double CentreY)
    {
        CentreX = 0.0;
        CentreY = 0.0;
        string SearchToken = BuildLabelSearchToken(Label);
        if (string.IsNullOrWhiteSpace(SearchToken))
        {
            return false;
        }

        MatchCollection Matches = Regex.Matches(
            Svg,
            @"<text([^>]*)>(.*?)</text>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        Match? BestMatch = null;
        double BestDistanceSquared = double.MaxValue;
        double BestCentreX = 0.0;
        double BestCentreY = 0.0;

        foreach (Match Match in Matches)
        {
            string PlainText = StripSvgTextMarkup(Match.Groups[2].Value);
            if (!TextMatchesLabel(PlainText, SearchToken))
            {
                continue;
            }

            string Attributes = Match.Groups[1].Value;
            string InnerMarkup = Match.Groups[2].Value;
            if (!TryParseTextCentre(Attributes, InnerMarkup, Options, out double CandidateCentreX, out double CandidateCentreY))
            {
                continue;
            }

            bool UsesStartAnchor = Attributes.Contains(
                "text-anchor=\"start\"",
                StringComparison.OrdinalIgnoreCase);
            if (UsesStartAnchor)
            {
                Match FirstTspanX = Regex.Match(InnerMarkup, @"<tspan[^>]*\bx=""([^""]+)""", RegexOptions.IgnoreCase);
                if (!FirstTspanX.Success)
                {
                    continue;
                }

                double BlockStartX = ParseCssPx(FirstTspanX.Groups[1].Value);
                double FontSizePx = Options.EdgeLabelFontSizePx;
                Match FontSizeMatch = Regex.Match(Attributes, @"font-size=""([^""]+)""", RegexOptions.IgnoreCase);
                if (FontSizeMatch.Success)
                {
                    FontSizePx = ParseCssPx(FontSizeMatch.Groups[1].Value);
                }

                double BlockWidthPx = FlowchartRichLabelSvgWriter.EstimateStyledLabelMaxWidthPx(Label, FontSizePx);
                CandidateCentreX = BlockStartX + (BlockWidthPx / 2.0);
            }

            double DeltaX = CandidateCentreX - ExpectedCentreSvgX;
            double DeltaY = CandidateCentreY - ExpectedCentreSvgY;
            double DistanceSquared = (DeltaX * DeltaX) + (DeltaY * DeltaY);
            if (DistanceSquared < BestDistanceSquared)
            {
                BestDistanceSquared = DistanceSquared;
                BestMatch = Match;
                BestCentreX = CandidateCentreX;
                BestCentreY = CandidateCentreY;
            }
        }

        if (BestMatch is null)
        {
            return false;
        }

        CentreX = BestCentreX;
        CentreY = BestCentreY;
        return true;
    }

    private static RenderedTextElement? FindTextElementForLabel(
        List<RenderedTextElement> TextElements,
        string Label,
        double ExpectedCentreSvgX,
        double ExpectedCentreSvgY)
    {
        string SearchToken = BuildLabelSearchToken(Label);
        if (string.IsNullOrWhiteSpace(SearchToken))
        {
            return null;
        }

        List<RenderedTextElement> Matches = TextElements
            .Where(Candidate => TextMatchesLabel(Candidate.Text, SearchToken))
            .ToList();

        if (Matches.Count == 0)
        {
            return null;
        }

        if (Matches.Count == 1)
        {
            return Matches[0];
        }

        return Matches
            .OrderBy(Candidate =>
            {
                double DeltaX = Candidate.CentreX - ExpectedCentreSvgX;
                double DeltaY = Candidate.CentreY - ExpectedCentreSvgY;
                return (DeltaX * DeltaX) + (DeltaY * DeltaY);
            })
            .First();
    }

    private static List<RenderedTextElement> ParseRenderedTextElements(
        string Svg,
        FlowchartRenderOptions Options)
    {
        List<RenderedTextElement> Elements = [];
        MatchCollection Matches = Regex.Matches(
            Svg,
            @"<text([^>]*)>(.*?)</text>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match Match in Matches)
        {
            string Attributes = Match.Groups[1].Value;
            string InnerMarkup = Match.Groups[2].Value;
            if (!TryParseTextCentre(Attributes, InnerMarkup, Options, out double CentreX, out double CentreY))
            {
                continue;
            }

            Elements.Add(new RenderedTextElement
            {
                Text = StripSvgTextMarkup(InnerMarkup),
                CentreX = CentreX,
                CentreY = CentreY
            });
        }

        return Elements;
    }

    private static bool TryParseTextCentre(
        string Attributes,
        string InnerMarkup,
        FlowchartRenderOptions Options,
        out double CentreX,
        out double CentreY)
    {
        CentreX = 0.0;
        CentreY = 0.0;

        Match XMatch = Regex.Match(Attributes, @"\bx=""([^""]+)""", RegexOptions.IgnoreCase);
        Match YMatch = Regex.Match(Attributes, @"\by=""([^""]+)""", RegexOptions.IgnoreCase);
        if (!YMatch.Success)
        {
            return false;
        }

        double FirstLineSvgY = ParseCssPx(YMatch.Groups[1].Value);
        bool UsesMiddleAnchor = Attributes.Contains(
            "text-anchor=\"middle\"",
            StringComparison.OrdinalIgnoreCase);

        int LineCount = CountRenderedLineTspans(InnerMarkup);

        double FontSizePx = Options.EdgeLabelFontSizePx;
        Match FontSizeMatch = Regex.Match(Attributes, @"font-size=""([^""]+)""", RegexOptions.IgnoreCase);
        if (FontSizeMatch.Success)
        {
            FontSizePx = ParseCssPx(FontSizeMatch.Groups[1].Value);
        }

        CentreY = FlowchartRichLabelSvgWriter.ComputeLabelBlockCentreY(FirstLineSvgY, LineCount, FontSizePx, FLOWCHART_MULTILINE_LINE_HEIGHT_FACTOR);

        if (XMatch.Success)
        {
            CentreX = ParseCssPx(XMatch.Groups[1].Value);
            return true;
        }

        Match FirstTspanX = Regex.Match(InnerMarkup, @"<tspan[^>]*\bx=""([^""]+)""", RegexOptions.IgnoreCase);
        if (!FirstTspanX.Success)
        {
            return false;
        }

        double BlockStartX = ParseCssPx(FirstTspanX.Groups[1].Value);
        double BlockWidthPx = 0.0;
        List<string> LineMarkupLines = ExtractRenderedLineMarkup(InnerMarkup);
        IEnumerable<string> WidthLines = LineMarkupLines.Count > 0
            ? LineMarkupLines
            : [InnerMarkup];
        foreach (string LineMarkup in WidthLines)
        {
            double LineWidthPx = FlowchartRichLabelSvgWriter.EstimateLineWidthPx(LineMarkup, FontSizePx);
            if (LineWidthPx > BlockWidthPx)
            {
                BlockWidthPx = LineWidthPx;
            }
        }

        CentreX = BlockStartX + (BlockWidthPx / 2.0);
        return true;
    }

    private static int CountRenderedLineTspans(string InnerMarkup)
    {
        MatchCollection DyMatches = Regex.Matches(
            InnerMarkup,
            @"<tspan[^>]*\bdy=""([^""]*)""",
            RegexOptions.IgnoreCase);
        if (DyMatches.Count < 1)
        {
            return 1;
        }

        return DyMatches.Count;
    }

    private static List<string> ExtractRenderedLineMarkup(string InnerMarkup)
    {
        List<string> Lines = [];
        MatchCollection LineTspans = Regex.Matches(
            InnerMarkup,
            @"<tspan[^>]*\bx=""[^""]+""[^>]*\bdy=""[^""]*""[^>]*>(.*?)</tspan>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match Match in LineTspans)
        {
            Lines.Add(Match.Groups[1].Value);
        }

        return Lines;
    }

    private static string StripSvgTextMarkup(string Markup) => Regex.Replace(Markup, "<[^>]+>", string.Empty, RegexOptions.Singleline)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Trim();

    private static double ParseCssPx(string Value)
    {
        string Normalised = Value.Trim();
        if (Normalised.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            Normalised = Normalised.Substring(0, Normalised.Length - 2).Trim();
        }

        return double.Parse(Normalised, CultureInfo.InvariantCulture);
    }

    private static bool TextMatchesLabel(string RenderedPlainText, string SearchToken) => NormaliseWhitespaceForMatching(RenderedPlainText)
            .Contains(NormaliseWhitespaceForMatching(SearchToken), StringComparison.Ordinal);

    private static string NormaliseWhitespaceForMatching(string Text) => Regex.Replace(Text, @"\s+", string.Empty);

    private static string BuildLabelSearchToken(string Label)
    {
        string PlainText = FlowchartRichLabelSvgWriter.BuildRenderedPlainText(Label);
        if (string.IsNullOrWhiteSpace(PlainText))
        {
            PlainText = Label.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        }

        if (PlainText.Length <= 24)
        {
            return PlainText;
        }

        return PlainText.Substring(0, 24);
    }

    private sealed class RenderedTextElement
    {
        public string Text { get; set; } = string.Empty;

        public double CentreX { get; set; }

        public double CentreY { get; set; }
    }
}
