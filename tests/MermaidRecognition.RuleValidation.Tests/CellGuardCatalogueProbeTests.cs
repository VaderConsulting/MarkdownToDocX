using System.Diagnostics;
using System.Reflection;
using MermaidRecognition.Config;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using MermaidRecognition.Render;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Explicit diagnostic experiment: binds declared regions before placement without changing the production pipeline.</summary>
[TestFixture]
[NonParallelizable]
[Explicit("Runs the separate cell-activation census; its provisional seed is not production placement.")]
public sealed class CellGuardCatalogueProbeTests
{
    /// <summary>Names every existing stage after grid preparation; the first illegal bound-node write is reported.</summary>
    private static readonly string[] Stages = ["PlacementHintStage", "NodePlacementStage", "GroupFrameStage", "RouteCoordinateStage",
        "JointPortAllocationStage", "RoutingHintStage", "RouteConstructionStage", "RouteRepairStage", "ModelRouteCompletionStage", "LegalityValidationStage"];

    /// <summary>Uses every unchanged catalogue source, with distinct names from the production audit.</summary>
    /// <returns>All catalogue scenarios, including native failures and open-mode exemptions.</returns>
    public static IEnumerable<TestCaseData> Catalogue()
    {
        foreach (TestCaseData Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            RouteRuleCatalogueAuditTests.Scenario Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            yield return new TestCaseData(Scenario).SetName("CellGuardProbe_" + Scenario.Id);
        }
    }

    /// <summary>Counts actual bound-node checks and records the first blocked path instead of mistaking an unbound run for guard coverage.</summary>
    /// <param name="Scenario">One unchanged source from the full catalogue.</param>
    [TestCaseSource(nameof(Catalogue))]
    [CancelAfter(60000)]
    public void ProbeBoundCatalogue(RouteRuleCatalogueAuditTests.Scenario Scenario)
        => RunBoundCatalogue(Scenario, RequireCompletion: false);

    /// <summary>Runs ordinary quality checks through production; explicit injection callbacks retain the separately seeded stage harness.</summary>
    /// <param name="Scenario">Unchanged catalogue source.</param>
    /// <param name="RequireCompletion">Whether a pre-binding skip or blocked writer must fail the regression.</param>
    /// <param name="ObserveCompleted">Optional audit consumer of the actual parsed contract and completed bound model.</param>
    /// <param name="ObserveBoundNodes">Optional audit consumer of authoritative ownership, never inferred from output pixels.</param>
    /// <param name="PrepareBeforeRouting">Optional test-only atomic placement fixture after automatic placement has finished.</param>
    /// <param name="ObserveCompletedState">Optional regression callback for exercising rejection against the completed real engine state.</param>
    internal static void RunBoundCatalogue(RouteRuleCatalogueAuditTests.Scenario Scenario, bool RequireCompletion,
        Action<DiagramContract, FlowchartLayoutResult>? ObserveCompleted = null,
        Action<IReadOnlyList<DiagramGridLayout.GridNode>>? ObserveBoundNodes = null,
        Action<List<DiagramGridLayout.GridNode>>? PrepareBeforeRouting = null,
        Action<object>? ObserveCompletedState = null)
    {
        // Catalogue quality checks need the complete production pipeline, including
        // selected wraps and final repairs. Only explicit stage-injection fixtures
        // retain the historical reflection harness below.
        if (RequireCompletion && PrepareBeforeRouting == null && ObserveCompletedState == null)
        {
            DiagramContract ProductionContract = Scenario.Enterprise
                ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
                : MermaidRecognizer.Parse(Scenario.Source);
            FlowchartRenderContext ProductionContext = new FlowchartRenderContext { UseCellRegions = true };
            bool Complete = FlowchartSugiyamaLayoutEngine.TryLayout(ProductionContract, ProductionContext, out FlowchartLayoutResult? Production);
            CellGuardCatalogueReporting.Write(Scenario.Id, "production", Production, Complete ? "completed" : "native-layout-failed");
            Assert.That(Complete, Is.True, "A production catalogue regression must finish: " + Scenario.Id);
            ObserveCompleted?.Invoke(ProductionContract, Production);
            ObserveBoundNodes?.Invoke(Production.CellNodes ?? []);
            return;
        }
        Stopwatch Clock = Stopwatch.StartNew();
        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        FlowchartRenderContext Context = new FlowchartRenderContext();
        FlowchartLayoutResult? Layout = null;
        string Mode = "on", Status = "native-layout-failed", Stage = "native-placement";
        Action<string>? PreviousFrameObserver = DiagramGridLayout.GroupFrameFitPassObserver;
        if (Environment.GetEnvironmentVariable("CELL_FRAME_TRACE") == "1")
        {
            DiagramGridLayout.GroupFrameFitPassObserver = Message => TestContext.Out.WriteLine($"CELL_FRAME_TRACE|{Scenario.Id}|{Message}");
        }
        else if (Environment.GetEnvironmentVariable("CELL_FRAME_COUNTS") == "1")
        {
            DiagramGridLayout.GroupFrameFitPassObserver = Message =>
            {
                PreviousFrameObserver?.Invoke(Message);
                if (Message.StartsWith("CELL_FRAME_SEARCH_RESULT|", StringComparison.Ordinal))
                {
                    TestContext.Out.WriteLine($"CELL_FRAME_COUNTS|{Scenario.Id}|{Message}");
                }
            };
        }

        try
        {
            if (Contract.LayoutMode == DiagramLayoutMode.Open)
            {
                Mode = "open-exempt";
                Status = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out Layout!) ? "completed" : "native-layout-failed";
                if (Status == "completed")
                {
                    ObserveCompleted?.Invoke(Contract, Layout!);
                }

                return;
            }
            if (Contract.Nodes.Count == 0 || !NativeRenderCapabilities.CanRender(Contract))
            {
                ReportNativeSkip(Scenario.Id, Contract,
                    Contract.Nodes.Count == 0 ? "no-nodes" : "native-render-ineligible");
                return;
            }
            if (!FlowchartDagrePlacement.TryPlace(Contract, out FlowchartLayoutResult? Native))
            {
                ReportNativeSkip(Scenario.Id, Contract, "native-placement-returned-false");
                return;
            }
            Type StateType = typeof(DiagramGridLayout).GetNestedType("GridLayoutState", BindingFlags.NonPublic)!;
            object State = Activator.CreateInstance(StateType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, [Contract, Native, Context], null)!;
            using IDisposable CoverageScope = FlowchartCellPixelWriteGuard.ActivateCoverage(
                (FlowchartCellWriteCoverage)StateType.GetProperty("CellWriteCoverage")!.GetValue(State)!);
            InvokeStage("GridPreparationStage", State);
            Layout = (FlowchartLayoutResult)StateType.GetProperty("Copy")!.GetValue(State)!;
            List<DiagramGridLayout.GridNode> Nodes = (List<DiagramGridLayout.GridNode>)StateType.GetProperty("Placed")!.GetValue(State)!;
            BindDeclaredSeed(StateType, State, Nodes, Context);
            Assert.That(Layout.CellWriteCoverage!.BoundNodes, Is.EqualTo(Nodes.Count));
            Status = "completed";
            foreach (string Next in Stages)
            {
                Stage = Next;
                Context.ActiveLayoutStage = Next;
                if (Next == "RouteCoordinateStage")
                {
                    PrepareBeforeRouting?.Invoke(Nodes);
                }

                InvokeStage(Next, State);
                if (Environment.GetEnvironmentVariable("CELL_FRAME_STAGE_TRACE") == "1")
                {
                    TestContext.Out.WriteLine($"CELL_FRAME_STATE|{Scenario.Id}|{Next}|" + System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Frames = ((List<DiagramGridLayout.GridFrame>)StateType.GetProperty("Frames")!.GetValue(State)!)
                            .Select(Frame => new { Frame.Id, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom }),
                        Copy = Layout.ClusterBounds.Select(Frame => new { Frame.NodeId, Frame.CenterX, Frame.CenterY, Frame.Width, Frame.Height })
                    }));
                }
            }
            if (Environment.GetEnvironmentVariable("CELL_CENSUS_METRICS") == "1")
            {
                int Bends = 0;
                foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
                {
                    int Count = FlowchartLayoutScore.CountCorners(Route.Points); Bends += Count;
                    TestContext.Out.WriteLine($"CELL_CENSUS_ROUTE|{Scenario.Id}|{Route.EdgeId}|bends={Count}");
                }
                TestContext.Out.WriteLine($"CELL_CENSUS_METRICS|{Scenario.Id}|routes={Layout.EdgeRoutes.Count}|bends={Bends}");
            }
            TestContext.Out.WriteLine($"CELL_HINT_MATCH|{Scenario.Id}|matches={FlowchartPlacementHints.CellHintsMatch(Contract, Nodes, Nodes[0].CellNode!.Grid.Capture().Regions)}");
            if (Environment.GetEnvironmentVariable("CELL_ROUTE_GEOMETRY_TRACE") == "1")
            {
                TestContext.Out.WriteLine($"CELL_ROUTE_GEOMETRY|{Scenario.Id}|" + System.Text.Json.JsonSerializer.Serialize(new
                {
                    Bounds = Layout.NodeBounds.Select(Box => new { Box.NodeId, Box.CenterX, Box.CenterY, Box.Width, Box.Height }),
                    Shapes = Contract.Nodes.Select(Node => new { Node.Id, Node.Shape }),
                    Routes = Layout.EdgeRoutes.Select(Route => new { Route.EdgeId, Route.SourceId, Route.TargetId,
                        Route.ExitSide, Route.EntrySide, Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY,
                        Points = Route.Points.Select(Point => new { Point.X, Point.Y }) })
                }));
            }

            ObserveCompleted?.Invoke(Contract, Layout);
            ObserveBoundNodes?.Invoke(Nodes);
            ObserveCompletedState?.Invoke(State);
        }
        catch (TargetInvocationException Failure) when (Failure.InnerException?.Data["CellPixelWriteDiagnostic"] is FlowchartLayoutQualityFailure Diagnostic)
        {
            Status = "guard-caught";
            TestContext.Out.WriteLine($"CELL_GUARD_CAUGHT|{Scenario.Id}|stage={Stage}|{Diagnostic.Description}");
            if (RequireCompletion)
            {
                Assert.Fail("Frame regression stopped at a direct pixel write: " + Diagnostic.Description);
            }
        }
        catch (Exception Failure)
        {
            Status = "other-failure";
            Exception Cause = Failure is TargetInvocationException Wrapped ? Wrapped.InnerException! : Failure;
            TestContext.Out.WriteLine($"CELL_GUARD_PROBE_FAILURE|{Scenario.Id}|stage={Stage}|{Cause.GetType().Name}|{Cause.Message}");
            if (Environment.GetEnvironmentVariable("CELL_DIAGNOSTIC_STACK") == "1")
            {
                TestContext.Out.WriteLine(Cause.ToString());
            }

            Assert.Fail("The diagnostic probe failed before a guard result: " + Cause.Message);
        }
        finally
        {
            DiagramGridLayout.GroupFrameFitPassObserver = PreviousFrameObserver;
            Context.ActiveLayoutStage = null;
            CellGuardCatalogueReporting.Write(Scenario.Id, Mode, Layout, Status);
            TestContext.Out.WriteLine($"CELL_GUARD_PROBE_TIME|{Scenario.Id}|milliseconds={Clock.Elapsed.TotalMilliseconds:0.000}|stage={Stage}");
            if (RequireCompletion && Status == "native-layout-failed")
            {
                Assert.Fail("A frame regression exited before binding: " + Scenario.Id);
            }
        }
        if (RequireCompletion)
        {
            Assert.That(Status, Is.EqualTo("completed"), "A frame regression must finish every real cell-bound stage.");
        }
    }

    /// <summary>Explains a pre-binding exit without treating unsupported or remarked source as a cell-repair failure.</summary>
    /// <param name="Id">Catalogue identity retained in the census.</param>
    /// <param name="Contract">Parsed source, including original eligibility diagnostics.</param>
    /// <param name="Reason">The exact predicate that prevented native placement or binding.</param>
    private static void ReportNativeSkip(string Id, DiagramContract Contract, string Reason) =>
        TestContext.Out.WriteLine($"CELL_GUARD_NATIVE_SKIP|{Id}|reason={Reason}|nodes={Contract.Nodes.Count}|remark={Contract.RemarkStatus}|diagnostics="
            + System.Text.Json.JsonSerializer.Serialize(Contract.Diagnostics.Select(Diagnostic => new
                { Diagnostic.Code, Diagnostic.Severity, Diagnostic.Message })));

    /// <summary>Binds the shared production common-span seed from logical ranks/columns, never from final pixel positions or inferred midpoints.</summary>
    /// <param name="StateType">Existing engine state type used by the stage fixture.</param>
    /// <param name="State">Prepared native rank/column data and measured common text box.</param>
    /// <param name="Nodes">All nodes to bind before placement and repairs.</param>
    /// <param name="Context">Explicit diagnostic sink for this scenario.</param>
    private static void BindDeclaredSeed(Type StateType, object State, List<DiagramGridLayout.GridNode> Nodes,
        FlowchartRenderContext Context)
    {
        int Width = (int)StateType.GetProperty("BoxWidth")!.GetValue(State)!;
        int Height = (int)StateType.GetProperty("BoxHeight")!.GetValue(State)!;
        bool Horizontal = (bool)StateType.GetProperty("Horizontal")!.GetValue(State)!;
        FlowchartCellInitialPlacement.Bind(Nodes, Width, Height, Horizontal, Context);
    }

    /// <summary>Calls the real private pipeline stage, preserving the original exception and caller path for the census.</summary>
    /// <param name="Name">Existing nested stage type name.</param>
    /// <param name="State">Actual engine state with test-declared regions.</param>
    private static void InvokeStage(string Name, object State) => typeof(DiagramGridLayout)
        .GetNestedType(Name, BindingFlags.NonPublic)!.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [State]);
}
