using System.Text.RegularExpressions;

namespace MermaidRecognition.Er;

/// <summary>
/// ER diagram L2 recognition (vendored <c>erDiagram.jison</c>).
/// </summary>
public sealed class ErKindRecognizer : IKindRecognizer
{
    private static readonly Regex HeaderRegex = new(
        @"^\s*erDiagram\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string ER_CARDINALITY_MARKER = @"(?:\|\||\|o|o\||\}\||\|\{|o\{|\}o)";

    private static readonly Regex RelationshipRegex = new(
        @"^\s*\S+\s+" + ER_CARDINALITY_MARKER + @"(?:--|\.\.)" + ER_CARDINALITY_MARKER + @"\s+\S+(\s*:\s*.+)?\s*$",
        RegexOptions.Compiled);
    private static readonly Regex EntityBlockOpenRegex = new(@"^\s*\S+\s*\{\s*$", RegexOptions.Compiled);
    private static readonly Regex EntityBlockCloseRegex = new(@"^\s*\}\s*$", RegexOptions.Compiled);
    private static readonly Regex AttributeRegex = new(
        @"^\s*\S+\s+\S+(\s+(PK|FK|UK)\b)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EntityAliasRegex = new(@"^\s*\S+\s*\[", RegexOptions.Compiled);
    private static readonly Regex ClassDefRegex = new(@"^\s*classDef\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"^\s*class\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleRegex = new(@"^\s*style\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DirectionRegex = new(
        @"^\s*direction\s+(TB|BT|RL|LR)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccTitleRegex = new(@"^\s*accTitle\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccDescrRegex = new(@"^\s*accDescr(\s*:|\s*\{)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StructCloseRegex = new(@"^\s*\}\s*$", RegexOptions.Compiled);

    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        DiagramContract Contract = new DiagramContract();
        Contract.Kind = Detection.ProgrammeKindId;
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
                AddStatement(Contract, LineIndex + 1, "erDiagram.comment", Line);
                continue;
            }

            string ActiveLine = MermaidRemarkAnalyzer.StripTrailingRemark(Line);

            if (!HeaderRecorded)
            {
                if (HeaderRegex.IsMatch(ActiveLine))
                {
                    Contract.Header = ActiveLine.Trim();
                    AddStatement(Contract, LineIndex + 1, "erDiagram.header", Line);
                    HeaderRecorded = true;
                }

                continue;
            }

            if (AccDescrBlockOpen)
            {
                if (StructCloseRegex.IsMatch(ActiveLine))
                {
                    AddStatement(Contract, LineIndex + 1, "erDiagram.structClose", Line);
                    AccDescrBlockOpen = false;
                }
                else
                {
                    AddStatement(Contract, LineIndex + 1, "erDiagram.body", Line);
                }

                continue;
            }

            string LineTypeId = ClassifyLine(ActiveLine);
            if (LineTypeId == "erDiagram.accDescr" && ActiveLine.Contains('{'))
            {
                AccDescrBlockOpen = true;
            }

            AddStatement(Contract, LineIndex + 1, LineTypeId, Line);
        }

        ErContractMapper.Apply(Contract);
        Contract.Configuration.BuildEffectiveConfiguration(Contract.Kind);
        return Contract;
    }

    static string ClassifyLine(string Line)
    {
        if (AccTitleRegex.IsMatch(Line))
        {
            return "erDiagram.accTitle";
        }

        if (AccDescrRegex.IsMatch(Line))
        {
            return "erDiagram.accDescr";
        }

        if (DirectionRegex.IsMatch(Line))
        {
            return "erDiagram.direction";
        }

        if (ClassDefRegex.IsMatch(Line))
        {
            return "erDiagram.classDef";
        }

        if (ClassRegex.IsMatch(Line))
        {
            return "erDiagram.class";
        }

        if (StyleRegex.IsMatch(Line))
        {
            return "erDiagram.style";
        }

        if (RelationshipRegex.IsMatch(Line))
        {
            return "erDiagram.relationship";
        }

        if (EntityBlockOpenRegex.IsMatch(Line))
        {
            return "erDiagram.entityBlockOpen";
        }

        if (EntityBlockCloseRegex.IsMatch(Line))
        {
            return "erDiagram.entityBlockClose";
        }

        if (EntityAliasRegex.IsMatch(Line))
        {
            return "erDiagram.entityAlias";
        }

        if (AttributeRegex.IsMatch(Line))
        {
            return "erDiagram.attribute";
        }

        return "erDiagram.body";
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
