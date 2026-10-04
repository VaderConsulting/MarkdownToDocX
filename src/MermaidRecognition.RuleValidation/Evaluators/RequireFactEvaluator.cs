using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using MermaidRecognition.RuleValidation.Engine;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Evaluators;

/// <summary>
/// Evaluates <c>Require</c> facts from actual-results models and render diagnostics.
/// </summary>
public sealed class RequireFactEvaluator : IRuleEvaluator
{
    /// <inheritdoc />
    public string EvaluatorId => "RequireFactEvaluator";

    /// <inheritdoc />
    public RuleEvaluationResult Evaluate(
        RuleDefinition RuleDefinition,
        EvaluatorBinding EvaluatorBinding,
        ActualResultsDocument ActualResults,
        EvaluationContext EvaluationContext)
    {
        RuleEvaluationResult Result = new RuleEvaluationResult();
        if (RuleDefinition.Require.Count == 0)
        {
            return Result;
        }

        Dictionary<string, object?> FactValues = BuildFactDictionary(EvaluationContext);
        foreach (KeyValuePair<string, JsonElement> RequiredEntry in RuleDefinition.Require)
        {
            bool Expected = RequiredEntry.Value.ValueKind == JsonValueKind.True;
            if (!TryResolveFact(FactValues, RequiredEntry.Key, out object? ActualValue))
            {
                if (Expected)
                {
                    Result.Status = "Fail";
                    Result.Failures.Add(CreateFailure(
                        RuleDefinition,
                        EvaluatorBinding,
                        EvaluationContext,
                        RequiredEntry.Key,
                        Expected,
                        null,
                        "Missing actual field for required fact."));
                }

                continue;
            }

            bool Actual = ConvertToBoolean(ActualValue);
            if (Actual != Expected)
            {
                Result.Status = "Fail";
                Result.Failures.Add(CreateFailure(
                    RuleDefinition,
                    EvaluatorBinding,
                    EvaluationContext,
                    RequiredEntry.Key,
                    Expected,
                    Actual,
                    RuleDefinition.DiagnosticTemplate));
            }
        }

        return Result;
    }

    private static Dictionary<string, object?> BuildFactDictionary(EvaluationContext Context)
    {
        Dictionary<string, object?> Facts = new Dictionary<string, object?>(StringComparer.Ordinal);
        MergeSectionFacts(Facts, Context.Diagram.Recognition, "Recognition.");
        MergeSectionFacts(Facts, Context.Diagram.Layout, "Layout.");
        MergeSectionFacts(Facts, Context.Diagram.Routing, "Routing.");
        MergeSectionFacts(Facts, Context.Diagram.Implementation, "Implementation.");

        if (Context.Diagram.Recognition.TryGetValue("IsCanonical", out object? IsCanonical))
        {
            Facts["Direction.IsCanonical"] = UnwrapValue(IsCanonical);
        }

        if (Context.Node is not null)
        {
            MergeElementFacts(Facts, Context.Node.Facts, "Node.");
        }

        if (Context.Edge is not null)
        {
            MergeElementFacts(Facts, Context.Edge.Facts, "Edge.");
            MergeSectionFacts(Facts, Context.Edge.LabelFacts, "Label.");
            MergeSectionFacts(Facts, Context.Edge.MarkerFacts, "Marker.");
        }

        if (Context.Group is not null)
        {
            MergeElementFacts(Facts, Context.Group.Facts, "Group.");
        }

        foreach (KeyValuePair<string, object> FieldValue in Context.FieldValues)
        {
            Facts[FieldValue.Key] = UnwrapValue(FieldValue.Value);
        }

        return Facts;
    }

    private static void MergeSectionFacts(
        Dictionary<string, object?> Target,
        Dictionary<string, object> Source,
        string Prefix)
    {
        foreach (KeyValuePair<string, object> Entry in Source)
        {
            object? Value = UnwrapValue(Entry.Value);
            Target[Prefix + Entry.Key] = Value;
            Target[Entry.Key] = Value;
        }
    }

    private static void MergeElementFacts(
        Dictionary<string, object?> Target,
        Dictionary<string, object> Source,
        string ElementPrefix)
    {
        foreach (KeyValuePair<string, object> Entry in Source)
        {
            object? Value = UnwrapValue(Entry.Value);
            Target[Entry.Key] = Value;
            if (!Entry.Key.Contains('.', StringComparison.Ordinal))
            {
                Target[ElementPrefix + Entry.Key] = Value;
            }
        }
    }

    private static bool TryResolveFact(
        Dictionary<string, object?> Facts,
        string RequireKey,
        out object? Value)
    {
        if (Facts.TryGetValue(RequireKey, out Value))
        {
            return true;
        }

        int LastDot = RequireKey.LastIndexOf('.');
        if (LastDot > 0 && LastDot < RequireKey.Length - 1)
        {
            string Suffix = RequireKey.Substring(LastDot + 1);
            if (Facts.TryGetValue(Suffix, out Value))
            {
                return true;
            }
        }

        Value = null;
        return false;
    }

    private static object? UnwrapValue(object? Value)
    {
        if (Value is JsonElement JsonElement)
        {
            return JsonElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => JsonElement.GetDouble(),
                JsonValueKind.String => JsonElement.GetString(),
                JsonValueKind.Null => null,
                _ => JsonElement.ToString()
            };
        }

        return Value;
    }

    private static bool ConvertToBoolean(object? Value)
    {
        Value = UnwrapValue(Value);
        if (Value is bool BoolValue)
        {
            return BoolValue;
        }

        if (Value is null)
        {
            return false;
        }

        if (bool.TryParse(Convert.ToString(Value, CultureInfo.InvariantCulture), out bool Parsed))
        {
            return Parsed;
        }

        return false;
    }

    private static ValidationFailureRecord CreateFailure(
        RuleDefinition RuleDefinition,
        EvaluatorBinding EvaluatorBinding,
        EvaluationContext Context,
        string RequireKey,
        bool Expected,
        object? Actual,
        string MessageTemplate)
    {
        string HumanElementDescription = ValidationFailureFormatter.DescribeElement(Context);
        string Message = ValidationFailureFormatter.BuildMessage(
            MessageTemplate,
            RuleDefinition.RuleId,
            Context,
            RequireKey,
            HumanElementDescription);

        ValidationFailureRecord Failure = new ValidationFailureRecord
        {
            RuleId = RuleDefinition.RuleId,
            EvaluatorId = EvaluatorBinding.EvaluatorId,
            Severity = RuleDefinition.Severity,
            DiagramId = Context.Diagram.DiagramId,
            ElementType = Context.ElementType,
            ElementId = Context.ElementId,
            Message = Message,
            Expected = new Dictionary<string, object> { [RequireKey] = Expected },
            Actual = new Dictionary<string, object> { [RequireKey] = Actual ?? "Missing" },
            SuggestedCorrection = ValidationFailureFormatter.BuildSuggestedCorrection(
                RuleDefinition.RuleId,
                RequireKey,
                Context,
                HumanElementDescription,
                RuleDefinition.SuggestedCorrectionTemplate)
        };
        ValidationFailureFormatter.PopulateHumanReadableFields(Failure, Context, HumanElementDescription);
        return Failure;
    }
}
