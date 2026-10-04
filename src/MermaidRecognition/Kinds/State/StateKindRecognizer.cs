using System.Text.RegularExpressions;

using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.State;

/// <summary>
/// State diagram L2 recognition (vendored <c>stateDiagram.jison</c>).
/// </summary>
public sealed class StateKindRecognizer : IKindRecognizer
{
    private static readonly Regex HeaderRegex = new(
        @"^\s*stateDiagram(?:-v2)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HashCommentRegex = new(@"^\s*#", RegexOptions.Compiled);
    private static readonly Regex TransitionRegex = new(@"-->", RegexOptions.Compiled);
    private static readonly Regex StateRegex = new(@"^\s*state\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ForkJoinRegex = new(@"<<(?:fork|join|choice)>>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NoteRegex = new(@"^\s*note\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClassDefRegex = new(@"^\s*classDef\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleRegex = new(@"^\s*style\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CssClassRegex = new(@"^\s*class\s+\w", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StructOpenRegex = new(@"^\s*\{\s*$", RegexOptions.Compiled);
    private static readonly Regex StructCloseRegex = new(@"^\s*\}\s*$", RegexOptions.Compiled);
    private static readonly Regex HideEmptyRegex = new(@"^\s*hide empty description", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DirectionRegex = new(
        @"^\s*direction\s+(TB|BT|RL|LR)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClickRegex = new(@"^\s*click\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccTitleRegex = new(@"^\s*accTitle\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccDescrRegex = new(@"^\s*accDescr(\s*:|\s*\{)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        DiagramContract Contract = new DiagramContract
        {
            Kind = Detection.ProgrammeKindId
        };
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

            if (IsCommentLine(Line))
            {
                AddStatement(Contract, LineIndex + 1, "stateDiagram.comment", Line);
                continue;
            }

            string ActiveLine = MermaidRemarkAnalyzer.StripTrailingRemark(Line);

            if (!HeaderRecorded)
            {
                if (HeaderRegex.IsMatch(ActiveLine))
                {
                    Contract.Header = ActiveLine.Trim();
                    AddStatement(Contract, LineIndex + 1, "stateDiagram.header", Line);
                    HeaderRecorded = true;
                }

                continue;
            }

            if (AccDescrBlockOpen)
            {
                if (StructCloseRegex.IsMatch(ActiveLine))
                {
                    AddStatement(Contract, LineIndex + 1, "stateDiagram.structClose", Line);
                    AccDescrBlockOpen = false;
                }
                else
                {
                    AddStatement(Contract, LineIndex + 1, "stateDiagram.body", Line);
                }

                continue;
            }

            string LineTypeId = ClassifyLine(ActiveLine);
            if (LineTypeId == "stateDiagram.accDescr" && ActiveLine.Contains('{'))
            {
                AccDescrBlockOpen = true;
            }

            AddStatement(Contract, LineIndex + 1, LineTypeId, Line);
        }

        StateContractMapper.Apply(Contract);
        Contract.Configuration.BuildEffectiveConfiguration(Contract.Kind);
        return Contract;
    }

    static string ClassifyLine(string Line)
    {
        if (AccTitleRegex.IsMatch(Line))
        {
            return "stateDiagram.accTitle";
        }

        if (AccDescrRegex.IsMatch(Line))
        {
            return "stateDiagram.accDescr";
        }

        if (DirectionRegex.IsMatch(Line))
        {
            return "stateDiagram.direction";
        }

        if (HideEmptyRegex.IsMatch(Line))
        {
            return "stateDiagram.hideEmpty";
        }

        if (ClassDefRegex.IsMatch(Line))
        {
            return "stateDiagram.classDef";
        }

        if (StyleRegex.IsMatch(Line))
        {
            return "stateDiagram.style";
        }

        if (CssClassRegex.IsMatch(Line))
        {
            return "stateDiagram.cssClass";
        }

        if (ClickRegex.IsMatch(Line))
        {
            return "stateDiagram.click";
        }

        if (NoteRegex.IsMatch(Line))
        {
            return "stateDiagram.note";
        }

        if (StateRegex.IsMatch(Line) || ForkJoinRegex.IsMatch(Line))
        {
            return "stateDiagram.state";
        }

        if (TransitionRegex.IsMatch(Line))
        {
            return "stateDiagram.transition";
        }

        if (StructOpenRegex.IsMatch(Line))
        {
            return "stateDiagram.structOpen";
        }

        if (StructCloseRegex.IsMatch(Line))
        {
            return "stateDiagram.structClose";
        }

        return "stateDiagram.body";
    }

    static bool IsCommentLine(string Line)
    {
        if (HashCommentRegex.IsMatch(Line))
        {
            return true;
        }

        return MermaidRemarkAnalyzer.IsFullLineRemark(Line);
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
        SourceStatementContract Statement = new SourceStatementContract
        {
            LineNumber = LineNumber,
            LineTypeId = LineTypeId,
            RawLine = RawLine
        };
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
