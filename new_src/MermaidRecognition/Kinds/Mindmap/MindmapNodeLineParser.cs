using System.Text.RegularExpressions;

namespace MermaidRecognition.Mindmap;

/// <summary>
/// Parses a trimmed mindmap node line into display label and shape (MMP-7–MMP-12).
/// </summary>
public static class MindmapNodeLineParser
{
    private static readonly Regex IdCirclePattern = new(
        @"^(\S+)\(\((.+)\)\)$",
        RegexOptions.Compiled);

    private static readonly Regex IdBangPattern = new(
        @"^(\S+)\)\)(.+)\(\($",
        RegexOptions.Compiled);

    private static readonly Regex IdHexagonPattern = new(
        @"^(\S+)\{\{(.+)\}\}$",
        RegexOptions.Compiled);

    private static readonly Regex IdCloudPattern = new(
        @"^(\S+)\)(.+)\($",
        RegexOptions.Compiled);

    private static readonly Regex IdRectPattern = new(
        @"^(\S+)\[(.+)\]$",
        RegexOptions.Compiled);

    private static readonly Regex IdRoundedPattern = new(
        @"^(\S+)\((.+)\)$",
        RegexOptions.Compiled);

    private static readonly Regex CirclePattern = new(
        @"^\(\((.+)\)\)$",
        RegexOptions.Compiled);

    private static readonly Regex BangPattern = new(
        @"^\)\)(.+)\(\($",
        RegexOptions.Compiled);

    private static readonly Regex HexagonPattern = new(
        @"^\{\{(.+)\}\}$",
        RegexOptions.Compiled);

    private static readonly Regex CloudPattern = new(
        @"^\)(.+)\($",
        RegexOptions.Compiled);

    private static readonly Regex RectPattern = new(
        @"^\[(.+)\]$",
        RegexOptions.Compiled);

    private static readonly Regex RoundedPattern = new(
        @"^\((.+)\)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses label, optional node id, and shape token from a mindmap node line body.
    /// </summary>
    /// <param name="TrimmedLine">Node line without leading indentation.</param>
    /// <returns>Parsed node line facts.</returns>
    public static MindmapParsedNodeLine Parse(string TrimmedLine)
    {
        Match IdCircleMatch = IdCirclePattern.Match(TrimmedLine);
        if (IdCircleMatch.Success)
        {
            return BuildParsedNodeLine(IdCircleMatch.Groups[2].Value, "circle", IdCircleMatch.Groups[1].Value);
        }

        Match IdBangMatch = IdBangPattern.Match(TrimmedLine);
        if (IdBangMatch.Success)
        {
            return BuildParsedNodeLine(IdBangMatch.Groups[2].Value, "bang", IdBangMatch.Groups[1].Value);
        }

        Match IdHexagonMatch = IdHexagonPattern.Match(TrimmedLine);
        if (IdHexagonMatch.Success)
        {
            return BuildParsedNodeLine(IdHexagonMatch.Groups[2].Value, "hexagon", IdHexagonMatch.Groups[1].Value);
        }

        Match IdCloudMatch = IdCloudPattern.Match(TrimmedLine);
        if (IdCloudMatch.Success)
        {
            return BuildParsedNodeLine(IdCloudMatch.Groups[2].Value, "cloud", IdCloudMatch.Groups[1].Value);
        }

        Match IdRectMatch = IdRectPattern.Match(TrimmedLine);
        if (IdRectMatch.Success)
        {
            return BuildParsedNodeLine(IdRectMatch.Groups[2].Value, "rect", IdRectMatch.Groups[1].Value);
        }

        Match IdRoundedMatch = IdRoundedPattern.Match(TrimmedLine);
        if (IdRoundedMatch.Success)
        {
            return BuildParsedNodeLine(IdRoundedMatch.Groups[2].Value, "rounded-rect", IdRoundedMatch.Groups[1].Value);
        }

        Match CircleMatch = CirclePattern.Match(TrimmedLine);
        if (CircleMatch.Success)
        {
            return BuildParsedNodeLine(CircleMatch.Groups[1].Value, "circle", null);
        }

        Match BangMatch = BangPattern.Match(TrimmedLine);
        if (BangMatch.Success)
        {
            return BuildParsedNodeLine(BangMatch.Groups[1].Value, "bang", null);
        }

        Match HexagonMatch = HexagonPattern.Match(TrimmedLine);
        if (HexagonMatch.Success)
        {
            return BuildParsedNodeLine(HexagonMatch.Groups[1].Value, "hexagon", null);
        }

        Match CloudMatch = CloudPattern.Match(TrimmedLine);
        if (CloudMatch.Success)
        {
            return BuildParsedNodeLine(CloudMatch.Groups[1].Value, "cloud", null);
        }

        Match RectMatch = RectPattern.Match(TrimmedLine);
        if (RectMatch.Success)
        {
            return BuildParsedNodeLine(RectMatch.Groups[1].Value, "rect", null);
        }

        Match RoundedMatch = RoundedPattern.Match(TrimmedLine);
        if (RoundedMatch.Success)
        {
            return BuildParsedNodeLine(RoundedMatch.Groups[1].Value, "rounded-rect", null);
        }

        return BuildParsedNodeLine(TrimmedLine, "mindmap", null);
    }

    static MindmapParsedNodeLine BuildParsedNodeLine(string Label, string Shape, string? NodeId)
    {
        MindmapParsedNodeLine Parsed = new MindmapParsedNodeLine();
        Parsed.Label = Label;
        Parsed.Shape = Shape;
        Parsed.NodeId = NodeId;
        return Parsed;
    }
}

/// <summary>
/// Parsed facts from one mindmap node source line.
/// </summary>
public sealed class MindmapParsedNodeLine
{
    private string _Label = string.Empty;

    public string Label
    {
        get
        {
            return _Label;
        }
        set
        {
            _Label = value;
        }
    }

    private string _Shape = "mindmap";

    public string Shape
    {
        get
        {
            return _Shape;
        }
        set
        {
            _Shape = value;
        }
    }

    private string? _NodeId;

    public string? NodeId
    {
        get
        {
            return _NodeId;
        }
        set
        {
            _NodeId = value;
        }
    }
}
