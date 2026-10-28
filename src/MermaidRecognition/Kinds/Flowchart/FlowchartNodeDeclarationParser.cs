using System.Text;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Parses flowchart vertex suffixes (bracket shapes, <c>@{ ... }</c> metadata) and merges into <see cref="FlowchartDb"/>.
/// </summary>
public static class FlowchartNodeDeclarationParser
{
    private static readonly Regex NodeIdPrefixRegex = new(
        @"^([A-Za-z][\w-]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Splits a node set (possibly joined with <c>&amp;</c>) into individual node tokens.
    /// </summary>
    public static List<string> SplitNodeSetTokens(string NodeSetText)
    {
        List<string> Tokens = [];
        StringBuilder Current = new StringBuilder();
        int BracketDepth = 0;
        int ParenDepth = 0;
        int BraceDepth = 0;

        for (int Index = 0; Index < NodeSetText.Length; Index++)
        {
            char Character = NodeSetText[Index];
            if (Character == '[')
            {
                BracketDepth++;
            }
            else if (Character == ']')
            {
                BracketDepth = Math.Max(0, BracketDepth - 1);
            }
            else if (Character == '(')
            {
                ParenDepth++;
            }
            else if (Character == ')')
            {
                ParenDepth = Math.Max(0, ParenDepth - 1);
            }
            else if (Character == '{')
            {
                if (Index + 1 < NodeSetText.Length && NodeSetText[Index + 1] == '@')
                {
                    BraceDepth++;
                }
                else if (BraceDepth == 0)
                {
                    BraceDepth++;
                }
            }
            else if (Character == '}')
            {
                BraceDepth = Math.Max(0, BraceDepth - 1);
            }

            if (Character == '&'
                && BracketDepth == 0
                && ParenDepth == 0
                && BraceDepth == 0)
            {
                string Token = Current.ToString().Trim();
                if (Token.Length > 0)
                {
                    Tokens.Add(Token);
                }

                Current.Clear();
                continue;
            }

            Current.Append(Character);
        }

        string Last = Current.ToString().Trim();
        if (Last.Length > 0)
        {
            Tokens.Add(Last);
        }

        return Tokens;
    }

    /// <summary>
    /// Parses one node token and merges facts into the diagram database.
    /// </summary>
    public static void ParseAndMerge(string NodeToken, int LineIndex, FlowchartDb Database)
    {
        string Trimmed = NodeToken.Trim();
        if (Trimmed.Length == 0)
        {
            return;
        }

        Match IdMatch = NodeIdPrefixRegex.Match(Trimmed);
        if (!IdMatch.Success)
        {
            return;
        }

        string NodeId = IdMatch.Groups[1].Value;
        string Suffix = Trimmed[IdMatch.Length..].Trim();
        string? ClassFromSuffix = null;
        int ClassSeparatorIndex = Suffix.IndexOf(":::", StringComparison.Ordinal);
        if (ClassSeparatorIndex >= 0)
        {
            ClassFromSuffix = Suffix[(ClassSeparatorIndex + 3)..].Trim();
            Suffix = Suffix[..ClassSeparatorIndex].Trim();
        }

        FlowchartNodeRecord Node = Database.GetOrCreateNode(NodeId);
        Database.AssignExplicitNodeToCurrentSubgraph(NodeId);

        MermaidSourceSpan Span = new MermaidSourceSpan(0, 0, LineIndex, 0, LineIndex, Trimmed.Length);
        Node.DeclarationsMutable.Add(new MermaidDeclaration("vertex", Trimmed, Span));

        if (ClassFromSuffix is not null && ClassFromSuffix.Length > 0)
        {
            Database.ApplyClassToTargets([NodeId], ClassFromSuffix);
        }

        if (Suffix.Length == 0)
        {
            return;
        }

        if (TryParseMetadataSuffix(Suffix, out string MetadataBody, out int Consumed))
        {
            ApplyMetadata(Node, MetadataBody);
            Suffix = Suffix[Consumed..].Trim();
            if (Suffix.Length == 0)
            {
                return;
            }
        }

        if (TryParseBracketSuffix(Suffix, out string? Label, out string? Shape, out string ShapeSource))
        {
            if (Label is not null)
            {
                Node.Label = FlowchartLabelNormalizer.Normalize(Label);
                Node.LabelType = MermaidLabelType.Text;
            }

            if (Shape is not null)
            {
                Node.Shape = Shape;
                Node.ShapeSource = ShapeSource;
            }
        }

    }

    static bool TryParseMetadataSuffix(string Suffix, out string MetadataBody, out int Consumed)
    {
        MetadataBody = string.Empty;
        Consumed = 0;
        if (!Suffix.StartsWith("@{", StringComparison.Ordinal))
        {
            return false;
        }

        int CloseIndex = FindMatchingBrace(Suffix, 1);
        if (CloseIndex < 0)
        {
            return false;
        }

        MetadataBody = Suffix.Substring(2, CloseIndex - 2).Trim();
        Consumed = CloseIndex + 1;
        return true;
    }

    static void ApplyMetadata(FlowchartNodeRecord Node, string MetadataBody)
    {
        foreach (string Entry in SplitMetadataEntriesCore(MetadataBody))
        {
            string Trimmed = Entry.Trim();
            if (Trimmed.Length == 0)
            {
                continue;
            }

            int ColonIndex = Trimmed.IndexOf(':', StringComparison.Ordinal);
            if (ColonIndex <= 0)
            {
                continue;
            }

            string Key = Trimmed[..ColonIndex].Trim();
            string Value = Trimmed[(ColonIndex + 1)..].Trim().Trim('"', '\'');
            if (string.Equals(Key, "shape", StringComparison.OrdinalIgnoreCase))
            {
                Node.Shape = Value;
                Node.ShapeSource = "metadata";
                Node.MetadataMutable["shape"] = new ShapeMetadata(Value);
            }
            else if (string.Equals(Key, "label", StringComparison.OrdinalIgnoreCase))
            {
                string NormalizedLabel = FlowchartLabelNormalizer.Normalize(Value);
                Node.Label = NormalizedLabel;
                Node.LabelType = MermaidLabelType.Text;
                Node.MetadataMutable["label"] = new LabelMetadata(NormalizedLabel, MermaidLabelType.Text);
            }
            else if (string.Equals(Key, "labelType", StringComparison.OrdinalIgnoreCase))
            {
                MermaidLabelType LabelType = ParseLabelType(Value);
                Node.LabelType = LabelType;
                Node.MetadataMutable["labelType"] = new LabelMetadata(Node.Label ?? Node.Id, LabelType);
            }
            else
            {
                Node.MetadataMutable[Key] = new RawMetadata(Key, Value);
            }
        }
    }

    /// <summary>
    /// Splits a metadata block body into key/value entries.
    /// </summary>
    public static IEnumerable<string> SplitMetadataEntries(string MetadataBody) => SplitMetadataEntriesCore(MetadataBody);

    static IEnumerable<string> SplitMetadataEntriesCore(string MetadataBody)
    {
        List<string> Entries = [];
        if (MetadataBody.Contains('\n', StringComparison.Ordinal))
        {
            foreach (string Line in MetadataBody.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                Entries.Add(Line.Trim());
            }

            return Entries;
        }

        int Depth = 0;
        int Start = 0;
        bool InQuotes = false;
        for (int Index = 0; Index < MetadataBody.Length; Index++)
        {
            char Character = MetadataBody[Index];
            if (Character == '"')
            {
                InQuotes = !InQuotes;
            }

            if (!InQuotes && Character == '{')
            {
                Depth++;
            }
            else if (!InQuotes && Character == '}')
            {
                Depth = Math.Max(0, Depth - 1);
            }

            if (!InQuotes && Character == ',' && Depth == 0)
            {
                Entries.Add(MetadataBody[Start..Index]);
                Start = Index + 1;
            }
        }

        Entries.Add(MetadataBody[Start..]);
        return Entries;
    }

    static MermaidLabelType ParseLabelType(string Value)
    {
        if (string.Equals(Value, "markdown", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidLabelType.Markdown;
        }

        if (string.Equals(Value, "string", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidLabelType.String;
        }

        if (string.Equals(Value, "html", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidLabelType.Html;
        }

        return MermaidLabelType.Text;
    }

    static bool TryParseBracketSuffix(
        string Suffix,
        out string? Label,
        out string? Shape,
        out string ShapeSource)
    {
        Label = null;
        Shape = null;
        ShapeSource = "bracket";

        if (Suffix.StartsWith("(((", StringComparison.Ordinal) && Suffix.EndsWith(")))", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(3, Suffix.Length - 6);
            Shape = "doublecircle";
            return true;
        }

        if (Suffix.StartsWith("[[", StringComparison.Ordinal) && Suffix.EndsWith("]]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "subroutine";
            return true;
        }

        if (Suffix.StartsWith("[(", StringComparison.Ordinal) && Suffix.EndsWith(")]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "cylinder";
            return true;
        }

        if (Suffix.StartsWith("((", StringComparison.Ordinal) && Suffix.EndsWith("))", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "circle";
            return true;
        }

        if (Suffix.StartsWith("{{", StringComparison.Ordinal) && Suffix.EndsWith("}}", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "hexagon";
            return true;
        }

        if (Suffix.StartsWith("([", StringComparison.Ordinal) && Suffix.EndsWith("])", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "stadium";
            return true;
        }

        if (Suffix.StartsWith("(", StringComparison.Ordinal) && Suffix.EndsWith(")", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(1, Suffix.Length - 2);
            Shape = "rounded";
            return true;
        }

        if (Suffix.StartsWith(">", StringComparison.Ordinal) && Suffix.EndsWith("]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(1, Suffix.Length - 2);
            Shape = "asymmetric";
            return true;
        }

        if (TryParseTrapezoidBracketSuffix(Suffix, out Label, out Shape))
        {
            return true;
        }

        if (Suffix.StartsWith("[", StringComparison.Ordinal) && Suffix.EndsWith("]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(1, Suffix.Length - 2);
            Shape = "rect";
            return true;
        }

        if (Suffix.StartsWith("{", StringComparison.Ordinal) && Suffix.EndsWith("}", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(1, Suffix.Length - 2);
            Shape = "diamond";
            return true;
        }

        return false;
    }

    static bool TryParseTrapezoidBracketSuffix(string Suffix, out string? Label, out string? Shape)
    {
        Label = null;
        Shape = null;

        if (Suffix.Length < 4)
        {
            return false;
        }

        if (Suffix.StartsWith("[/", StringComparison.Ordinal) && Suffix.EndsWith("/]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "lean_right";
            return true;
        }

        if (Suffix.StartsWith("[/", StringComparison.Ordinal) && Suffix.EndsWith("\\]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "trapezoid";
            return true;
        }

        if (Suffix.StartsWith(@"[\", StringComparison.Ordinal) && Suffix.EndsWith("/]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "inv_trapezoid";
            return true;
        }

        if (Suffix.StartsWith(@"[\", StringComparison.Ordinal) && Suffix.EndsWith("\\]", StringComparison.Ordinal))
        {
            Label = Suffix.Substring(2, Suffix.Length - 4);
            Shape = "lean_left";
            return true;
        }

        return false;
    }

    static int FindMatchingBrace(string Text, int OpenBraceIndex)
    {
        int Depth = 0;
        for (int Index = OpenBraceIndex; Index < Text.Length; Index++)
        {
            if (Text[Index] == '{')
            {
                Depth++;
            }
            else if (Text[Index] == '}')
            {
                Depth--;
                if (Depth == 0)
                {
                    return Index;
                }
            }
        }

        return -1;
    }
}
