using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Extracts inline edge labels from link text (Mermaid START_LINK / edgeText / LINK model).
/// </summary>
public static class FlowchartInlineLinkLabelExtractor
{
    private static readonly Regex LabeledNormalLinkRegex = new(
        @"\s*(?<start>[xo<]?--)\s+(?<label>(?:[^-]|-(?!-))+?)\s+(?<end>[xo<]?--+>)\s*",
        RegexOptions.Compiled);

    private static readonly Regex LabeledThickLinkRegex = new(
        @"\s*(?<start>[xo<]?==)\s+(?<label>(?:[^=]|=(?!=))+?)\s+(?<end>[xo<]?==+>)\s*",
        RegexOptions.Compiled);

    private static readonly Regex LabeledDottedLinkRegex = new(
        @"\s*(?<start>[xo<]?-\.)\s+(?<label>(?:[^.-]|\.(?!-)|-(?!\.))+?)\s+(?<end>[xo<]?-?\.+-+>)\s*",
        RegexOptions.Compiled);

    /// <summary>
    /// Replaces labelled links with compact link tokens and returns labels in source order.
    /// </summary>
    public static string Normalize(string Line, List<string> InlineLabels)
    {
        string Result = Line;
        Result = LabeledNormalLinkRegex.Replace(
            Result,
            Match => ReplaceLabeledMatch(Match, InlineLabels));
        Result = LabeledThickLinkRegex.Replace(
            Result,
            Match => ReplaceLabeledMatch(Match, InlineLabels));
        Result = LabeledDottedLinkRegex.Replace(
            Result,
            Match => ReplaceLabeledMatch(Match, InlineLabels));
        return Result;
    }

    static string ReplaceLabeledMatch(Match Match, List<string> InlineLabels)
    {
        InlineLabels.Add(FlowchartLabelNormalizer.Normalize(Match.Groups["label"].Value));
        return " " + Match.Groups["start"].Value + Match.Groups["end"].Value + " ";
    }
}
