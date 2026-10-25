using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
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
    /// Colours the editor text without scrolling or flicker.
    /// </summary>
    /// <param name="Editor">Target editor.</param>
    /// <param name="DarkTheme">When true, uses dark-theme colours.</param>
    public static void Apply(RichTextBox Editor, bool DarkTheme)
    {
        if (Editor == null || Editor.IsDisposed || !Editor.IsHandleCreated)
        {
            return;
        }

        string Text = Editor.Text;
        Color DefaultColour = DarkTheme ? Color.Gainsboro : Color.Black;
        Color Background = DarkTheme ? Color.FromArgb(30, 30, 30) : Color.White;
        using RichTextBoxPaintScope Scope = new RichTextBoxPaintScope(Editor);
        Editor.BackColor = Background;
        Editor.ForeColor = DefaultColour;
        Editor.Select(0, Editor.TextLength);
        Editor.SelectionColor = DefaultColour;
        ClearCharacterBackground(Editor);
        ColourMatches(Editor, CommentRegex, Text, DarkTheme ? Color.FromArgb(106, 153, 85) : Color.FromArgb(0, 128, 0));
        ColourMatches(Editor, StringRegex, Text, DarkTheme ? Color.FromArgb(206, 145, 120) : Color.FromArgb(163, 21, 21));
        ColourMatches(Editor, HeaderRegex, Text, DarkTheme ? Color.FromArgb(86, 156, 214) : Color.FromArgb(0, 0, 255));
        ColourMatches(Editor, KeywordRegex, Text, DarkTheme ? Color.FromArgb(197, 134, 192) : Color.FromArgb(128, 0, 128));
        ColourMatches(Editor, RelationshipRegex, Text, DarkTheme ? Color.FromArgb(78, 201, 176) : Color.FromArgb(0, 128, 128));
        ColourMatches(Editor, GetElementTypeRegex(), Text, DarkTheme ? Color.FromArgb(78, 201, 176) : Color.FromArgb(43, 145, 175));
    }

    /// <summary>
    /// Tints whole lines that carry diagnostics, without scrolling the caret away.
    /// </summary>
    /// <param name="Editor">Target editor.</param>
    /// <param name="LineNumbers">1-based line numbers to tint.</param>
    /// <param name="DarkTheme">When true, uses dark-theme colours.</param>
    public static void TintLines(RichTextBox Editor, IReadOnlyList<int> LineNumbers, bool DarkTheme)
    {
        if (Editor == null || Editor.IsDisposed || !Editor.IsHandleCreated || LineNumbers == null || LineNumbers.Count == 0)
        {
            return;
        }

        Color ErrorColour = DarkTheme ? Color.FromArgb(90, 55, 55) : Color.MistyRose;
        using RichTextBoxPaintScope Scope = new RichTextBoxPaintScope(Editor);
        foreach (int LineNumber in LineNumbers)
        {
            int LineIndex = LineNumber - 1;
            if (LineIndex < 0 || LineIndex >= Editor.Lines.Length)
            {
                continue;
            }

            int Start = Editor.GetFirstCharIndexFromLine(LineIndex);
            if (Start < 0)
            {
                continue;
            }

            int LineLength = Editor.Lines[LineIndex].Length;
            if (LineLength <= 0)
            {
                continue;
            }

            Editor.Select(Start, Math.Min(LineLength, Editor.TextLength - Start));
            Editor.SelectionBackColor = ErrorColour;
        }
    }

    /// <summary>
    /// Background used for copies of an identifier other than the one that is selected.
    /// The selected copy keeps the Windows selection colours.
    /// </summary>
    /// <param name="DarkTheme">When true, uses a dark-theme gold.</param>
    /// <returns>Mark colour.</returns>
    public static Color OtherOccurrenceColour(bool DarkTheme)
    {
        if (DarkTheme)
        {
            return Color.FromArgb(120, 96, 40);
        }

        return Color.FromArgb(255, 230, 150);
    }

    /// <summary>
    /// Paints every listed identifier except the selected one.
    /// </summary>
    /// <param name="Editor">Target editor.</param>
    /// <param name="Starts">Start index of each identifier.</param>
    /// <param name="Length">Identifier length.</param>
    /// <param name="SelectedStart">Start of the Windows selection, which is left unchanged.</param>
    /// <param name="DarkTheme">When true, uses the dark mark colour.</param>
    public static void MarkOtherOccurrences(RichTextBox Editor, IReadOnlyList<int> Starts, int Length, int SelectedStart, bool DarkTheme)
    {
        if (Editor == null || Editor.IsDisposed || !Editor.IsHandleCreated || Starts == null || Length <= 0)
        {
            return;
        }

        Color Mark = OtherOccurrenceColour(DarkTheme);
        using RichTextBoxPaintScope Scope = new RichTextBoxPaintScope(Editor);
        foreach (int Start in Starts)
        {
            if (Start == SelectedStart || Start < 0 || Start + Length > Editor.TextLength)
            {
                continue;
            }

            Editor.Select(Start, Length);
            Editor.SelectionBackColor = Mark;
        }
    }

    /// <summary>
    /// Removes the mark colour from each listed span so the editor background shows again.
    /// </summary>
    /// <param name="Editor">Target editor.</param>
    /// <param name="Starts">Start index of each identifier.</param>
    /// <param name="Length">Identifier length.</param>
    public static void ClearOccurrenceMarks(RichTextBox Editor, IReadOnlyList<int> Starts, int Length)
    {
        if (Editor == null || Editor.IsDisposed || !Editor.IsHandleCreated || Starts == null || Length <= 0)
        {
            return;
        }

        using RichTextBoxPaintScope Scope = new RichTextBoxPaintScope(Editor);
        foreach (int Start in Starts)
        {
            if (Start < 0 || Start + Length > Editor.TextLength)
            {
                continue;
            }

            Editor.Select(Start, Length);
            Editor.SelectionBackColor = Editor.BackColor;
            ClearCharacterBackground(Editor);
        }
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

    /// <summary>
    /// Removes character background so the editor background, and the Windows selection colour, show through.
    /// Setting <see cref="RichTextBox.SelectionBackColor"/> to the editor background is ignored once a mark has been painted.
    /// </summary>
    /// <param name="Editor">Editor whose current selection is cleared.</param>
    private static void ClearCharacterBackground(RichTextBox Editor)
    {
        CharacterFormat Format = new CharacterFormat
        {
            FaceName = string.Empty
        };
        Format.Size = Marshal.SizeOf(Format);
        Format.Mask = CFM_BACKCOLOR;
        Format.Effects = CFE_AUTOBACKCOLOR;
        SendMessage(Editor.Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, ref Format);
    }

    private const int WM_USER = 0x0400;
    private const int EM_SETCHARFORMAT = WM_USER + 68;
    private const int SCF_SELECTION = 1;
    private const uint CFM_BACKCOLOR = 0x04000000;
    private const uint CFE_AUTOBACKCOLOR = 0x04000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr Handle, int Message, IntPtr WParam, ref CharacterFormat Format);

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    private struct CharacterFormat
    {
        public int Size;
        public uint Mask;
        public uint Effects;
        public int Height;
        public int Offset;
        public int TextColour;
        public byte CharacterSet;
        public byte PitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
        public short Weight;
        public short Spacing;
        public int BackColour;
        public int LocaleId;
        public int Reserved;
        public short Style;
        public short Kerning;
        public byte UnderlineType;
        public byte Animation;
        public byte RevisionAuthor;
        public byte ReservedByte;
    }
}
