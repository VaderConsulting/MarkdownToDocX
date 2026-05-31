namespace MermaidRecognition.Detection;

/// <summary>
/// Stage 11.A programme kinds and L1 progress helpers (22 Naiad-aligned smoke kinds).
/// </summary>
public static class RecognitionProgramme
{
    /// <summary>
    /// Programme kinds requiring L1 before Stage 11.A exit (§11.1.13 / Naiad test-renders).
    /// </summary>
    public static readonly string[] L1ProgrammeKindIds =
    {
        "flowchart",
        "sequence",
        "classDiagram",
        "stateDiagram",
        "erDiagram",
        "gantt",
        "pie",
        "gitGraph",
        "mindmap",
        "timeline",
        "journey",
        "quadrantChart",
        "xychart",
        "requirement",
        "architecture",
        "kanban",
        "sankey",
        "block",
        "packet",
        "radar",
        "treemap",
        "c4",
    };

    /// <summary>
    /// Total L1 programme kinds (22).
    /// </summary>
    public static int L1ProgrammeKindCount
    {
        get
        {
            return L1ProgrammeKindIds.Length;
        }
    }

    /// <summary>
    /// Maps a Mermaid detector id to the normalised programme kind id.
    /// </summary>
    public static string NormalizeDetectorId(string MermaidDetectorId)
    {
        if (MermaidDetectorId.StartsWith("flowchart", StringComparison.OrdinalIgnoreCase)
            || string.Equals(MermaidDetectorId, "graph", StringComparison.OrdinalIgnoreCase))
        {
            return "flowchart";
        }

        if (string.Equals(MermaidDetectorId, "state", StringComparison.OrdinalIgnoreCase))
        {
            return "stateDiagram";
        }

        if (string.Equals(MermaidDetectorId, "class", StringComparison.OrdinalIgnoreCase))
        {
            return "classDiagram";
        }

        if (string.Equals(MermaidDetectorId, "erDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "erDiagram";
        }

        if (string.Equals(MermaidDetectorId, "gitGraph", StringComparison.OrdinalIgnoreCase))
        {
            return "gitGraph";
        }

        if (string.Equals(MermaidDetectorId, "quadrantChart", StringComparison.OrdinalIgnoreCase))
        {
            return "quadrantChart";
        }

        if (string.Equals(MermaidDetectorId, "xychart", StringComparison.OrdinalIgnoreCase))
        {
            return "xychart";
        }

        if (string.Equals(MermaidDetectorId, "requirement", StringComparison.OrdinalIgnoreCase))
        {
            return "requirement";
        }

        if (string.Equals(MermaidDetectorId, "classDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "classDiagram";
        }

        if (string.Equals(MermaidDetectorId, "c4", StringComparison.OrdinalIgnoreCase))
        {
            return "c4";
        }

        if (string.Equals(MermaidDetectorId, "journey", StringComparison.OrdinalIgnoreCase))
        {
            return "journey";
        }

        if (string.Equals(MermaidDetectorId, "treeView", StringComparison.OrdinalIgnoreCase))
        {
            return "treeview";
        }

        if (string.Equals(MermaidDetectorId, "---", StringComparison.OrdinalIgnoreCase))
        {
            return "yaml-front-matter";
        }

        return MermaidDetectorId;
    }

    /// <summary>
    /// Returns a human-readable L1 progress line (for example <c>L1: 3 of 22 — sequence (step 2 of 4)</c>).
    /// </summary>
    public static string FormatL1Progress(int CompletedKindCount, string? CurrentKindId, int CurrentStep, int StepsPerKind)
    {
        string KindPart = string.IsNullOrEmpty(CurrentKindId)
            ? string.Empty
            : " — " + CurrentKindId + " (step " + CurrentStep + " of " + StepsPerKind + ")";

        return "L1: " + CompletedKindCount + " of " + L1ProgrammeKindCount + KindPart;
    }
}
