using System.Text;
using System.Text.RegularExpressions;

using MermaidRecognition.Config;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Public entry point for flowchart recognition (parse source text to <see cref="DiagramContract"/>).
/// </summary>
public static class FlowchartRecognizer
{
    private static readonly Regex HeaderWithDirectionRegex = new(
        @"^\s*(?:graph|flowchart)(?:-elk)?\s+(TD|TB|BT|RL|LR|[<>^v])\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HeaderOnlyRegex = new(
        @"^\s*(?:graph|flowchart)(?:-elk)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ElkHeaderRegex = new(
        @"^\s*flowchart-elk\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubgraphStartRegex = new(
        @"^\s*subgraph\s+(\w+)(?:\s*\[(.+)\])?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubgraphDirectionRegex = new(
        @"^\s*direction\s+(TD|TB|BT|RL|LR)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccTitleLineRegex = new(
        @"^\s*accTitle\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrLineRegex = new(
        @"^\s*accDescr\s*:\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrBlockStartRegex = new(
        @"^\s*accDescr\s*\{\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses Mermaid flowchart source into a <see cref="DiagramContract"/>.
    /// </summary>
    public static DiagramContract Parse(string Source)
    {
        DiagramConfigContract Configuration = new DiagramConfigContract();
        string AfterFrontMatter = MermaidConfigurationPipeline.ParseFlowchartLeadingDirectives(Source, Configuration);

        string Body = StripLeadingInitBlocks(AfterFrontMatter);
        FlowchartDb Database = new FlowchartDb();
        ParseBody(Body, Database);

        MermaidConfigurationPipeline.BuildEffectiveConfiguration(Configuration, "flowchart");

        var Contract = FlowchartContractMapper.ToContract(Database, Configuration);
        Contract.LayoutMode = DiagramLayoutSettings.Resolve(Configuration.RawDirectiveValues,
            Message => Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(MermaidDiagnosticSeverity.Warning,
                "configuration.layout.unknown", Message, null, null)));
        return Contract;
    }

    static string StripLeadingInitBlocks(string Source)
    {
        string Remaining = Source.TrimStart();
        while (MermaidJsonConfigFlattener.TryExtractNextInitBlock(Remaining, out _, out int Consumed))
        {
            Remaining = Remaining[Consumed..].TrimStart();
        }

        return Remaining;
    }

    static void ParseBody(string Body, FlowchartDb Database)
    {
        string[] Lines = Body.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool HeaderParsed = false;
        AccDescrParseState AccState = AccDescrParseState.None;
        StringBuilder AccDescrBuilder = new StringBuilder();

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line))
            {
                if (AccState == AccDescrParseState.Multiline)
                {
                    AccDescrBuilder.AppendLine();
                }

                continue;
            }

            if (MermaidRemarkAnalyzer.IsFullLineRemark(Line))
            {
                continue;
            }

            Line = MermaidRemarkAnalyzer.StripTrailingRemark(Line).Trim();
            if (Line.Length == 0)
            {
                continue;
            }

            if (AccState == AccDescrParseState.Multiline)
            {
                if (string.Equals(Line, "}", StringComparison.Ordinal))
                {
                    Database.AccessibilityDescription = AccDescrBuilder.ToString().Trim();
                    AccState = AccDescrParseState.None;
                    AccDescrBuilder.Clear();
                }
                else
                {
                    if (AccDescrBuilder.Length > 0)
                    {
                        AccDescrBuilder.AppendLine();
                    }

                    AccDescrBuilder.Append(Line);
                }

                continue;
            }

            Match AccTitleMatch = AccTitleLineRegex.Match(Line);
            if (AccTitleMatch.Success)
            {
                Database.AccessibilityTitle = AccTitleMatch.Groups[1].Value.Trim();
                continue;
            }

            Match AccDescrMatch = AccDescrLineRegex.Match(Line);
            if (AccDescrMatch.Success)
            {
                Database.AccessibilityDescription = AccDescrMatch.Groups[1].Value.Trim();
                continue;
            }

            if (AccDescrBlockStartRegex.IsMatch(Line))
            {
                AccState = AccDescrParseState.Multiline;
                AccDescrBuilder.Clear();
                continue;
            }

            if (!HeaderParsed)
            {
                if (ElkHeaderRegex.IsMatch(Line))
                {
                    Database.DiagnosticsMutable.Add(
                        new MermaidDiagnostic(
                            MermaidDiagnosticSeverity.Warning,
                            "flowchart.elk-not-native",
                            "flowchart-elk uses ELK layout; fallback required unless strict native mode is disabled.",
                            new MermaidSourceSpan(0, 0, LineIndex + 1, 0, LineIndex + 1, Line.Length),
                            null));
                }

                Match HeaderWithDirectionMatch = HeaderWithDirectionRegex.Match(Line);
                if (HeaderWithDirectionMatch.Success)
                {
                    Database.Header = Line;
                    Database.Direction = ParseDirection(HeaderWithDirectionMatch.Groups[1].Value);
                    HeaderParsed = true;
                    continue;
                }

                Match HeaderOnlyMatch = HeaderOnlyRegex.Match(Line);
                if (HeaderOnlyMatch.Success)
                {
                    Database.Header = Line;
                    Database.Direction = FlowchartDirection.TopToBottom;
                    HeaderParsed = true;
                    continue;
                }
            }

            if (string.Equals(Line, "end", StringComparison.OrdinalIgnoreCase))
            {
                Database.PopSubgraph();
                continue;
            }

            Match SubgraphMatch = SubgraphStartRegex.Match(Line);
            if (SubgraphMatch.Success)
            {
                string? Title = SubgraphMatch.Groups[2].Success
                    ? FlowchartLabelNormalizer.Normalize(SubgraphMatch.Groups[2].Value)
                    : null;
                Database.PushSubgraph(SubgraphMatch.Groups[1].Value, Title);
                continue;
            }

            Match SubgraphDirectionMatch = SubgraphDirectionRegex.Match(Line);
            if (SubgraphDirectionMatch.Success)
            {
                Database.SetCurrentSubgraphDirection(ParseDirection(SubgraphDirectionMatch.Groups[1].Value));
                continue;
            }

            if (FlowchartEdgeMetadataParser.TryParse(Line, Database))
            {
                continue;
            }

            if (FlowchartDirectiveParser.TryParseDirective(Line, LineIndex + 1, Database))
            {
                continue;
            }

            if (FlowchartLinkStatementParser.IsLinkStatement(Line))
            {
                FlowchartLinkStatementParser.ParseLinkStatement(Line, LineIndex + 1, Database);
                continue;
            }

            if (FlowchartClickParser.IsClickLine(Line))
            {
                FlowchartClickParser.TryParse(Line, LineIndex + 1, Database);
                continue;
            }

            ParseBareNodeLine(Line, LineIndex + 1, Database);
        }
    }

    static void ParseBareNodeLine(string Line, int LineIndex, FlowchartDb Database) => FlowchartNodeDeclarationParser.ParseAndMerge(Line, LineIndex, Database);

    /// <summary>
    /// Maps diagram header / subgraph direction tokens (including Mermaid symbolic arrows) to layout direction.
    /// Mirrors vendored <c>flowDb.setDirection</c>.
    /// </summary>
    static FlowchartDirection ParseDirection(string Token)
    {
        if (Token.Contains('<', StringComparison.Ordinal))
        {
            return FlowchartDirection.RightToLeft;
        }

        if (Token.Contains('^', StringComparison.Ordinal))
        {
            return FlowchartDirection.BottomToTop;
        }

        if (Token.Contains('>', StringComparison.Ordinal))
        {
            return FlowchartDirection.LeftToRight;
        }

        if (Token.Contains('v', StringComparison.OrdinalIgnoreCase))
        {
            return FlowchartDirection.TopToBottom;
        }

        string Upper = Token.ToUpperInvariant();
        if (Upper == "LR")
        {
            return FlowchartDirection.LeftToRight;
        }

        if (Upper == "RL")
        {
            return FlowchartDirection.RightToLeft;
        }

        if (Upper == "BT")
        {
            return FlowchartDirection.BottomToTop;
        }

        return FlowchartDirection.TopToBottom;
    }

    enum AccDescrParseState
    {
        None,
        Multiline
    }
}
