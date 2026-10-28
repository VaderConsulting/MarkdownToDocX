using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Charts;

/// <summary>
/// Shared helpers for chart and note kind parsers that mainly collect named lines.
/// </summary>
internal static class ChartLineHelpers
{
    private static readonly Regex QuotedLabelRegex = new Regex(
        @"^""(?<label>[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex IdTokenRegex = new Regex(
        @"^(?<id>[A-Za-z_][\w-]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Maps a non-directive line into a node using a quoted label or leading identifier.
    /// </summary>
    public static bool TryMapNamedLine(string Trimmed, int LineNumber, string TypeToken, out DiagramItem? Item)
    {
        Item = null;
        Match Quoted = QuotedLabelRegex.Match(Trimmed);
        if (Quoted.Success)
        {
            string Label = Quoted.Groups["label"].Value;
            Item = new DiagramItem(LineNumber, DiagramItemKind.Node, Label, Label, TypeToken, string.Empty, string.Empty);
            return true;
        }

        Match Id = IdTokenRegex.Match(Trimmed);
        if (Id.Success)
        {
            string Name = Id.Groups["id"].Value;
            Item = new DiagramItem(LineNumber, DiagramItemKind.Node, Name, Name, TypeToken, string.Empty, string.Empty);
            return true;
        }

        return false;
    }

    /// <summary>
    /// True for common chart directive lines that are not selectable ids.
    /// </summary>
    public static bool IsChartDirective(string Trimmed)
    {
        return Trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("dateFormat", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("axisFormat", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("excludes", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("todayMarker", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("section", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("showData", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("x-axis", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("y-axis", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("bar", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("line", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("commit", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("branch", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("checkout", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("merge", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("cherry-pick", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("axis", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("curve", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("graticule", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("ticks", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("max", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("min", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Gantt chart LiveEditor parser.</summary>
public sealed class GanttParser : HeaderKindParser
{
    private static readonly string[] Headers = ["gantt"];

    private static readonly Regex TaskRegex = new Regex(
        @"^(?<label>.+?)\s*:\s*(?<id>[A-Za-z_][\w-]*)?\s*,?",
        RegexOptions.Compiled);

    /// <summary>Shared instance.</summary>
    public static GanttParser Default { get; } = new GanttParser();

    static GanttParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (ChartLineHelpers.IsChartDirective(Trimmed))
        {
            if (Trimmed.StartsWith("section ", StringComparison.OrdinalIgnoreCase))
            {
                string Name = Trimmed.Substring(8).Trim();
                Item = Container(LineNumber, Name, Name, "section");
                return true;
            }

            return false;
        }

        Match Task = TaskRegex.Match(Trimmed);
        if (Task.Success)
        {
            string Label = Task.Groups["label"].Value.Trim();
            string Id = Task.Groups["id"].Success && Task.Groups["id"].Value.Length > 0
                ? Task.Groups["id"].Value
                : Label;
            Item = Node(LineNumber, Id, Label, "task");
            return true;
        }

        return false;
    }
}

/// <summary>Pie chart LiveEditor parser.</summary>
public sealed class PieParser : HeaderKindParser
{
    private static readonly string[] Headers = ["pie"];

    private static readonly Regex SliceRegex = new Regex(
        @"^""(?<label>[^""]+)""\s*:\s*(?<value>.+)$",
        RegexOptions.Compiled);

    /// <summary>Shared instance.</summary>
    public static PieParser Default { get; } = new PieParser();

    static PieParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (ChartLineHelpers.IsChartDirective(Trimmed))
        {
            return false;
        }

        Match Slice = SliceRegex.Match(Trimmed);
        if (Slice.Success)
        {
            string Label = Slice.Groups["label"].Value;
            Item = Node(LineNumber, Label, Label, "slice");
            return true;
        }

        return ChartLineHelpers.TryMapNamedLine(Trimmed, LineNumber, "slice", out Item);
    }
}

/// <summary>Git graph LiveEditor parser.</summary>
public sealed class GitGraphParser : HeaderKindParser
{
    private static readonly string[] Headers = ["gitGraph"];

    private static readonly Regex BranchRegex = new Regex(
        @"^(?:branch|checkout|merge)\s+(?<id>\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CommitIdRegex = new Regex(
        @"^commit\b.*\bid:\s*[""']?(?<id>[A-Za-z_][\w-]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Shared instance.</summary>
    public static GitGraphParser Default { get; } = new GitGraphParser();

    static GitGraphParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        Match Commit = CommitIdRegex.Match(Trimmed);
        if (Commit.Success)
        {
            string Id = Commit.Groups["id"].Value;
            Item = Node(LineNumber, Id, Id, "commit");
            return true;
        }

        Match Branch = BranchRegex.Match(Trimmed);
        if (Branch.Success)
        {
            string Id = Branch.Groups["id"].Value;
            string Type = Trimmed.Split(' ')[0].ToLowerInvariant();
            Item = Node(LineNumber, Id, Id, Type);
            return true;
        }

        return false;
    }
}

/// <summary>Mindmap LiveEditor parser.</summary>
public sealed class MindmapParser : HeaderKindParser
{
    private static readonly string[] Headers = ["mindmap"];

    /// <summary>Shared instance.</summary>
    public static MindmapParser Default { get; } = new MindmapParser();

    static MindmapParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (Trimmed.Length == 0)
        {
            return false;
        }

        string Body = Trimmed;
        if (Body.StartsWith("root", StringComparison.OrdinalIgnoreCase))
        {
            Body = Body.Substring(4).TrimStart();
        }

        string Label = StripShape(Body);
        if (Label.Length == 0)
        {
            return false;
        }

        Item = Node(LineNumber, Label, Label, "topic");
        return true;
    }

    private static string StripShape(string Body)
    {
        string Inner = Body.Trim();
        if (Inner.Length >= 2)
        {
            char First = Inner[0];
            char Last = Inner[Inner.Length - 1];
            if ((First == '(' && Last == ')') || (First == '[' && Last == ']'))
            {
                Inner = Inner.Substring(1, Inner.Length - 2).Trim();
                return StripShape(Inner);
            }
        }

        if (Inner.Length >= 2 && Inner[0] == '"' && Inner[Inner.Length - 1] == '"')
        {
            return Inner.Substring(1, Inner.Length - 2);
        }

        return Inner;
    }
}

/// <summary>Timeline LiveEditor parser.</summary>
public sealed class TimelineParser : HeaderKindParser
{
    private static readonly string[] Headers = ["timeline"];

    /// <summary>Shared instance.</summary>
    public static TimelineParser Default { get; } = new TimelineParser();

    static TimelineParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (Trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Trimmed.StartsWith("section ", StringComparison.OrdinalIgnoreCase))
        {
            string Name = Trimmed.Substring(8).Trim();
            Item = Container(LineNumber, Name, Name, "section");
            return true;
        }

        return ChartLineHelpers.TryMapNamedLine(Trimmed, LineNumber, "event", out Item);
    }
}

/// <summary>Journey LiveEditor parser.</summary>
public sealed class JourneyParser : HeaderKindParser
{
    private static readonly string[] Headers = ["journey"];

    /// <summary>Shared instance.</summary>
    public static JourneyParser Default { get; } = new JourneyParser();

    static JourneyParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (Trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Trimmed.StartsWith("section ", StringComparison.OrdinalIgnoreCase))
        {
            string Name = Trimmed.Substring(8).Trim();
            Item = Container(LineNumber, Name, Name, "section");
            return true;
        }

        int Colon = Trimmed.IndexOf(':');
        if (Colon > 0)
        {
            string Label = Trimmed.Substring(0, Colon).Trim();
            Item = Node(LineNumber, Label, Label, "task");
            return true;
        }

        return ChartLineHelpers.TryMapNamedLine(Trimmed, LineNumber, "task", out Item);
    }
}

/// <summary>Quadrant chart LiveEditor parser.</summary>
public sealed class QuadrantChartParser : HeaderKindParser
{
    private static readonly string[] Headers = ["quadrantChart"];

    private static readonly Regex PointRegex = new Regex(
        @"^(?<label>.+?)\s*:\s*\[",
        RegexOptions.Compiled);

    /// <summary>Shared instance.</summary>
    public static QuadrantChartParser Default { get; } = new QuadrantChartParser();

    static QuadrantChartParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (ChartLineHelpers.IsChartDirective(Trimmed)
            || Trimmed.StartsWith("quadrant-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Match Point = PointRegex.Match(Trimmed);
        if (Point.Success)
        {
            string Label = Point.Groups["label"].Value.Trim().Trim('"');
            Item = Node(LineNumber, Label, Label, "point");
            return true;
        }

        return ChartLineHelpers.TryMapNamedLine(Trimmed, LineNumber, "point", out Item);
    }
}

/// <summary>XY chart LiveEditor parser.</summary>
public sealed class XyChartParser : HeaderKindParser
{
    private static readonly string[] Headers = ["xychart-beta", "xychart"];

    /// <summary>Shared instance.</summary>
    public static XyChartParser Default { get; } = new XyChartParser();

    static XyChartParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (Trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("x-axis", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("y-axis", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Trimmed.StartsWith("bar", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("line", StringComparison.OrdinalIgnoreCase))
        {
            string Type = Trimmed.StartsWith("bar", StringComparison.OrdinalIgnoreCase) ? "bar" : "line";
            Item = Node(LineNumber, Type, Type, "series");
            return true;
        }

        return ChartLineHelpers.TryMapNamedLine(Trimmed, LineNumber, "series", out Item);
    }
}

/// <summary>Packet LiveEditor parser.</summary>
public sealed class PacketParser : HeaderKindParser
{
    private static readonly string[] Headers = ["packet-beta", "packet"];

    private static readonly Regex FieldRegex = new Regex(
        @"^(?<bits>\d+(?:-\d+)?)\s*:\s*""(?<label>[^""]+)""",
        RegexOptions.Compiled);

    /// <summary>Shared instance.</summary>
    public static PacketParser Default { get; } = new PacketParser();

    static PacketParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        Match Field = FieldRegex.Match(Trimmed);
        if (Field.Success)
        {
            string Label = Field.Groups["label"].Value;
            Item = Node(LineNumber, Label, Label, "field");
            return true;
        }

        return false;
    }
}

/// <summary>Radar LiveEditor parser.</summary>
public sealed class RadarParser : HeaderKindParser
{
    private static readonly string[] Headers = ["radar-beta", "radar"];

    private static readonly Regex AxisRegex = new Regex(
        @"^axis\s+(?<label>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Shared instance.</summary>
    public static RadarParser Default { get; } = new RadarParser();

    static RadarParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        Match Axis = AxisRegex.Match(Trimmed);
        if (Axis.Success)
        {
            string Label = Axis.Groups["label"].Value.Trim().Trim('"');
            Item = Node(LineNumber, Label, Label, "axis");
            return true;
        }

        if (ChartLineHelpers.IsChartDirective(Trimmed))
        {
            return false;
        }

        return ChartLineHelpers.TryMapNamedLine(Trimmed, LineNumber, "series", out Item);
    }
}

/// <summary>Treemap LiveEditor parser.</summary>
public sealed class TreemapParser : HeaderKindParser
{
    private static readonly string[] Headers = ["treemap-beta", "treemap"];

    private static readonly Regex LeafRegex = new Regex(
        @"^(?<label>.+?)\s*:\s*(?<value>[\d.]+)\s*$",
        RegexOptions.Compiled);

    /// <summary>Shared instance.</summary>
    public static TreemapParser Default { get; } = new TreemapParser();

    static TreemapParser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        if (Trimmed.StartsWith("title", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Match Leaf = LeafRegex.Match(Trimmed);
        if (Leaf.Success)
        {
            string Label = Leaf.Groups["label"].Value.Trim().Trim('"');
            Item = Node(LineNumber, Label, Label, "leaf");
            return true;
        }

        return ChartLineHelpers.TryMapNamedLine(Trimmed, LineNumber, "node", out Item);
    }
}
