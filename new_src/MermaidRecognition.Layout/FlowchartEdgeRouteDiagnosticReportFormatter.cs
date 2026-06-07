using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>
/// Formats routing diagnostic findings for test failures and command-line tools.
/// </summary>
public static class FlowchartEdgeRouteDiagnosticReportFormatter
{
    /// <summary>
    /// Builds a multi-line report describing all findings.
    /// </summary>
    public static string FormatReport(
        IReadOnlyList<FlowchartEdgeRouteDiagnosticFinding> Findings,
        string DiagramTitle)
    {
        if (Findings.Count == 0)
        {
            return "Routing diagnostics: no defects detected for " + DiagramTitle + ".";
        }

        StringBuilder Builder = new StringBuilder();
        Builder.AppendLine("Routing diagnostics for " + DiagramTitle + ": " + Findings.Count.ToString(CultureInfo.InvariantCulture) + " defect(s).");
        Builder.AppendLine("These checks explain geometry rule violations; they do not replace SVG or visual comparison.");
        Builder.AppendLine();

        for (int FindingIndex = 0; FindingIndex < Findings.Count; FindingIndex++)
        {
            Builder.Append(FindingIndex + 1);
            Builder.Append(". ");
            Builder.AppendLine(Findings[FindingIndex].FormatLine());
        }

        return Builder.ToString().TrimEnd();
    }
}
