namespace MermaidRecognition.Layout.Tests;

/// <summary>Exercises the complete-candidate exception without changing the audit's finding identities.</summary>
[TestFixture]
public sealed class FinalRepairFindingPolicyTests
{
    /// <summary>Rejects a locally straightened pair when two remote tracks gain crossings in the complete model.</summary>
    [Test]
    public void LocalImprovementWithRemoteCrossings_IsRejected()
    {
        (double X, double Y)[][] Before = [ [(0, 0), (10, 0)], [(5, -5), (5, 5)],
            [(100, 0), (110, 0)], [(120, -5), (120, 5)], [(130, -5), (130, 5)] ];
        (double X, double Y)[][] After = [ [(0, 0), (10, 0)], [(15, -5), (15, 5)],
            [(100, 0), (140, 0)], [(120, -5), (120, 5)], [(130, -5), (130, 5)] ];
        Assert.That(FlowchartBranchRouteOrder.Intersections(After[0], After[1]).Crossings, Is.Zero);
        int Original = Count(Before), Candidate = Count(After);
        Assert.That(Original, Is.EqualTo(1)); Assert.That(Candidate, Is.EqualTo(2));
        Assert.That(FlowchartFinalRepairPolicy.Allows(Findings("local"), Findings("remote"),
            Facts(100, 4, Original), Facts(60, 3, Candidate)), Is.False);

        int Count((double X, double Y)[][] Paths)
        {
            int Result = 0;
            for (int A = 0; A < Paths.Length; A++) for (int B = A + 1; B < Paths.Length; B++)
                Result += FlowchartBranchRouteOrder.Intersections(Paths[A], Paths[B]).Crossings;
            return Result;
        }
    }
    /// <summary>Accepts a new crossing identity only when the complete option improves the required measurements.</summary>
    [Test]
    public void FewerFindingsBendsAndCrossings_AllowsChangedIdentity()
    {
        Assert.That(FlowchartFinalRepairPolicy.Allows(Findings("old1", "old2"), Findings("new"),
            Facts(100, 4, 3), Facts(60, 3, 2)), Is.True);
    }

    /// <summary>A lower score and fewer findings cannot excuse one additional bend.</summary>
    [Test]
    public void AddedBend_RejectsChangedIdentity()
    {
        Assert.That(FlowchartFinalRepairPolicy.Allows(Findings("old1", "old2"), Findings("new"),
            Facts(100, 4, 3), Facts(60, 5, 2)), Is.False);
    }

    /// <summary>A better route cannot excuse a higher complete finding count.</summary>
    [Test]
    public void AddedFinding_RejectsChangedIdentity()
    {
        Assert.That(FlowchartFinalRepairPolicy.Allows(Findings("old"), Findings("new1", "new2"),
            Facts(100, 4, 3), Facts(60, 3, 2)), Is.False);
    }

    /// <summary>Quality faults, geometric crossings, hints and score remain independent rejection conditions.</summary>
    /// <param name="Reason">The independently violated admission condition.</param>
    [TestCase("fault"), TestCase("crossing"), TestCase("hint"), TestCase("score")]
    public void OtherConditions_RejectChangedIdentity(string Reason)
    {
        var Candidate = Facts(60, 3, 2);
        Candidate = Reason switch
        {
            "fault" => Candidate with { Faults = [new("NoLegalRoute", "missing", ["line"], null)] },
            "crossing" => Candidate with { Crossings = () => 4 },
            "hint" => Candidate with { HintsHonoured = 0 },
            _ => Candidate with { Cost = new(100, 0, 4, 60) }
        };
        Assert.That(FlowchartFinalRepairPolicy.Allows(Findings("old1", "old2"), Findings("new"),
            Facts(100, 4, 3), Candidate), Is.False);
    }

    /// <summary>Creates an unchanged checker's crossing reports for isolated policy tests.</summary>
    /// <param name="Identities">Exact subjects reported by the checker.</param>
    /// <returns>Finding inventory.</returns>
    private static FlowchartRouteRuleChecker.Finding[] Findings(params string[] Identities) =>
        Identities.Select(Id => new FlowchartRouteRuleChecker.Finding("5.9", Id, "crossing")).ToArray();

    /// <summary>Creates complete immutable measurements without a mutable diagram.</summary>
    /// <param name="Cost">Primary score.</param>
    /// <param name="Bends">Completed bends.</param>
    /// <param name="Crossings">Exact geometric crossings.</param>
    /// <returns>Measurements including one honoured hint and no quality faults.</returns>
    private static FlowchartFinalRepairPolicy.Measurement Facts(double Cost, int Bends, int Crossings) =>
        new(new(Cost, 0, Bends, Crossings * 20), Bends, () => Crossings, 0, 1, []);
}
