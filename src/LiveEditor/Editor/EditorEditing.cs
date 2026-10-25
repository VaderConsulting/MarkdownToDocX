using System;
using System.Collections.Generic;
using System.Text;

namespace LiveEditor.Editor;

/// <summary>
/// One change to the source: the new text and the caret or selection that should follow it.
/// </summary>
internal sealed class TextChange
{
    /// <summary>Gets or sets the source after the change. Null when only the selection moves.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the caret or selection start.</summary>
    public int SelectionStart { get; set; }

    /// <summary>Gets or sets the selection length.</summary>
    public int SelectionLength { get; set; }

    /// <summary>Gets or sets a short status message. Empty when there is nothing to report.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the change could be made.</summary>
    public bool Succeeded { get; set; }
}

/// <summary>
/// Find, replace, indent, comment, and line edits for the source editor.
/// </summary>
internal static class EditorEditing
{
    /// <summary>Finds the next or previous match.</summary>
    public static TextChange Find(string Text, int SelectionStart, int SelectionLength, string Query, bool Forward, bool MatchCase)
    {
        TextChange Change = new TextChange
        {
            SelectionStart = SelectionStart,
            SelectionLength = SelectionLength
        };
        if (string.IsNullOrEmpty(Query))
        {
            Change.Status = "Enter text to find.";
            return Change;
        }

        StringComparison Comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int StartAt = SelectionStart;
        if (SelectionLength > 0 && string.Equals(Text.Substring(SelectionStart, Math.Min(SelectionLength, Text.Length - SelectionStart)), Query, Comparison))
        {
            StartAt = Forward ? SelectionStart + SelectionLength : SelectionStart - 1;
        }

        int Found = Forward ? IndexAfter(Text, Query, StartAt, Comparison) : IndexBefore(Text, Query, StartAt, Comparison);
        if (Found < 0)
        {
            Found = Forward ? IndexAfter(Text, Query, 0, Comparison) : IndexBefore(Text, Query, Text.Length, Comparison);
        }

        if (Found < 0)
        {
            Change.Status = "No matches.";
            return Change;
        }

        Change.Succeeded = true;
        Change.SelectionStart = Found;
        Change.SelectionLength = Query.Length;
        return Change;
    }

    /// <summary>
    /// Gives a trailing blank line the same leading whitespace as the line above it.
    /// A file or sample that ends with a newline otherwise shows that line at column zero.
    /// </summary>
    /// <param name="Source">Text being loaded.</param>
    /// <returns>The same text, with the trailing blank line indented when the line above is indented.</returns>
    public static string IndentTrailingBlankLine(string Source)
    {
        if (string.IsNullOrEmpty(Source))
        {
            return Source ?? string.Empty;
        }

        bool WindowsNewlines = Source.Contains("\r\n", StringComparison.Ordinal);
        string Text = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (!Text.EndsWith('\n'))
        {
            return Source;
        }

        int ContentEnd = Text.Length - 1;
        int PreviousBreak = Text.LastIndexOf('\n', ContentEnd - 1);
        int PreviousStart = PreviousBreak < 0 ? 0 : PreviousBreak + 1;
        int IndentEnd = PreviousStart;
        while (IndentEnd < ContentEnd && (Text[IndentEnd] == ' ' || Text[IndentEnd] == '\t'))
        {
            IndentEnd++;
        }

        if (IndentEnd == PreviousStart)
        {
            return Source;
        }

        string Indented = Text + Text.Substring(PreviousStart, IndentEnd - PreviousStart);
        if (WindowsNewlines)
        {
            return Indented.Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        return Indented;
    }

    /// <summary>Replaces the current match when it is selected, then finds the next one.</summary>
    public static TextChange Replace(string Text, int SelectionStart, int SelectionLength, string Query, string Replacement, bool MatchCase)
    {
        StringComparison Comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        string SafeReplacement = Replacement ?? string.Empty;
        if (SelectionLength == Query.Length && SelectionStart >= 0 && SelectionStart + SelectionLength <= Text.Length
            && string.Equals(Text.Substring(SelectionStart, SelectionLength), Query, Comparison))
        {
            string Updated = Text.Substring(0, SelectionStart) + SafeReplacement + Text.Substring(SelectionStart + SelectionLength);
            TextChange Next = Find(Updated, SelectionStart, SafeReplacement.Length, Query, true, MatchCase);
            Next.Text = Updated;
            Next.Succeeded = true;
            Next.Status = "Replaced 1.";
            return Next;
        }

        return Find(Text, SelectionStart, SelectionLength, Query, true, MatchCase);
    }

    /// <summary>Replaces every match.</summary>
    public static TextChange ReplaceAll(string Text, string Query, string Replacement, bool MatchCase)
    {
        TextChange Change = new TextChange();
        if (string.IsNullOrEmpty(Query))
        {
            Change.Status = "Enter text to find.";
            return Change;
        }

        StringComparison Comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        StringBuilder Builder = new StringBuilder();
        int Count = 0;
        int Index = 0;
        string SafeReplacement = Replacement ?? string.Empty;
        while (Index < Text.Length)
        {
            int Found = Text.IndexOf(Query, Index, Comparison);
            if (Found < 0)
            {
                Builder.Append(Text, Index, Text.Length - Index);
                break;
            }

            Builder.Append(Text, Index, Found - Index);
            Builder.Append(SafeReplacement);
            Index = Found + Query.Length;
            Count++;
        }

        Change.Text = Builder.ToString();
        Change.Succeeded = Count > 0;
        Change.Status = Count == 0 ? "No matches." : "Replaced " + Count.ToString() + ".";
        Change.SelectionStart = 0;
        Change.SelectionLength = 0;
        return Change;
    }

    /// <summary>Moves the caret to a 1-based line.</summary>
    public static TextChange GoToLine(string Text, int LineNumber)
    {
        TextChange Change = new TextChange();
        List<int> Starts = LineStartsOf(Text);
        if (LineNumber < 1 || LineNumber > Starts.Count)
        {
            Change.Status = "Line " + LineNumber.ToString() + " is outside this file.";
            return Change;
        }

        Change.Succeeded = true;
        Change.SelectionStart = Starts[LineNumber - 1];
        Change.SelectionLength = 0;
        Change.Status = "Line " + LineNumber.ToString() + ".";
        return Change;
    }

    /// <summary>Inserts one indent at the start of each selected line.</summary>
    public static TextChange Indent(string Text, int SelectionStart, int SelectionLength, string IndentUnit)
    {
        return ShiftIndent(Text, SelectionStart, SelectionLength, IndentUnit, true);
    }

    /// <summary>Removes one indent from the start of each selected line.</summary>
    public static TextChange Outdent(string Text, int SelectionStart, int SelectionLength, string IndentUnit)
    {
        return ShiftIndent(Text, SelectionStart, SelectionLength, IndentUnit, false);
    }

    /// <summary>Puts a Mermaid comment marker on each selected line.</summary>
    public static TextChange Comment(string Text, int SelectionStart, int SelectionLength)
    {
        return RewriteLines(Text, SelectionStart, SelectionLength, delegate (string Line)
        {
            int Indent = LeadingWhitespace(Line);
            return Line.Substring(0, Indent) + "%% " + Line.Substring(Indent);
        });
    }

    /// <summary>Removes one Mermaid comment marker from each selected line.</summary>
    public static TextChange Uncomment(string Text, int SelectionStart, int SelectionLength)
    {
        return RewriteLines(Text, SelectionStart, SelectionLength, delegate (string Line)
        {
            int Indent = LeadingWhitespace(Line);
            string Rest = Line.Substring(Indent);
            if (!Rest.StartsWith("%%", StringComparison.Ordinal))
            {
                return Line;
            }

            Rest = Rest.Substring(2);
            if (Rest.StartsWith(" ", StringComparison.Ordinal))
            {
                Rest = Rest.Substring(1);
            }

            return Line.Substring(0, Indent) + Rest;
        });
    }

    /// <summary>Copies the selected lines and places the caret on the copy.</summary>
    public static TextChange Duplicate(string Text, int SelectionStart, int SelectionLength)
    {
        LineSpan Span = LinesTouched(Text, SelectionStart, SelectionLength);
        string Block = Text.Substring(Span.Start, Span.End - Span.Start);
        bool BlockEndsWithNewline = Block.EndsWith("\n", StringComparison.Ordinal);
        string Inserted = BlockEndsWithNewline ? Block : Block + "\n";
        string Updated = Text.Substring(0, Span.End) + Inserted + Text.Substring(Span.End);
        if (!BlockEndsWithNewline && Span.End >= Text.Length)
        {
            Updated = Text + "\n" + Block;
        }

        TextChange Change = new TextChange
        {
            Succeeded = true,
            Text = Updated
        };
        int CopyStart = BlockEndsWithNewline || Span.End < Text.Length ? Span.End : Text.Length + 1;
        Change.SelectionStart = CopyStart + (SelectionStart - Span.Start);
        Change.SelectionLength = SelectionLength;
        return Change;
    }

    /// <summary>Deletes the selected lines.</summary>
    public static TextChange DeleteLines(string Text, int SelectionStart, int SelectionLength)
    {
        LineSpan Span = LinesTouched(Text, SelectionStart, SelectionLength);
        int RemoveStart = Span.Start;
        int RemoveEnd = Span.End;
        if (RemoveEnd >= Text.Length && RemoveStart > 0 && Text[RemoveStart - 1] == '\n')
        {
            RemoveStart--;
        }

        TextChange Change = new TextChange
        {
            Succeeded = true,
            Text = Text.Substring(0, RemoveStart) + Text.Substring(Math.Min(RemoveEnd, Text.Length))
        };
        Change.SelectionStart = Math.Min(RemoveStart, Change.Text.Length);
        Change.SelectionLength = 0;
        return Change;
    }

    /// <summary>Selects the next diagnostic line after the caret, wrapping to the first.</summary>
    public static TextChange NextDiagnostic(string Text, int SelectionStart, IReadOnlyList<int> LineNumbers, bool Forward)
    {
        TextChange Change = new TextChange
        {
            SelectionStart = SelectionStart
        };
        if (LineNumbers == null || LineNumbers.Count == 0)
        {
            Change.Status = "No errors.";
            return Change;
        }

        List<int> Ordered = new List<int>();
        foreach (int LineNumber in LineNumbers)
        {
            if (LineNumber > 0 && !Ordered.Contains(LineNumber))
            {
                Ordered.Add(LineNumber);
            }
        }

        Ordered.Sort();
        if (Ordered.Count == 0)
        {
            Change.Status = "No errors.";
            return Change;
        }

        List<int> Starts = LineStartsOf(Text);
        int Current = 1;
        for (int Index = 0; Index < Starts.Count; Index++)
        {
            if (Starts[Index] <= SelectionStart)
            {
                Current = Index + 1;
            }
        }

        int Chosen = Ordered[0];
        if (Forward)
        {
            foreach (int LineNumber in Ordered)
            {
                if (LineNumber > Current)
                {
                    Chosen = LineNumber;
                    break;
                }
            }
        }
        else
        {
            Chosen = Ordered[Ordered.Count - 1];
            for (int Index = Ordered.Count - 1; Index >= 0; Index--)
            {
                if (Ordered[Index] < Current)
                {
                    Chosen = Ordered[Index];
                    break;
                }
            }
        }

        if (Chosen < 1 || Chosen > Starts.Count)
        {
            Change.Status = "No errors.";
            return Change;
        }

        Change.Succeeded = true;
        Change.SelectionStart = Starts[Chosen - 1];
        Change.SelectionLength = 0;
        Change.Status = "Line " + Chosen.ToString() + ".";
        return Change;
    }

    private static TextChange ShiftIndent(string Text, int SelectionStart, int SelectionLength, string IndentUnit, bool Indenting)
    {
        string Unit = string.IsNullOrEmpty(IndentUnit) ? "  " : IndentUnit;
        return RewriteLines(Text, SelectionStart, SelectionLength, delegate (string Line)
        {
            if (Indenting)
            {
                return Unit + Line;
            }

            if (Line.StartsWith(Unit, StringComparison.Ordinal))
            {
                return Line.Substring(Unit.Length);
            }

            if (Line.StartsWith("\t", StringComparison.Ordinal))
            {
                return Line.Substring(1);
            }

            int Spaces = 0;
            while (Spaces < Line.Length && Line[Spaces] == ' ' && Spaces < Unit.Length)
            {
                Spaces++;
            }

            return Line.Substring(Spaces);
        });
    }

    private static TextChange RewriteLines(string Text, int SelectionStart, int SelectionLength, Func<string, string> Rewrite)
    {
        LineSpan Span = LinesTouched(Text, SelectionStart, SelectionLength);
        string Region = Text.Substring(Span.Start, Span.End - Span.Start);
        bool EndsWithNewline = Region.EndsWith("\n", StringComparison.Ordinal);
        string Body = EndsWithNewline ? Region.Substring(0, Region.Length - 1) : Region;
        string[] Lines = Body.Split('\n');
        for (int Index = 0; Index < Lines.Length; Index++)
        {
            Lines[Index] = Rewrite(Lines[Index]);
        }

        string Rewritten = string.Join("\n", Lines);
        if (EndsWithNewline)
        {
            Rewritten += "\n";
        }

        TextChange Change = new TextChange
        {
            Succeeded = true,
            Text = Text.Substring(0, Span.Start) + Rewritten + Text.Substring(Span.End),
            SelectionStart = Span.Start,
            SelectionLength = Rewritten.Length
        };
        return Change;
    }

    private static int IndexAfter(string Text, string Query, int Start, StringComparison Comparison)
    {
        if (Start < 0)
        {
            Start = 0;
        }

        if (Start > Text.Length)
        {
            return -1;
        }

        return Text.IndexOf(Query, Start, Comparison);
    }

    private static int IndexBefore(string Text, string Query, int Start, StringComparison Comparison)
    {
        if (Start > Text.Length)
        {
            Start = Text.Length;
        }

        int From = Math.Min(Start, Text.Length - 1);
        if (From < 0 || Query.Length == 0 || From < Query.Length - 1)
        {
            return -1;
        }

        return Text.LastIndexOf(Query, From, Comparison);
    }

    private static int LeadingWhitespace(string Line)
    {
        int Index = 0;
        while (Index < Line.Length && (Line[Index] == ' ' || Line[Index] == '\t'))
        {
            Index++;
        }

        return Index;
    }

    private static LineSpan LinesTouched(string Text, int SelectionStart, int SelectionLength)
    {
        List<int> Starts = LineStartsOf(Text);
        int Start = Math.Max(0, Math.Min(SelectionStart, Text.Length));
        int End = Math.Max(Start, Math.Min(Start + SelectionLength, Text.Length));
        int StartLine = LineIndexAt(Starts, Start);
        int EndLine = LineIndexAt(Starts, End);
        if (SelectionLength > 0 && EndLine > StartLine && End == Starts[EndLine])
        {
            EndLine--;
        }

        LineSpan Span = new LineSpan
        {
            Start = Starts[StartLine],
            End = EndLine + 1 < Starts.Count ? Starts[EndLine + 1] : Text.Length
        };
        return Span;
    }

    private static List<int> LineStartsOf(string Text)
    {
        List<int> Starts = new List<int>();
        Starts.Add(0);
        for (int Index = 0; Index < Text.Length; Index++)
        {
            if (Text[Index] == '\n')
            {
                Starts.Add(Index + 1);
            }
        }

        return Starts;
    }

    private static int LineIndexAt(List<int> Starts, int Position)
    {
        int Line = 0;
        for (int Index = 1; Index < Starts.Count; Index++)
        {
            if (Starts[Index] > Position)
            {
                break;
            }

            Line = Index;
        }

        return Line;
    }

    private struct LineSpan
    {
        public int Start;

        public int End;
    }
}
