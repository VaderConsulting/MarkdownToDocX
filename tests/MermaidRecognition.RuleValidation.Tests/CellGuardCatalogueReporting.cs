using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Reports real cell binding and guard coverage, including native failures that never reached placement.</summary>
internal static class CellGuardCatalogueReporting
{
    /// <summary>Writes one machine-readable coverage row without changing any route-audit finding or gate.</summary>
    /// <param name="Id">Catalogue scenario identity.</param>
    /// <param name="Mode">Production-off, experimental-on or open-exempt run.</param>
    /// <param name="Layout">Current layout, which may have failed before creating nodes.</param>
    /// <param name="Status">Completed, rejected or failed stage status.</param>
    internal static void Write(string Id, string Mode, FlowchartLayoutResult? Layout, string Status)
    {
        var Coverage = Layout?.CellWriteCoverage;
        TestContext.Out.WriteLine($"CELL_GUARD_COVERAGE|{Id}|mode={Mode}|nodes={Coverage?.Nodes ?? 0}|bound={Coverage?.BoundNodes ?? 0}"
            + $"|checks={Coverage?.Checks ?? 0}|boundChecks={Coverage?.BoundChecks ?? 0}|rejections={Coverage?.Rejections ?? 0}|status={Status}");
    }
}
