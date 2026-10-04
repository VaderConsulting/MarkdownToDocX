using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Engine;

/// <summary>
/// Evaluates <c>When</c> expressions from <c>ruleset.json</c> against an evaluation context.
/// </summary>
public static class RuleConditionEvaluator
{
    /// <summary>
    /// Returns true when the optional condition is absent or evaluates true.
    /// </summary>
    public static bool IsApplicable(JsonElement? When, EvaluationContext Context)
    {
        if (When is null)
        {
            return true;
        }

        if (!When.HasValue)
        {
            return true;
        }

        return EvaluateCondition(When.Value, Context);
    }

    private static bool EvaluateCondition(JsonElement Condition, EvaluationContext Context)
    {
        if (Condition.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (Condition.TryGetProperty("All", out JsonElement AllElement))
        {
            foreach (JsonElement Child in AllElement.EnumerateArray())
            {
                if (!EvaluateCondition(Child, Context))
                {
                    return false;
                }
            }

            return true;
        }

        if (Condition.TryGetProperty("Any", out JsonElement AnyElement))
        {
            foreach (JsonElement Child in AnyElement.EnumerateArray())
            {
                if (EvaluateCondition(Child, Context))
                {
                    return true;
                }
            }

            return false;
        }

        if (Condition.TryGetProperty("Not", out JsonElement NotElement))
        {
            return !EvaluateCondition(NotElement, Context);
        }

        if (!Condition.TryGetProperty("Field", out JsonElement FieldElement))
        {
            return false;
        }

        if (!Condition.TryGetProperty("Operator", out JsonElement OperatorElement))
        {
            return false;
        }

        string Field = FieldElement.GetString() ?? string.Empty;
        string Operator = OperatorElement.GetString() ?? string.Empty;
        object? ActualValue = ResolveFieldValue(Field, Context);
        JsonElement? ExpectedElement = null;
        if (Condition.TryGetProperty("Value", out JsonElement ValueElement))
        {
            ExpectedElement = ValueElement;
        }

        return Compare(Operator, ActualValue, ExpectedElement);
    }

    private static object? ResolveFieldValue(string Field, EvaluationContext Context)
    {
        if (Context.FieldValues.TryGetValue(Field, out object? DirectValue))
        {
            return DirectValue;
        }

        string[] Parts = Field.Split('.');
        if (Parts.Length == 0)
        {
            return null;
        }

        if (Parts[0] == "Diagram")
        {
            return ResolveDiagramField(Parts, Context.Diagram);
        }

        if (Parts[0] == "Node" && Context.Node is not null)
        {
            return ResolveNodeField(Parts, Context.Node);
        }

        if (Parts[0] == "Edge" && Context.Edge is not null)
        {
            return ResolveEdgeField(Parts, Context.Edge);
        }

        if (Parts[0] == "Group" && Context.Group is not null)
        {
            return ResolveGroupField(Parts, Context.Group);
        }

        if (Parts[0] == "SourceNode" && Context.Edge is not null)
        {
            NodeActualResult? SourceNode = FindNode(Context.Diagram, Context.Edge.SourceNodeId);
            if (SourceNode is not null)
            {
                return ResolveNodeField(Parts, SourceNode, SkipPrefix: true);
            }
        }

        if (Parts[0] == "TargetNode" && Context.Edge is not null)
        {
            NodeActualResult? TargetNode = FindNode(Context.Diagram, Context.Edge.TargetNodeId);
            if (TargetNode is not null)
            {
                return ResolveNodeField(Parts, TargetNode, SkipPrefix: true);
            }
        }

        if (Parts[0] == "Implementation")
        {
            return ResolveDictionaryField(Context.Diagram.Implementation, Parts, 1);
        }

        if (Parts[0] == "Recognition")
        {
            return ResolveDictionaryField(Context.Diagram.Recognition, Parts, 1);
        }

        if (Parts[0] == "Config")
        {
            return ResolveDictionaryField(Context.Diagram.Config, Parts, 1);
        }

        if (Parts[0] == "EdgeSet")
        {
            if (Context.FieldValues.TryGetValue("EdgeSet." + string.Join('.', Parts, 1, Parts.Length - 1), out object? EdgeSetValue))
            {
                return EdgeSetValue;
            }
        }

        if (Context.Edge is not null
            && (Parts[0] == "Path" || Parts[0] == "Ports" || Parts[0] == "Layout"))
        {
            string FactKey = string.Join('.', Parts);
            if (Context.Edge.Facts.TryGetValue(FactKey, out object? EdgeFactValue))
            {
                return EdgeFactValue;
            }
        }

        return null;
    }

    private static NodeActualResult? FindNode(DiagramActualResult Diagram, string NodeId)
    {
        foreach (NodeActualResult Node in Diagram.Nodes)
        {
            if (Node.NodeId == NodeId)
            {
                return Node;
            }
        }

        return null;
    }

    private static object? ResolveDiagramField(string[] Parts, DiagramActualResult Diagram)
    {
        if (Parts.Length < 2)
        {
            return null;
        }

        return Parts[1] switch
        {
            "Kind" => Diagram.Kind,
            "Direction" => Diagram.Direction,
            "NodeCount" => Diagram.Nodes.Count,
            "LayoutEngine" => Diagram.Config.TryGetValue("LayoutEngine", out object? LayoutEngine) ? LayoutEngine : null,
            "HasStyleMutations" => Diagram.Recognition.TryGetValue("HasStyleMutations", out object? HasStyleMutations) ? HasStyleMutations : false,
            "HasUnsupportedFeature" => Diagram.Diagnostics.Count > 0,
            _ => ResolveDictionaryField(Diagram.Recognition, Parts, 1)
        };
    }

    private static object? ResolveNodeField(string[] Parts, NodeActualResult Node, bool SkipPrefix = false)
    {
        int Index = SkipPrefix ? 0 : 1;
        if (Parts.Length <= Index)
        {
            return null;
        }

        string FieldName = Parts[Index];
        return FieldName switch
        {
            "Shape" => Node.Shape,
            "IsRendered" => Node.IsRendered,
            "IsConnected" => Node.IsConnected,
            "DeclarationCount" => Node.DeclarationCount,
            "InDegree" => Node.InDegree,
            "OutDegree" => Node.OutDegree,
            "ParentGroupId" => Node.ParentGroupId,
            _ => Node.Facts.TryGetValue(FieldName, out object? FactValue) ? FactValue : null
        };
    }

    private static object? ResolveEdgeField(string[] Parts, EdgeActualResult Edge)
    {
        if (Parts.Length < 2)
        {
            return null;
        }

        return Parts[1] switch
        {
            "IsVisible" => Edge.IsVisible,
            "RouteClass" => Edge.RouteClass,
            "PathFamily" => Edge.PathFamily,
            "Stroke" => Edge.Stroke,
            "ArrowStart" => Edge.ArrowStart,
            "ArrowEnd" => Edge.ArrowEnd,
            "IsBidirectional" => Edge.IsBidirectional,
            "IsPrimaryFlow" => Edge.IsPrimaryFlow,
            "MinLength" => Edge.MinLength,
            "LinkToken" => Edge.Facts.TryGetValue("LinkToken", out object? LinkToken) ? LinkToken : null,
            "RequiresExpansion" => Edge.Facts.TryGetValue("RequiresExpansion", out object? RequiresExpansion) ? RequiresExpansion : false,
            "RequiresGroupExpansion" => Edge.Facts.TryGetValue("RequiresGroupExpansion", out object? RequiresGroupExpansion) ? RequiresGroupExpansion : false,
            "LabelSyntax" => Edge.Facts.TryGetValue("LabelSyntax", out object? LabelSyntax) ? LabelSyntax : null,
            "IsSubgraphInternal" => Edge.Facts.TryGetValue("IsSubgraphInternal", out object? IsSubgraphInternal) ? IsSubgraphInternal : false,
            "IsSubgraphCrossBoundary" => Edge.Facts.TryGetValue("IsSubgraphCrossBoundary", out object? IsSubgraphCrossBoundary) ? IsSubgraphCrossBoundary : false,
            "Label" => Edge.Label,
            _ => Edge.Facts.TryGetValue(Parts[1], out object? FactValue) ? FactValue : null
        };
    }

    private static object? ResolveGroupField(string[] Parts, GroupActualResult Group)
    {
        if (Parts.Length < 2)
        {
            return null;
        }

        return Parts[1] switch
        {
            "IsSubgraph" => Group.IsSubgraph,
            "NestingDepth" => Group.NestingDepth,
            "LocalDirection" => Group.LocalDirection,
            _ => Group.Facts.TryGetValue(Parts[1], out object? FactValue) ? FactValue : null
        };
    }

    private static object? ResolveDictionaryField(Dictionary<string, object> Values, string[] Parts, int StartIndex)
    {
        object? Current = Values;
        for (int Index = StartIndex; Index < Parts.Length; Index++)
        {
            if (Current is not Dictionary<string, object> Dictionary)
            {
                return null;
            }

            if (!Dictionary.TryGetValue(Parts[Index], out Current))
            {
                return null;
            }
        }

        return Current;
    }

    private static bool Compare(string Operator, object? ActualValue, JsonElement? ExpectedElement)
    {
        switch (Operator)
        {
            case "Exists":
                if (ActualValue is null)
                {
                    return false;
                }

                if (ActualValue is string StringValue)
                {
                    return !string.IsNullOrWhiteSpace(StringValue);
                }

                return true;
            case "NotExists":
                return ActualValue is null || (ActualValue is string Text && string.IsNullOrWhiteSpace(Text));
            case "Equals":
                return ValuesEqual(ActualValue, ExpectedElement);
            case "NotEquals":
                return !ValuesEqual(ActualValue, ExpectedElement);
            case "In":
                return IsInList(ActualValue, ExpectedElement);
            case "NotIn":
                return !IsInList(ActualValue, ExpectedElement);
            case "GreaterThan":
                return CompareNumbers(ActualValue, ExpectedElement) > 0;
            case "GreaterThanOrEqual":
                return CompareNumbers(ActualValue, ExpectedElement) >= 0;
            case "LessThan":
                return CompareNumbers(ActualValue, ExpectedElement) < 0;
            case "LessThanOrEqual":
                return CompareNumbers(ActualValue, ExpectedElement) <= 0;
            default:
                return false;
        }
    }

    private static bool ValuesEqual(object? ActualValue, JsonElement? ExpectedElement)
    {
        if (ExpectedElement is null)
        {
            return ActualValue is null;
        }

        JsonElement Expected = ExpectedElement.Value;
        if (ActualValue is null)
        {
            return Expected.ValueKind == JsonValueKind.Null;
        }

        if (Expected.ValueKind == JsonValueKind.True || Expected.ValueKind == JsonValueKind.False)
        {
            return ActualValue is bool BoolValue && BoolValue == Expected.GetBoolean();
        }

        if (Expected.ValueKind == JsonValueKind.Number)
        {
            return CompareNumbers(ActualValue, Expected) == 0;
        }

        string ExpectedText = Expected.GetString() ?? string.Empty;
        return string.Equals(Convert.ToString(ActualValue, CultureInfo.InvariantCulture), ExpectedText, StringComparison.Ordinal);
    }

    private static bool IsInList(object? ActualValue, JsonElement? ExpectedElement)
    {
        if (ExpectedElement is null || ExpectedElement.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement Item in ExpectedElement.Value.EnumerateArray())
        {
            if (ValuesEqual(ActualValue, Item))
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareNumbers(object? ActualValue, JsonElement? ExpectedElement)
    {
        double ActualNumber = Convert.ToDouble(ActualValue ?? 0, CultureInfo.InvariantCulture);
        double ExpectedNumber = ExpectedElement?.GetDouble() ?? 0;
        if (ActualNumber < ExpectedNumber)
        {
            return -1;
        }

        if (ActualNumber > ExpectedNumber)
        {
            return 1;
        }

        return 0;
    }
}
