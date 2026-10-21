using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>Admits complete final-pass candidates under the narrowly approved rule 5.10 exception.</summary>
internal static class FlowchartFinalRepairPolicy
{
    /// <summary>Independent measurements retained for one complete repair option.</summary>
    /// <param name="Cost">Full layout comparison including unchanged repair charges.</param>
    /// <param name="Bends">Completed bend count.</param>
    /// <param name="Crossings">Lazy exact geometric crossing count, evaluated only for an identity-changing option.</param>
    /// <param name="Overlaps">Measured node overlap count.</param>
    /// <param name="HintsHonoured">Original honoured hints retained by the candidate.</param>
    /// <param name="Faults">Complete quality/legality failure inventory, independent of crossing-finding identities.</param>
    internal sealed record Measurement(FlowchartLayoutScore.ComparisonCost Cost, int Bends, Func<int> Crossings,
        int Overlaps, int HintsHonoured, IReadOnlyList<FlowchartLayoutQualityFailure> Faults);

    /// <summary>Builds immutable score facts while leaving expensive crossing work deferred.</summary>
    /// <param name="Score">Finished full or equivalent incremental score.</param>
    /// <param name="Crossings">Exact complete-layout crossing measurement.</param>
    /// <param name="RepairCost">Optional separate rule 5.10 charges, already calculated by the existing caller.</param>
    /// <returns>Facts for this candidate, never a reference to mutable route points.</returns>
    internal static Measurement Measure(FlowchartLayoutScore.Result Score, Func<int> Crossings,
        FlowchartLayoutScore.ComparisonCost? RepairCost = null) =>
        new(RepairCost ?? Score.Cost, Score.Bends, Crossings, Score.Overlaps, Score.HintsHonoured, Score.Violations);

    /// <summary>Requires every complete final-pass option to satisfy the exception; earlier passes never call this policy.</summary>
    /// <param name="Before">Original checker findings.</param>
    /// <param name="After">Finished candidate checker findings.</param>
    /// <param name="Original">Original complete score and fault facts.</param>
    /// <param name="Candidate">Finished complete score and fault facts.</param>
    /// <returns>Whether the repair may proceed to its unchanged remaining limits and legality guards.</returns>
    internal static bool Allows(IReadOnlyList<FlowchartRouteRuleChecker.Finding> Before,
        IReadOnlyList<FlowchartRouteRuleChecker.Finding> After, Measurement Original, Measurement Candidate)
    {
        if (After.Count > Before.Count || Candidate.Bends > Original.Bends || Candidate.Crossings() > Original.Crossings()
            || Candidate.Overlaps > Original.Overlaps || Candidate.HintsHonoured < Original.HintsHonoured
            || Candidate.Cost >= Original.Cost) return false;
        // A raw 5.9 entry is a geometric crossing report, not a proof of an
        // avoidable fault. Its identities may change under the exact complete
        // crossing and total-finding checks above. All other reported routing
        // and legality categories retain their established fault counts.
        foreach (string Rule in After.Select(Finding => Finding.Rule).Where(Rule => Rule != "5.9").Distinct(StringComparer.Ordinal))
            if (After.Count(Finding => Finding.Rule == Rule) > Before.Count(Finding => Finding.Rule == Rule)) return false;
        var FaultCounts = Original.Faults.Where(Fault => Fault.Kind != "ArrangementKept")
            .GroupBy(Fault => Fault.Kind).ToDictionary(Group => Group.Key, Group => Group.Count(), StringComparer.Ordinal);
        return !Candidate.Faults.Where(Fault => Fault.Kind != "ArrangementKept").GroupBy(Fault => Fault.Kind)
            .Any(Group => Group.Count() > FaultCounts.GetValueOrDefault(Group.Key));
    }
}
