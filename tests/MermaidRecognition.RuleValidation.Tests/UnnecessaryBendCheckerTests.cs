using MermaidRecognition.RuleValidation;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

[TestFixture]
public sealed class UnnecessaryBendCheckerTests
{
    [Test]
    public void AlignedPorts_AcceptsStraightAndRejectsBentRoute()
    {
        Assert.That(Check(Route("right", "left", (0, 0), (120, 0))), Is.Null);
        Assert.That(Check(Route("right", "left", (0, 0), (30, 0), (30, 60), (90, 60), (90, 0), (120, 0))), Is.Not.Null);
    }

    [Test]
    public void PerpendicularSides_AcceptsOneCornerAndRejectsThree()
    {
        Assert.That(Check(Route("right", "bottom", (0, 0), (90, 0), (90, 90))), Is.Null);
        Assert.That(Check(Route("right", "bottom", (0, 0), (30, 0), (30, 60), (90, 60), (90, 90))), Is.Not.Null);
    }

    [Test]
    public void SameDirectionSides_AcceptsTwoCornersAndRejectsFour()
    {
        Assert.That(Check(Route("right", "right", (0, 0), (120, 0), (120, 90), (90, 90))), Is.Null);
        Assert.That(Check(Route("right", "right", (0, 0), (30, 0), (30, 30), (120, 30), (120, 90), (90, 90))), Is.Not.Null);
    }

    [Test]
    public void ZigZag_IsRejected_ButBlockingBoxMakesDetourNecessary()
    {
        BendRoute ZigZag = Route("right", "left", (0, 0), (30, 0), (30, 60), (90, 60), (90, 0), (120, 0));
        Assert.That(Check(ZigZag), Is.Not.Null);
        BendObstacle Block = new("block", 40, -20, 80, 20);
        Assert.That(Check(ZigZag, [Block]), Is.Null);
    }

    [Test]
    public void ShortSidewaysJog_IsReportedAsPlacementCause()
    {
        BendRoute Jog = Route("right", "left", (0, 0), (30, 0), (30, 10), (90, 10), (90, 0), (120, 0));
        Assert.That(Check(Jog)?.Check, Is.EqualTo('c'));
    }

    [Test]
    public void HintNeededRoute_IsAccepted_AndSameRouteWithoutHintIsRejected()
    {
        BendRoute Bent = Route("right", "left", (0, 0), (30, 0), (30, 60), (90, 60), (90, 0), (120, 0));
        Assert.That(Check(Bent with { HintNeedsExtraCorners = true }), Is.Null);
        Assert.That(Check(Bent), Is.Not.Null);
    }

    [Test]
    public void BetterRouteReturnedByFinding_IsAccepted()
    {
        BendRoute Bent = Route("right", "left", (0, 0), (30, 0), (30, 60), (90, 60), (90, 0), (120, 0));
        UnnecessaryBendViolation Finding = Check(Bent)!;
        Assert.That(Check(Bent with { Points = Finding.BetterRoute }), Is.Null);
    }

    [Test]
    public void ShorterRouteCrossingAnotherLine_DoesNotMakeTheDetourUnnecessary()
    {
        BendRoute Detour = Route("right", "left", (0, 0), (30, 0), (30, 90), (120, 90));
        BendSegment BlockingLine = new("other", new BendPoint(75, 30), new BendPoint(75, 120));
        Assert.That(UnnecessaryBendChecker.CheckRoute("unit", Detour, [], [BlockingLine]), Is.Null);
    }

    [Test]
    public void CrossingWithinForcedBudget_IsReportedAsCrossingAvoidanceBend()
    {
        BendRoute Detour = Route("right", "left", (0, 0), (30, 0), (30, 60), (90, 60), (90, 0), (120, 0))
            with { AllowedCrossings = 1 };
        BendSegment CrossingLine = new("other", new BendPoint(75, -30), new BendPoint(75, 30));
        Assert.That(UnnecessaryBendChecker.CheckRoute("unit", Detour, [], [CrossingLine])?.Check, Is.EqualTo('e'));
    }

    private static UnnecessaryBendViolation? Check(BendRoute Route, IReadOnlyList<BendObstacle>? Obstacles = null) =>
        UnnecessaryBendChecker.CheckRoute("unit", Route, Obstacles ?? []);

    private static BendRoute Route(string Exit, string Entry, params (double X, double Y)[] Points) =>
        new("line", "source", "target", Exit, Entry,
            Points.Select(Point => new BendPoint(Point.X, Point.Y)).ToList());
}
