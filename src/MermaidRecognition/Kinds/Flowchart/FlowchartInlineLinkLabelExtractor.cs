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

    /// <summary>
    /// Keeps only the start marker (<c>x</c>, <c>o</c> or <c>&lt;</c>) and the closing link, because Mermaid's
    /// <c>destructLink</c> takes the edge length from the closing link alone (<c>-- text --&gt;</c> has length 1).
    /// </summary>
    static string ReplaceLabeledMatch(Match Match, List<string> InlineLabels)
    {
        InlineLabels.Add(FlowchartLabelNormalizer.Normalize(Match.Groups["label"].Value));
        string Start = Match.Groups["start"].Value;
        string StartMarker = Start.Length > 0 && (Start[0] == 'x' || Start[0] == 'o' || Start[0] == '<') ? Start[..1] : string.Empty;
        string End = Match.Groups["end"].Value;
        if (StartMarker.Length > 0 && End.Length > 0 && (End[0] == 'x' || End[0] == 'o' || End[0] == '<'))
        {
            End = End[1..];
        }

        return $" {StartMarker}{End} ";
    }
}
