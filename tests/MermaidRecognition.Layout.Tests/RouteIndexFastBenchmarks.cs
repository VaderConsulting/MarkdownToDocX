using System.Diagnostics;
using System.Text;

using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

[TestFixture]
public sealed class RouteIndexFastBenchmarks
{
    [Test]
    public void CompleteManyToMany_4x4() => Run(4);

    [Test]
    public void CompleteManyToMany_6x6() => Run(6);

    private static void Run(int Size)
    {
        StringBuilder Source = new("flowchart LR\n");
        for (int Index = 0; Index < Size; Index++)
        {
            Source.AppendLine($"s{Index}[Source {Index}]");
            Source.AppendLine($"t{Index}[Target {Index}]");
        }
        for (int From = 0; From < Size; From++)
        for (int To = 0; To < Size; To++)
            Source.AppendLine($"s{From} --> t{To}");

        var Contract = MermaidRecognizer.Parse(Source.ToString());
        Stopwatch Timer = Stopwatch.StartNew();
        bool Success = FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout);
        Timer.Stop();
        if (!Success)
        {
            TestContext.WriteLine($"SIZE={Size} ELAPSED_MS={Timer.ElapsedMilliseconds} TRY_LAYOUT=false FAILURE_TEXT=TryLayout returned false");
            return;
        }
        FlowchartLayoutScore.Result Metrics = FlowchartLayoutScore.Measure(Contract, Layout);
        string Failures = Metrics.Violations.Count == 0 ? "none"
            : string.Join(" || ", Metrics.Violations.Select(Failure => Failure.Kind + ": " + Failure.Description));
        TestContext.WriteLine($"SIZE={Size} ELAPSED_MS={Timer.ElapsedMilliseconds} CROSSINGS={Metrics.Crossings} "
            + $"OVERLAP={Metrics.Overlaps} FAILURE_TEXT={Failures}");
    }
}
