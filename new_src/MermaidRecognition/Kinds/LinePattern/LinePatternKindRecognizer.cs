using System.Text.RegularExpressions;
using MermaidRecognition.Contract;

namespace MermaidRecognition.KindRecognition;

/// <summary>
/// L1 recogniser: init/config, header, and line-type catalogue stubs (Mermaid-aligned patterns).
/// </summary>
public sealed class LinePatternKindRecognizer : IKindRecognizer
{
    private readonly string _ProgrammeKindId;
    private readonly Regex _HeaderPattern;
    private readonly KindLinePattern[] _LinePatterns;
    private readonly Action<DiagramContract>? _PostParseMapper;

    public LinePatternKindRecognizer(
        string ProgrammeKindId,
        Regex HeaderPattern,
        KindLinePattern[] LinePatterns,
        Action<DiagramContract>? PostParseMapper = null)
    {
        _ProgrammeKindId = ProgrammeKindId;
        _HeaderPattern = HeaderPattern;
        _LinePatterns = LinePatterns;
        _PostParseMapper = PostParseMapper;
    }

    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        DiagramContract Contract = new DiagramContract();
        Contract.Kind = _ProgrammeKindId;

        MermaidConfigurationPipeline.ParseLeadingInitDirectives(Source, Contract.Configuration);

        string Body = StripLeadingInitBlocks(Source);
        string[] Lines = Body.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool HeaderRecorded = false;

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line))
            {
                continue;
            }

            if (MermaidRemarkAnalyzer.IsFullLineRemark(Line))
            {
                AddStatement(Contract, LineIndex + 1, _ProgrammeKindId + ".comment", Line);
                continue;
            }

            string ActiveLine = MermaidRemarkAnalyzer.StripTrailingRemark(Line);

            if (!HeaderRecorded)
            {
                if (_HeaderPattern.IsMatch(ActiveLine))
                {
                    Contract.Header = ActiveLine.Trim();
                    AddStatement(Contract, LineIndex + 1, _ProgrammeKindId + ".header", Line);
                    HeaderRecorded = true;
                }
                else
                {
                    Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
                        MermaidDiagnosticSeverity.Warning,
                        "recognition.missing-header",
                        "Expected diagram header line for kind " + _ProgrammeKindId + ".",
                        new MermaidSourceSpan(0, 0, LineIndex + 1, 0, LineIndex + 1, Line.Length),
                        null));
                }

                continue;
            }

            string LineTypeId = ClassifyLine(ActiveLine);
            AddStatement(Contract, LineIndex + 1, LineTypeId, Line);
        }

        if (!HeaderRecorded)
        {
            Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
                MermaidDiagnosticSeverity.Warning,
                "recognition.missing-header",
                "No header line found for kind " + _ProgrammeKindId + ".",
                null,
                null));
        }

        Contract.Configuration.BuildEffectiveConfiguration(_ProgrammeKindId);

        if (_PostParseMapper is not null)
        {
            _PostParseMapper(Contract);
        }

        return Contract;
    }

    string ClassifyLine(string Line)
    {
        foreach (KindLinePattern Entry in _LinePatterns)
        {
            if (Entry.Pattern != null && Entry.Pattern.IsMatch(Line))
            {
                return Entry.LineTypeId;
            }
        }

        return _ProgrammeKindId + ".body";
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
