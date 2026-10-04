using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Extracts Mermaid pipe edge labels (<c>--&gt;|Yes|</c>, <c>---|text|</c>, <c>-.-&gt;|text|</c>) from link text.
/// </summary>
public static class FlowchartPipeEdgeLabelExtractor
{
    private const string PIPE_LABEL_CAPTURE =
        @"(?:""(?:[^""\\]|\\.)*"")|(?:'(?:[^'\\]|\\.)*')|(?:[^|]+?)";

    private const string LINK_TOKEN_PATTERN =
        @"(?:~{2,}|[xo<]?==>(?![=>])|[xo<]?={3,}(?![=>ox])|[xo<]?=+[=xo>]|[xo<]?-{2,}[-xo>]+|[xo<]?-\.[\w\s""'-]*\.?-+>|[xo<]?-?\.+\-[xo>]?)";

    private static readonly Regex LinkWithPipeLabelRegex = new(
        "(" + LINK_TOKEN_PATTERN + @")\s*\|\s*(" + PIPE_LABEL_CAPTURE + @")\s*\|",
        RegexOptions.Compiled);

    private static readonly Regex LinkSegmentPipeLabelSuffixRegex = new(
        @"^(?<link>.+?)\|\s*(?<label>" + PIPE_LABEL_CAPTURE + @")\s*\|$",
        RegexOptions.Compiled);

    /// <summary>
    /// Removes pipe labels from a link statement line and collects normalised label text.
    /// </summary>
    public static string StripFromLine(string Line, List<string> Labels) => LinkWithPipeLabelRegex.Replace(
            Line,
            Match =>
            {
                Labels.Add(FlowchartLabelNormalizer.Normalize(Match.Groups[2].Value));
                return Match.Groups[1].Value;
            });

    /// <summary>
    /// When chain splitting leaves a pipe suffix on a link segment, splits arrow text from label text.
    /// </summary>
    public static bool TryExtractFromLinkSegment(
        string LinkSegment,
        out string ArrowSegment,
        out string? PipeLabel)
    {
        string Trimmed = LinkSegment.Trim();
        Match Match = LinkSegmentPipeLabelSuffixRegex.Match(Trimmed);
        if (!Match.Success)
        {
            ArrowSegment = Trimmed;
            PipeLabel = null;
            return false;
        }

        ArrowSegment = Match.Groups["link"].Value.Trim();
        PipeLabel = FlowchartLabelNormalizer.Normalize(Match.Groups["label"].Value);
        return true;
    }
}
