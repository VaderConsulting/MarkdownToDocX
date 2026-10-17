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
    {
        var Clock = Stopwatch.StartNew();
        var Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        var Context = new FlowchartRenderContext();
        FlowchartLayoutResult? Layout = null;
        string Mode = "on", Status = "native-layout-failed", Stage = "native-placement";
        try
        {
            if (Contract.LayoutMode == DiagramLayoutMode.Open)
            {
                Mode = "open-exempt";
                Status = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out Layout!) ? "completed" : "native-layout-failed";
                return;
            }
            if (Contract.Nodes.Count == 0 || !NativeRenderCapabilities.CanRender(Contract)
                || !FlowchartDagrePlacement.TryPlace(Contract, out var Native)) return;
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
        }
        catch (TargetInvocationException Failure) when (Failure.InnerException?.Data["CellPixelWriteDiagnostic"] is FlowchartLayoutQualityFailure Diagnostic)
        {
            Status = "guard-caught";
            TestContext.Out.WriteLine($"CELL_GUARD_CAUGHT|{Scenario.Id}|stage={Stage}|{Diagnostic.Description}");
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
            Context.ActiveLayoutStage = null;
            CellGuardCatalogueReporting.Write(Scenario.Id, Mode, Layout, Status);
            TestContext.Out.WriteLine($"CELL_GUARD_PROBE_TIME|{Scenario.Id}|milliseconds={Clock.Elapsed.TotalMilliseconds:0.000}|stage={Stage}");
        }
    }

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
