using MermaidRecognition.RuleValidation;
using MermaidRecognition.Kinds.Flowchart;
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

    [Test]
    public void DecisionOuterBranch_RequiresSideVertexOneCornerAndTopCentre()
    {
        BendBox Diamond = new(0, 120, 120, 120);
        BendBox Target = new(-120, 0, 120, 60);
        BendRoute Good = Route("left", "top", (-60, 120), (-120, 120), (-120, 30)) with
        {
            SourceBox = Diamond, TargetBox = Target, SourceIsDiamond = true, OutgoingCount = 2
        };
        BendRoute Bad = Route("bottom", "top", (0, 60), (-120, 60), (-120, 30)) with
        {
            SourceBox = Diamond, TargetBox = Target, SourceIsDiamond = true, OutgoingCount = 2
        };
        Assert.That(Check(Good), Is.Null);
        Assert.That(Check(Bad)?.Check, Is.EqualTo('f'));
    }

    [Test]
    public void LabelWithoutOneLongStraightRun_IsRejectedAsLabelJog()
    {
        BendRoute LabelJog = Route("right", "top", (0, 90), (30, 90), (30, 60)) with
        {
            Label = "decision label", LabelRequiredLengthPx = 90
        };
        Assert.That(Check(LabelJog)?.Check, Is.EqualTo('g'));
    }

    [Test]
    public void Convergence_UsesStraightAlignedFeedAndFacingSideForOffsetFeed()
    {
        BendBox Target = new(0, 0, 120, 60);
        BendRoute Straight = Route("bottom", "top", (0, 90), (0, 30)) with
        {
            SourceBox = new BendBox(0, 120, 120, 60), TargetBox = Target, IncomingCount = 2
        };
        BendRoute Side = Route("bottom", "left", (-120, 90), (-120, 0), (-60, 0)) with
        {
            SourceBox = new BendBox(-120, 120, 120, 60), TargetBox = Target, IncomingCount = 2
        };
        BendRoute GapTop = Route("bottom", "top", (-120, 90), (-120, 60), (0, 60), (0, 30)) with
        {
            SourceBox = new BendBox(-120, 120, 120, 60), TargetBox = Target, IncomingCount = 2
        };
        Assert.That(Check(Straight), Is.Null);
        Assert.That(Check(Side), Is.Null);
        Assert.That(Check(GapTop)?.Check, Is.EqualTo('h'));
    }

    [Test]
    public void FanBetweenColumns_RejectsFarSideAndSharedRail()
    {
        BendBox Source = new(0, 60, 120, 60);
        BendBox Target = new(240, 120, 120, 60);
        BendRoute Good = Route("right", "left", (60, 60), (180, 60), (180, 120)) with
        {
            SourceBox = Source, TargetBox = Target, FanBetweenColumns = true
        };
        BendRoute FarSide = Route("left", "left", (-60, 60), (-120, 60), (-120, 120), (180, 120)) with
        {
            SourceBox = Source, TargetBox = Target, FanBetweenColumns = true
        };
        Assert.That(Check(Good), Is.Null);
        Assert.That(Check(FarSide)?.Check, Is.EqualTo('i'));
        BendSegment Shared = new("fan-2", new BendPoint(60, 60), new BendPoint(180, 60));
        Assert.That(UnnecessaryBendChecker.CheckRoute("unit", Good, [], [Shared])?.Check, Is.EqualTo('i'));
    }

    [Test]
    public void CrossingWithClearSamePortAlternative_IsRejectedAsAvoidable()
    {
        BendRoute Crossing = Route("right", "left", (0, 0), (30, 0), (30, 120), (120, 120)) with
        {
            RoutedCrossings = 1
        };
        BendSegment Other = new("other", new BendPoint(-30, 60), new BendPoint(60, 60));
        Assert.That(UnnecessaryBendChecker.CheckRoute("unit", Crossing, [], [Other])?.Check, Is.EqualTo('j'));
    }

    private static UnnecessaryBendViolation? Check(BendRoute Route, IReadOnlyList<BendObstacle>? Obstacles = null) =>
        UnnecessaryBendChecker.CheckRoute("unit", Route, Obstacles ?? []);

    private static BendRoute Route(string Exit, string Entry, params (double X, double Y)[] Points) =>
        new("line", "source", "target", Exit, Entry,
            Points.Select(Point => new BendPoint(Point.X, Point.Y)).ToList());
}
