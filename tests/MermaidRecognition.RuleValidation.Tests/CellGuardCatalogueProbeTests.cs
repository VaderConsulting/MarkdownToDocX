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
        foreach (var Case in RouteRuleCatalogueAuditTests.Catalogue())
        {
            var Scenario = (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!;
            yield return new TestCaseData(Scenario).SetName("CellGuardProbe_" + Scenario.Id);
        }
    }

    /// <summary>Counts actual bound-node checks and records the first blocked path instead of mistaking an unbound run for guard coverage.</summary>
    /// <param name="Scenario">One unchanged source from the full catalogue.</param>
    [TestCaseSource(nameof(Catalogue))]
    [CancelAfter(60000)]
    public void ProbeBoundCatalogue(RouteRuleCatalogueAuditTests.Scenario Scenario)
        => RunBoundCatalogue(Scenario, RequireCompletion: false);

    /// <summary>Runs the real bound pipeline; regression callers require completion rather than accepting a diagnostic first catch.</summary>
    /// <param name="Scenario">Unchanged catalogue source.</param>
    /// <param name="RequireCompletion">Whether a pre-binding skip or blocked writer must fail the regression.</param>
    internal static void RunBoundCatalogue(RouteRuleCatalogueAuditTests.Scenario Scenario, bool RequireCompletion)
    {
        var Clock = Stopwatch.StartNew();
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        var Context = new FlowchartRenderContext();
        FlowchartLayoutResult? Layout = null;
        string Mode = "on", Status = "native-layout-failed", Stage = "native-placement";
        if (Environment.GetEnvironmentVariable("CELL_FRAME_TRACE") == "1")
            DiagramGridLayout.GroupFrameFitPassObserver = Message => TestContext.Out.WriteLine($"CELL_FRAME_TRACE|{Scenario.Id}|{Message}");
        try
        {
            if (Contract.LayoutMode == DiagramLayoutMode.Open)
            {
                Mode = "open-exempt";
                Status = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out Layout!) ? "completed" : "native-layout-failed";
                return;
            }
            if (Contract.Nodes.Count == 0 || !NativeRenderCapabilities.CanRender(Contract))
            {
                ReportNativeSkip(Scenario.Id, Contract,
                    Contract.Nodes.Count == 0 ? "no-nodes" : "native-render-ineligible");
                return;
            }
            if (!FlowchartDagrePlacement.TryPlace(Contract, out var Native))
            {
                ReportNativeSkip(Scenario.Id, Contract, "native-placement-returned-false");
                return;
            }
            Type StateType = typeof(DiagramGridLayout).GetNestedType("GridLayoutState", BindingFlags.NonPublic)!;
            object State = Activator.CreateInstance(StateType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, [Contract, Native, Context], null)!;
            using var CoverageScope = FlowchartCellPixelWriteGuard.ActivateCoverage(
                (FlowchartCellWriteCoverage)StateType.GetProperty("CellWriteCoverage")!.GetValue(State)!);
            InvokeStage("GridPreparationStage", State);
            Layout = (FlowchartLayoutResult)StateType.GetProperty("Copy")!.GetValue(State)!;
            var Nodes = (List<DiagramGridLayout.GridNode>)StateType.GetProperty("Placed")!.GetValue(State)!;
            BindDeclaredSeed(StateType, State, Nodes, Context);
            Assert.That(Layout.CellWriteCoverage!.BoundNodes, Is.EqualTo(Nodes.Count));
            Status = "completed";
            foreach (string Next in Stages)
            {
                Stage = Next;
                Context.ActiveLayoutStage = Next;
                InvokeStage(Next, State);
            }
            if (Environment.GetEnvironmentVariable("CELL_CENSUS_METRICS") == "1")
            {
                int Bends = 0;
                foreach (var Route in Layout.EdgeRoutes)
                {
                    int Count = FlowchartLayoutScore.CountCorners(Route.Points); Bends += Count;
                    TestContext.Out.WriteLine($"CELL_CENSUS_ROUTE|{Scenario.Id}|{Route.EdgeId}|bends={Count}");
                }
                TestContext.Out.WriteLine($"CELL_CENSUS_METRICS|{Scenario.Id}|routes={Layout.EdgeRoutes.Count}|bends={Bends}");
            }
        }
        catch (TargetInvocationException Failure) when (Failure.InnerException?.Data["CellPixelWriteDiagnostic"] is FlowchartLayoutQualityFailure Diagnostic)
        {
            Status = "guard-caught";
            TestContext.Out.WriteLine($"CELL_GUARD_CAUGHT|{Scenario.Id}|stage={Stage}|{Diagnostic.Description}");
            if (RequireCompletion) Assert.Fail("Frame regression stopped at a direct pixel write: " + Diagnostic.Description);
        }
        catch (Exception Failure)
        {
            Status = "other-failure";
            Exception Cause = Failure is TargetInvocationException Wrapped ? Wrapped.InnerException! : Failure;
            TestContext.Out.WriteLine($"CELL_GUARD_PROBE_FAILURE|{Scenario.Id}|stage={Stage}|{Cause.GetType().Name}|{Cause.Message}");
            Assert.Fail("The diagnostic probe failed before a guard result: " + Cause.Message);
        }
        finally
        {
            DiagramGridLayout.GroupFrameFitPassObserver = null;
            Context.ActiveLayoutStage = null;
            CellGuardCatalogueReporting.Write(Scenario.Id, Mode, Layout, Status);
            TestContext.Out.WriteLine($"CELL_GUARD_PROBE_TIME|{Scenario.Id}|milliseconds={Clock.Elapsed.TotalMilliseconds:0.000}|stage={Stage}");
            if (RequireCompletion && Status == "native-layout-failed")
                Assert.Fail("A frame regression exited before binding: " + Scenario.Id);
        }
        if (RequireCompletion) Assert.That(Status, Is.EqualTo("completed"), "A frame regression must finish every real cell-bound stage.");
    }

    /// <summary>Explains a pre-binding exit without treating unsupported or remarked source as a cell-repair failure.</summary>
    /// <param name="Id">Catalogue identity retained in the census.</param>
    /// <param name="Contract">Parsed source, including original eligibility diagnostics.</param>
    /// <param name="Reason">The exact predicate that prevented native placement or binding.</param>
    private static void ReportNativeSkip(string Id, DiagramContract Contract, string Reason) =>
        TestContext.Out.WriteLine($"CELL_GUARD_NATIVE_SKIP|{Id}|reason={Reason}|nodes={Contract.Nodes.Count}|remark={Contract.RemarkStatus}|diagnostics="
            + System.Text.Json.JsonSerializer.Serialize(Contract.Diagnostics.Select(Diagnostic => new
                { Diagnostic.Code, Diagnostic.Severity, Diagnostic.Message })));

    /// <summary>Builds a provisional common-span test seed from logical ranks/columns, never from final pixel positions or inferred midpoints.</summary>
    /// <param name="StateType">Existing engine state type used by the stage fixture.</param>
    /// <param name="State">Prepared native rank/column data and measured common text box.</param>
    /// <param name="Nodes">All nodes to bind before placement and repairs.</param>
    /// <param name="Context">Explicit diagnostic sink for this scenario.</param>
    private static void BindDeclaredSeed(Type StateType, object State, List<DiagramGridLayout.GridNode> Nodes,
        FlowchartRenderContext Context)
    {
        var Config = FlowchartLogicalGridConfiguration.FromRules();
        int Width = (int)StateType.GetProperty("BoxWidth")!.GetValue(State)!;
        int Height = (int)StateType.GetProperty("BoxHeight")!.GetValue(State)!;
        int Columns = Math.Max(1, (int)Math.Ceiling((Width + Config.ColumnGap) / (Config.CellWidth + Config.ColumnGap)));
        int Rows = Math.Max(1, (int)Math.Ceiling((Height + Config.RowGap) / (Config.CellHeight + Config.RowGap)));
        bool Horizontal = (bool)StateType.GetProperty("Horizontal")!.GetValue(State)!;
        int ColumnPitch = Math.Max(Rows, Columns);
        var Grid = FlowchartCellInitialPlacement.Create(Config, Nodes.Select(Node => new FlowchartCellPlacementSeed(
            Node.Id, Node.Rank, Node.Column, Node.IsDiamond ? 2 * ColumnPitch : Rows,
            Node.IsDiamond ? ColumnPitch : Columns)).ToArray(), Horizontal);
        foreach (var Node in Nodes) Node.BindCells(Grid, Context);
    }

    /// <summary>Calls the real private pipeline stage, preserving the original exception and caller path for the census.</summary>
    /// <param name="Name">Existing nested stage type name.</param>
    /// <param name="State">Actual engine state with test-declared regions.</param>
    private static void InvokeStage(string Name, object State) => typeof(DiagramGridLayout)
        .GetNestedType(Name, BindingFlags.NonPublic)!.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [State]);
}
