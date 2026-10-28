using System.Globalization;
using System.Diagnostics;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Config;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Render;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Measures production by default; ROUTE_AUDIT_CELLS_ON=off explicitly requests the cells-off compatibility audit.</summary>
[TestFixture]
[NonParallelizable]
public sealed class RouteRuleCatalogueAuditTests
{
    public sealed record Scenario(string Id, string Source, bool Enterprise);
    private readonly HashSet<string> _summaryRows = new(StringComparer.Ordinal);
    private string? _catalogueRoot;

    [OneTimeSetUp]
    public void BeginAudit()
    {
        _catalogueRoot = FindRoot();
        _summaryRows.Clear();
    }

    [OneTimeTearDown]
    public void RequireCompleteAuditWhenRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ROUTE_AUDIT_REQUIRE_ALL"), "1", StringComparison.Ordinal))
        {
            return;
        }

        HashSet<string> expected = ReadCatalogue(_catalogueRoot ?? FindRoot())
            .Select(scenario => scenario.Id).ToHashSet(StringComparer.Ordinal);
        string[] missing = [.. expected.Except(_summaryRows, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)];
        string[] unexpected = [.. _summaryRows.Except(expected, StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)];
        TestContext.Out.WriteLine($"ROUTE_AUDIT_COMPLETENESS|expected={expected.Count}|summaries={_summaryRows.Count}|missing={string.Join(",", missing)}|unexpected={string.Join(",", unexpected)}");
        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Empty, "Every catalogue scenario must produce a route audit summary row; missing rows are not zero findings.");
            Assert.That(unexpected, Is.Empty, "The route audit emitted summary rows for scenarios outside the current catalogue.");
        });
    }


    public static IEnumerable<TestCaseData> Catalogue()
    {
        string root = FindRoot();
        foreach (Scenario scenario in ReadCatalogue(root))
        {
            yield return CatalogueScenarioPolicy.Apply(new TestCaseData(scenario).SetName("RouteRuleAudit_" + scenario.Id), scenario.Id);
        }
    }

    [TestCaseSource(nameof(Catalogue))]
    [CancelAfter(60000)]
    public void AuditModelRoutes(Scenario scenario)
    {
        Stopwatch auditTimer = Stopwatch.StartNew();
        EnterpriseDocument? enterprise = scenario.Enterprise ? EnterpriseValidator.Analyze(scenario.Source).Document : null;
        DiagramContract contract = scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(enterprise!)
            : MermaidRecognizer.Parse(scenario.Source);
        string[] droppedLinks = scenario.Id == "A90"
            ? [.. contract.Diagnostics.Where(diagnostic => diagnostic.Code == "enterprise.dropped-link").Select(diagnostic => diagnostic.Message)]
            : [];
        List<(string Stage, FlowchartLayoutResult Layout)> snapshots = [];
        List<(string Stage, double Ms)> stageTimes = [];
        Dictionary<string, double> compactFinalRepairCosts = new(StringComparer.Ordinal);
        List<FinalRepairAuditCapture.Decision> finalRepairDecisions = [];
        List<FinalRepairAuditCapture.Confirmation> finalConfirmations = [];
        DiagramGridLayout.FinalRepairConfirmationObserver = Environment.GetEnvironmentVariable("FINAL_REPAIR_CONFIRMATIONS") == "1"
            ? (Key, Geometry, Input, Base, Reused, Ms) => finalConfirmations.Add(new(Key, Geometry, Input, Base, Reused, Ms)) : null;
        DiagramGridLayout.FinalRepairObserver = (Key, Reason, ExtraLength) => finalRepairDecisions.Add(new(Key, Reason, ExtraLength));
        int moveBeforeDetourMoves = 0, moveBeforeDetourCandidates = 0;
        int routeRepairChainRuns = 0, routeRepairChainDepthTotal = 0, routeRepairChainMaxDepth = 0,
            routeRepairChainMaxLines = 0;
        double routeRepairChainMaxExtraLength = 0;
        HashSet<(string Option, string Subject)> routeRepairOptionWins = [];
        List<string> routeRepairCandidates = [];
        List<string> orthogonalCleanupRoutes = [];
        List<string> moveBeforeDetourCases = [];
        int fitPasses = 0;
        List<string> frameEvents = [];
        List<(string Stage, int Count)> frameOverlapStages = [];
        DiagramGridLayout.LayoutStageObserver = (stage, layout) =>
        {
            snapshots.Add((stage, layout));
        };
        bool profileStages = string.Equals(Environment.GetEnvironmentVariable("ROUTE_AUDIT_PROFILE"), "1", StringComparison.Ordinal);
        DiagramGridLayout.LayoutStageTimingObserver = profileStages
            ? (stage, ms) => stageTimes.Add((stage, ms))
            : Environment.GetEnvironmentVariable("FINAL_REPAIR_PROFILE") == "1"
                ? (stage, ms) =>
                {
                    if (ms >= 0 && (stage.StartsWith("cell-cost/exclusive/", StringComparison.Ordinal) || stage == "final-complete-layout-repair"))
                    {
                        compactFinalRepairCosts[stage] = compactFinalRepairCosts.GetValueOrDefault(stage) + ms;
                    }
                } : null;
        DiagramGridLayout.GroupFrameFitPassObserver = message => { fitPasses++; frameEvents.Add(message); };
        DiagramGridLayout.GroupFrameCandidateObserver = scenario.Id == "A165"
            ? message => frameEvents.Add(message) : null;
        DiagramGridLayout.MoveBeforeDetourObserver = (moved, candidates, cases) =>
        {
            moveBeforeDetourMoves += moved;
            moveBeforeDetourCandidates += candidates;
            moveBeforeDetourCases.AddRange(cases);
        };
        DiagramGridLayout.RouteRepairChainObserver = (depth, changedLines, extraLength) =>
        {
            if (depth <= 0)
            {
                return;
            }

            routeRepairChainRuns++;
            routeRepairChainDepthTotal += depth;
            routeRepairChainMaxDepth = Math.Max(routeRepairChainMaxDepth, depth);
            routeRepairChainMaxLines = Math.Max(routeRepairChainMaxLines, changedLines);
            routeRepairChainMaxExtraLength = Math.Max(routeRepairChainMaxExtraLength, extraLength);
        };
        DiagramGridLayout.RouteRepairOptionObserver = (option, subject) =>
        {
            routeRepairOptionWins.Add((option, subject));
        };
        DiagramGridLayout.RouteRepairCandidateObserver = (option, subject, score, clearsFault) =>
            routeRepairCandidates.Add($"5.10_CANDIDATE|{scenario.Id}|{option}|{subject}|score={score:0.00}|clearsFault={clearsFault}");
        DiagramGridLayout.FrameOverlapStageObserver = scenario.Id == "A169"
            ? (stage, count) => frameOverlapStages.Add((stage, count)) : null;
        DiagramGridLayout.OrthogonalRouteCleanupObserver = edgeId => orthogonalCleanupRoutes.Add(edgeId);
        FlowchartLayoutResult layout = null!;
        IReadOnlyList<DiagramGridLayout.GridNode>? CellNodes = null;
        Stopwatch layoutClock = Stopwatch.StartNew();
        try
        {
            bool CellsOn = Environment.GetEnvironmentVariable("ROUTE_AUDIT_CELLS_ON") == "1";
            bool layoutSucceeded;
            if (CellsOn)
            {
                layoutSucceeded = false;
                try
                {
                    CellGuardCatalogueProbeTests.RunBoundCatalogue(scenario, false, (actualContract, actualLayout) =>
                    { contract = actualContract; layout = actualLayout; layoutSucceeded = true; }, actualNodes => CellNodes = actualNodes);
                }
                catch (AssertionException Failure)
                {
                    TestContext.Out.WriteLine($"CELL_AUDIT_LAYOUT_FAILURE|{scenario.Id}|{Failure.Message}");
                }
            }
            else
            {
                FlowchartRenderContext Context = new FlowchartRenderContext
                {
                    UseCellRegions = Environment.GetEnvironmentVariable("ROUTE_AUDIT_CELLS_ON") != "off"
                };
                layoutSucceeded = FlowchartSugiyamaLayoutEngine.TryLayout(contract, Context, out layout!);
                CellNodes = layout!.CellNodes;
                CellGuardCatalogueReporting.Write(scenario.Id, Context.UseCellRegions ? "on" : "off", layout,
                    layoutSucceeded ? "completed" : "native-layout-failed");
            }
            if (!layoutSucceeded)
            {
                string failedCategory = scenario.Enterprise ? "enterprise"
                    : scenario.Id.StartsWith("EIP", StringComparison.Ordinal) ? "EIP" : "flow";
                _summaryRows.Add(scenario.Id);
                TestContext.Out.WriteLine($"ROUTE_AUDIT|{failedCategory}|{scenario.Id}|status=layout-failed|offSlotPorts=NA|offGridNodes=NA|offGridFrameEdges=NA|titleBand=NA|missing=NA|cleanupCorrections=NA");
                Assert.Fail("Cannot measure layout invariants because layout failed: " + scenario.Id);
                return;
            }
            if (Environment.GetEnvironmentVariable("ROUTE_AUDIT_GEOMETRY") == "1")
            {
                string Signature = CellProductionParityTests.Snapshot(contract, layout);
                string Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Signature)));
                TestContext.Out.WriteLine($"ROUTE_AUDIT_GEOMETRY|{scenario.Id}|{Hash}|bends={layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points))}");
            }
        }
        finally
        {
            layoutClock.Stop();
            FinalRepairAuditCapture.Record(scenario.Id, contract, layout, layoutClock.Elapsed.TotalSeconds, finalRepairDecisions,
                stageTimes.Concat(compactFinalRepairCosts.Select(Pair => (Stage: Pair.Key, Ms: Pair.Value))).ToArray(), finalConfirmations);
            DiagramGridLayout.FinalRepairConfirmationObserver = null;
            DiagramGridLayout.FinalRepairObserver = null;
            // The older AUDIT_TIME line is below assertions and omits failed
            // scenarios such as A142. Always preserve their measured stages too.
            if (profileStages)
            {
                TestContext.Out.WriteLine($"CELL_AUDIT_PROFILE|{scenario.Id}|layoutMs={layoutClock.Elapsed.TotalMilliseconds:0.000}|"
                + System.Text.Json.JsonSerializer.Serialize(stageTimes.Where(item => item.Ms >= 0)
                    .Select(item => new { Name = item.Stage, Milliseconds = item.Ms })));
            }

            DiagramGridLayout.LayoutStageObserver = null;
            DiagramGridLayout.LayoutStageTimingObserver = null;
            DiagramGridLayout.GroupFrameFitPassObserver = null;
            DiagramGridLayout.GroupFrameCandidateObserver = null;
            DiagramGridLayout.MoveBeforeDetourObserver = null;
            DiagramGridLayout.RouteRepairChainObserver = null;
            DiagramGridLayout.RouteRepairOptionObserver = null;
            DiagramGridLayout.RouteRepairCandidateObserver = null;
            DiagramGridLayout.FrameOverlapStageObserver = null;
            DiagramGridLayout.OrthogonalRouteCleanupObserver = null;
        }
        TestContext.Out.WriteLine($"ORTHOGONAL_CLEANUP|{scenario.Id}|count={orthogonalCleanupRoutes.Count}|routes={string.Join(",", orthogonalCleanupRoutes)}");
        foreach (string frameEvent in frameEvents.Where(message => message.Contains("rule 7.8", StringComparison.Ordinal)))
        {
            TestContext.Out.WriteLine($"FRAME_REPAIR|{scenario.Id}|{frameEvent}");
        }

        foreach (string candidate in routeRepairCandidates)
        {
            TestContext.Out.WriteLine(candidate);
        }

        foreach ((string stage, int count) in frameOverlapStages)
        {
            TestContext.Out.WriteLine($"FRAME_STAGE|{scenario.Id}|{stage}|{count}");
        }

        foreach (FlowchartLayoutEdgeRoute route in layout.EdgeRoutes.Where(route => orthogonalCleanupRoutes.Contains(route.EdgeId)))
        {
            TestContext.Out.WriteLine($"CLEANUP_ROUTE|{scenario.Id}|{route.EdgeId}|{route.ExitSide}/{route.EntrySide}|{string.Join(";", route.Points.Select(point => $"{point.X:0.##},{point.Y:0.##}"))}");
        }

        IReadOnlyList<FlowchartRouteRuleChecker.Finding> findings = FlowchartRouteRuleChecker.Check(contract, layout);
        if (scenario.Id == "A142")
        {
            FlowchartLayoutScore.Result score = FlowchartLayoutScore.Measure(contract, layout,
                IncludeArrangementCheck: false);
            string rejectionCauses = string.Join(", ", score.Violations
                .GroupBy(failure => failure.Kind, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}={group.Count()}"));
            TestContext.Out.WriteLine($"A142_METRICS|scoreCrossings={score.Crossings}|rule59Findings={findings.Count(finding => finding.Rule == "5.9")}|overlaps={score.Overlaps}|score={score.Score?.ToString("0.00", CultureInfo.InvariantCulture) ?? "rejected"}|optimizationScore={score.OptimizationScore:0.00}|rejectionCauses={rejectionCauses}");
        }
        int a163ActiveHints = 0, a163ReportedHints = 0;
        FlowchartLayoutScore.Result? a163HintScore = null;
        if (scenario.Id == "A163")
        {
            a163HintScore = FlowchartLayoutScore.Measure(contract, layout, IncludeArrangementCheck: false);
            a163ActiveHints = contract.Nodes.Count(node => node.PlacementHint?.IsActive == true)
                + contract.Groups.Count(group => group.PlacementHint?.IsActive == true)
                + contract.Edges.Count(edge => edge.RoutingHint?.IsActive == true);
            a163ReportedHints = contract.Nodes.Count(node => node.PlacementHint is { IsActive: true, UnhonouredReason: not null })
                + contract.Groups.Count(group => group.PlacementHint is { IsActive: true, UnhonouredReason: not null })
                + contract.Edges.Count(edge => edge.RoutingHint is { IsActive: true, UnhonouredReason: not null });
            TestContext.Out.WriteLine($"HINT_AUDIT|A163|active={a163ActiveHints}|honoured={a163HintScore.HintsHonoured}|reported={a163ReportedHints}|unreported={a163ActiveHints - a163HintScore.HintsHonoured - a163ReportedHints}");
        }
        if (scenario.Id is "A164" or "A165" or "A166" or "A167" or "A168" or "A169")
        {
            (int SideChoice, int BlockedNode, int CorrectBend, int RealFault) breakdown = ClassifyRule42(contract, layout, findings);
            TestContext.Out.WriteLine($"RULE42_BREAKDOWN|{scenario.Id}|findings={findings.Count(finding => finding.Rule == "4.2")}|sideChoice={breakdown.SideChoice}|blockedNode={breakdown.BlockedNode}|correctBend={breakdown.CorrectBend}|realFault={breakdown.RealFault}");
        }
        HashSet<string>? a163HintedCrossings = scenario.Id == "A163"
            ? findings.Where(finding => finding.Rule == "5.9").Select(finding => finding.Subject)
                .ToHashSet(StringComparer.Ordinal)
            : null;
        int geometrySideChoiceFaults = CountGeometrySideChoiceFindings(scenario.Id, contract, layout);
        TestContext.Out.WriteLine($"SIDE_CHOICE|{scenario.Id}|findings={geometrySideChoiceFaults}");
        if (scenario.Id is "F59" or "F137")
        {
            Assert.That(findings.Where(finding => finding.Rule == "3.2/4.7"), Is.Empty,
                "Shared ports keep at least line_spacing clearance along the edge.");
            foreach (FlowchartLayoutEdgeRoute route in layout.EdgeRoutes)
            {
                FlowchartLayoutNodeBounds? source = layout.NodeBounds.FirstOrDefault(box => box.NodeId == route.SourceId);
                FlowchartLayoutNodeBounds? target = layout.NodeBounds.FirstOrDefault(box => box.NodeId == route.TargetId);
                TestContext.Out.WriteLine($"SLOT_REVIEW|{scenario.Id}|{route.EdgeId}|{route.SourceId}:{route.ExitSide}=({route.Points[0].X:0.###},{route.Points[0].Y:0.###})[{source?.CenterX:0.###},{source?.Width:0.###}]|{route.TargetId}:{route.EntrySide}=({route.Points[^1].X:0.###},{route.Points[^1].Y:0.###})[{target?.CenterX:0.###},{target?.Width:0.###}]");
            }
        }
        if (scenario.Id == "A142")
        {
            FlowchartLayoutNodeBounds? box = layout.NodeBounds.FirstOrDefault(item => item.NodeId == "dmeOther");
            string? shape = contract.Nodes.FirstOrDefault(item => item.Id == "dmeOther")?.Shape;
            foreach (FlowchartLayoutEdgeRoute route in layout.EdgeRoutes.Where(item =>
                         item.SourceId == "dmeOther" || item.TargetId == "dmeOther"))
            {
                TestContext.Out.WriteLine($"A142_DMEOTHER_PORT|shape={shape}|{route.EdgeId}|{route.SourceId}:{route.ExitSide}=({route.Points[0].X:0.##},{route.Points[0].Y:0.##})|{route.TargetId}:{route.EntrySide}=({route.Points[^1].X:0.##},{route.Points[^1].Y:0.##})|box=({box?.CenterX:0.##},{box?.CenterY:0.##},{box?.Width:0.##},{box?.Height:0.##})");
            }
        }
        string category = scenario.Enterprise ? "enterprise" : scenario.Id.StartsWith("EIP", StringComparison.Ordinal) ? "EIP" : "flow";
        HashSet<string> portRules = ["3.1", "3.2/4.7", "3.3/5.2", "4.2", "4.11", "5.8"];
        int portFaults = findings.Count(finding => portRules.Contains(finding.Rule));
        int gridFaults = 0;
        int offGridNodes = 0, offGridFrameEdges = 0;
        string[] gridCauses = [];
        if (contract.LayoutMode != DiagramLayoutMode.Open)
        {
            double step = DiagramGridMetrics.GRID_UNIT_PX;
            FlowchartLayoutNodeBounds? origin = layout.NodeBounds.FirstOrDefault();
            double originX = origin?.CenterX ?? 0, originY = origin?.CenterY ?? 0;
            Dictionary<string, string> finalGridFaults = CellNodes != null
                ? CellGridAudit.Faults(contract, layout, CellNodes) : GridFaults(layout, originX, originY, step);
            if (CellNodes != null)
            {
                Dictionary<string, string> LegacyFaults = GridFaults(layout, originX, originY, step);
                TestContext.Out.WriteLine($"CELL_GRID_COMPARE|{scenario.Id}|newOnly={string.Join(",", finalGridFaults.Keys.Except(LegacyFaults.Keys))}|legacy={LegacyFaults.Count}|cells={finalGridFaults.Count}");
            }
            offGridNodes = finalGridFaults.Count(pair => pair.Value == "node");
            offGridFrameEdges = finalGridFaults.Count - offGridNodes;
            gridFaults = finalGridFaults.Count;
            Dictionary<string, string> lastBadStage = [];
            HashSet<string> previous = [];
            foreach ((string stage, FlowchartLayoutResult snapshot) in CellNodes == null ? snapshots : [])
            {
                HashSet<string> current = GridFaults(snapshot, originX, originY, step).Keys.ToHashSet(StringComparer.Ordinal);
                foreach (string key in current)
                {
                    if (!previous.Contains(key))
                    {
                        lastBadStage[key] = stage;
                    }
                }

                previous = current;
            }
            gridCauses = [.. finalGridFaults.Select(pair =>
                    $"{scenario.Id}|{pair.Value}|{lastBadStage.GetValueOrDefault(pair.Key, CellNodes == null ? "unobserved" : "final-cell-regions")}")];
        }
        int missingRoutes = findings.Count(finding => finding.Rule == "3.1"
            && finding.Description == "visible edge has no stored route with two endpoints");
        if (scenario.Id == "A161")
        {
            EdgeContract[] northHints = [.. contract.Edges.Where(edge => edge.RoutingHint is
                { Leave: LayoutHintDirection.North })];
            Assert.That(northHints, Has.Length.EqualTo(7), "A161 includes seven explicit leave-North hints.");
            foreach (EdgeContract edge in northHints)
            {
                FlowchartLayoutEdgeRoute? route = layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == edge.Id);
                bool valid = route is { Points.Count: >= 2 } && DiagramGridRouteBuilder.PathIsValid(
                    [.. route.Points.Select(point => (point.X, point.Y))],
                    layout.NodeBounds.Concat(layout.ClusterBounds).GroupBy(box => box.NodeId, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal),
                    route.SourceId, route.TargetId, contract);
                TestContext.Out.WriteLine($"A161_HINT|{edge.Id}|active={edge.RoutingHint!.IsActive}|reason={edge.RoutingHint.UnhonouredReason ?? "none"}|route={route?.ExitSide ?? "missing"}/{route?.EntrySide ?? "missing"}|valid={valid}|points={string.Join(";", route?.Points.Select(point => $"{point.X:0.##},{point.Y:0.##}") ?? [])}");
                Assert.Multiple(() =>
                {
                    Assert.That(edge.RoutingHint.IsActive, Is.True, $"{edge.Id} must retain its feasible North hint.");
                    Assert.That(edge.RoutingHint.UnhonouredReason, Is.Null, $"{edge.Id} must not report a dropped hint.");
                    Assert.That(route?.ExitSide, Is.EqualTo("top"), $"{edge.Id} must leave its North/top port.");
                    Assert.That(valid, Is.True, $"{edge.Id} must use a legal route.");
                });
            }
        }
        int noLegalRouteFailures = layout.RenderContext.LayoutStageViolations.Count(finding => finding.Kind == "NoLegalRoute");
        foreach (FlowchartLayoutQualityFailure failure in layout.RenderContext.LayoutStageViolations
                     .Where(failure => failure.Kind == "NoLegalRoute"))
        {
            TestContext.Out.WriteLine($"NO_LEGAL_ROUTE|{scenario.Id}|{string.Join(",", failure.InvolvedIds)}|{failure.Description}");
        }

        foreach (EdgeContract edge in contract.Edges.Where(candidate =>
                     candidate.RoutingHint is { IsActive: false, UnhonouredReason: not null }))
        {
            TestContext.Out.WriteLine($"ROUTING_HINT_DROPPED|{scenario.Id}|{edge.Id}|{edge.RoutingHint!.UnhonouredReason}");
        }

        int titleBandRoutes = layout.RenderContext.LayoutStageViolations.Count(finding => finding.Kind == "TitleBandRoute");
        string[] unrelatedFrameOverlaps = [.. UnrelatedFrameOverlaps(contract, layout)];
        foreach (string overlap in unrelatedFrameOverlaps)
        {
            TestContext.Out.WriteLine($"FRAME_OVERLAP|{scenario.Id}|{overlap}");
        }

        int offSlotPorts = OffSlotPortFaults(contract, layout).Count;
        foreach (string fault in OffSlotPortFaults(contract, layout))
        {
            TestContext.Out.WriteLine($"OFF_SLOT_PORT|{scenario.Id}|{fault}");
        }

        foreach (FlowchartRouteRuleChecker.Finding finding in findings.Where(finding => finding.Rule == "4.2"))
        {
            FlowchartLayoutEdgeRoute? route = layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == finding.Subject);
            if (route != null)
            {
                TestContext.Out.WriteLine($"RULE42|{scenario.Id}|{route.EdgeId}|{route.ExitSide},{route.EntrySide}|{string.Join(";", route.Points.Select(point => $"{point.X:0.##},{point.Y:0.##}"))}");
                if (scenario.Id is "F111" or "EIP84")
                {
                    FlowchartLayoutPoint a = route.Points[0], b = route.Points[^1];
                    foreach (string nodeId in new[] { route.SourceId, route.TargetId })
                    {
                        NodeContract? node = contract.Nodes.FirstOrDefault(candidate => candidate.Id == nodeId);
                        FlowchartLayoutNodeBounds? box = layout.NodeBounds.FirstOrDefault(candidate => candidate.NodeId == nodeId);
                        TestContext.Out.WriteLine($"RULE42_NODE|{scenario.Id}|{nodeId}|shape={node?.Shape}|bounds={box?.CenterX:0.##},{box?.CenterY:0.##},{box?.Width:0.##},{box?.Height:0.##}");
                    }
                    foreach (FlowchartLayoutNodeBounds box in layout.NodeBounds.Where(box =>
                                 box.NodeId != route.SourceId && box.NodeId != route.TargetId
                                 && DirectSegmentHits(box, a, b)))
                    {
                        TestContext.Out.WriteLine($"RULE42_BLOCKER|{scenario.Id}|{route.EdgeId}|{box.NodeId}");
                    }
                }
            }
        }
        string counts = string.Join(",", findings.GroupBy(finding => finding.Rule)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + "=" + group.Count().ToString(CultureInfo.InvariantCulture)));
        int diamonds = contract.Nodes.Count(node => FlowchartDiamondAttachHelper.IsDiamondShape(node.Shape));
        (int alignedPortJogs, int nonalignedPortJogs) = CountJogClasses(contract, layout);
        (int correctBends, int sideChoiceFaultBends, int blockedNodeBends, int realBendFaults) =
            ClassifyBends(contract, layout, findings);
        TestContext.Out.WriteLine($"ROUTE_AUDIT|{category}|{scenario.Id}|diamonds={diamonds}|edges={contract.Edges.Count}|routes={layout.EdgeRoutes.Count}|missing={missingRoutes}|noLegalRoute={noLegalRouteFailures}|titleBand={titleBandRoutes}|frameOverlaps={unrelatedFrameOverlaps.Length}|offGridNodes={offGridNodes}|offGridFrameEdges={offGridFrameEdges}|port={portFaults}|offSlotPorts={offSlotPorts}|offSlotGroups={findings.Count(finding => finding.Rule == "3.2/4.7")}|alignedPort4.2={findings.Count(finding => finding.Rule == "4.2")}|alignedPortJogs={alignedPortJogs}|nonalignedPortJogs={nonalignedPortJogs}|grid={gridFaults}|{counts}");
        _summaryRows.Add(scenario.Id);
        TestContext.Out.WriteLine($"ROUTE_LEGALITY|{scenario.Id}|illegalPaths={findings.Count(finding => finding.Rule == "10")}|sharedTracks={findings.Count(finding => finding.Rule == "5.1")}");
        TestContext.Out.WriteLine($"JOG_CLASS|{scenario.Id}|correct={correctBends}|sideChoice={sideChoiceFaultBends}|blockedNode={blockedNodeBends}|real={realBendFaults}");
        TestContext.Out.WriteLine($"MOVE_BEFORE_DETOUR|{scenario.Id}|moved={moveBeforeDetourMoves}|candidates={moveBeforeDetourCandidates}|blockers={string.Join(",", moveBeforeDetourCases.Distinct(StringComparer.Ordinal))}");
        TestContext.Out.WriteLine($"ROUTE_REPAIR_CHAIN|{scenario.Id}|wins={routeRepairChainRuns}|depthTotal={routeRepairChainDepthTotal}|maxDepth={routeRepairChainMaxDepth}|maxLines={routeRepairChainMaxLines}|maxExtraLength={routeRepairChainMaxExtraLength:0.##}");
        TestContext.Out.WriteLine($"ROUTE_REPAIR_OPTIONS|{scenario.Id}|{string.Join(",", routeRepairOptionWins.GroupBy(item => item.Option, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => group.Key + "=" + group.Count()))}");
        // Assertions follow the summary so failures remain represented in catalogue totals.
        // Stress exclusion applies to quality totals, never to port validity.
        // Checking the same aggregate that we publish prevents a scenario such
        // as A107 from silently passing with measured side/allocation faults.
        if (CatalogueScenarioPolicy.StressScenarios.Contains(scenario.Id))
        {
            Assert.That(portFaults, Is.Zero,
                "Every stress case must have zero measured port faults: " + string.Join("; ",
                    findings.Where(finding => portRules.Contains(finding.Rule))
                        .Select(finding => finding.Rule + " " + finding.Subject)));
        }

        if (scenario.Id == "A90")
        {
            Assert.That(droppedLinks, Has.Length.EqualTo(2), "Both dropped A90 links must be explicit errors.");
            Assert.That(string.Join("\n", droppedLinks), Does.Contain("basket->shop"));
            Assert.That(string.Join("\n", droppedLinks), Does.Contain("ordering->checkout"));
            InvalidOperationException outputError = Assert.Throws<InvalidOperationException>(
                () => EnterpriseRenderBridge.RenderEnterpriseSvg(scenario.Source))!;
            Assert.That(outputError.Message, Does.Contain("basket->shop"));
            Assert.That(outputError.Message, Does.Contain("ordering->checkout"));
        }
        if (scenario.Id == "A163")
        {
            Assert.That(a163ActiveHints - a163HintScore!.HintsHonoured - a163ReportedHints, Is.Zero,
                "Every A163 hint must be honoured or carry an explicit unhonoured reason.");
        }

        if (scenario.Id == "A161")
        {
            EdgeContract[] northHints = [.. contract.Edges.Where(edge => edge.RoutingHint is
                { IsActive: true, Leave: LayoutHintDirection.North })];
            foreach (EdgeContract edge in northHints)
            {
                FlowchartLayoutEdgeRoute? route = layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == edge.Id);
                Assert.That(edge.RoutingHint!.UnhonouredReason, Is.Null, $"Routing hint for {edge.Id} must remain active and honoured.");
                Assert.That(route, Is.Not.Null, $"Hinted route {edge.Id} must be present.");
                Assert.That(route!.ExitSide, Is.EqualTo("top"), $"Hinted route {edge.Id} must leave the north/top port.");
            }
        }
        if (scenario.Id == "A163")
        {
            EnterpriseDocument withoutHintsDocument = EnterpriseValidator.Analyze(scenario.Source).Document;
            foreach (EnterpriseElement element in withoutHintsDocument.Elements.Values)
            {
                element.PlacementHint = null;
            }

            foreach (EnterpriseGroup group in withoutHintsDocument.Groups.Values)
            {
                group.PlacementHint = null;
            }

            foreach (EnterpriseNote note in withoutHintsDocument.Notes.Values)
            {
                note.PlacementHint = null;
            }

            DiagramContract withoutHintsContract = EnterpriseFlowchartContractMapper.Build(withoutHintsDocument);
            bool withoutHintsSucceeded = FlowchartSugiyamaLayoutEngine.TryLayout(withoutHintsContract,
                new FlowchartRenderContext { UseCellRegions = Environment.GetEnvironmentVariable("ROUTE_AUDIT_CELLS_ON") != "off" }, out FlowchartLayoutResult withoutHintsLayout);
            Assert.That(withoutHintsSucceeded, Is.True);
            HashSet<string> crossingsWithoutHints = FlowchartRouteRuleChecker.Check(withoutHintsContract, withoutHintsLayout)
                .Where(finding => finding.Rule == "5.9").Select(finding => finding.Subject)
                .ToHashSet(StringComparer.Ordinal);
            int hintSpecificPairs = a163HintedCrossings!.Except(crossingsWithoutHints, StringComparer.Ordinal).Count();
            int persistentPairs = a163HintedCrossings.Intersect(crossingsWithoutHints, StringComparer.Ordinal).Count();
            TestContext.Out.WriteLine($"A163_HINT_CROSSINGS|hinted={a163HintedCrossings.Count}|withoutPlacementHints={crossingsWithoutHints.Count}|hintSpecificPairs={hintSpecificPairs}|persistentPairs={persistentPairs}|hintedScore={a163HintScore!.OptimizationScore:0.##}");
        }
        if (scenario.Id is "F33" or "A117" or "A160" or "A161")
        {
            Assert.That(findings.Where(finding => finding.Rule is "3.1" or "4.1"), Is.Empty,
                "Box links must use orthogonal segments and square-on cardinal exits after the final route stage.");
        }

        if (scenario.Id == "F125")
        {
            Assert.That(findings.Where(finding => finding.Rule is "3.1" or "3.2/4.7" or "3.3/5.2" or "4.1"),
                Is.Empty, "The F125 diamond ports lie on their slanted edges, are symmetric, ordered, and edge-normal.");
        }

        if (scenario.Id is "F11" or "F17" or "F118")
        {
            Assert.That(findings.Where(finding => finding.Rule is "3.1" or "4.1"), Is.Empty,
                "Circle ports end on the drawn circle and leave along their radius.");
        }

        if (scenario.Id is "F59" or "F137")
        {
            Assert.That(offSlotPorts, Is.Zero, "Shared ports keep at least line_spacing clearance along the edge.");
        }
        // Emit the complete per-diagram measurement before asserting it. The full
        // audit aggregates these lines; previously an off-slot failure happened
        // first, so failed scenarios disappeared from the report and looked like 0.
        Assert.That(findings.Where(finding => finding.Rule is "10" or "5.1"), Is.Empty,
            "Every route must represent a declared link, avoid node interiors/title bands, and own its overlapping tracks.");
        Assert.That(offSlotPorts, Is.Zero, "Route cleanup must preserve minimum port spacing on each edge.");
        Assert.That(orthogonalCleanupRoutes, Is.Empty,
            "Source routing stages must produce orthogonal routes; the final safety net is diagnostic only.");
        Assert.That(titleBandRoutes, Is.Zero, "Route cleanup must not introduce title-band crossings.");
        Assert.That(unrelatedFrameOverlaps, Is.Empty,
            "Rule 7.8 requires every unrelated group frame to remain disjoint after all layout and repair stages.");
        if (scenario.Id is "A115" or "A165")
        {
            Assert.That(offSlotPorts, Is.Zero, "Final frame fitting must not crowd ports below line_spacing.");
        }

        foreach (FlowchartLayoutQualityFailure finding in layout.RenderContext.LayoutStageViolations
                     .Where(finding => finding.Kind is "TitleBandRoute" or "TitleBandRepairFailed"))
        {
            TestContext.Out.WriteLine($"{finding.Kind.ToUpperInvariant()}|{scenario.Id}|{string.Join(",", finding.InvolvedIds)}|{finding.Description}");
            if (finding.Kind != "TitleBandRoute")
            {
                continue;
            }

            FlowchartLayoutEdgeRoute? route = layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == finding.InvolvedIds[0]);
            if (route != null)
            {
                EdgeContract? contractEdge = contract.Edges.FirstOrDefault(edge => edge.Id == route.EdgeId);
                TestContext.Out.WriteLine($"TITLE_BAND_HINT|{scenario.Id}|{route.EdgeId}|{contractEdge?.RoutingHint?.IsActive == true}");
                TestContext.Out.WriteLine($"TITLE_BAND_POINTS|{scenario.Id}|{route.EdgeId}|{string.Join(";", route.Points.Select(point => $"{point.X:0.##},{point.Y:0.##}"))}");
                foreach (string bandId in finding.InvolvedIds.Skip(2))
                {
                    FlowchartLayoutNodeBounds? frame = layout.ClusterBounds.FirstOrDefault(box => box.NodeId == bandId);
                    TestContext.Out.WriteLine($"TITLE_BAND_GEOMETRY|{scenario.Id}|{bandId}|{frame?.CenterX:0.##},{frame?.CenterY:0.##},{frame?.Width:0.##},{frame?.Height:0.##}|source={layout.NodeBounds.FirstOrDefault(box => box.NodeId == route.SourceId)?.CenterX:0.##},{layout.NodeBounds.FirstOrDefault(box => box.NodeId == route.SourceId)?.CenterY:0.##}|target={layout.NodeBounds.FirstOrDefault(box => box.NodeId == route.TargetId)?.CenterX:0.##},{layout.NodeBounds.FirstOrDefault(box => box.NodeId == route.TargetId)?.CenterY:0.##}");
                }
            }
        }
        if (scenario.Id is "A63" or "A142")
        {
            Dictionary<string, FlowchartLayoutNodeBounds> nodeBounds = layout.NodeBounds
                .GroupBy(box => box.NodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            FlowchartRenderOptions renderOptions = FlowchartRenderOptions.FromContract(contract);
            foreach (FlowchartSubgraphLayoutRect frame in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(
                         contract, nodeBounds,
                         FlowchartSubgraphBoundsRequest.FromContract(contract, renderOptions)))
            {
                TestContext.Out.WriteLine($"{scenario.Id}_FRAME|{frame.GroupId}|{frame.Left:0.##},{frame.Bottom:0.##},{frame.Right:0.##},{frame.Top:0.##}");
            }
        }
        auditTimer.Stop();
        TestContext.Out.WriteLine($"AUDIT_TIME|{category}|{scenario.Id}|{auditTimer.Elapsed.TotalMilliseconds:0.0}ms|fitPasses={fitPasses}|stages={string.Join(",", stageTimes.Select(item => $"{item.Stage}:{item.Ms:0.0}"))}");
        foreach (string cause in gridCauses)
        {
            TestContext.Out.WriteLine($"GRID_CAUSE|{category}|{cause}");
        }

        if (diamonds > 0)
        {
            foreach (FlowchartRouteRuleChecker.Finding finding in findings.Where(finding => finding.Rule is "3.1" or "3.2/4.7" or "3.3/5.2" or "4.1"))
            {
                TestContext.Out.WriteLine($"DIAMOND_REVIEW|{scenario.Id}|{finding.Rule}|{finding.Subject}|{finding.Description}");
                EdgeContract? edge = contract.Edges.FirstOrDefault(candidate => candidate.Id == finding.Subject);
                FlowchartLayoutEdgeRoute? route = edge == null ? null : layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == edge.Id);
                if (edge != null && route != null)
                {
                    foreach (bool source in new[] { true, false })
                    {
                        string endpointId = source ? edge.SourceId : edge.TargetId;
                        NodeContract? endpoint = contract.Nodes.FirstOrDefault(node => node.Id == endpointId);
                        FlowchartLayoutNodeBounds? bounds = layout.NodeBounds.FirstOrDefault(box => box.NodeId == endpointId);
                        if (!FlowchartDiamondAttachHelper.IsDiamondShape(endpoint?.Shape) || bounds == null)
                        {
                            continue;
                        }

                        FlowchartLayoutPoint point = source ? route.Points[0] : route.Points[^1];
                        double equation = (Math.Abs(point.X - bounds.CenterX) / (bounds.Width / 2))
                            + (Math.Abs(point.Y - bounds.CenterY) / (bounds.Height / 2));
                        TestContext.Out.WriteLine($"DIAMOND_GEOMETRY|{scenario.Id}|{endpointId}|{(source ? "source" : "target")}|port=({point.X:0.##},{point.Y:0.##})|center=({bounds.CenterX:0.##},{bounds.CenterY:0.##})|size=({bounds.Width:0.##},{bounds.Height:0.##})|outlineEquation={equation:0.###}");
                    }
                }
            }
        }
    }

    private static bool DirectSegmentHits(FlowchartLayoutNodeBounds box,
        FlowchartLayoutPoint a, FlowchartLayoutPoint b)
    {
        double left = box.CenterX - (box.Width / 2), right = box.CenterX + (box.Width / 2);
        double bottom = box.CenterY - (box.Height / 2), top = box.CenterY + (box.Height / 2);
        if (Math.Abs(a.X - b.X) <= 1)
        {
            return a.X > left + 1 && a.X < right - 1
            && Math.Min(a.Y, b.Y) < top - 1 && Math.Max(a.Y, b.Y) > bottom + 1;
        }

        if (Math.Abs(a.Y - b.Y) <= 1)
        {
            return a.Y > bottom + 1 && a.Y < top - 1
            && Math.Min(a.X, b.X) < right - 1 && Math.Max(a.X, b.X) > left + 1;
        }

        return false;
    }

    private static (int Aligned, int Nonaligned) CountJogClasses(DiagramContract contract,
        FlowchartLayoutResult layout)
    {
        int aligned = 0, nonaligned = 0;
        foreach (EdgeContract edge in contract.Edges.Where(edge =>
                     !string.Equals(edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
        {
            FlowchartLayoutEdgeRoute? route = !string.IsNullOrEmpty(edge.Id)
                ? layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == edge.Id)
                : null;
            route ??= layout.EdgeRoutes.FirstOrDefault(candidate => candidate.SourceId == edge.SourceId
                && candidate.TargetId == edge.TargetId);
            if (route == null || edge.RoutingHint != null || route.Points.Count < 3 || !HasTurn(route))
            {
                continue;
            }

            FlowchartLayoutPoint start = route.Points[0], end = route.Points[^1];
            bool horizontal = Math.Abs(start.Y - end.Y) <= 1.0
                && ((end.X >= start.X && route.ExitSide == "right" && route.EntrySide == "left")
                    || (end.X < start.X && route.ExitSide == "left" && route.EntrySide == "right"));
            bool vertical = Math.Abs(start.X - end.X) <= 1.0
                && ((end.Y >= start.Y && route.ExitSide == "top" && route.EntrySide == "bottom")
                    || (end.Y < start.Y && route.ExitSide == "bottom" && route.EntrySide == "top"));
            if (horizontal || vertical)
            {
                aligned++;
            }
            else
            {
                nonaligned++;
            }
        }
        return (aligned, nonaligned);

        static bool HasTurn(FlowchartLayoutEdgeRoute route)
        {
            for (int i = 1; i + 1 < route.Points.Count; i++)
            {
                FlowchartLayoutPoint a = route.Points[i - 1], b = route.Points[i], c = route.Points[i + 1];
                bool abHorizontal = Math.Abs(a.Y - b.Y) <= 1.0;
                bool bcHorizontal = Math.Abs(b.Y - c.Y) <= 1.0;
                bool abVertical = Math.Abs(a.X - b.X) <= 1.0;
                bool bcVertical = Math.Abs(b.X - c.X) <= 1.0;
                if ((abHorizontal && bcVertical) || (abVertical && bcHorizontal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static (int Correct, int SideChoiceFault, int BlockedNode, int RealFault) ClassifyBends(
        DiagramContract contract, FlowchartLayoutResult layout,
        IReadOnlyList<FlowchartRouteRuleChecker.Finding> findings)
    {
        int correct = 0, sideChoice = 0, blocked = 0, real = 0;
        Dictionary<string, FlowchartLayoutNodeBounds> boxes = layout.NodeBounds
            .Concat(layout.ClusterBounds).GroupBy(box => box.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, NodeContract> nodes = contract.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach (EdgeContract edge in contract.Edges.Where(edge =>
                     !string.Equals(edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
        {
            FlowchartLayoutEdgeRoute? route = !string.IsNullOrEmpty(edge.Id)
                ? layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == edge.Id)
                : null;
            route ??= layout.EdgeRoutes.FirstOrDefault(candidate => candidate.SourceId == edge.SourceId
                && candidate.TargetId == edge.TargetId);
            if (route == null || route.Points.Count < 3 || !HasTurn(route)
                || !boxes.TryGetValue(edge.SourceId, out FlowchartLayoutNodeBounds? source)
                || !boxes.TryGetValue(edge.TargetId, out FlowchartLayoutNodeBounds? target))
            {
                continue;
            }

            string edgeName = string.IsNullOrEmpty(edge.Id) ? edge.SourceId + "->" + edge.TargetId : edge.Id;
            bool sideFault = edge.RoutingHint == null && findings.Any(finding => finding.Subject == edgeName
                && (finding.Rule is "4.11" or "5.8"));
            if (sideFault) { sideChoice++; continue; }

            FlowchartLayoutPoint start = route.Points[0], end = route.Points[^1];
            bool horizontal = Math.Abs(start.Y - end.Y) <= 1.0
                && ((end.X >= start.X && route.ExitSide == "right" && route.EntrySide == "left")
                    || (end.X < start.X && route.ExitSide == "left" && route.EntrySide == "right"));
            bool vertical = Math.Abs(start.X - end.X) <= 1.0
                && ((end.Y >= start.Y && route.ExitSide == "top" && route.EntrySide == "bottom")
                    || (end.Y < start.Y && route.ExitSide == "bottom" && route.EntrySide == "top"));
            if (!horizontal && !vertical) { correct++; continue; }
            bool blocker = layout.NodeBounds.Any(box => box.NodeId != edge.SourceId && box.NodeId != edge.TargetId
                && DirectSegmentHits(box, start, end));
            if (blocker)
            {
                blocked++;
            }
            else
            {
                real++;
            }
        }
        return (correct, sideChoice, blocked, real);

        static bool HasTurn(FlowchartLayoutEdgeRoute route)
        {
            for (int i = 1; i + 1 < route.Points.Count; i++)
            {
                FlowchartLayoutPoint a = route.Points[i - 1], b = route.Points[i], c = route.Points[i + 1];
                bool firstHorizontal = Math.Abs(a.Y - b.Y) <= 1.0;
                bool secondHorizontal = Math.Abs(b.Y - c.Y) <= 1.0;
                bool firstVertical = Math.Abs(a.X - b.X) <= 1.0;
                bool secondVertical = Math.Abs(b.X - c.X) <= 1.0;
                if ((firstHorizontal && secondVertical) || (firstVertical && secondHorizontal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static (int SideChoice, int BlockedNode, int CorrectBend, int RealFault) ClassifyRule42(
        DiagramContract contract, FlowchartLayoutResult layout,
        IReadOnlyList<FlowchartRouteRuleChecker.Finding> findings)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> boxes = layout.NodeBounds.Concat(layout.ClusterBounds)
            .GroupBy(box => box.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        int sideChoice = 0, blockedNode = 0, correctBend = 0, realFault = 0;
        foreach (FlowchartRouteRuleChecker.Finding finding in findings.Where(finding => finding.Rule == "4.2"))
        {
            FlowchartLayoutEdgeRoute? route = layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == finding.Subject);
            if (route == null || route.Points.Count < 3) { realFault++; continue; }
            if (!boxes.TryGetValue(route.SourceId, out FlowchartLayoutNodeBounds? source)
                || !boxes.TryGetValue(route.TargetId, out FlowchartLayoutNodeBounds? target))
            { realFault++; continue; }

            string sourceSide = FacingSide(source, target);
            string targetSide = FacingSide(target, source);
            if (route.ExitSide != sourceSide || route.EntrySide != targetSide)
            { sideChoice++; continue; }

            FlowchartLayoutPoint start = route.Points[0], end = route.Points[^1];
            bool blocked = layout.NodeBounds.Any(box => box.NodeId != route.SourceId && box.NodeId != route.TargetId
                && DirectSegmentHits(box, start, end));
            if (blocked) { blockedNode++; continue; }

            // Rule 4.2 is only a fault when endpoint ports align. The finding itself
            // establishes that condition; a bend without a blocker is therefore real.
            realFault++;
        }

        // Correct bends are counted among all non-4.2 bent routes in this diagram:
        // their allocated endpoints do not line up, so straightening would move a port.
        HashSet<string> faultIds = findings.Where(finding => finding.Rule == "4.2")
            .Select(finding => finding.Subject).ToHashSet(StringComparer.Ordinal);
        foreach (FlowchartLayoutEdgeRoute route in layout.EdgeRoutes.Where(route => !faultIds.Contains(route.EdgeId)
                     && route.Points.Count >= 3 && HasTurn(route.Points)))
        {
            FlowchartLayoutPoint start = route.Points[0], end = route.Points[^1];
            if (Math.Abs(start.X - end.X) <= 1 || Math.Abs(start.Y - end.Y) <= 1)
            {
                continue;
            }

            if (!boxes.TryGetValue(route.SourceId, out FlowchartLayoutNodeBounds? source)
                || !boxes.TryGetValue(route.TargetId, out FlowchartLayoutNodeBounds? target))
            {
                continue;
            }

            if (layout.NodeBounds.Any(box => box.NodeId != route.SourceId && box.NodeId != route.TargetId
                && DirectSegmentHits(box, start, end)))
            {
                continue;
            }

            if (route.ExitSide == FacingSide(source, target) && route.EntrySide == FacingSide(target, source))
            {
                correctBend++;
            }
        }
        return (sideChoice, blockedNode, correctBend, realFault);

        static string FacingSide(FlowchartLayoutNodeBounds from, FlowchartLayoutNodeBounds to)
        {
            double dx = to.CenterX - from.CenterX, dy = to.CenterY - from.CenterY;
            return Math.Abs(dx) >= Math.Abs(dy) ? dx >= 0 ? "right" : "left"
                : dy >= 0 ? "top" : "bottom";
        }

        static bool HasTurn(IReadOnlyList<FlowchartLayoutPoint> points)
        {
            for (int index = 1; index + 1 < points.Count; index++)
            {
                bool beforeHorizontal = Math.Abs(points[index].Y - points[index - 1].Y) <= 1;
                bool afterHorizontal = Math.Abs(points[index + 1].Y - points[index].Y) <= 1;
                bool beforeVertical = Math.Abs(points[index].X - points[index - 1].X) <= 1;
                bool afterVertical = Math.Abs(points[index + 1].X - points[index].X) <= 1;
                if ((beforeHorizontal && afterVertical) || (beforeVertical && afterHorizontal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    private static int CountGeometrySideChoiceFindings(string scenarioId, DiagramContract contract, FlowchartLayoutResult layout)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> boxes = layout.NodeBounds.Concat(layout.ClusterBounds)
            .GroupBy(box => box.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        int findings = 0;
        foreach (EdgeContract edge in contract.Edges.Where(edge =>
                     !string.Equals(edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
                     && edge.RoutingHint?.IsActive != true))
        {
            FlowchartLayoutEdgeRoute? route = !string.IsNullOrEmpty(edge.Id)
                ? layout.EdgeRoutes.FirstOrDefault(candidate => candidate.EdgeId == edge.Id)
                : null;
            route ??= layout.EdgeRoutes.FirstOrDefault(candidate => candidate.SourceId == edge.SourceId
                && candidate.TargetId == edge.TargetId);
            if (route == null || !boxes.TryGetValue(edge.SourceId, out FlowchartLayoutNodeBounds? source)
                || !boxes.TryGetValue(edge.TargetId, out FlowchartLayoutNodeBounds? target))
            {
                continue;
            }
            // If one endpoint is fully nested inside the other (overlap on
            // both axes), center-to-center geometry does not define a unique
            // facing side. Do not report that ambiguous relationship as a
            // side-choice fault.
            bool SeparatedX = source.CenterX + (source.Width / 2) <= target.CenterX - (target.Width / 2)
                || target.CenterX + (target.Width / 2) <= source.CenterX - (source.Width / 2);
            bool SeparatedY = source.CenterY + (source.Height / 2) <= target.CenterY - (target.Height / 2)
                || target.CenterY + (target.Height / 2) <= source.CenterY - (source.Height / 2);
            if (!SeparatedX && !SeparatedY)
            {
                continue;
            }

            string? expectedExit = FacingSide(source, target), expectedEntry = FacingSide(target, source);
            if (expectedExit == null || expectedEntry == null)
            {
                continue;
            }

            if (route.ExitSide != expectedExit)
            {
                findings++;
                TestContext.Out.WriteLine($"SIDE_CHOICE_FAULT|{scenarioId}|{route.EdgeId}|source={route.SourceId}:{route.ExitSide}->{expectedExit}@{source.CenterX:0.##},{source.CenterY:0.##},{source.Width:0.##},{source.Height:0.##}|target={route.TargetId}:{route.EntrySide}->{expectedEntry}@{target.CenterX:0.##},{target.CenterY:0.##},{target.Width:0.##},{target.Height:0.##}");
            }
            if (route.EntrySide != expectedEntry)
            {
                findings++;
                TestContext.Out.WriteLine($"SIDE_CHOICE_FAULT|{scenarioId}|{route.EdgeId}|source={route.SourceId}:{route.ExitSide}->{expectedExit}@{source.CenterX:0.##},{source.CenterY:0.##},{source.Width:0.##},{source.Height:0.##}|target={route.TargetId}:{route.EntrySide}->{expectedEntry}@{target.CenterX:0.##},{target.CenterY:0.##},{target.Width:0.##},{target.Height:0.##}");
            }
        }
        return findings;

        static string? FacingSide(FlowchartLayoutNodeBounds from, FlowchartLayoutNodeBounds to)
        {
            double dx = to.CenterX - from.CenterX, dy = to.CenterY - from.CenterY;
            return Math.Abs(dx) >= Math.Abs(dy) ? dx >= 0 ? "right" : "left"
                : dy >= 0 ? "top" : "bottom";
        }
    }

    /// <summary>Retains the legacy centre and frame-edge lattice audit for layouts without authoritative cell regions.</summary>
    /// <param name="layout">Output to inspect.</param>
    /// <param name="originX">Legacy horizontal lattice origin.</param>
    /// <param name="originY">Legacy vertical lattice origin.</param>
    /// <param name="step">Existing drawing-grid step.</param>
    /// <returns>Off-lattice node centres and frame edges.</returns>
    internal static Dictionary<string, string> GridFaults(FlowchartLayoutResult layout, double originX, double originY, double step)
    {
        Dictionary<string, string> faults = new(StringComparer.Ordinal);
        foreach (FlowchartLayoutNodeBounds box in layout.NodeBounds)
        {
            if (!OnGrid(box.CenterX, originX, step) || !OnGrid(box.CenterY, originY, step))
            {
                faults["node:" + box.NodeId] = "node";
            }
        }

        foreach (FlowchartLayoutNodeBounds frame in layout.ClusterBounds)
        {
            if (!OnGrid(frame.CenterX - (frame.Width / 2), originX, step))
            {
                faults[$"frame:{frame.NodeId}:left"] = "frame-edge";
            }

            if (!OnGrid(frame.CenterX + (frame.Width / 2), originX, step))
            {
                faults[$"frame:{frame.NodeId}:right"] = "frame-edge";
            }

            if (!OnGrid(frame.CenterY - (frame.Height / 2), originY, step))
            {
                faults[$"frame:{frame.NodeId}:bottom"] = "frame-edge";
            }

            if (!OnGrid(frame.CenterY + (frame.Height / 2), originY, step))
            {
                faults[$"frame:{frame.NodeId}:top"] = "frame-edge";
            }
        }
        return faults;
    }

    /// <summary>Reports overlapping unrelated output frames, excluding declared ancestor containment.</summary>
    /// <param name="contract">Declared group ancestry.</param>
    /// <param name="layout">Final output geometry.</param>
    /// <returns>Every unrelated positive-area overlap above the existing audit tolerance.</returns>
    internal static IEnumerable<string> UnrelatedFrameOverlaps(DiagramContract contract,
        FlowchartLayoutResult layout)
    {
        Dictionary<string, GroupContract> groups = contract.Groups.ToDictionary(group => group.Id,
            StringComparer.Ordinal);
        FlowchartLayoutNodeBounds[] frames = [.. layout.ClusterBounds
            .GroupBy(frame => frame.NodeId, StringComparer.Ordinal).Select(group => group.First())
            .OrderBy(frame => frame.NodeId, StringComparer.Ordinal)];
        bool Ancestor(string possibleAncestor, string child)
        {
            string? parent = groups.TryGetValue(child, out GroupContract? group) ? group.ParentGroupId : null;
            while (parent != null)
            {
                if (string.Equals(parent, possibleAncestor, StringComparison.Ordinal))
                {
                    return true;
                }

                parent = groups.TryGetValue(parent, out group) ? group.ParentGroupId : null;
            }
            return false;
        }

        for (int first = 0; first < frames.Length; first++)
        {
            for (int second = first + 1; second < frames.Length; second++)
        {
            FlowchartLayoutNodeBounds a = frames[first], b = frames[second];
            if (Ancestor(a.NodeId, b.NodeId) || Ancestor(b.NodeId, a.NodeId))
                {
                    continue;
                }

                double overlapX = Math.Min(a.CenterX + (a.Width / 2), b.CenterX + (b.Width / 2))
                - Math.Max(a.CenterX - (a.Width / 2), b.CenterX - (b.Width / 2));
            double overlapY = Math.Min(a.CenterY + (a.Height / 2), b.CenterY + (b.Height / 2))
                - Math.Max(a.CenterY - (a.Height / 2), b.CenterY - (b.Height / 2));
            if (overlapX > 0.5 && overlapY > 0.5)
                {
                    yield return $"{a.NodeId} x {b.NodeId} ({overlapX:0.##}x{overlapY:0.##})";
                }
            }
        }
    }

    private static bool OnGrid(double value, double origin, double step) =>
        Math.Abs(((value - origin) / step) - Math.Round((value - origin) / step)) <= 0.01;

    private readonly record struct AuditPort(string NodeId, string Side, string PeerId,
        FlowchartLayoutPoint Point, FlowchartLayoutEdgeRoute Route, bool IsSource, string Shape);

    /// <summary>Checks final route terminals for minimum port spacing along each edge group.</summary>
    /// <param name="contract">Declared endpoints and renderer geometry.</param>
    /// <param name="layout">Final allocated route model.</param>
    /// <returns>Adjacent ports closer than line_spacing on the same connection segment.</returns>
    internal static HashSet<string> OffSlotPortFaults(DiagramContract contract, FlowchartLayoutResult layout)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> boxes = layout.NodeBounds.Concat(layout.ClusterBounds)
            .GroupBy(box => box.NodeId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, NodeContract> nodes = contract.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        List<AuditPort> ports = [];
        foreach (EdgeContract edge in contract.Edges.Where(edge => !string.Equals(edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase)))
        {
            FlowchartLayoutEdgeRoute? route = layout.EdgeRoutes.FirstOrDefault(candidate => !string.IsNullOrEmpty(edge.Id)
                && candidate.EdgeId == edge.Id) ?? layout.EdgeRoutes.FirstOrDefault(candidate =>
                    candidate.SourceId == edge.SourceId && candidate.TargetId == edge.TargetId);
            if (route == null || route.Points.Count < 2)
            {
                continue;
            }

            string sourceShape = nodes.GetValueOrDefault(edge.SourceId)?.Shape ?? "rect";
            string targetShape = nodes.GetValueOrDefault(edge.TargetId)?.Shape ?? "rect";
            ports.Add(new AuditPort(edge.SourceId, route.ExitSide, edge.TargetId, route.Points[0], route, true, sourceShape));
            ports.Add(new AuditPort(edge.TargetId, route.EntrySide, edge.SourceId, route.Points[^1], route, false, targetShape));
        }

        double minimumSpacing = FlowchartLayoutRules.LineSpacing;
        HashSet<string> offSlotFaults = new(StringComparer.Ordinal);
        foreach (IGrouping<(string NodeId, string PortGroup), AuditPort> group in ports.GroupBy(port =>
                     (port.NodeId, AuditPortGroup(port))))
        {
            if (group.Count() < 2 || !boxes.TryGetValue(group.Key.NodeId, out FlowchartLayoutNodeBounds? box))
            {
                continue;
            }

            AuditPort[] ordered = [.. group.OrderBy(port => PortParameter(port, box))];
            for (int index = 1; index < ordered.Length; index++)
            {
                AuditPort left = ordered[index - 1];
                AuditPort right = ordered[index];
                double separation = Math.Sqrt(Math.Pow(left.Point.X - right.Point.X, 2)
                    + Math.Pow(left.Point.Y - right.Point.Y, 2));
                if (separation + 1.0 < minimumSpacing)
                {
                    offSlotFaults.Add($"{group.Key.NodeId}|shape={left.Shape}|{group.Key.PortGroup}|{right.Route.EdgeId}|spacing={separation:0.###}|({right.Point.X:0.##},{right.Point.Y:0.##})|box=({box.CenterX:0.##},{box.CenterY:0.##},{box.Width:0.##},{box.Height:0.##})");
                }
            }
        }
        return offSlotFaults;
    }

    private static string AuditPortGroup(AuditPort port)
    {
        if (!port.Shape.Equals("diamond", StringComparison.OrdinalIgnoreCase))
        {
            return port.Side;
        }

        double nx = port.IsSource ? port.Route.ExitNormalX : port.Route.EntryNormalX;
        double ny = port.IsSource ? port.Route.ExitNormalY : port.Route.EntryNormalY;
        if (Math.Abs(nx) < 0.001 || Math.Abs(ny) < 0.001)
        {
            return "diamond-tip-" + port.Side;
        }

        return "diamond-" + (nx >= 0 ? "+" : "-") + (ny >= 0 ? "+" : "-");
    }

    private static double PortParameter(AuditPort port, FlowchartLayoutNodeBounds box)
    {
        if (port.Shape.Equals("diamond", StringComparison.OrdinalIgnoreCase)
            && ((Math.Abs(port.Point.X - box.CenterX) < 0.001
                && Math.Abs(Math.Abs(port.Point.Y - box.CenterY) - (box.Height / 2)) < 1)
                || (Math.Abs(port.Point.Y - box.CenterY) < 0.001
                && Math.Abs(Math.Abs(port.Point.X - box.CenterX) - (box.Width / 2)) < 1)))
        {
            return 0.5;
        }

        bool circle = port.Shape.Equals("circle", StringComparison.OrdinalIgnoreCase)
            || port.Shape.Equals("doublecircle", StringComparison.OrdinalIgnoreCase);
        if (circle)
        {
            double angle = Math.Atan2(port.Point.Y - box.CenterY, port.Point.X - box.CenterX);
            return port.Side switch
            {
                "top" => (Math.PI - angle) / Math.PI,
                "bottom" => ((angle < 0 ? angle + (2 * Math.PI) : angle) - Math.PI) / Math.PI,
                "left" => ((angle < Math.PI / 2 ? angle + (2 * Math.PI) : angle) - (Math.PI / 2)) / Math.PI,
                _ => ((Math.PI / 2) - angle) / Math.PI
            };
        }
        if (port.Shape.Equals("diamond", StringComparison.OrdinalIgnoreCase))
        {
            double nx = port.IsSource ? port.Route.ExitNormalX : port.Route.EntryNormalX;
            double ny = port.IsSource ? port.Route.ExitNormalY : port.Route.EntryNormalY;
            bool rightHalf = nx >= 0, topHalf = ny >= 0;
            double sx = box.CenterX, sy = topHalf ? box.CenterY + (box.Height / 2) : box.CenterY - (box.Height / 2);
            double ex = rightHalf ? box.CenterX + (box.Width / 2) : box.CenterX - (box.Width / 2), ey = box.CenterY;
            double vx = ex - sx, vy = ey - sy, lengthSquared = (vx * vx) + (vy * vy);
            return lengthSquared <= 1e-9 ? 0.5
                : Math.Clamp((((port.Point.X - sx) * vx) + ((port.Point.Y - sy) * vy)) / lengthSquared, 0, 1);
        }
        return port.Side switch
        {
            "left" or "right" => (port.Point.Y - (box.CenterY - (box.Height / 2))) / box.Height,
            _ => (port.Point.X - (box.CenterX - (box.Width / 2))) / box.Width
        };
    }

    private static double DiamondEdgeLength(AuditPort port, FlowchartLayoutNodeBounds box) =>
        Math.Sqrt((box.Width * box.Width) + (box.Height * box.Height)) / 2.0;

    private static IEnumerable<Scenario> ReadCatalogue(string root)
    {
        foreach ((string file, bool enterprise) in new[] { ("flow.md", false), ("enterprise.md", true), ("EIP.md", false) })
        {
            string path = Path.Combine(root, file);
            string markdown = File.ReadAllText(path);
            if (file == "EIP.md")
            {
                MatchCollection blocks = Regex.Matches(markdown, @"(?ms)^\s*:::mermaid\s*\r?\n(?<Body>[\s\S]*?)\r?\n\s*:::\s*$", RegexOptions.CultureInvariant);
                for (int i = 0; i < blocks.Count; i++)
                {
                    yield return new Scenario("EIP" + (i + 1).ToString(CultureInfo.InvariantCulture), blocks[i].Groups["Body"].Value.Trim(), false);
                }

                continue;
            }

            List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> diagrams = MermaidMarkdownFenceExtractor.ExtractFlowcharts(markdown);
            Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
            MatchCollection fences = Regex.Matches(markdown, "(?m)^```mermaid\\s*$", RegexOptions.CultureInvariant);
            foreach (MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram diagram in diagrams)
            {
                string id;
                if (enterprise)
                {
                    int fenceIndex = diagram.Index < fences.Count ? fences[diagram.Index].Index : markdown.Length;
                    MatchCollection headings = Regex.Matches(markdown[..fenceIndex], @"(?m)^###\s+(A\d+)\b", RegexOptions.CultureInvariant);
                    id = headings.Count == 0 ? "EIP" + (diagram.Index + 1).ToString(CultureInfo.InvariantCulture) : headings[^1].Groups[1].Value;
                }
                else
                {
                    id = diagram.DiagramId;
                }

                if (occurrences.TryGetValue(id, out int count))
                {
                    count++;
                    occurrences[id] = count;
                    id += "." + count.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    occurrences.Add(id, 1);
                }

                yield return new Scenario(id, diagram.Source, enterprise);
            }
        }
    }

    private static string FindRoot()
    {
        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor != null)
        {
            if (File.Exists(Path.Combine(cursor.FullName, "flow.md"))
                && File.Exists(Path.Combine(cursor.FullName, "enterprise.md"))
                && File.Exists(Path.Combine(cursor.FullName, "EIP.md")))
            {
                return cursor.FullName;
            }

            cursor = cursor.Parent;
        }
        throw new DirectoryNotFoundException("Could not find catalogue markdown sources.");
    }
}
