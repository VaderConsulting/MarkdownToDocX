using System.Collections.Frozen;

namespace LayoutCatalogueBench;

/// <summary>
/// Diagrams omitted from the layout catalogue SVG bench. See enterprise.md and flow.md gate sections.
/// </summary>
internal static class CatalogueBenchExclusions
{
    private static readonly FrozenSet<string> EnterpriseExpectedDiagnostic = FrozenSet.ToFrozenSet(
        new[]
        {
            "A78", "A79", "A80", "A81", "A82", "A83", "A84", "A86", "A87", "A88", "A89", "A90", "A91", "A92", "A93",
            "A94", "A102", "A129", "A130"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> EnterpriseLayoutStress = FrozenSet.ToFrozenSet(
        new[] { "A104", "A105", "A106", "A107", "A108", "A109", "A110", "A111", "A112" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> EnterpriseGridAndHintStress = FrozenSet.ToFrozenSet(
        new[]
        {
            "A142",
            "A154", "A155", "A156", "A157", "A158", "A159", "A160", "A161", "A162", "A163"
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> FlowWrapStress = FrozenSet.ToFrozenSet(
        new[] { "F151", "F152", "F153" },
        StringComparer.OrdinalIgnoreCase);

    internal static bool TryGetSkipReason(string Id, string Source, out string Reason)
    {
        if (IsFullyRemarked(Source))
        {
            Reason = "remarked";
            return true;
        }

        if (EnterpriseExpectedDiagnostic.Contains(Id))
        {
            Reason = "enterprise expected-diagnostic";
            return true;
        }

        if (EnterpriseLayoutStress.Contains(Id))
        {
            Reason = "enterprise layout stress (level 17)";
            return true;
        }

        if (EnterpriseGridAndHintStress.Contains(Id))
        {
            Reason = "enterprise layout stress (grid or hints)";
            return true;
        }

        if (FlowWrapStress.Contains(Id))
        {
            Reason = "flowchart wrap stress (level 17)";
            return true;
        }

        Reason = string.Empty;
        return false;
    }

    private static bool IsFullyRemarked(string Source)
    {
        bool SawNonBlank = false;
        foreach (string Line in Source.Split('\n'))
        {
            string Trimmed = Line.Trim();
            if (Trimmed.Length == 0)
            {
                continue;
            }

            SawNonBlank = true;
            if (!Trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return SawNonBlank;
    }
}
