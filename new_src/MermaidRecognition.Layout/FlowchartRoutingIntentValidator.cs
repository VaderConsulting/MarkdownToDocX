using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>
/// Validates routing-intent output against expected requirements in <c>ruleset.json</c>.
/// </summary>
public static class FlowchartRoutingIntentValidator
{
    /// <summary>
    /// Validates one diagram result against the matching diagram expectation in the ruleset.
    /// </summary>
    public static FlowchartRoutingIntentValidationResult ValidateAgainstRuleset(
        FlowchartRoutingIntentDiagramResult Intent,
        FlowchartRoutingIntentRuleset Ruleset)
    {
        FlowchartRoutingIntentValidationResult Result = new FlowchartRoutingIntentValidationResult
        {
            DiagramId = Intent.DiagramId
        };

        FlowchartRoutingIntentDiagramExpectation? DiagramExpectation = Ruleset.ResolveDiagramExpectations()
            .FirstOrDefault(
                Diagram => string.Equals(Diagram.DiagramId, Intent.DiagramId, StringComparison.Ordinal));
        if (DiagramExpectation is null)
        {
            Result.Findings.Add(
                new FlowchartRoutingIntentValidationFinding
                {
                    EdgeId = string.Empty,
                    Message = "No ruleset entry found for diagram " + Intent.DiagramId + "."
                });
            Result.IntentValid = false;
            return Result;
        }

        if (!string.IsNullOrWhiteSpace(DiagramExpectation.Direction)
            && !string.Equals(DiagramExpectation.Direction, Intent.Direction, StringComparison.OrdinalIgnoreCase))
        {
            Result.Findings.Add(
                new FlowchartRoutingIntentValidationFinding
                {
                    EdgeId = string.Empty,
                    Message = "Diagram direction mismatch. Expected "
                        + DiagramExpectation.Direction
                        + " but got "
                        + Intent.Direction
                        + "."
                });
        }

        foreach (FlowchartRoutingIntentEdgeExpectation EdgeExpectation in DiagramExpectation.Edges)
        {
            FlowchartEdgeRoutingIntentRecord? ActualEdge = FindMatchingEdge(Intent, EdgeExpectation);
            if (ActualEdge is null)
            {
                Result.Findings.Add(
                    new FlowchartRoutingIntentValidationFinding
                    {
                        EdgeId = EdgeExpectation.EdgeId,
                        Message = "Expected edge "
                            + EdgeExpectation.SourceNodeId
                            + "->"
                            + EdgeExpectation.TargetNodeId
                            + " was not found in routing intent output."
                    });
                continue;
            }

            ValidateEdge(Result, ActualEdge, EdgeExpectation, Ruleset.GlobalInvalidBehaviours);
        }

        Result.IntentValid = Result.Findings.Count == 0;
        return Result;
    }

    private static FlowchartEdgeRoutingIntentRecord? FindMatchingEdge(
        FlowchartRoutingIntentDiagramResult Intent,
        FlowchartRoutingIntentEdgeExpectation EdgeExpectation)
    {
        if (!string.IsNullOrWhiteSpace(EdgeExpectation.EdgeId))
        {
            FlowchartEdgeRoutingIntentRecord? ById = Intent.Edges
                .FirstOrDefault(
                    Edge => string.Equals(Edge.EdgeId, EdgeExpectation.EdgeId, StringComparison.Ordinal));
            if (ById is not null)
            {
                return ById;
            }
        }

        return Intent.Edges.FirstOrDefault(
            Edge => string.Equals(Edge.SourceNodeId, EdgeExpectation.SourceNodeId, StringComparison.Ordinal)
                && string.Equals(Edge.TargetNodeId, EdgeExpectation.TargetNodeId, StringComparison.Ordinal));
    }

    private static void ValidateEdge(
        FlowchartRoutingIntentValidationResult Result,
        FlowchartEdgeRoutingIntentRecord ActualEdge,
        FlowchartRoutingIntentEdgeExpectation EdgeExpectation,
        List<string> GlobalInvalidBehaviours)
    {
        if (!string.IsNullOrWhiteSpace(EdgeExpectation.RouteClass)
            && !string.Equals(
                EdgeExpectation.RouteClass,
                ActualEdge.RouteClass.ToString(),
                StringComparison.Ordinal))
        {
            Result.Findings.Add(
                new FlowchartRoutingIntentValidationFinding
                {
                    EdgeId = ActualEdge.EdgeId,
                    Message = "RouteClass mismatch for "
                        + ActualEdge.SourceNodeId
                        + "->"
                        + ActualEdge.TargetNodeId
                        + ". Expected "
                        + EdgeExpectation.RouteClass
                        + " but got "
                        + ActualEdge.RouteClass
                        + "."
                });
        }

        ValidateOptionalEnum(
            Result,
            ActualEdge,
            EdgeExpectation.SourceBoundaryIntent,
            ActualEdge.SourceBoundaryIntent.ToString(),
            "SourceBoundaryIntent");
        ValidateOptionalEnum(
            Result,
            ActualEdge,
            EdgeExpectation.TargetBoundaryIntent,
            ActualEdge.TargetBoundaryIntent.ToString(),
            "TargetBoundaryIntent");
        ValidateOptionalEnum(
            Result,
            ActualEdge,
            EdgeExpectation.PathShapeIntent,
            ActualEdge.PathShapeIntent.ToString(),
            "PathShapeIntent");
        ValidateOptionalEnum(
            Result,
            ActualEdge,
            EdgeExpectation.LabelPlacementIntent,
            ActualEdge.LabelPlacementIntent.ToString(),
            "LabelPlacementIntent");
        ValidateOptionalEnum(
            Result,
            ActualEdge,
            EdgeExpectation.ObstacleAvoidanceIntent,
            ActualEdge.ObstacleAvoidanceIntent.ToString(),
            "ObstacleAvoidanceIntent");

        ValidateRequiredStrings(
            Result,
            ActualEdge,
            EdgeExpectation.ExpectedBehaviour,
            ActualEdge.ExpectedBehaviour,
            "ExpectedBehaviour");
        ValidateRequiredStrings(
            Result,
            ActualEdge,
            EdgeExpectation.InvalidBehaviours,
            ActualEdge.InvalidBehaviours,
            "InvalidBehaviours");

        List<string> ForbiddenInvalidBehaviours = EdgeExpectation.MustNotIncludeInvalidBehaviours
            .Concat(GlobalInvalidBehaviours)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (string ForbiddenBehaviour in ForbiddenInvalidBehaviours)
        {
            if (ActualEdge.InvalidBehaviours.Contains(ForbiddenBehaviour, StringComparer.Ordinal))
            {
                Result.Findings.Add(
                    new FlowchartRoutingIntentValidationFinding
                    {
                        EdgeId = ActualEdge.EdgeId,
                        Message = "Invalid behaviour "
                            + ForbiddenBehaviour
                            + " must not be listed for edge "
                            + ActualEdge.SourceNodeId
                            + "->"
                            + ActualEdge.TargetNodeId
                            + "."
                    });
            }
        }
    }

    private static void ValidateOptionalEnum(
        FlowchartRoutingIntentValidationResult Result,
        FlowchartEdgeRoutingIntentRecord ActualEdge,
        string ExpectedValue,
        string ActualValue,
        string FieldName)
    {
        if (string.IsNullOrWhiteSpace(ExpectedValue))
        {
            return;
        }

        if (!string.Equals(ExpectedValue, ActualValue, StringComparison.Ordinal))
        {
            Result.Findings.Add(
                new FlowchartRoutingIntentValidationFinding
                {
                    EdgeId = ActualEdge.EdgeId,
                    Message = FieldName
                        + " mismatch for "
                        + ActualEdge.SourceNodeId
                        + "->"
                        + ActualEdge.TargetNodeId
                        + ". Expected "
                        + ExpectedValue
                        + " but got "
                        + ActualValue
                        + "."
                });
        }
    }

    private static void ValidateRequiredStrings(
        FlowchartRoutingIntentValidationResult Result,
        FlowchartEdgeRoutingIntentRecord ActualEdge,
        List<string> RequiredValues,
        List<string> ActualValues,
        string FieldName)
    {
        foreach (string RequiredValue in RequiredValues)
        {
            if (!ActualValues.Contains(RequiredValue, StringComparer.Ordinal))
            {
                Result.Findings.Add(
                    new FlowchartRoutingIntentValidationFinding
                    {
                        EdgeId = ActualEdge.EdgeId,
                        Message = FieldName
                            + " missing required value "
                            + RequiredValue
                            + " for edge "
                            + ActualEdge.SourceNodeId
                            + "->"
                            + ActualEdge.TargetNodeId
                            + "."
                    });
            }
        }
    }
}
