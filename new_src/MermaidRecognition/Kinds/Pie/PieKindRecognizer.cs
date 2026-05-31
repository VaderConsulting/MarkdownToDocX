using System.Text.RegularExpressions;

namespace MermaidRecognition.Pie;

/// <summary>
/// Pie diagram recognition (vendored <c>pieDetector.ts</c> / pie parser sections).
/// </summary>
public sealed class PieKindRecognizer : IKindRecognizer
{
    private static readonly Regex HeaderLineRegex = new(
        @"^\s*pie(?:\s+showData)?(?:\s+title\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new(
        @"^\s*title\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SectionRegex = new(
        @"^\s*(?:""[^""]+""|'[^']+')\s*:\s*\d+(?:\.\d+)?",
        RegexOptions.Compiled);

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
                AddStatement(Contract, LineIndex + 1, "pie.comment", Line);
                continue;
            }

            string ActiveLine = MermaidRemarkAnalyzer.StripTrailingRemark(Line);

            if (!HeaderRecorded)
            {
                Match HeaderMatch = HeaderLineRegex.Match(ActiveLine);
                if (HeaderMatch.Success)
                {
                    Contract.Header = ActiveLine.Trim();
                    AddStatement(Contract, LineIndex + 1, "pie.header", Line);
                    HeaderRecorded = true;

                    if (HeaderMatch.Groups[1].Success && HeaderMatch.Groups[1].Value.Trim().Length > 0)
                    {
                        string InlineTitle = HeaderMatch.Groups[1].Value.Trim();
                        AddStatement(Contract, LineIndex + 1, "pie.title", "title " + InlineTitle);
                    }
                }

                continue;
            }

            if (AccDescrBlockOpen)
            {
                if (StructCloseRegex.IsMatch(ActiveLine))
                {
                    AddStatement(Contract, LineIndex + 1, "pie.structClose", Line);
                    AccDescrBlockOpen = false;
                }
                else
                {
                    AddStatement(Contract, LineIndex + 1, "pie.body", Line);
                }

                continue;
            }

            if (AccTitleRegex.IsMatch(ActiveLine))
            {
                AddStatement(Contract, LineIndex + 1, "pie.accTitle", Line);
            }
            else if (AccDescrRegex.IsMatch(ActiveLine))
            {
                AddStatement(Contract, LineIndex + 1, "pie.accDescr", Line);
                if (ActiveLine.Contains('{'))
                {
                    AccDescrBlockOpen = true;
                }
            }
            else if (TitleRegex.IsMatch(ActiveLine))
            {
                AddStatement(Contract, LineIndex + 1, "pie.title", Line);
            }
            else if (SectionRegex.IsMatch(ActiveLine))
            {
                AddStatement(Contract, LineIndex + 1, "pie.section", Line);
            }
            else
            {
                AddStatement(Contract, LineIndex + 1, "pie.body", Line);
                Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
                    MermaidDiagnosticSeverity.Info,
                    "recognition.pie.unclassified-line",
                    "Unclassified pie line.",
                    new MermaidSourceSpan(0, 0, LineIndex + 1, 0, LineIndex + 1, Line.Length),
                    null));
            }
        }

        PieContractMapper.Apply(Contract);
        Contract.Configuration.BuildEffectiveConfiguration(Contract.Kind);
        return Contract;
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
