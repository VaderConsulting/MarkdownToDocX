using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.LinePattern;

/// <summary>
/// Extra line patterns for line-pattern diagram kinds (§11.1.13 L1+).
/// </summary>
public static class KindRecognizerPatterns
{
    public static KindLinePattern[] ForTimeline() => new[]
        {
            KindLinePatternProfiles.TitlePattern("timeline"),
            Line("timeline.event", @"^\s*[^\s].+\s*:\s*.+")
        };

    public static KindLinePattern[] ForJourney() => new[]
        {
            KindLinePatternProfiles.TitlePattern("journey"),
            Line("journey.section", @"^\s*section\s+", RegexOptions.IgnoreCase),
            Line("journey.task", @"^\s+.+\d+\s*:\s*.+")
        };

    public static KindLinePattern[] ForQuadrantChart() => new[]
        {
            KindLinePatternProfiles.TitlePattern("quadrantChart"),
            Line("quadrantChart.axis", @"^\s*[xy]-axis\b", RegexOptions.IgnoreCase),
            Line("quadrantChart.point", @"^.+:\s*\[")
        };

    public static KindLinePattern[] ForXychart() => new[]
        {
            KindLinePatternProfiles.TitlePattern("xychart"),
            Line("xychart.axis", @"^\s*[xy]-axis\b", RegexOptions.IgnoreCase),
            Line("xychart.series", @"^\s*(bar|line)\s", RegexOptions.IgnoreCase)
        };

    public static KindLinePattern[] ForRequirement() => new[]
        {
            Line("requirement.blockOpen", @"^\s*(requirement|element|interface)\s+\w+\s*\{", RegexOptions.IgnoreCase),
            Line("requirement.blockClose", @"^\s*\}\s*$"),
            Line("requirement.attribute", @"^\s*\w+\s*:"),
            Line("requirement.relationship", @"^\s*\w+\s*-\s*", RegexOptions.IgnoreCase)
        };

    public static KindLinePattern[] ForArchitecture() => new[]
        {
            Line("architecture.service", @"^\s*service\s", RegexOptions.IgnoreCase),
            Line("architecture.group", @"^\s*group\s", RegexOptions.IgnoreCase)
        };

    public static KindLinePattern[] ForKanban() => new[]
        {
            KindLinePatternProfiles.TitlePattern("kanban"),
            Line("kanban.column", @"^[A-Za-z]\w*\["),
            Line("kanban.task", @"^\s*task\w*\[", RegexOptions.IgnoreCase)
        };

    public static KindLinePattern[] ForSankey() => new[]
        {
            Line("sankey.link", @"^[^,\s]+,[^,\s]+,\d+")
        };

    public static KindLinePattern[] ForBlock() => new[]
        {
            Line("block.columns", @"^\s*columns\s+", RegexOptions.IgnoreCase),
            Line("block.blocks", @"\[")
        };

    public static KindLinePattern[] ForPacket() => new[]
        {
            Line("packet.field", @"^\d+-\d+:")
        };

    public static KindLinePattern[] ForRadar() => new[]
        {
            KindLinePatternProfiles.TitlePattern("radar"),
            Line("radar.axis", @"^\s*axis\s", RegexOptions.IgnoreCase),
            Line("radar.curve", @"^\s*curve\s", RegexOptions.IgnoreCase)
        };

    public static KindLinePattern[] ForTreemap() => new[]
        {
            KindLinePatternProfiles.TitlePattern("treemap"),
            Line("treemap.section", @"^\s*"""),
            Line("treemap.item", @""".+""\s*:\s*\d+")
        };

    public static KindLinePattern[] ForC4() => new[]
        {
            KindLinePatternProfiles.TitlePattern("c4"),
            Line("c4.element", @"^\s*(Person|System|Container|Component|Rel|Boundary|Enterprise_Boundary|System_Boundary|Container_Boundary|Person_Ext|System_Ext|SystemDb|ContainerDb|ComponentDb|UpdateRelStyle|UpdateElementStyle|UpdateLayoutConfig)\b")
        };

    public static KindLinePattern[] ForEventmodeling() => new[]
        {
            KindLinePatternProfiles.TitlePattern("eventmodeling"),
            Line("eventmodeling.frame", @"^\s*(cmd|command|evt|event|pcr|processor|rmo|readmodel)\b", RegexOptions.IgnoreCase)
        };

    public static KindLinePattern[] ForTreeview() => new[]
        {
            Line("treeview.node", @"^\s{2,}\S")
        };

    public static KindLinePattern[] ForIshikawa() => new[]
        {
            KindLinePatternProfiles.TitlePattern("ishikawa"),
            Line("ishikawa.branch", @"^\s+\S")
        };

    public static KindLinePattern[] ForVenn() => new[]
        {
            KindLinePatternProfiles.TitlePattern("venn"),
            Line("venn.set", @"^\s*\w+\s*\[")
        };

    public static KindLinePattern[] ForWardley() => new[]
        {
            KindLinePatternProfiles.TitlePattern("wardley"),
            Line("wardley.element", @"^\s*\w+\[")
        };

    public static KindLinePattern[] ForInfo() => new[]
        {
            Line("info.show", @"^\s*show\b", RegexOptions.IgnoreCase)
        };

    static KindLinePattern Line(string LineTypeId, string Pattern, RegexOptions Options = RegexOptions.Compiled)
    {
        KindLinePattern Entry = new KindLinePattern
        {
            LineTypeId = LineTypeId,
            Pattern = new Regex(Pattern, Options)
        };
        return Entry;
    }
}
