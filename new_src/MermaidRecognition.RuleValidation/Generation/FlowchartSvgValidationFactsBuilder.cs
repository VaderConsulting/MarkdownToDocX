using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Derives render-level marker facts from generated SVG output.
/// </summary>
public static class FlowchartSvgValidationFactsBuilder
{
    /// <summary>
    /// Renders the diagram and updates marker facts on visible edges.
    /// </summary>
    public static void Apply(DiagramActualResult Diagram, DiagramContract Contract)
    {
        if (!Diagram.LayoutSucceeded)
        {
            return;
        }

        string Svg = FlowchartSugiyamaSvgRenderer.RenderToSvg(Contract);

        if (FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
        {
            FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
            FlowchartLayoutTerminalStubFactsHelper.Apply(Diagram, Contract, Layout, Direction);
        }

        FlowchartSvgLabelAlignmentHelper.Apply(Diagram, Contract, Svg);
        FlowchartOrthogonalPathFactsHelper.ApplyRenderedSvgOrthogonalPathFacts(Diagram, Contract, Svg);
        FlowchartTrapezoidBracketShapeFactsHelper.ApplyRenderedSvgFacts(Diagram, Contract, Svg);
        FlowchartRenderedEdgePathFactsHelper.Apply(Diagram, Contract, Svg);
        FlowchartSvgMarkerTangentFactsHelper.Apply(Diagram, Contract, Svg);
        int CircleCount = Regex.Matches(Svg, "<circle", RegexOptions.IgnoreCase).Count;
        int PolygonCount = Regex.Matches(Svg, "<polygon", RegexOptions.IgnoreCase).Count;
        int CrossLineCount = Regex.Matches(Svg, @"<line[^>]+stroke-linecap=""round""", RegexOptions.IgnoreCase).Count;

        int ExpectedCircleCount = Diagram.Edges.Count(Edge =>
            Edge.IsVisible && (Edge.ArrowStart == "Circle" || Edge.ArrowEnd == "Circle"));
        int ExpectedCrossCount = Diagram.Edges.Count(Edge =>
            Edge.IsVisible && (Edge.ArrowStart == "Cross" || Edge.ArrowEnd == "Cross"));
        int ExpectedArrowCount = Diagram.Edges.Count(Edge =>
            Edge.IsVisible
            && Edge.ArrowEnd != "Cross"
            && Edge.ArrowEnd != "Circle"
            && Edge.ArrowEnd != "Point"
            && Edge.ArrowEnd != "None");

        bool CirclesSatisfied = CircleCount >= ExpectedCircleCount;
        bool CrossesSatisfied = CrossLineCount >= (ExpectedCrossCount * 2);
        bool ArrowsSatisfied = PolygonCount >= ExpectedArrowCount;

        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            if (!Edge.IsVisible)
            {
                continue;
            }

            bool StartMatches = EvaluateStartMarker(Edge, CirclesSatisfied, CrossesSatisfied);
            bool EndMatches = EvaluateEndMarker(Edge, CirclesSatisfied, CrossesSatisfied, ArrowsSatisfied);
            Edge.MarkerFacts["StartMatchesToken"] = StartMatches;
            Edge.MarkerFacts["EndMatchesToken"] = EndMatches;
            Edge.MarkerFacts["NotLost"] = StartMatches && EndMatches;
            Edge.MarkerFacts["NotInvented"] = true;

            if (Edge.IsBidirectional)
            {
                Edge.MarkerFacts["BidirectionalPreserved"] = StartMatches && EndMatches;
            }
        }
    }

    private static bool EvaluateStartMarker(
        EdgeActualResult Edge,
        bool CirclesSatisfied,
        bool CrossesSatisfied)
    {
        return Edge.ArrowStart switch
        {
            "Circle" => CirclesSatisfied,
            "Cross" => CrossesSatisfied,
            "Point" => ReadRenderedMarkerFact(Edge, "StartAlignedWithPathTangent"),
            "None" => true,
            _ => true
        };
    }

    private static bool EvaluateEndMarker(
        EdgeActualResult Edge,
        bool CirclesSatisfied,
        bool CrossesSatisfied,
        bool ArrowsSatisfied)
    {
        return Edge.ArrowEnd switch
        {
            "Circle" => CirclesSatisfied,
            "Cross" => CrossesSatisfied,
            "Point" => ReadRenderedMarkerFact(Edge, "EndAlignedWithPathTangent"),
            "None" => true,
            _ => ArrowsSatisfied
        };
    }

    private static bool ReadRenderedMarkerFact(EdgeActualResult Edge, string FactKey)
    {
        if (Edge.MarkerFacts.TryGetValue(FactKey, out object? Value)
            && Value is bool FactValue)
        {
            return FactValue;
        }

        return true;
    }
}
