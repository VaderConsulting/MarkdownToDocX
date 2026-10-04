using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Validates node shape recognition facts from the diagram contract.
/// </summary>
public static class FlowchartShapeRecognitionFactsHelper
{
    private static readonly HashSet<string> KnownCanonicalShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "rect",
        "rounded",
        "diamond",
        "circle",
        "doublecircle",
        "stadium",
        "cylinder",
        "subroutine",
        "hexagon",
        "asymmetric",
        "lean_right",
        "lean_left",
        "trapezoid",
        "inv_trapezoid"
    };

    /// <summary>
    /// Populates shape recognition facts on diagram nodes.
    /// </summary>
    public static void Apply(DiagramActualResult Diagram, DiagramContract Contract)
    {
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node, StringComparer.Ordinal);

        foreach (NodeActualResult Node in Diagram.Nodes)
        {
            if (!NodeById.TryGetValue(Node.NodeId, out NodeContract? SourceNode))
            {
                continue;
            }

            string Shape = string.IsNullOrWhiteSpace(SourceNode.Shape) ? "rect" : SourceNode.Shape.Trim();
            string ShapeSource = string.IsNullOrWhiteSpace(SourceNode.ShapeSource) ? "syntax" : SourceNode.ShapeSource.Trim();

            Node.Shape = Shape;
            Node.Facts["ShapeSource"] = ShapeSource;
            Node.Facts["TrapezoidBracketShapeRecognised"] = EvaluateTrapezoidBracketRecognition(Shape, ShapeSource);
            Node.Facts["ShapeCanonicalised"] = EvaluateShapeCanonicalised(Shape, ShapeSource);
        }
    }

    private static bool EvaluateShapeCanonicalised(string Shape, string ShapeSource) => KnownCanonicalShapes.Contains(Shape);

    private static bool EvaluateTrapezoidBracketRecognition(string Shape, string ShapeSource)
    {
        if (!FlowchartTrapezoidBracketPathAnalyser.IsTrapezoidBracketShape(Shape))
        {
            return true;
        }

        return string.Equals(ShapeSource, "bracket", StringComparison.OrdinalIgnoreCase);
    }
}
