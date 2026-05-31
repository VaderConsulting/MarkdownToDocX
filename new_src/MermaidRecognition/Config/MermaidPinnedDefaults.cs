namespace MermaidRecognition.Config;

/// <summary>
/// Pinned Mermaid 11.14.0 configuration defaults (spec.md section 3.17).
/// Values are generated from vendored config.schema.yaml into MermaidPinnedDefaults.generated.cs.
/// </summary>
public static partial class MermaidPinnedDefaults
{
    private static readonly Dictionary<string, string> ProgrammeKindToInitKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "flowchart", "flowchart" },
            { "sequence", "sequence" },
            { "classDiagram", "class" },
            { "stateDiagram", "state" },
            { "erDiagram", "er" },
            { "gantt", "gantt" },
            { "pie", "pie" },
            { "gitGraph", "gitGraph" },
            { "mindmap", "mindmap" },
            { "timeline", "timeline" },
            { "journey", "journey" },
            { "quadrantChart", "quadrantChart" },
            { "xychart", "xyChart" },
            { "requirement", "requirement" },
            { "architecture", "architecture" },
            { "kanban", "kanban" },
            { "sankey", "sankey" },
            { "block", "block" },
            { "packet", "packet" },
            { "radar", "radar" },
            { "treemap", "treemap" },
            { "c4", "c4" },
            { "treeview", "treeView" },
            { "ishikawa", "ishikawa" },
            { "venn", "venn" },
            { "wardley-beta", "wardley-beta" },
            { "eventmodelling", "eventmodeling" },
            { "yaml-front-matter", "flowchart" },
        };

    /// <summary>
    /// Applies global, theme, and diagram-kind defaults from spec section 3.17.
    /// Existing effective values and later directive overrides are preserved.
    /// </summary>
    public static void ApplyTo(DiagramConfigContract Configuration, string? ProgrammeKindId)
    {
        ApplyPairs(Configuration, GlobalRootDefaults);

        ApplyPairs(Configuration, ThemeVariableDefaults);

        string? InitKey = ResolveInitKey(ProgrammeKindId);
        if (InitKey != null
            && KindDefaultsByInitKey.TryGetValue(InitKey, out (string Key, string Value)[] KindDefaults))
        {
            ApplyPairs(Configuration, KindDefaults);
        }
    }

    /// <summary>
    /// Resolves a programme kind id to the Mermaid init config key (for example <c>classDiagram</c> to <c>class</c>).
    /// </summary>
    public static string? ResolveInitKey(string? ProgrammeKindId)
    {
        if (string.IsNullOrWhiteSpace(ProgrammeKindId))
        {
            return null;
        }

        if (ProgrammeKindToInitKey.TryGetValue(ProgrammeKindId, out string MappedInitKey))
        {
            return MappedInitKey;
        }

        if (KindDefaultsByInitKey.ContainsKey(ProgrammeKindId))
        {
            return ProgrammeKindId;
        }

        return null;
    }

    static void ApplyPairs(DiagramConfigContract Configuration, (string Key, string Value)[] Pairs)
    {
        foreach ((string Key, string Value) Entry in Pairs)
        {
            Configuration.SetDefaultIfMissing(Entry.Key, Entry.Value);
        }
    }
}
