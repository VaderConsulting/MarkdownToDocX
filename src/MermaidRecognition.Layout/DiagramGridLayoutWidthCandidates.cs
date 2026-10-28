using System;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    /// <summary>Experimental width-first selection among already legal, hint-preserving complete candidates.</summary>
    private static bool PreferWidthCandidate(FlowchartLayoutScore.Result Candidate, FlowchartLayoutScore.Result Incumbent,
        double TargetWidth, double Padding)
    {
        double CandidateOverflow = Math.Max(0, Candidate.Width + 2 * Padding - TargetWidth);
        double IncumbentOverflow = Math.Max(0, Incumbent.Width + 2 * Padding - TargetWidth);
        if (Math.Abs(CandidateOverflow - IncumbentOverflow) > 1e-9) return CandidateOverflow < IncumbentOverflow;
        // Preserve the canonical bend/crossing weights. Width is an explicit
        // experimental constraint; among fits, prefer routing quality, then a
        // shorter vertical footprint rather than continuing to squeeze width.
        double CandidateRouting = Candidate.Bends * FlowchartLayoutRules.BendScore + Candidate.Cost.CrossingCost;
        double IncumbentRouting = Incumbent.Bends * FlowchartLayoutRules.BendScore + Incumbent.Cost.CrossingCost;
        if (CandidateRouting != IncumbentRouting) return CandidateRouting < IncumbentRouting;
        if (Math.Abs(Candidate.Height - Incumbent.Height) > 1e-9) return Candidate.Height < Incumbent.Height;
        return Candidate.Cost < Incumbent.Cost;
    }
}
