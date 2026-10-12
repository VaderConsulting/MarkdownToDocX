using MermaidRecognition.RuleValidation;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using NUnit.Framework;
using System.Text;

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
            SourceBox = Diamond, TargetBox = Target, SourceIsDiamond = true, OutgoingCount = 2,
            FanToNextRank = true
        };
        BendRoute Bad = Route("bottom", "top", (0, 60), (-120, 60), (-120, 30)) with
        {
            SourceBox = Diamond, TargetBox = Target, SourceIsDiamond = true, OutgoingCount = 2
        };
        Assert.That(Check(Good), Is.Null);
        Assert.That(Check(Bad)?.Check, Is.EqualTo('f'));
    }

    [Test]
    public void DecisionOuterBranch_UsesFacingSideWhenTopCentreIsOccupied()
    {
        BendBox Diamond = new(0, 120, 120, 120);
        BendBox Target = new(-180, 0, 120, 60);
        BendRoute SideEntry = Route("left", "right", (-60, 120), (-60, 0), (-120, 0)) with
        {
            SourceBox = Diamond, TargetBox = Target, SourceIsDiamond = true, OutgoingCount = 2
        };
        BendSegment TopOccupant = new("other", new BendPoint(-180, 90), new BendPoint(-180, 30),
            "other-source", "target");

        Assert.That(UnnecessaryBendChecker.CheckRoute("unit", SideEntry, [], [TopOccupant]), Is.Null);
    }

    [Test]
    public void LabelWithoutOneLongStraightRun_IsRejectedAsLabelJog()
    {
        BendRoute LabelJog = Route("right", "top", (0, 90), (30, 90), (30, 60)) with
        {
            Label = "decision label", LabelRequiredHorizontalPx = 90, LabelRequiredVerticalPx = 90
        };
        Assert.That(Check(LabelJog)?.Check, Is.EqualTo('g'));
    }

    [Test]
    public void LabelledRoute_IsNotShortenedToALineWithoutLabelRoom()
    {
        BendRoute LabelledLoop = Route("right", "right", (0, 15), (120, 15), (120, -15), (0, -15)) with
        {
            Label = "retry", LabelRequiredHorizontalPx = 60, LabelRequiredVerticalPx = 40
        };

        Assert.That(Check(LabelledLoop), Is.Null);
    }

    [Test]
    public void InvisibleLabelledEdge_IsNotCheckedAsRenderedLine()
    {
        var Contract = MermaidRecognizer.Parse("flowchart LR\nA[A] ~~~|skip| B[B]");
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        Assert.That(UnnecessaryBendChecker.Check("invisible", Contract, Layout), Is.Empty);
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
    public void Convergence_AdjacentRankBlockLineUsesBlockRules()
    {
        BendRoute CrossedPair = Route("bottom", "top", (-120, 90), (-120, 60), (0, 60), (0, 30)) with
        {
            SourceBox = new BendBox(-120, 120, 120, 60),
            TargetBox = new BendBox(0, 0, 120, 60),
            IncomingCount = 2,
            AdjacentRankBlockLine = true
        };

        Assert.That(Check(CrossedPair), Is.Null);
    }

    [TestCase(2, 3)]
    [TestCase(3, 3)]
    [TestCase(4, 2)]
    public void AdjacentRankBlocks_AreCheckedByBlockRules(int SourceCount, int TargetCount)
    {
        var Text = new StringBuilder("flowchart TB\n");
        for (int Source = 0; Source < SourceCount; Source++) Text.AppendLine($"s{Source}[\"Source {Source}\"]");
        for (int Target = 0; Target < TargetCount; Target++) Text.AppendLine($"t{Target}[\"Target {Target}\"]");
        for (int Source = 0; Source < SourceCount; Source++)
        for (int Target = 0; Target < TargetCount; Target++) Text.AppendLine($"s{Source} --> t{Target}");
        var Contract = MermaidRecognizer.Parse(Text.ToString());
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout), Is.True);

        IReadOnlyList<UnnecessaryBendViolation> Findings = UnnecessaryBendChecker.Check("block", Contract, Layout);
        Assert.That(Findings.Where(Finding => Finding.Check is 'h' or 'i' or 'j'), Is.Empty,
            string.Join(Environment.NewLine, Findings.Select(Finding =>
            {
                FlowchartLayoutEdgeRoute Route = Layout.EdgeRoutes.Single(Item => Item.EdgeId == Finding.LineId);
                return $"{Finding.LineId}: {Finding.Check} {Route.ExitSide}->{Route.EntrySide} "
                    + string.Join(" ", Route.Points.Select(Point => $"({Point.X},{Point.Y})"));
            })));
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
        BendSegment Shared = new("fan-2", new BendPoint(60, 60), new BendPoint(180, 60), "source", "other");
        Assert.That(UnnecessaryBendChecker.CheckRoute("unit", Good, [], [Shared])?.Check, Is.EqualTo('i'));
    }

    [Test]
    public void FanToNextRank_UsesBottomAndTopUnlessHinted()
    {
        BendBox Source = new(0, 120, 120, 60);
        BendBox Target = new(-120, 0, 120, 60);
        BendRoute Good = Route("bottom", "top", (0, 90), (0, 60), (-120, 60), (-120, 30)) with
        {
            SourceBox = Source, TargetBox = Target, FanToNextRank = true
        };
        BendRoute HintedSide = Route("left", "right", (-60, 120), (-60, 0), (-60, 0)) with
        {
            SourceBox = Source, TargetBox = Target, FanToNextRank = true,
            HintNeedsExtraCorners = true, HintOverridesRoutingFault = true
        };

        Assert.That(Check(Good), Is.Null);
        Assert.That(Check(HintedSide), Is.Null);
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
