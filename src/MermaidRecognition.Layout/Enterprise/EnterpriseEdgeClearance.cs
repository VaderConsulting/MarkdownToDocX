using System;
using System.Collections.Generic;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Parity checks: enterprise connector paths must match the flowchart routing path
/// for the same mapped graph. Flowchart itself may cross nodes; this does not
/// assert clearance beyond that shared path.
/// </summary>
public static class EnterpriseEdgeClearance
{
    /// <summary>
    /// Returns one diagnostic string per enterprise edge whose path differs from
    /// the flowchart resolver for the same contract.
    /// </summary>
    /// <param name="Source">Enterprise diagram source.</param>
    /// <returns>Mismatch messages; empty when paths match.</returns>
    public static IReadOnlyList<string> FindViolations(string Source)
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        if (Analysis.HasErrors)
        {
            throw new InvalidOperationException("Enterprise diagram has validation errors.");
        }

        return FindViolations(Analysis.Document);
    }

    /// <summary>
    /// Returns one diagnostic string per enterprise edge whose path differs from
    /// the flowchart resolver for the same contract.
    /// </summary>
    /// <param name="Document">Validated document.</param>
    /// <returns>Mismatch messages; empty when paths match.</returns>
    public static IReadOnlyList<string> FindViolations(EnterpriseDocument Document)
    {
        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        List<EnterpriseRoutedEdge> Routed = EnterpriseFlowchartEdgeRouter.Route(Document, Layout.Nodes);
        List<EnterpriseFlowchartPathFact> FlowchartFacts =
            EnterpriseFlowchartEdgeRouter.ResolveFlowchartPathFacts(Document, Layout.Nodes);
        Dictionary<string, string> FlowchartByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (EnterpriseFlowchartPathFact Fact in FlowchartFacts)
        {
            string Key = Fact.SourceId + "->" + Fact.TargetId;
            if (!FlowchartByKey.ContainsKey(Key))
            {
                FlowchartByKey[Key] = Fact.PathData;
            }
        }

        List<string> Violations = [];
        foreach (EnterpriseRoutedEdge Edge in Routed)
        {
            string Key = Edge.Relationship.SourceId + "->" + Edge.Relationship.TargetId;
            if (!FlowchartByKey.TryGetValue(Key, out string? Expected))
            {
                Violations.Add(Key + " has no flowchart path");
                continue;
            }

            if (!string.Equals(Expected, Edge.PathData, StringComparison.Ordinal))
            {
                Violations.Add(Key + " path differs from flowchart routing");
            }
        }

        return Violations;
    }
}
