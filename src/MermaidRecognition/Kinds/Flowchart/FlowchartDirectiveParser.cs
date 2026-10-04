using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Parses flowchart directives (<c>linkStyle</c>, <c>classDef</c>, <c>style</c>, <c>class</c>, <c>click</c>).
/// </summary>
public static class FlowchartDirectiveParser
{
    private static readonly Regex LinkStyleRegex = new(
        @"^\s*linkStyle\s+(default|[\d,\s]+)(?:\s+interpolate\s+(\S+))?(?:\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassDefRegex = new(
        @"^\s*classDef\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StyleRegex = new(
        @"^\s*style\s+(\S+)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassRegex = new(
        @"^\s*class\s+([^\s]+)\s+(\S+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Attempts to parse a non-link, non-subgraph directive line.
    /// </summary>
    public static bool TryParseDirective(string Line, int LineIndex, FlowchartDb Database)
    {
        if (TryParseLinkStyle(Line, Database))
        {
            return true;
        }

        if (TryParseClassDef(Line, Database))
        {
            return true;
        }

        if (TryParseStyle(Line, Database))
        {
            return true;
        }

        if (TryParseClass(Line, Database))
        {
            return true;
        }

        if (FlowchartClickParser.TryParse(Line, LineIndex, Database))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true when the line is a <c>linkStyle</c> directive.
    /// </summary>
    public static bool TryParseLinkStyle(string Line, FlowchartDb Database)
    {
        Match Match = LinkStyleRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        string TargetToken = Match.Groups[1].Value.Trim();
        string? Curve = Match.Groups[2].Success ? Match.Groups[2].Value : null;
        string StyleList = Match.Groups[3].Success ? Match.Groups[3].Value : string.Empty;
        if (Curve is null && string.IsNullOrWhiteSpace(StyleList))
        {
            return false;
        }

        RecognitionStyle Style = ParseStyleList(StyleList);

        if (string.Equals(TargetToken, "default", StringComparison.OrdinalIgnoreCase))
        {
            Database.LinkStylesMutable.Add(LinkStyleDirective.ForDefault(Style, Curve));
            return true;
        }

        List<int> EdgeIndexes = [];
        foreach (string Part in TargetToken.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(Part, out int EdgeIndex))
            {
                EdgeIndexes.Add(EdgeIndex);
            }
        }

        Database.LinkStylesMutable.Add(LinkStyleDirective.ForIndexes(EdgeIndexes, Style, Curve));
        return true;
    }

    static bool TryParseClassDef(string Line, FlowchartDb Database)
    {
        Match Match = ClassDefRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        Database.RegisterClassDef(Match.Groups[1].Value, ParseStyleList(Match.Groups[2].Value));
        return true;
    }

    static bool TryParseStyle(string Line, FlowchartDb Database)
    {
        Match Match = StyleRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        IEnumerable<string> Targets = SplitTargetIds(Match.Groups[1].Value);
        Database.ApplyInlineStyleToTargets(Targets, ParseStyleList(Match.Groups[2].Value));
        return true;
    }

    static bool TryParseClass(string Line, FlowchartDb Database)
    {
        Match Match = ClassRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        IEnumerable<string> Targets = SplitTargetIds(Match.Groups[1].Value);
        Database.ApplyClassToTargets(Targets, Match.Groups[2].Value);
        return true;
    }

    static IEnumerable<string> SplitTargetIds(string TargetList) => TargetList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static RecognitionStyle ParseStyleList(string StyleList)
    {
        RecognitionStyle Style = new RecognitionStyle();
        foreach (string Part in StyleList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int ColonIndex = Part.IndexOf(':', StringComparison.Ordinal);
            if (ColonIndex <= 0)
            {
                continue;
            }

            string Key = Part[..ColonIndex].Trim();
            string Value = Part[(ColonIndex + 1)..].Trim();
            if (string.Equals(Key, "fill", StringComparison.OrdinalIgnoreCase))
            {
                Style.Fill = Value;
            }
            else if (string.Equals(Key, "stroke", StringComparison.OrdinalIgnoreCase))
            {
                Style.Stroke = Value;
            }
            else if (string.Equals(Key, "stroke-width", StringComparison.OrdinalIgnoreCase))
            {
                string Numeric = Value.TrimEnd('p', 'x');
                if (double.TryParse(Numeric, out double Width))
                {
                    Style.StrokeWidth = Width;
                }
            }
            else if (string.Equals(Key, "stroke-dasharray", StringComparison.OrdinalIgnoreCase))
            {
                Style.StrokeDasharray = Value;
            }
            else if (string.Equals(Key, "color", StringComparison.OrdinalIgnoreCase))
            {
                Style.TextColor = Value;
            }
            else if (string.Equals(Key, "animation", StringComparison.OrdinalIgnoreCase))
            {
                Style.Animation = Value;
            }
        }

        return Style;
    }
}
