using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Expands flowchart scripts so nodes that only appear on link lines get their own declaration lines.
/// That gives each item a source-line id before the editor shows the script.
/// Also tracks which node ids are already taken so expand and palette inserts stay unique.
/// </summary>
public static class FlowchartScriptNormalizer
{
    private static readonly Regex HeaderRegex = new Regex(
        @"^(flowchart|graph)(\s+(TB|TD|BT|LR|RL))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubgraphRegex = new Regex(
        @"^subgraph\s+(?<id>[A-Za-z][\w-]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NodeTokenRegex = new Regex(
        @"(?<id>[A-Za-z][\w-]*)(?<shape>\[\[[^\]]*\]\]|\[\([^\)]*\)\]|\(\[[^\]]*\]\)|\(\([^\)]*\)\)|\{\{[^\}]*\}\}|\{[^\}]*\}|\([^\)]*\)|\[[^\]]*\])?",
        RegexOptions.Compiled);

    private static readonly Regex ArrowSplitRegex = new Regex(
        @"(\s*(?:--+|==+|-\.+|~~+)>?\s*(?:\|[^|]*\|)?\s*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Collects every node and subgraph id currently present in the script.
    /// Re-scan after edits or deletes so freed ids become available again.
    /// </summary>
    /// <param name="Source">Editor or file text.</param>
    /// <returns>Ids in use. Comparison is case-insensitive.</returns>
    public static HashSet<string> CollectUsedIds(string? Source)
    {
        HashSet<string> Used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(Source))
        {
            return Used;
        }

        string Normalised = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] Lines = Normalised.Split('\n');
        for (int Index = 0; Index < Lines.Length; Index++)
        {
            string Trimmed = StripComment(Lines[Index]).Trim();
            if (Trimmed.Length == 0 || HeaderRegex.IsMatch(Trimmed) || IsDirectiveLine(Trimmed))
            {
                continue;
            }

            Match Subgraph = SubgraphRegex.Match(Trimmed);
            if (Subgraph.Success)
            {
                Used.Add(Subgraph.Groups["id"].Value);
                continue;
            }

            foreach (Match Token in NodeTokenRegex.Matches(Trimmed))
            {
                Used.Add(Token.Groups["id"].Value);
            }
        }

        return Used;
    }

    /// <summary>
    /// Picks a short id from a seed and records it in <paramref name="Used"/>.
    /// Call again after the user adds a node so the next suggestion skips it.
    /// </summary>
    /// <param name="PreferredSeed">Preferred id or label start.</param>
    /// <param name="Used">Ids already taken. The new id is added here.</param>
    /// <returns>A unique id that starts with a letter.</returns>
    public static string AllocateUniqueId(string? PreferredSeed, HashSet<string> Used)
    {
        if (Used == null)
        {
            Used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        string Seed = BuildSeed(PreferredSeed);
        string Candidate = Seed;
        int Suffix = 2;
        while (Used.Contains(Candidate))
        {
            Candidate = Seed + Suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Suffix++;
        }

        Used.Add(Candidate);
        return Candidate;
    }

    /// <summary>
    /// Rewrites a flowchart so inline-only nodes become separate declaration lines.
    /// Non-flowchart scripts are returned unchanged.
    /// </summary>
    /// <param name="Source">Editor or file text.</param>
    /// <returns>The expanded script, or the original text when no change is needed.</returns>
    public static string ExpandInlineNodes(string? Source)
    {
        if (string.IsNullOrWhiteSpace(Source) || !FlowchartParser.Default.Detect(Source))
        {
            return Source ?? string.Empty;
        }

        string Newline = Source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string Normalised = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] Lines = Normalised.Split('\n');
        HashSet<string> DeclaredAlone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<InlineNode> InlineOnly = [];
        Dictionary<string, InlineNode> ByOriginalId = new Dictionary<string, InlineNode>(StringComparer.OrdinalIgnoreCase);

        for (int Index = 0; Index < Lines.Length; Index++)
        {
            string Trimmed = StripComment(Lines[Index]).Trim();
            if (Trimmed.Length == 0 || HeaderRegex.IsMatch(Trimmed))
            {
                continue;
            }

            if (FlowchartLinkStatementParser.IsLinkStatement(Trimmed))
            {
                CollectInlineNodes(Trimmed, DeclaredAlone, InlineOnly, ByOriginalId);
                continue;
            }

            if (IsStandaloneNodeLine(Trimmed))
            {
                Match IdMatch = NodeTokenRegex.Match(Trimmed);
                if (IdMatch.Success)
                {
                    DeclaredAlone.Add(IdMatch.Groups["id"].Value);
                }
            }
        }

        for (int Index = InlineOnly.Count - 1; Index >= 0; Index--)
        {
            if (DeclaredAlone.Contains(InlineOnly[Index].OriginalId))
            {
                ByOriginalId.Remove(InlineOnly[Index].OriginalId);
                InlineOnly.RemoveAt(Index);
            }
        }

        if (InlineOnly.Count == 0)
        {
            return Source;
        }

        HashSet<string> Used = CollectUsedIds(Source);
        foreach (InlineNode Node in InlineOnly)
        {
            Used.Remove(Node.OriginalId);
        }

        foreach (InlineNode Node in InlineOnly)
        {
            Node.ShortId = AllocateUniqueId(Node.OriginalId, Used);
        }

        List<string> Output = [];
        bool NodesEmitted = false;
        string Indent = DetectIndent(Lines);

        for (int Index = 0; Index < Lines.Length; Index++)
        {
            string Raw = Lines[Index];
            string Trimmed = StripComment(Raw).Trim();
            if (FlowchartLinkStatementParser.IsLinkStatement(Trimmed))
            {
                if (!NodesEmitted)
                {
                    foreach (InlineNode Node in InlineOnly)
                    {
                        Output.Add(Indent + FormatNodeDeclaration(Node));
                    }

                    NodesEmitted = true;
                }

                Output.Add(RewriteLinkLine(Raw, ByOriginalId));
                continue;
            }

            Output.Add(Raw);
        }

        if (!NodesEmitted)
        {
            foreach (InlineNode Node in InlineOnly)
            {
                Output.Add(Indent + FormatNodeDeclaration(Node));
            }
        }

        return string.Join(Newline, Output);
    }

    private static string BuildSeed(string? PreferredSeed)
    {
        if (string.IsNullOrWhiteSpace(PreferredSeed))
        {
            return "n";
        }

        string Trimmed = PreferredSeed.Trim();
        if (Trimmed.Length > 0 && char.IsLetter(Trimmed[0]))
        {
            return char.ToLowerInvariant(Trimmed[0]).ToString();
        }

        foreach (char Character in Trimmed)
        {
            if (char.IsLetter(Character))
            {
                return char.ToLowerInvariant(Character).ToString();
            }
        }

        return "n";
    }

    private static void CollectInlineNodes(
        string LinkLine,
        HashSet<string> DeclaredAlone,
        List<InlineNode> InlineOnly,
        Dictionary<string, InlineNode> ByOriginalId)
    {
        string[] Parts = ArrowSplitRegex.Split(LinkLine);
        foreach (string Part in Parts)
        {
            string Piece = Part.Trim();
            if (Piece.Length == 0 || ArrowSplitRegex.IsMatch(Piece))
            {
                continue;
            }

            foreach (Match Token in NodeTokenRegex.Matches(Piece))
            {
                string Id = Token.Groups["id"].Value;
                string Shape = Token.Groups["shape"].Success ? Token.Groups["shape"].Value : string.Empty;
                if (Shape.Length == 0 || DeclaredAlone.Contains(Id) || ByOriginalId.ContainsKey(Id))
                {
                    continue;
                }

                string Label = ExtractLabel(Shape, Id);
                InlineNode Node = new InlineNode
                {
                    OriginalId = Id,
                    Label = Label
                };
                InlineOnly.Add(Node);
                ByOriginalId[Id] = Node;
            }
        }
    }

    private static bool IsDirectiveLine(string Trimmed)
    {
        return string.Equals(Trimmed, "end", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("direction ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("style ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("linkStyle ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("classDef ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("class ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("click ", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatNodeDeclaration(InlineNode Node)
    {
        string Escaped = Node.Label.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return Node.ShortId + "(\"" + Escaped + "\")";
    }

    private static string RewriteLinkLine(string RawLine, Dictionary<string, InlineNode> ByOriginalId)
    {
        int ContentStart = 0;
        while (ContentStart < RawLine.Length && char.IsWhiteSpace(RawLine[ContentStart]))
        {
            ContentStart++;
        }

        string Indent = RawLine.Substring(0, ContentStart);
        string Content = StripComment(RawLine.Substring(ContentStart));
        string TrailingComment = string.Empty;
        int CommentAt = RawLine.IndexOf("%%", ContentStart, StringComparison.Ordinal);
        if (CommentAt >= 0)
        {
            TrailingComment = RawLine.Substring(CommentAt);
        }

        StringBuilder Builder = new StringBuilder();
        Builder.Append(Indent);
        string[] Parts = ArrowSplitRegex.Split(Content.Trim());
        foreach (string Part in Parts)
        {
            if (Part.Length == 0)
            {
                continue;
            }

            if (ArrowSplitRegex.IsMatch(Part))
            {
                Builder.Append(Part);
                continue;
            }

            Builder.Append(RewriteNodeSet(Part.Trim(), ByOriginalId));
        }

        Builder.Append(TrailingComment);
        return Builder.ToString();
    }

    private static string RewriteNodeSet(string NodeSet, Dictionary<string, InlineNode> ByOriginalId)
    {
        return NodeTokenRegex.Replace(NodeSet, Match =>
        {
            string Id = Match.Groups["id"].Value;
            if (ByOriginalId.TryGetValue(Id, out InlineNode? Node))
            {
                return Node.ShortId;
            }

            return Id;
        });
    }

    private static bool IsStandaloneNodeLine(string Trimmed)
    {
        if (string.Equals(Trimmed, "end", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("direction ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("subgraph ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("style ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("classDef ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("class ", StringComparison.OrdinalIgnoreCase)
            || Trimmed.StartsWith("click ", StringComparison.OrdinalIgnoreCase)
            || FlowchartLinkStatementParser.IsLinkStatement(Trimmed))
        {
            return false;
        }

        return NodeTokenRegex.IsMatch(Trimmed);
    }

    private static string ExtractLabel(string Shape, string FallbackId)
    {
        if (Shape.Length < 2)
        {
            return FallbackId;
        }

        string Inner = Shape;
        while (Inner.Length > 0 && (Inner[0] == '[' || Inner[0] == '(' || Inner[0] == '{'))
        {
            Inner = Inner.Substring(1);
        }

        while (Inner.Length > 0)
        {
            char Last = Inner[Inner.Length - 1];
            if (Last != ']' && Last != ')' && Last != '}')
            {
                break;
            }

            Inner = Inner.Substring(0, Inner.Length - 1);
        }

        Inner = Inner.Trim();
        if (Inner.Length >= 2 && Inner[0] == '"' && Inner[Inner.Length - 1] == '"')
        {
            Inner = Inner.Substring(1, Inner.Length - 2);
        }

        return Inner.Length == 0 ? FallbackId : Inner;
    }

    private static string DetectIndent(string[] Lines)
    {
        foreach (string Line in Lines)
        {
            if (Line.Length == 0)
            {
                continue;
            }

            int Spaces = 0;
            while (Spaces < Line.Length && Line[Spaces] == ' ')
            {
                Spaces++;
            }

            if (Spaces > 0 && Spaces < Line.Length)
            {
                return Line.Substring(0, Spaces);
            }

            if (Line.StartsWith('\t'))
            {
                return "\t";
            }
        }

        return "  ";
    }

    private static string StripComment(string Line)
    {
        int Comment = Line.IndexOf("%%", StringComparison.Ordinal);
        if (Comment < 0)
        {
            return Line;
        }

        return Line.Substring(0, Comment);
    }

    private sealed class InlineNode
    {
        public string OriginalId = string.Empty;

        public string ShortId = string.Empty;

        public string Label = string.Empty;
    }
}
