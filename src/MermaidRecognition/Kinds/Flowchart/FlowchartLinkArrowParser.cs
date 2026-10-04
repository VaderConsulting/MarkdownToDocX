namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Parses Mermaid flowchart link arrows (ported from vendored <c>flowDb.destructLink</c>).
/// </summary>
public static class FlowchartLinkArrowParser
{
    /// <summary>
    /// Populates stroke, min length, and endpoint markers on <paramref name="Token"/> from a link segment.
    /// </summary>
    public static void ApplyToToken(string LinkSegment, FlowchartLinkToken Token)
    {
        string Text = LinkSegment.Trim();
        if (Text.Length == 0)
        {
            return;
        }

        if (IsInvisibleLink(Text))
        {
            Token.Stroke = "invisible";
            Token.MarkerStart = EdgeEndpointMarker.None;
            Token.MarkerEnd = EdgeEndpointMarker.None;
            Token.MinLength = CountChar('~', Text);
            return;
        }

        int SeparatorIndex = FindLinkSeparatorIndex(Text);
        string EndPart = SeparatorIndex >= 0 ? Text[(SeparatorIndex + 1)..].Trim() : Text;
        string StartPart = SeparatorIndex >= 0 ? Text[..SeparatorIndex].Trim() : string.Empty;

        LinkEndFacts EndFacts = ParseEndLink(EndPart.Length > 0 ? EndPart : Text);
        Token.MinLength = EndFacts.Length;
        Token.MarkerEnd = MapMarker(EndFacts.Type, false);

        if (StartPart.Length > 0)
        {
            LinkStartFacts StartFacts = ParseStartLink(StartPart);
            Token.Stroke = MergeLinkStroke(StartFacts.Stroke, EndFacts.Stroke);
            if (string.Equals(StartFacts.Type, "arrow_open", StringComparison.Ordinal))
            {
                Token.MarkerStart = EdgeEndpointMarker.None;
            }
            else
            {
                Token.MarkerStart = MapMarker(StartFacts.Type, true);
            }
        }
        else
        {
            Token.Stroke = EndFacts.Stroke;
            if (EndFacts.Type.StartsWith("double_arrow_", StringComparison.Ordinal))
            {
                EdgeEndpointMarker Marker = MapMarker(EndFacts.Type.Replace("double_", "", StringComparison.Ordinal), false);
                Token.MarkerStart = Marker;
                Token.MarkerEnd = Marker;
            }
            else
            {
                Token.MarkerStart = EdgeEndpointMarker.None;
            }
        }
    }

    /// <summary>
    /// Applies start and end link fragments from a labelled link (for example <c>&lt;-- text --&gt;</c>).
    /// </summary>
    public static void ApplyDestructLink(string EndLink, string StartLink, FlowchartLinkToken Token)
    {
        LinkEndFacts EndFacts = ParseEndLink(EndLink.Trim());
        Token.Stroke = EndFacts.Stroke;
        Token.MinLength = EndFacts.Length;
        Token.MarkerEnd = MapMarker(EndFacts.Type, false);

        if (string.IsNullOrWhiteSpace(StartLink))
        {
            if (EndFacts.Type.StartsWith("double_arrow_", StringComparison.Ordinal))
            {
                EdgeEndpointMarker Marker = MapMarker(EndFacts.Type.Replace("double_", "", StringComparison.Ordinal), false);
                Token.MarkerStart = Marker;
                Token.MarkerEnd = Marker;
            }
            else
            {
                Token.MarkerStart = EdgeEndpointMarker.None;
            }

            return;
        }

        LinkStartFacts StartFacts = ParseStartLink(StartLink);
        Token.Stroke = MergeLinkStroke(StartFacts.Stroke, EndFacts.Stroke);

        if (string.Equals(StartFacts.Type, "arrow_open", StringComparison.Ordinal))
        {
            Token.MarkerStart = EdgeEndpointMarker.None;
            if (EndFacts.Type.StartsWith("double_arrow_", StringComparison.Ordinal))
            {
                EdgeEndpointMarker Marker = MapMarker(EndFacts.Type.Replace("double_", "", StringComparison.Ordinal), false);
                Token.MarkerEnd = Marker;
            }
        }
        else if (EndFacts.Type.StartsWith("double_arrow_", StringComparison.Ordinal)
            || StartFacts.Type.StartsWith("double_", StringComparison.Ordinal))
        {
            EdgeEndpointMarker Marker = MapMarker(EndFacts.Type.Replace("double_", "", StringComparison.Ordinal), false);
            Token.MarkerStart = Marker;
            Token.MarkerEnd = Marker;
        }
        else
        {
            Token.MarkerStart = MapMarker(StartFacts.Type, true);
        }
    }

    static bool IsInvisibleLink(string Text)
    {
        for (int Index = 0; Index < Text.Length; Index++)
        {
            if (Text[Index] != '~')
            {
                return false;
            }
        }

        return Text.Length >= 2;
    }

    static int FindLinkSeparatorIndex(string Text)
    {
        bool InLabel = false;
        for (int Index = 1; Index < Text.Length - 1; Index++)
        {
            char Character = Text[Index];
            if (Character == '"')
            {
                InLabel = !InLabel;
                continue;
            }

            if (InLabel)
            {
                continue;
            }

            if (Character == '-')
            {
                char Previous = Text[Index - 1];
                if (Previous == '.')
                {
                    continue;
                }
            }

            if (Character == '-' || Character == '=' || Character == '~' || Character == '.')
            {
                char Previous = Text[Index - 1];
                if (Previous != '-' && Previous != '=' && Previous != '~' && Previous != '.'
                    && Previous != 'x' && Previous != 'o' && Previous != '<' && Previous != '>')
                {
                    return Index;
                }
            }
        }

        return -1;
    }

    static LinkStartFacts ParseStartLink(string Text)
    {
        string Remaining = Text.Trim();
        string Type = "arrow_open";

        if (Remaining.Length > 0 && Remaining[0] == '<')
        {
            Type = "arrow_point";
            Remaining = Remaining[1..];
        }
        else if (Remaining.Length > 0 && Remaining[0] == 'x')
        {
            Type = "arrow_cross";
            Remaining = Remaining[1..];
        }
        else if (Remaining.Length > 0 && Remaining[0] == 'o')
        {
            Type = "arrow_circle";
            Remaining = Remaining[1..];
        }

        string Stroke = "normal";
        if (Remaining.Contains('=', StringComparison.Ordinal))
        {
            Stroke = "thick";
        }

        if (Remaining.Contains('.', StringComparison.Ordinal))
        {
            Stroke = "dotted";
        }

        LinkStartFacts Facts = new LinkStartFacts
        {
            Type = Type,
            Stroke = Stroke
        };
        return Facts;
    }

    static LinkEndFacts ParseEndLink(string Text)
    {
        string Trimmed = Text.Trim();
        if (Trimmed.Length == 0)
        {
            LinkEndFacts Empty = new LinkEndFacts
            {
                Type = "arrow_open",
                Stroke = "normal",
                Length = 1
            };
            return Empty;
        }

        string Line = Trimmed[..^1];
        string Type = "arrow_open";

        char Last = Trimmed[^1];
        if (Last == 'x')
        {
            Type = "arrow_cross";
            if (Trimmed.StartsWith('x'))
            {
                Type = "double_arrow_cross";
                Line = Line[1..];
            }
        }
        else if (Last == '>')
        {
            Type = "arrow_point";
            if (Trimmed.StartsWith('<'))
            {
                Type = "double_arrow_point";
                Line = Line[1..];
            }
        }
        else if (Last == 'o')
        {
            Type = "arrow_circle";
            if (Trimmed.StartsWith('o'))
            {
                Type = "double_arrow_circle";
                Line = Line[1..];
            }
        }

        string Stroke = "normal";
        int Length = Math.Max(1, Line.Length - 1);

        if (Line.Length > 0 && Line[0] == '=')
        {
            Stroke = "thick";
        }

        if (Line.Length > 0 && Line[0] == '~')
        {
            Stroke = "invisible";
        }

        int DotCount = CountChar('.', Line);
        if (DotCount > 0)
        {
            Stroke = "dotted";
            Length = DotCount;
        }

        LinkEndFacts Facts = new LinkEndFacts
        {
            Type = Type,
            Stroke = Stroke,
            Length = Length
        };
        return Facts;
    }

    static string MergeLinkStroke(string StartStroke, string EndStroke)
    {
        if (string.Equals(StartStroke, "invisible", StringComparison.Ordinal)
            || string.Equals(EndStroke, "invisible", StringComparison.Ordinal))
        {
            return "invisible";
        }

        if (string.Equals(StartStroke, "dotted", StringComparison.Ordinal)
            || string.Equals(EndStroke, "dotted", StringComparison.Ordinal))
        {
            return "dotted";
        }

        if (string.Equals(StartStroke, "thick", StringComparison.Ordinal)
            || string.Equals(EndStroke, "thick", StringComparison.Ordinal))
        {
            return "thick";
        }

        return "normal";
    }

    static EdgeEndpointMarker MapMarker(string Type, bool IsStart)
    {
        if (Type.Contains("cross", StringComparison.Ordinal))
        {
            return EdgeEndpointMarker.Cross;
        }

        if (Type.Contains("circle", StringComparison.Ordinal))
        {
            return EdgeEndpointMarker.Circle;
        }

        if (Type.Contains("open", StringComparison.Ordinal))
        {
            return EdgeEndpointMarker.None;
        }

        if (Type.Contains("point", StringComparison.Ordinal))
        {
            return EdgeEndpointMarker.Point;
        }

        return EdgeEndpointMarker.None;
    }

    static int CountChar(char Character, string Text)
    {
        int Count = 0;
        for (int Index = 0; Index < Text.Length; Index++)
        {
            if (Text[Index] == Character)
            {
                Count++;
            }
        }

        return Count;
    }

    sealed class LinkStartFacts
    {
        public string Type { get; set; } = "arrow_open";

        public string Stroke { get; set; } = "normal";
    }

    sealed class LinkEndFacts
    {
        public string Type { get; set; } = "arrow_open";

        public string Stroke { get; set; } = "normal";

        public int Length { get; set; } = 1;
    }
}
