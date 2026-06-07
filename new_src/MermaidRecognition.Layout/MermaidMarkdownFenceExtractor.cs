using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Layout;

/// <summary>
/// Extracts Mermaid fenced code blocks from Markdown torture-test fixtures.
/// </summary>
public static class MermaidMarkdownFenceExtractor
{
    private static readonly Regex _DiagramIdCommentPattern = new Regex(
        @"<!--\s*DiagramId:\s*(?<Id>[^-]+?)\s*-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex _FlowCatalogueHeadingPattern = new Regex(
        @"###\s+\*{0,2}(?<Id>F\d+[A-Z]?)\*{0,2}\s*[—\-]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const int HEADING_LOOKUP_MAX_CHARS = 1200;

    private static readonly Regex _MermaidFencePattern = new Regex(
        @"```mermaid\s*\r?\n(?<Body>[\s\S]*?)\r?\n```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// One extracted Mermaid diagram from a Markdown document.
    /// </summary>
    public sealed class ExtractedMermaidDiagram
    {
        private string _DiagramId = string.Empty;

        private string _Source = string.Empty;

        /// <summary>
        /// Diagram identifier from a preceding HTML comment or generated sequence.
        /// </summary>
        public string DiagramId
        {
            get
            {
                return _DiagramId;
            }
            set
            {
                _DiagramId = value;
            }
        }

        /// <summary>
        /// Raw Mermaid source text from the fenced block.
        /// </summary>
        public string Source
        {
            get
            {
                return _Source;
            }
            set
            {
                _Source = value;
            }
        }

        /// <summary>
        /// Zero-based extraction order within the Markdown file.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// True when every non-empty source line is a Mermaid remark (%%).
        /// </summary>
        public bool IsFullyRemarked { get; set; }

        /// <summary>
        /// Catalogue section title when extracted from a flow.md heading.
        /// </summary>
        public string? ScenarioTitle { get; set; }
    }

    /// <summary>
    /// Extracts all Mermaid fenced blocks from Markdown text.
    /// </summary>
    public static List<ExtractedMermaidDiagram> ExtractFlowcharts(string Markdown)
    {
        List<ExtractedMermaidDiagram> Diagrams = new List<ExtractedMermaidDiagram>();
        if (string.IsNullOrEmpty(Markdown))
        {
            return Diagrams;
        }

        MatchCollection Matches = _MermaidFencePattern.Matches(Markdown);
        int DiagramIndex = 0;
        foreach (Match Match in Matches)
        {
            DiagramIndex++;
            string Prefix = Markdown.Substring(0, Match.Index);
            string DiagramId = ResolveDiagramId(Prefix, DiagramIndex);
            string? ScenarioTitle = ResolveScenarioTitle(Prefix);
            string Body = Match.Groups["Body"].Value.Trim();
            bool IsFullyRemarked = IsFullyRemarkedSource(Body);

            ExtractedMermaidDiagram Diagram = new ExtractedMermaidDiagram
            {
                DiagramId = DiagramId,
                Source = Body,
                Index = DiagramIndex - 1,
                IsFullyRemarked = IsFullyRemarked,
                ScenarioTitle = ScenarioTitle
            };
            Diagrams.Add(Diagram);
        }

        return Diagrams;
    }

    private static string ResolveDiagramId(string Prefix, int DiagramIndex)
    {
        MatchCollection CommentMatches = _DiagramIdCommentPattern.Matches(Prefix);
        if (CommentMatches.Count > 0)
        {
            string RawId = CommentMatches[CommentMatches.Count - 1].Groups["Id"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(RawId))
            {
                return RawId;
            }
        }

        string HeadingPrefix = Prefix;
        if (HeadingPrefix.Length > HEADING_LOOKUP_MAX_CHARS)
        {
            HeadingPrefix = HeadingPrefix.Substring(HeadingPrefix.Length - HEADING_LOOKUP_MAX_CHARS);
        }

        MatchCollection HeadingMatches = _FlowCatalogueHeadingPattern.Matches(HeadingPrefix);
        if (HeadingMatches.Count > 0)
        {
            string HeadingId = HeadingMatches[HeadingMatches.Count - 1].Groups["Id"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(HeadingId))
            {
                return HeadingId;
            }
        }

        return "Diagram" + DiagramIndex.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? ResolveScenarioTitle(string Prefix)
    {
        int LastHeadingIndex = Prefix.LastIndexOf("###", StringComparison.Ordinal);
        if (LastHeadingIndex < 0)
        {
            return null;
        }

        string HeadingLine = Prefix.Substring(LastHeadingIndex);
        int LineBreak = HeadingLine.IndexOf('\n');
        if (LineBreak >= 0)
        {
            HeadingLine = HeadingLine.Substring(0, LineBreak);
        }

        HeadingLine = HeadingLine.Trim();
        if (HeadingLine.Length == 0)
        {
            return null;
        }

        return HeadingLine;
    }

    private static bool IsFullyRemarkedSource(string Body)
    {
        if (string.IsNullOrWhiteSpace(Body))
        {
            return false;
        }

        string[] Lines = Body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        bool SawContent = false;
        foreach (string Line in Lines)
        {
            string Trimmed = Line.Trim();
            if (Trimmed.Length == 0)
            {
                continue;
            }

            SawContent = true;
            if (!Trimmed.StartsWith("%%", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return SawContent;
    }
}
