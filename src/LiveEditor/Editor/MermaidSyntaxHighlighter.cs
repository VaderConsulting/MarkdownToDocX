using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Editor;

/// <summary>
/// Applies lightweight Mermaid / enterprise syntax colours to a <see cref="RichTextBox"/>.
/// </summary>
internal static class MermaidSyntaxHighlighter
{
    private static readonly Regex CommentRegex = new Regex(@"%%[^\n]*", RegexOptions.Compiled);

    private static readonly Regex StringRegex = new Regex(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'", RegexOptions.Compiled);

    private static readonly Regex HeaderRegex = new Regex(
        @"^\s*(enterprise(?:-beta)?|flowchart|graph|sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|journey|gantt|pie|mindmap|timeline|sankey-beta|architecture-beta)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex KeywordRegex = new Regex(
        @"\b(title|version|direction|group|in|note|classDef|class|style|linkStyle|accTitle|accDescr)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RelationshipRegex = new Regex(
        @"-(composition|aggregation|assignment|realization|serving|access|influence|triggering|flow|specialization|association|[cgiavnrtfso])(?:\([^)]*\))?->",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string? _ElementTypePattern;

    /// <summary>
    /// Colours the editor text. Preserves caret and scroll.
    /// </summary>
    /// <param name="Editor">Target editor.</param>
    /// <param name="DarkTheme">When true, uses dark-theme colours.</param>
    public static void Apply(RichTextBox Editor, bool DarkTheme)
    {
        if (Editor == null || Editor.IsDisposed)
        {
            return;
        }

        int Caret = Editor.SelectionStart;
        int SelectionLength = Editor.SelectionLength;
        int FirstLine = Editor.GetLineFromCharIndex(Editor.GetCharIndexFromPosition(new Point(0, 0)));
        string Text = Editor.Text;
        Color DefaultColour = DarkTheme ? Color.Gainsboro : Color.Black;
        Color Background = DarkTheme ? Color.FromArgb(30, 30, 30) : Color.White;
        Editor.BackColor = Background;
        Editor.ForeColor = DefaultColour;

        Editor.SelectAll();
        Editor.SelectionColor = DefaultColour;
        Editor.SelectionBackColor = Background;

        ColourMatches(Editor, CommentRegex, Text, DarkTheme ? Color.FromArgb(106, 153, 85) : Color.FromArgb(0, 128, 0));
        ColourMatches(Editor, StringRegex, Text, DarkTheme ? Color.FromArgb(206, 145, 120) : Color.FromArgb(163, 21, 21));
        ColourMatches(Editor, HeaderRegex, Text, DarkTheme ? Color.FromArgb(86, 156, 214) : Color.FromArgb(0, 0, 255));
        ColourMatches(Editor, KeywordRegex, Text, DarkTheme ? Color.FromArgb(197, 134, 192) : Color.FromArgb(128, 0, 128));
        ColourMatches(Editor, RelationshipRegex, Text, DarkTheme ? Color.FromArgb(78, 201, 176) : Color.FromArgb(0, 128, 128));
        ColourMatches(Editor, GetElementTypeRegex(), Text, DarkTheme ? Color.FromArgb(78, 201, 176) : Color.FromArgb(43, 145, 175));

        Editor.SelectionStart = Caret;
        Editor.SelectionLength = SelectionLength;
        Editor.SelectionColor = DefaultColour;
        if (FirstLine >= 0 && FirstLine < Editor.Lines.Length)
        {
            int RestoreIndex = Editor.GetFirstCharIndexFromLine(FirstLine);
            if (RestoreIndex >= 0)
            {
                Editor.Select(RestoreIndex, 0);
                Editor.ScrollToCaret();
            }
        }

        Editor.SelectionStart = Caret;
        Editor.SelectionLength = SelectionLength;
    }

    private static Regex GetElementTypeRegex()
    {
        if (_ElementTypePattern != null)
        {
            return new Regex(_ElementTypePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        List<string> Names = new List<string>();
        foreach (KeyValuePair<EnterpriseElementType, EnterpriseElementMetadata> Pair in EnterpriseElementCatalog.Elements)
        {
            Names.Add(Regex.Escape(Pair.Value.KebabName));
        }

        Names.Add("and-junction");
        Names.Add("or-junction");
        Names.Add("junction");
        _ElementTypePattern = @"\b(" + string.Join("|", Names) + @")\b";
        return new Regex(_ElementTypePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    private static void ColourMatches(RichTextBox Editor, Regex Pattern, string Text, Color Colour)
    {
        foreach (Match Match in Pattern.Matches(Text))
        {
            if (!Match.Success || Match.Length == 0)
            {
                continue;
            }

            Editor.Select(Match.Index, Match.Length);
            Editor.SelectionColor = Colour;
        }
    }
}
