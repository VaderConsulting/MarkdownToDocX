using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MermaidRecognition.RuleValidation.Evaluators;
using MermaidRecognition.RuleValidation.Generation;
using MermaidRecognition.RuleValidation.Loading;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Engine;

/// <summary>
/// Executes the universal ruleset against an actual-results document.
/// </summary>
/// <remarks>
/// Creates a rules engine from loaded specification artefacts.
/// </remarks>
public sealed class RulesEngine(LoadedSpecBundle SpecBundle, IEnumerable<IRuleEvaluator> Evaluators)
{
    private readonly RulesetDocument _Ruleset = SpecBundle.Ruleset;

    private readonly EvaluatorMapDocument _EvaluatorMap = SpecBundle.EvaluatorMap;

    private readonly Dictionary<string, EvaluatorBinding> _BindingsByRuleId = SpecBundle.EvaluatorMap.EvaluatorBindings
            .ToDictionary(Binding => Binding.RuleId, Binding => Binding);

    private readonly Dictionary<string, IRuleEvaluator> _EvaluatorsById = Evaluators.ToDictionary(Evaluator => Evaluator.EvaluatorId, Evaluator => Evaluator);

    /// <summary>
    /// Validates all diagrams in the actual-results document.
    /// </summary>
    public ValidationReportDocument Validate(ActualResultsDocument ActualResults)
    {
        ValidationReportDocument Report = new ValidationReportDocument
        {
            RunId = ActualResults.RunId,
            Summary = new ValidationReportSummary
            {
                DiagramCount = ActualResults.Diagrams.Count,
                RuleCount = _Ruleset.Rules.Count
            }
        };

        foreach (DiagramActualResult Diagram in ActualResults.Diagrams)
        {
            DiagramValidationResult DiagramResult = new DiagramValidationResult
            {
                DiagramId = Diagram.DiagramId,
                ParserSucceeded = Diagram.ParserSucceeded,
                LayoutSucceeded = Diagram.LayoutSucceeded,
                IsFullyRemarked = Diagram.IsFullyRemarked
            };

            if (Diagram.IsFullyRemarked || !Diagram.ParserSucceeded)
            {
                DiagramResult.Result = "Skipped";
                Report.Summary.SkippedDiagramCount++;
                Report.DiagramResults.Add(DiagramResult);
                continue;
            }

            EvaluateDiagram(Diagram, Report, DiagramResult);
            Report.DiagramResults.Add(DiagramResult);
        }

        Report.Summary.PassCount = Report.DiagramResults.Sum(Diagram => Diagram.PassCount);
        Report.Summary.WarningCount = Report.DiagramResults.Sum(Diagram => Diagram.WarningCount);
        Report.Summary.ErrorCount = Report.Failures.Count(Failure => Failure.Severity == "Error");
        Report.Summary.NotApplicableCount = (_Ruleset.Rules.Count * ActualResults.Diagrams.Count)
            - Report.Summary.PassCount
            - Report.Summary.WarningCount
            - Report.Summary.ErrorCount;

        if (Report.Failures.Any(Failure => Failure.Severity == "Error"))
        {
            Report.OverallResult = "Fail";
        }
        else if (Report.Failures.Any(Failure => Failure.Severity == "Warning"))
        {
            Report.OverallResult = "WarningOnly";
        }
        else
        {
            Report.OverallResult = "Pass";
        }

        foreach (DiagramValidationResult DiagramResult in Report.DiagramResults)
        {
            if (DiagramResult.Result == "Skipped")
            {
                continue;
            }

            DiagramResult.Result = DiagramResult.ErrorCount > 0
                ? "Fail"
                : DiagramResult.WarningCount > 0 ? "WarningOnly" : "Pass";
        }

        return Report;
    }

    private void EvaluateDiagram(
        DiagramActualResult Diagram,
        ValidationReportDocument Report,
        DiagramValidationResult DiagramResult)
    {
        foreach (RuleDefinition Rule in _Ruleset.Rules)
        {
            if (!_BindingsByRuleId.TryGetValue(Rule.RuleId, out EvaluatorBinding? Binding))
            {
                ValidationFailureRecord MissingBinding = new ValidationFailureRecord
                {
                    RuleId = Rule.RuleId,
                    EvaluatorId = "MissingBinding",
                    Severity = "Error",
                    DiagramId = Diagram.DiagramId,
                    ElementType = "Diagram",
                    ElementId = Diagram.DiagramId,
                    Message = "No evaluator binding exists for rule " + Rule.RuleId + ".",
                    SuggestedCorrection = "Add an evaluator binding in evaluator-map.json."
                };
                Report.Failures.Add(MissingBinding);
                DiagramResult.ErrorCount++;
                continue;
            }

            if (!_EvaluatorsById.TryGetValue(Binding.EvaluatorId, out IRuleEvaluator? Evaluator))
            {
                ValidationFailureRecord MissingEvaluator = new ValidationFailureRecord
                {
                    RuleId = Rule.RuleId,
                    EvaluatorId = Binding.EvaluatorId,
                    Severity = "Error",
                    DiagramId = Diagram.DiagramId,
                    ElementType = "Diagram",
                    ElementId = Diagram.DiagramId,
                    Message = "Evaluator " + Binding.EvaluatorId + " is not registered.",
                    SuggestedCorrection = "Register the evaluator implementation in the rules engine."
                };
                Report.Failures.Add(MissingEvaluator);
                DiagramResult.ErrorCount++;
                continue;
            }

            foreach (EvaluationContext Context in BuildContexts(Diagram, Rule))
            {
                if (!RuleConditionEvaluator.IsApplicable(Rule.When, Context))
                {
                    DiagramResult.NotApplicableCount++;
                    continue;
                }

                RuleEvaluationResult Evaluation = Evaluator.Evaluate(Rule, Binding, new ActualResultsDocument(), Context);
                if (Evaluation.Status == "Fail")
                {
                    foreach (ValidationFailureRecord Failure in Evaluation.Failures)
                    {
                        Report.Failures.Add(Failure);
                        if (Failure.Severity == "Error")
                        {
                            DiagramResult.ErrorCount++;
                        }
                        else if (Failure.Severity == "Warning")
                        {
                            DiagramResult.WarningCount++;
                        }
                    }
                }
                else
                {
                    DiagramResult.PassCount++;
                }
            }
        }
    }

    private static IEnumerable<EvaluationContext> BuildContexts(DiagramActualResult Diagram, RuleDefinition Rule)
    {
        switch (Rule.AppliesTo)
        {
            case "Node":
                if (RuleReferencesNodePair(Rule))
                {
                    foreach (EvaluationContext PairContext in BuildNodePairContexts(Diagram))
                    {
                        yield return PairContext;
                    }
                }
                else
                {
                    foreach (NodeActualResult Node in Diagram.Nodes)
                    {
                        yield return BuildNodeContext(Diagram, Node);
                    }
                }

                break;
            case "Edge":
            case "Edge label":
                foreach (EdgeActualResult Edge in Diagram.Edges)
                {
                    yield return BuildEdgeContext(Diagram, Edge);
                }

                break;
            case "Group":
                foreach (GroupActualResult Group in Diagram.Groups)
                {
                    yield return BuildGroupContext(Diagram, Group);
                }

                if (Diagram.Groups.Count == 0)
                {
                    yield return BuildDiagramContext(Diagram);
                }

                break;
            case "EdgeSet":
                yield return BuildEdgeSetContext(Diagram);
                break;
            case "Implementation":
                yield return BuildDiagramContext(Diagram);
                break;
            default:
                yield return BuildDiagramContext(Diagram);
                break;
        }
    }

    private static EvaluationContext BuildDiagramContext(DiagramActualResult Diagram)
    {
        EvaluationContext Context = new EvaluationContext
        {
            Diagram = Diagram,
            ElementType = "Diagram",
            ElementId = Diagram.DiagramId
        };
        PopulateDiagramFields(Context, Diagram);
        return Context;
    }

    private static EvaluationContext BuildNodeContext(DiagramActualResult Diagram, NodeActualResult Node)
    {
        EvaluationContext Context = new EvaluationContext
        {
            Diagram = Diagram,
            Node = Node,
            ElementType = "Node",
            ElementId = Node.NodeId
        };
        PopulateDiagramFields(Context, Diagram);
        Context.FieldValues["Node.IsRendered"] = Node.IsRendered;
        Context.FieldValues["Node.IsConnected"] = Node.IsConnected;
        Context.FieldValues["Node.DeclarationCount"] = Node.DeclarationCount;
        Context.FieldValues["Node.Shape"] = Node.Shape;
        Context.FieldValues["Node.ShapeSource"] = Node.Facts.TryGetValue("ShapeSource", out object? ShapeSource) ? ShapeSource : Node.Shape;
        Context.FieldValues["Node.ParentGroupId"] = Node.ParentGroupId ?? string.Empty;
        return Context;
    }

    private static EvaluationContext BuildEdgeContext(DiagramActualResult Diagram, EdgeActualResult Edge)
    {
        EvaluationContext Context = new EvaluationContext
        {
            Diagram = Diagram,
            Edge = Edge,
            ElementType = "Edge",
            ElementId = Edge.EdgeId
        };
        PopulateDiagramFields(Context, Diagram);
        Context.FieldValues["Edge.IsVisible"] = Edge.IsVisible;
        Context.FieldValues["Edge.RouteClass"] = Edge.RouteClass;
        Context.FieldValues["Edge.PathFamily"] = Edge.PathFamily;
        Context.FieldValues["Edge.Stroke"] = Edge.Stroke;
        Context.FieldValues["Edge.ArrowStart"] = Edge.ArrowStart;
        Context.FieldValues["Edge.ArrowEnd"] = Edge.ArrowEnd;
        Context.FieldValues["Edge.IsBidirectional"] = Edge.IsBidirectional;
        Context.FieldValues["Edge.IsPrimaryFlow"] = Edge.IsPrimaryFlow;
        Context.FieldValues["Edge.MinLength"] = Edge.MinLength;
        Context.FieldValues["Edge.Label"] = Edge.Label ?? string.Empty;
        Context.FieldValues["Edge.LinkToken"] = Edge.Facts.TryGetValue("LinkToken", out object? LinkToken) ? LinkToken : true;
        Context.FieldValues["Edge.RequiresExpansion"] = Edge.Facts.TryGetValue("RequiresExpansion", out object? RequiresExpansion) && Convert.ToBoolean(RequiresExpansion);
        Context.FieldValues["Edge.Label.IsLong"] = Edge.Facts.TryGetValue("Label.IsLong", out object? IsLong) && Convert.ToBoolean(IsLong);
        return Context;
    }

    private static EvaluationContext BuildGroupContext(DiagramActualResult Diagram, GroupActualResult Group)
    {
        EvaluationContext Context = new EvaluationContext
        {
            Diagram = Diagram,
            Group = Group,
            ElementType = "Group",
            ElementId = Group.GroupId
        };
        PopulateDiagramFields(Context, Diagram);
        Context.FieldValues["Group.IsSubgraph"] = Group.IsSubgraph;
        Context.FieldValues["Group.NestingDepth"] = Group.NestingDepth;
        Context.FieldValues["Group.LocalDirection"] = Group.LocalDirection ?? string.Empty;
        return Context;
    }

    private static EvaluationContext BuildEdgeSetContext(DiagramActualResult Diagram)
    {
        EvaluationContext Context = BuildDiagramContext(Diagram);
        Context.ElementType = "EdgeSet";
        Context.ElementId = Diagram.DiagramId + ":edges";
        Context.FieldValues["EdgeSet.HasVisibleEdges"] = Diagram.Edges.Any(Edge => Edge.IsVisible);
        Context.FieldValues["EdgeSet.IsSiblingDecisionBranchSet"] = Diagram.Edges.Any(Edge => Edge.RouteClass == "LocalDecisionBranch");
        if (Diagram.Routing.TryGetValue("Path.SiblingBranchesDoNotCross", out object? SiblingBranchesValue))
        {
            Context.FieldValues["Path.SiblingBranchesDoNotCross"] = SiblingBranchesValue;
        }

        return Context;
    }

    private static bool RuleReferencesNodePair(RuleDefinition Rule)
    {
        if (Rule.When is null || !Rule.When.HasValue)
        {
            return false;
        }

        return Rule.When.Value.GetRawText().Contains("NodePair", StringComparison.Ordinal);
    }

    private static IEnumerable<EvaluationContext> BuildNodePairContexts(DiagramActualResult Diagram)
    {
        List<NodeActualResult> RenderedNodes = [.. Diagram.Nodes.Where(Node => Node.IsRendered && Node.Bounds.Width > 0 && Node.Bounds.Height > 0)];

        for (int LeftIndex = 0; LeftIndex < RenderedNodes.Count; LeftIndex++)
        {
            for (int RightIndex = LeftIndex + 1; RightIndex < RenderedNodes.Count; RightIndex++)
            {
                NodeActualResult LeftNode = RenderedNodes[LeftIndex];
                NodeActualResult RightNode = RenderedNodes[RightIndex];
                bool Overlaps = FlowchartLayoutOverlapHelper.NodeBoundsOverlap(LeftNode.Bounds, RightNode.Bounds);

                EvaluationContext Context = BuildNodeContext(Diagram, LeftNode);
                Context.ElementType = "NodePair";
                Context.ElementId = LeftNode.NodeId + "+" + RightNode.NodeId;
                Context.FieldValues["NodePair.IsUnrelated"] = true;
                Context.FieldValues["OtherNodeId"] = RightNode.NodeId;
                Context.FieldValues["OtherNodeLabel"] = RightNode.Label;
                Context.FieldValues["Layout.NodeBodiesDoNotOverlap"] = !Overlaps;
                yield return Context;
            }
        }
    }

    private static void PopulateDiagramFields(EvaluationContext Context, DiagramActualResult Diagram)
    {
        Context.FieldValues["Diagram.Kind"] = Diagram.Kind;
        Context.FieldValues["Diagram.Direction"] = Diagram.Direction;
        Context.FieldValues["Diagram.NodeCount"] = Diagram.Nodes.Count;
        Context.FieldValues["Diagram.LayoutEngine"] = Diagram.Config.TryGetValue("LayoutEngine", out object? LayoutEngine) ? LayoutEngine : "Dagre";
        Context.FieldValues["Diagram.HasStyleMutations"] = Diagram.Recognition.TryGetValue("HasStyleMutations", out object? HasStyleMutations) && Convert.ToBoolean(HasStyleMutations);
        Context.FieldValues["Diagram.HasUnsupportedFeature"] = Diagram.Recognition.TryGetValue("HasUnsupportedConstruct", out object? HasUnsupportedFeature)
            && Convert.ToBoolean(HasUnsupportedFeature);
        Context.FieldValues["Recognition.HasUnsupportedConstruct"] = Diagram.Recognition.TryGetValue("HasUnsupportedConstruct", out object? HasUnsupportedConstruct)
            && Convert.ToBoolean(HasUnsupportedConstruct);
        Context.FieldValues["Implementation.StaticAnalysis.Available"] = Diagram.Implementation.TryGetValue("StaticAnalysis", out object? StaticAnalysis)
            && StaticAnalysis is Dictionary<string, object> StaticAnalysisDictionary
            && StaticAnalysisDictionary.TryGetValue("Available", out object? Available)
            && Convert.ToBoolean(Available);
        Context.FieldValues["Config.HasInitDirective"] = Diagram.Config.TryGetValue("HasInitDirective", out object? HasInitDirective) && Convert.ToBoolean(HasInitDirective);
        Context.FieldValues["Config.HasFrontMatter"] = Diagram.Config.TryGetValue("HasFrontMatter", out object? HasFrontMatter) && Convert.ToBoolean(HasFrontMatter);
        Context.FieldValues["Recognition.HeaderToken"] = Diagram.Recognition.TryGetValue("HeaderToken", out object? HeaderToken) ? HeaderToken : string.Empty;
    }
}
