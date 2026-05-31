using System.Text.RegularExpressions;

namespace MermaidRecognition.Class;

/// <summary>
/// Class diagram L2 recognition (vendored <c>classDiagram.jison</c>).
/// </summary>
public sealed class ClassKindRecognizer : IKindRecognizer
{
    private static readonly Regex HeaderRegex = new(
        @"^\s*classDiagram(?:-v2)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClassRegex = new(@"^\s*class\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RelationRegex = new(
        @"^\s*.+(<\|--|<\|\.\.|\.\.\>|<\|--|\*--|o--|-->|<--|--|\.\.)",
        RegexOptions.Compiled);
    private static readonly Regex NamespaceRegex = new(@"^\s*namespace\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StructOpenRegex = new(@"^\s*\{\s*$", RegexOptions.Compiled);
    private static readonly Regex StructCloseRegex = new(@"^\s*\}\s*$", RegexOptions.Compiled);
    private static readonly Regex MemberRegex = new(@"^\s*[+\-#~]", RegexOptions.Compiled);
    private static readonly Regex EnumMemberRegex = new(@"^\s*[A-Za-z_][A-Za-z0-9_]*\s*$", RegexOptions.Compiled);
    private static readonly Regex AnnotationRegex = new(@"<<", RegexOptions.Compiled);
    private static readonly Regex ClassDefRegex = new(@"^\s*classDef\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleRegex = new(@"^\s*style\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CssClassRegex = new(@"^\s*cssClass\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NoteRegex = new(@"^\s*note(\s+for\s+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClickRegex = new(@"^\s*(click|link|callback)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DirectionRegex = new(
        @"^\s*direction\s+(TB|BT|RL|LR)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccTitleRegex = new(@"^\s*accTitle\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccDescrRegex = new(@"^\s*accDescr(\s*:|\s*\{)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        return ParseLines(Source, Detection.ProgrammeKindId, "classDiagram");
    }

    internal static DiagramContract ParseLines(string Source, string Kind, string LineTypePrefix)
    {
        DiagramContract Contract = new DiagramContract();
        Contract.Kind = Kind;
        ApplyInit(Source, Contract);

        string Body = StripLeadingInitBlocks(Source);
        string[] Lines = Body.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool HeaderRecorded = false;
        bool AccDescrBlockOpen = false;

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line))
            {
                continue;
            }

            if (MermaidRemarkAnalyzer.IsFullLineRemark(Line))
            {
                AddStatement(Contract, LineIndex + 1, LineTypePrefix + ".comment", Line);
                continue;
            }

            string ActiveLine = MermaidRemarkAnalyzer.StripTrailingRemark(Line);

            if (!HeaderRecorded)
            {
                if (HeaderRegex.IsMatch(ActiveLine))
                {
                    Contract.Header = ActiveLine.Trim();
                    AddStatement(Contract, LineIndex + 1, LineTypePrefix + ".header", Line);
                    HeaderRecorded = true;
                }

                continue;
            }

            if (AccDescrBlockOpen)
            {
                if (StructCloseRegex.IsMatch(ActiveLine))
                {
                    AddStatement(Contract, LineIndex + 1, LineTypePrefix + ".structClose", Line);
                    AccDescrBlockOpen = false;
                }
                else
                {
                    AddStatement(Contract, LineIndex + 1, LineTypePrefix + ".body", Line);
                }

                continue;
            }

            string LineTypeId = ClassifyLine(ActiveLine, LineTypePrefix);
            if (LineTypeId == LineTypePrefix + ".accDescr" && ActiveLine.Contains('{'))
            {
                AccDescrBlockOpen = true;
            }

            AddStatement(Contract, LineIndex + 1, LineTypeId, Line);
        }

        ClassContractMapper.Apply(Contract);
        Contract.Configuration.BuildEffectiveConfiguration(Kind);
        return Contract;
    }

    static string ClassifyLine(string Line, string Prefix)
    {
        if (AccTitleRegex.IsMatch(Line))
        {
            return Prefix + ".accTitle";
        }

        if (AccDescrRegex.IsMatch(Line))
        {
            return Prefix + ".accDescr";
        }

        if (DirectionRegex.IsMatch(Line))
        {
            return Prefix + ".direction";
        }

        if (NamespaceRegex.IsMatch(Line))
        {
            return Prefix + ".namespace";
        }

        if (ClassDefRegex.IsMatch(Line))
        {
            return Prefix + ".classDef";
        }

        if (StyleRegex.IsMatch(Line))
        {
            return Prefix + ".style";
        }

        if (CssClassRegex.IsMatch(Line))
        {
            return Prefix + ".cssClass";
        }

        if (NoteRegex.IsMatch(Line))
        {
            return Prefix + ".note";
        }

        if (ClickRegex.IsMatch(Line))
        {
            return Prefix + ".click";
        }

        if (ClassRegex.IsMatch(Line))
        {
            return Prefix + ".class";
        }

        if (RelationRegex.IsMatch(Line))
        {
            return Prefix + ".relation";
        }

        if (StructOpenRegex.IsMatch(Line))
        {
            return Prefix + ".structOpen";
        }

        if (StructCloseRegex.IsMatch(Line))
        {
            return Prefix + ".structClose";
        }

        if (MemberRegex.IsMatch(Line))
        {
            return Prefix + ".member";
        }

        if (EnumMemberRegex.IsMatch(Line))
        {
            return Prefix + ".member";
        }

        if (AnnotationRegex.IsMatch(Line))
        {
            return Prefix + ".annotation";
        }

        return Prefix + ".body";
    }

    static void ApplyInit(string Source, DiagramContract Contract)
    {
        Dictionary<string, string> InitValues = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(Source);
        foreach (KeyValuePair<string, string> Entry in InitValues)
        {
            Contract.Configuration.RawDirectiveValuesMutable[Entry.Key] = Entry.Value;
        }
    }

    static void AddStatement(DiagramContract Contract, int LineNumber, string LineTypeId, string RawLine)
    {
        SourceStatementContract Statement = new SourceStatementContract();
        Statement.LineNumber = LineNumber;
        Statement.LineTypeId = LineTypeId;
        Statement.RawLine = RawLine;
        Contract.SourceStatementsMutable.Add(Statement);
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
}
