using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using MermaidRecognition.Layout;

namespace LiveEditor;

public partial class MainForm
{
    internal DiagramRouteReport? LastRouteReport { get; private set; }
    private ImmutableDictionary<string, int> _RouteDiagnosticLines = ImmutableDictionary<string, int>.Empty;

    private void ClearRouteDiagnostics()
    {
        LastRouteReport = null;
        _RouteDiagnosticLines = ImmutableDictionary<string, int>.Empty;
        RefreshRouteDiagnostics();
    }

    private void RefreshRouteDiagnostics()
    {
        foreach (var Item in DiagnosticsList.Items.Cast<object>().Where(Item => Item is RouteDiagnosticListItem or RouteSummaryListItem).ToArray())
            DiagnosticsList.Items.Remove(Item);
        if (!LiveEditorFeatureGate.CaptureLayoutOptions().EnableRouteAwareConstruction || LastRouteReport is not { } Report) return;
        DiagnosticsList.Items.Add(new RouteSummaryListItem(Report));
        foreach (var Route in Report.Routes)
            DiagnosticsList.Items.Add(new RouteDiagnosticListItem(Route, _RouteDiagnosticLines.GetValueOrDefault(Route.Identity)));
    }

    private sealed record RouteSummaryListItem(DiagramRouteReport Report)
    {
        public override string ToString() => $"Routes: {Report.Routes.Length}; bends {Report.Bends}; crossing pairs {Report.CrossingPairs}; shared-track pairs {Report.SharedTrackPairs}; findings {Report.Findings.Length}. Improvements require a verified trial.";
    }

    private sealed record RouteDiagnosticListItem(DiagramRouteMeasurement Route, int LineNumber)
    {
        public override string ToString()
        {
            string Px(double? Value) => Value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "unavailable";
            string State = Route.Assessment switch { DiagramRouteAssessment.Invalid => "Invalid", DiagramRouteAssessment.Investigate => "Investigate (improvement unverified)", _ => "Measured" };
            string Partners(string Name, ImmutableArray<string> Ids) => Ids.Length == 0 ? "" : $"; {Name}: {string.Join(", ", Ids)}";
            string Issues = Route.Findings.Length == 0 ? "" : "; " + string.Join("; ", Route.Findings.Select(Item => Item.Rule + " " + Item.Description));
            return $"{State}: {Route.Identity} ({Route.SourceId} → {Route.TargetId}), line {LineNumber}: bends {Route.Bends}; length {Px(Route.LengthPx)} px; extra {Px(Route.ExcessLengthPx)} px; detour {Px(Route.DetourRatio)}×"
                + Partners("crosses", Route.CrossingPartners) + Partners("shares track with", Route.SharedTrackPartners) + Issues;
        }
    }
}
