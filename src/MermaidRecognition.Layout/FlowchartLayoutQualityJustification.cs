using System;

namespace MermaidRecognition.Layout;

/// <summary>
/// Checkable reasons that may tolerate a remaining layout defect.
/// A generic "searched" note is not a reason.
/// </summary>
public static class FlowchartLayoutQualityJustification
{
    /// <summary>
    /// True when <paramref name="Reason"/> names a specific, checkable cause.
    /// </summary>
    /// <param name="Reason">Candidate justification.</param>
    /// <returns>True when the reason is one of the allowed forms.</returns>
    public static bool IsSpecificReason(string? Reason)
    {
        if (string.IsNullOrWhiteSpace(Reason))
        {
            return false;
        }

        if (Reason.StartsWith("K3,3", StringComparison.Ordinal))
        {
            return true;
        }

        if (Reason.Contains("fully enclosed", StringComparison.Ordinal))
        {
            return true;
        }

        if (Reason.StartsWith("layered inversion", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
