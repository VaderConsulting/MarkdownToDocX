using System.Text.RegularExpressions;

namespace MermaidRecognition.Flowchart;

/// <summary>
/// Parses flowchart link statements (node sets, chained links, grouped <c>&amp;</c>).
/// </summary>
public static class FlowchartLinkStatementParser
{
    private static readonly Regex ChainSplitRegex = new(
        @"(\s*(?:~{2,}|[xo<]?==>(?![=>])|[xo<]?={3,}(?![=>ox])|[xo<]?=+[=xo>]|[xo<]?-{2,}[-xo>]+|[xo<]?-\.[\w\s""'-]*\.?-+>|[xo<]?-?\.+\-[xo>]?|(?:[xo<]?--)\s+[^-].+?\s+(?:--+>))\s*)",
        RegexOptions.Compiled);

    private static readonly Regex EdgeIdBeforeLinkRegex = new(
        @"([A-Za-z][\w-]*)@\s*(?=(?:~|[xo<]?[-=]))",
        RegexOptions.Compiled);

    private static readonly Regex NodeIdRegex = new(
        @"([A-Za-z][\w-]*)",
        RegexOptions.Compiled);

    private static readonly Regex LinkPresenceRegex = new(
        @"~{2,}|[xo<]?==>(?![=>])|[xo<]?={3,}(?![=>ox])|[xo<]?=+[=xo>]|[xo<]?-{2,}[-xo>]|[xo<]?-\.[\w\s""'-]*\.?-+>|[xo<]?-?\.+\-[xo>]?|(?:[xo<]?--)\s+[^-].+?\s+(?:--+>)",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true when the line contains a Mermaid flowchart link token.
    /// </summary>
    public static bool IsLinkStatement(string Line)
    {
        return LinkPresenceRegex.IsMatch(Line);
    }

    /// <summary>
    /// Parses a link statement into node-set chains and adds expanded edges to <paramref name="Database"/>.
    /// </summary>
    public static void ParseLinkStatement(string Line, int LineIndex, FlowchartDb Database)
    {
        FlowchartLinkToken LineLevelId = new FlowchartLinkToken();
        TryApplyLineLevelEdgeId(Line, LineLevelId);
        string NormalizedLine = EdgeIdBeforeLinkRegex.Replace(Line.Trim(), string.Empty);
        List<string> EdgeLabels = new List<string>();
        NormalizedLine = FlowchartPipeEdgeLabelExtractor.StripFromLine(NormalizedLine, EdgeLabels);
        List<string> InlineLabels = new List<string>();
        NormalizedLine = FlowchartInlineLinkLabelExtractor.Normalize(NormalizedLine, InlineLabels);
        string[] Parts = ChainSplitRegex.Split(NormalizedLine);
        List<FlowchartNodeSet> NodeSets = new List<FlowchartNodeSet>();
        List<FlowchartLinkToken> LinkTokens = new List<FlowchartLinkToken>();
        List<string> LinkSegments = new List<string>();
        int LabelIndex = 0;
        int InlineLabelIndex = 0;

        for (int Index = 0; Index < Parts.Length; Index++)
        {
            string Part = Parts[Index].Trim();
            if (Part.Length == 0)
            {
                continue;
            }

            if (LinkPresenceRegex.IsMatch(Part))
            {
                string ArrowSegment = Part;
                string? SegmentPipeLabel = null;
                if (FlowchartPipeEdgeLabelExtractor.TryExtractFromLinkSegment(Part, out string ParsedArrowSegment, out string? ParsedPipeLabel))
                {
                    ArrowSegment = ParsedArrowSegment;
                    SegmentPipeLabel = ParsedPipeLabel;
                }

                FlowchartLinkToken Token = ParseArrowToken(ArrowSegment);
                if (LabelIndex < EdgeLabels.Count)
                {
                    Token.Label = FlowchartLabelNormalizer.Normalize(EdgeLabels[LabelIndex]);
                    LabelIndex++;
                }
                else if (SegmentPipeLabel is not null)
                {
                    Token.Label = SegmentPipeLabel;
                }
                else if (InlineLabelIndex < InlineLabels.Count)
                {
                    Token.Label = FlowchartLabelNormalizer.Normalize(InlineLabels[InlineLabelIndex]);
                    InlineLabelIndex++;
                }

                LinkTokens.Add(Token);
                LinkSegments.Add(ArrowSegment);
            }
            else
            {
                NodeSets.Add(ParseNodeSet(Part, LineIndex, Database));
            }
        }

        if (NodeSets.Count < 2 || LinkTokens.Count != NodeSets.Count - 1)
        {
            return;
        }

        if (LineLevelId.UserDefinedEdgeId is not null && LinkTokens.Count > 0)
        {
            LinkTokens[0].UserDefinedEdgeId = LineLevelId.UserDefinedEdgeId;
        }

        for (int LinkIndex = 0; LinkIndex < LinkTokens.Count; LinkIndex++)
        {
            Database.AddLink(
                NodeSets[LinkIndex],
                NodeSets[LinkIndex + 1],
                LinkTokens[LinkIndex],
                LineIndex,
                LinkIndex,
                LinkSegments[LinkIndex]);
        }
    }

    /// <summary>
    /// Parses statements like <c>A e1@--&gt; B</c> where the edge id sits before the first link on the line.
    /// </summary>
    public static void TryApplyLineLevelEdgeId(string Line, FlowchartLinkToken FirstToken)
    {
        Match Match = EdgeIdBeforeLinkRegex.Match(Line);
        if (Match.Success)
        {
            FirstToken.UserDefinedEdgeId = Match.Groups[1].Value;
        }
    }

    static FlowchartNodeSet ParseNodeSet(string Text, int LineIndex, FlowchartDb Database)
    {
        FlowchartNodeSet Set = new FlowchartNodeSet();
        string Cleaned = EdgeIdBeforeLinkRegex.Replace(Text, string.Empty);
        List<string> Tokens = FlowchartNodeDeclarationParser.SplitNodeSetTokens(Cleaned);
        if (Tokens.Count == 0)
        {
            foreach (Match Match in NodeIdRegex.Matches(Cleaned))
            {
                Set.ItemsMutable.Add(Match.Groups[1].Value);
            }

            return Set;
        }

        foreach (string Token in Tokens)
        {
            FlowchartNodeDeclarationParser.ParseAndMerge(Token, LineIndex, Database);
            Match Match = NodeIdRegex.Match(Token.Trim());
            if (Match.Success)
            {
                Set.ItemsMutable.Add(Match.Groups[1].Value);
            }
        }

        return Set;
    }

    static FlowchartLinkToken ParseArrowToken(string ArrowSegment)
    {
        FlowchartLinkToken Token = new FlowchartLinkToken();
        FlowchartLinkArrowParser.ApplyToToken(ArrowSegment, Token);
        return Token;
    }
}
