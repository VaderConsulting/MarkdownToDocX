using System.Text.Json;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Captures every catalogue result and prevents per-scenario bend or crossing regressions during markup repairs.</summary>
[TestFixture, NonParallelizable, Explicit("Measures all catalogue layouts against a supplied unchanged baseline.")]
public sealed class MarkupCatalogueReviewTests
{
    /// <summary>One complete measured result; an unmeasured native source is never counted as a zero-route success.</summary>
    /// <param name="Id">Stable catalogue identity.</param>
    /// <param name="Complete">Whether native layout produced a measurable model.</param>
    /// <param name="Bends">All corners on completed declared paths.</param>
    /// <param name="Crossings">All geometric crossings, including paths sharing a node.</param>
    /// <param name="Length">Total Euclidean path length.</param>
    /// <param name="Model">Exact diagnostic snapshot for marked sources.</param>
    internal sealed record Measurement(string Id, bool Complete, int Bends, int Crossings, double Length, string? Model);
    private static readonly HashSet<string> Marked = ["A14", "A15", "A17", "A20", "A23", "A24", "A25", "A26", "A29", "A36", "A37", "A38", "A42", "A46", "A48", "A104", "A105", "A152"];
    private Dictionary<string, Measurement>? Baseline;
    private string Output = "";

    /// <summary>Loads the supplied baseline and starts a separate capture file before any case can append data.</summary>
    [OneTimeSetUp]
    public void BeginCapture()
    {
        string? Path = Environment.GetEnvironmentVariable("MARKUP_BASELINE");
        if (Path != null)
        {
            Baseline = File.ReadLines(Path).Select(Line => JsonSerializer.Deserialize<Measurement>(Line)!)
            .ToDictionary(Row => Row.Id, StringComparer.Ordinal);
        }

        Output = Environment.GetEnvironmentVariable("MARKUP_OUTPUT") ?? throw new InvalidOperationException("MARKUP_OUTPUT is required.");
        File.WriteAllText(Output, "");
    }

    /// <summary>Requires all 673 identities in both reference and new captures.</summary>
    [OneTimeTearDown]
    public void RequireCompleteCapture()
    {
        Assert.That(File.ReadLines(Output).Select(Line => JsonSerializer.Deserialize<Measurement>(Line)!.Id).Distinct().Count(), Is.EqualTo(673));
        if (Baseline != null)
        {
            Assert.That(Baseline.Count, Is.EqualTo(673));
        }
    }

    /// <summary>Measures every source independently, including open-mode and native-ineligible cases.</summary>
    /// <returns>All unchanged catalogue cases.</returns>
    public static IEnumerable<TestCaseData> Cases() => RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
        new TestCaseData(Case.Arguments[0]).SetName("MarkupReview_" + ((RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Id));

    /// <summary>Rejects hard faults and any per-scenario bend/crossing increase while retaining full before/after evidence.</summary>
    /// <param name="Scenario">Unchanged source under production cell settings.</param>
    [TestCaseSource(nameof(Cases))]
    public void MeasureAndCompare(RouteRuleCatalogueAuditTests.Scenario Scenario)
    {
        DiagramContract Contract = Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source);
        FlowchartRenderContext Context = new FlowchartRenderContext { UseCellRegions = true };
        bool Complete = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, Context, out FlowchartLayoutResult? Layout);
        int Crossings = 0;
        (double X, double Y)[][] Paths = [.. Layout.EdgeRoutes.Select(Route => Route.Points.Select(Point => (Point.X, Point.Y)).ToArray())];
        for (int A = 0; A < Paths.Length; A++)
        {
            for (int B = A + 1; B < Paths.Length; B++)
            {
                Crossings += FlowchartBranchRouteOrder.Intersections(Paths[A], Paths[B]).Crossings;
            }
        }

        double Length = Paths.Sum(Path => Enumerable.Range(1, Math.Max(0, Path.Length - 1))
            .Sum(double (int Index) => Math.Sqrt(Math.Pow(Path[Index].X - Path[Index - 1].X, 2) + Math.Pow(Path[Index].Y - Path[Index - 1].Y, 2))));
        Measurement Actual = new Measurement(Scenario.Id, Complete, Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)),
            Crossings, Length, Complete && Marked.Contains(Scenario.Id) ? CellProductionParityTests.Snapshot(Contract, Layout) : null);
        File.AppendAllText(Output, JsonSerializer.Serialize(Actual) + Environment.NewLine);
        if (Baseline != null)
        {
            Assert.That(Actual.Complete, Is.EqualTo(Baseline[Scenario.Id].Complete));
            Assert.That(Actual.Bends, Is.LessThanOrEqualTo(Baseline[Scenario.Id].Bends), "Bend regression: " + Scenario.Id);
            Assert.That(Actual.Crossings, Is.LessThanOrEqualTo(Baseline[Scenario.Id].Crossings), "Crossing regression: " + Scenario.Id);
        }
        if (!Complete)
        {
            return;
        }

        Assert.That(FlowchartRouteRuleChecker.Check(Contract, Layout).Where(Finding => Finding.Rule is "10" or "4.1" or "3.2/4.7"), Is.Empty);
        Assert.That(Context.RejectedCellPixelWrites, Is.Empty);
        if (Layout.CellNodes != null)
        {
            Assert.That(CellGridAudit.Faults(Contract, Layout, Layout.CellNodes), Is.Empty);
        }
    }
}
