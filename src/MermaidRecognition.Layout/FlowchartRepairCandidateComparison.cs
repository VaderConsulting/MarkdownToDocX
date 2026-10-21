using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Compares complete repair candidates using rule 9.1 precedence.</summary>
internal static class FlowchartRepairCandidateComparison
{
    /// <summary>Counts routing-fault and hard-gate quality failures, excluding arrangement bookkeeping.</summary>
    /// <param name="Faults">Complete candidate fault inventory.</param>
    /// <returns>Non-exempt faults that participate in precedence level 3.</returns>
    internal static int RoutingFaultCount(IReadOnlyList<FlowchartLayoutQualityFailure> Faults)
    {
        return Faults.Count(Failure => Failure.Kind != "ArrangementKept");
    }

    /// <summary>Returns whether the candidate is strictly better than the baseline under rule 9.1.</summary>
    /// <param name="BaselineFaults">Baseline routing-fault count.</param>
    /// <param name="BaselineHints">Honoured hints on the baseline.</param>
    /// <param name="BaselineCost">Baseline comparison cost.</param>
    /// <param name="CandidateFaults">Candidate routing-fault count.</param>
    /// <param name="CandidateHints">Honoured hints on the candidate.</param>
    /// <param name="CandidateCost">Candidate comparison cost.</param>
    /// <returns>True when the candidate wins every applicable precedence level.</returns>
    internal static bool CandidateBeatsBaseline(int BaselineFaults, int BaselineHints, FlowchartLayoutScore.ComparisonCost BaselineCost,
        int CandidateFaults, int CandidateHints, FlowchartLayoutScore.ComparisonCost CandidateCost)
    {
        if (CandidateFaults < BaselineFaults)
        {
            return true;
        }

        if (CandidateFaults > BaselineFaults)
        {
            return false;
        }

        if (CandidateHints < BaselineHints)
        {
            return false;
        }

        if (CandidateHints > BaselineHints)
        {
            return true;
        }

        return CandidateCost < BaselineCost;
    }

    /// <summary>Geometric tolerance for repair candidate comparison.</summary>
    internal static double GeometryTolerancePx => DiagramGridMetrics.ALIGNMENT_TOLERANCE_PX;

    /// <summary>Minimum parallel separation for comparisons that use line spacing.</summary>
    internal static double MinimumLineSeparationPx => FlowchartLayoutRules.LineSpacing - GeometryTolerancePx;
}
