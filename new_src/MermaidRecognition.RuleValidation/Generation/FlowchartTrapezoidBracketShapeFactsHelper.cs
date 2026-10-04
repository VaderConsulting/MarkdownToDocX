using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates rendered SVG geometry for legacy trapezoid bracket node shapes (F81/F82).
/// </summary>
public static class FlowchartTrapezoidBracketShapeFactsHelper
{
    /// <summary>
    /// Parses rendered SVG and records bracket-shape geometry facts on nodes.
    /// </summary>
    public static void ApplyRenderedSvgFacts(DiagramActualResult Diagram, DiagramContract Contract, string Svg)
    {
        List<string> NodeFillPaths = ExtractNodeFillPaths(Svg);
        HashSet<string> ClaimedPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (NodeActualResult Node in Diagram.Nodes.Where(Candidate => Candidate.IsRendered))
        {
            Node.Facts["RenderedAsTrapezoidBracket"] = true;
            Node.Facts["LabelDelimiterStripped"] = true;

            if (!FlowchartTrapezoidBracketPathAnalyser.IsTrapezoidBracketShape(Node.Shape))
            {
                continue;
            }

            string Label = (Node.Label ?? Node.NodeId).Trim();
            bool LabelDelimiterStripped = !Svg.Contains("/" + Label + "/", StringComparison.Ordinal)
                && !Svg.Contains(@"\" + Label + @"\", StringComparison.Ordinal);
            Node.Facts["LabelDelimiterStripped"] = LabelDelimiterStripped;

            string? MatchedPath = NodeFillPaths
                .FirstOrDefault(Path =>
                    !ClaimedPaths.Contains(Path)
                    && FlowchartTrapezoidBracketPathAnalyser.ValidateRenderedBracketGeometry(Node.Shape, Path));

            if (MatchedPath is null)
            {
                Node.Facts["RenderedAsTrapezoidBracket"] = false;
                continue;
            }

            ClaimedPaths.Add(MatchedPath);
            Node.Facts["RenderedAsTrapezoidBracket"] = LabelDelimiterStripped;
        }
    }

    private static List<string> ExtractNodeFillPaths(string Svg)
    {
        List<string> FillPaths = [];
        MatchCollection PathMatches = Regex.Matches(
            Svg,
            @"<path d=""([^""]+)"" fill=""([^""]+)""",
            RegexOptions.CultureInvariant);

        foreach (Match Match in PathMatches)
        {
            if (Match.Groups.Count > 2
                && !string.Equals(Match.Groups[2].Value, "none", StringComparison.OrdinalIgnoreCase))
            {
                FillPaths.Add(Match.Groups[1].Value);
            }
        }

        return FillPaths;
    }
}
