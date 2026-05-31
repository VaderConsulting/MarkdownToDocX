using System.Text.RegularExpressions;

namespace MermaidRecognition.Sequence;

/// <summary>
/// Sequence diagram recognition (vendored <c>sequenceDetector.ts</c> / <c>sequenceDiagram.jison</c>).
/// </summary>
public sealed class SequenceKindRecognizer : IKindRecognizer
{
    private static readonly Regex HeaderRegex = new(
        @"^\s*sequenceDiagram\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParticipantRegex = new(
        @"^\s*(participant|actor)\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MessageRegex = new(
        @"^\s*.+?(->>|-->>|<<-->>|<<->>|-x>>|x->|-->|-x|--x|->|-\)|--\)|-\||--\\|--\/|\/\/-|\\\\-).+?:",
        RegexOptions.Compiled);

    private static readonly Regex ActivateRegex = new(
        @"^\s*(activate|deactivate)\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NoteRegex = new(
        @"^\s*note\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new(
        @"^\s*title(\s|:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AutonumberRegex = new(
        @"^\s*autonumber\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BlockKeywordRegex = new(
        @"^\s*(loop|alt|opt|par|rect|critical|break|else|and|par_over|option)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EndRegex = new(
        @"^\s*end\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BoxRegex = new(
        @"^\s*box\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CreateDestroyRegex = new(
        @"^\s*(create|destroy)\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccTitleRegex = new(
        @"^\s*accTitle\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccDescrRegex = new(
        @"^\s*accDescr(\s*:|\s*\{)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StructCloseRegex = new(
        @"^\s*\}\s*$",
        RegexOptions.Compiled);

    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        DiagramContract Contract = new DiagramContract();
        Contract.Kind = Detection.ProgrammeKindId;

        MermaidConfigurationPipeline.ParseLeadingInitDirectives(Source, Contract.Configuration);

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
                AddStatement(Contract, LineIndex + 1, "sequence.comment", Line);
                continue;
            }

            string ActiveLine = MermaidRemarkAnalyzer.StripTrailingRemark(Line);

            if (!HeaderRecorded)
            {
                if (HeaderRegex.IsMatch(ActiveLine))
                {
                    Contract.Header = ActiveLine.Trim();
                    AddStatement(Contract, LineIndex + 1, "sequence.header", Line);
                    HeaderRecorded = true;
                }

                continue;
            }

            if (AccDescrBlockOpen)
            {
                if (StructCloseRegex.IsMatch(ActiveLine))
                {
                    AddStatement(Contract, LineIndex + 1, "sequence.structClose", Line);
                    AccDescrBlockOpen = false;
                }
                else
                {
                    AddStatement(Contract, LineIndex + 1, "sequence.body", Line);
                }

                continue;
            }

            string LineTypeId = ClassifyLine(ActiveLine);
            if (LineTypeId == "sequence.accDescr" && ActiveLine.Contains('{'))
            {
                AccDescrBlockOpen = true;
            }

            AddStatement(Contract, LineIndex + 1, LineTypeId, Line);
        }

        SequenceContractMapper.Apply(Contract);
        Contract.Configuration.BuildEffectiveConfiguration(Contract.Kind);
        return Contract;
    }

    static string ClassifyLine(string Line)
    {
        if (ParticipantRegex.IsMatch(Line))
        {
            return "sequence.participant";
        }

        if (MessageRegex.IsMatch(Line))
        {
            return "sequence.message";
        }

        if (ActivateRegex.IsMatch(Line))
        {
            return "sequence.activate";
        }

        if (NoteRegex.IsMatch(Line))
        {
            return "sequence.note";
        }

        if (TitleRegex.IsMatch(Line))
        {
            return "sequence.title";
        }

        if (AutonumberRegex.IsMatch(Line))
        {
            return "sequence.autonumber";
        }

        if (BlockKeywordRegex.IsMatch(Line))
        {
            return "sequence.block";
        }

        if (EndRegex.IsMatch(Line))
        {
            return "sequence.end";
        }

        if (BoxRegex.IsMatch(Line))
        {
            return "sequence.box";
        }

        if (CreateDestroyRegex.IsMatch(Line))
        {
            return "sequence.lifecycle";
        }

        if (AccTitleRegex.IsMatch(Line))
        {
            return "sequence.accTitle";
        }

        if (AccDescrRegex.IsMatch(Line))
        {
            return "sequence.accDescr";
        }

        return "sequence.body";
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
