using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Records optional measurement evidence without altering any existing audit checker or assertion.</summary>
internal static class FinalRepairAuditCapture
{
    /// <summary>One completed or discarded final-pass assignment.</summary>
    /// <param name="Key">Existing assignment identity.</param>
    /// <param name="Reason">Acceptance or exact guard that rejected it.</param>
    /// <param name="ExtraLength">Measured Manhattan length change in pixels.</param>
    internal sealed record Decision(string Key, string Reason, double ExtraLength);

    /// <summary>One timed full routing call and its ordered geometry and route-input signatures.</summary>
    /// <param name="Key">Candidate in the original enumeration order.</param>
    /// <param name="Geometry">Placement-only signature.</param>
    /// <param name="Input">Signature including incoming routes.</param>
    /// <param name="Base">Whether only tracks or ports changed before completion.</param>
    /// <param name="Reused">Whether an already completed placement supplied the exact reference routes.</param>
    /// <param name="Milliseconds">Inclusive reference routing and completion duration.</param>
    internal sealed record Confirmation(string Key, string Geometry, string Input, bool Base, bool Reused, double Milliseconds);

    /// <summary>Appends a complete scenario measurement to an explicitly selected local JSONL file.</summary>
    /// <param name="Id">Catalogue identity.</param>
    /// <param name="Contract">Unchanged source.</param>
    /// <param name="Layout">Finished model, or an empty native-ineligible result.</param>
    /// <param name="Seconds">The existing audit's measured layout duration.</param>
    /// <param name="Decisions">Final-pass candidate decisions.</param>
    /// <param name="Stages">Optional existing stage timers, summarised without printing individual route operations.</param>
    internal static void Record(string Id, DiagramContract Contract, FlowchartLayoutResult? Layout, double Seconds,
        IReadOnlyList<Decision> Decisions, IReadOnlyList<(string Stage, double Ms)> Stages,
        IReadOnlyList<Confirmation> Confirmations)
    {
        string? Path = Environment.GetEnvironmentVariable("FINAL_REPAIR_CAPTURE");
        if (Path == null || Layout == null) return;
        int Crossings = 0, Shared = 0;
        for (int A = 0; A < Layout.EdgeRoutes.Count; A++) for (int B = A + 1; B < Layout.EdgeRoutes.Count; B++)
        {
            var Hits = FlowchartBranchRouteOrder.Intersections(Layout.EdgeRoutes[A].Points.Select(Point => (Point.X, Point.Y)).ToArray(),
                Layout.EdgeRoutes[B].Points.Select(Point => (Point.X, Point.Y)).ToArray());
            Crossings += Hits.Crossings; if (Hits.Overlap > 0) Shared++;
        }
        var Findings = FlowchartRouteRuleChecker.Check(Contract, Layout);
        File.AppendAllText(Path, JsonSerializer.Serialize(new { Id, Complete = Layout.NodeBounds.Count > 0, Seconds,
            Bends = Layout.EdgeRoutes.Sum(Route => FlowchartLayoutScore.CountCorners(Route.Points)), Crossings, Shared,
            FiveNine = Findings.Count(Finding => Finding.Rule == "5.9"), TotalFindings = Findings.Count,
            Rules = Findings.GroupBy(Finding => Finding.Rule).ToDictionary(Group => Group.Key, Group => Group.Count()),
            Findings, Decisions, Confirmations,
            Costs = Stages.Where(Item => Item.Ms >= 0 && (Item.Stage.StartsWith("cell-cost/exclusive/", StringComparison.Ordinal)
                || Item.Stage == "final-complete-layout-repair")).GroupBy(Item => Item.Stage)
                .ToDictionary(Group => Group.Key, Group => Group.Sum(Item => Item.Ms))
        }) + Environment.NewLine);
    }
}
