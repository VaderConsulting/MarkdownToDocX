using System.Text;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Class;
using MermaidRecognition.Kinds.Sequence;

namespace MermaidRecognition.Kinds.Pie;

/// <summary>
/// Maps classified pie chart source statements into <see cref="DiagramContract"/> entities (L3 G4/G7/G9/G11).
/// </summary>
public static class PieContractMapper
{
    private static readonly Regex ShowDataHeaderRegex = new(
        @"^\s*pie\s+showData\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SectionRegex = new(
        @"^\s*(?:""([^""]+)""|'([^']+)')\s*:\s*(\d+(?:\.\d+)?)\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Populates pie slice nodes, title configuration, showData flag, and accessibility on an L2-classified pie chart contract.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        int SliceIndex = 0;
        string? AccessibilityTitle = null;
        string? AccessibilityDescription = null;
        AccDescrParseState AccState = AccDescrParseState.None;
        StringBuilder AccDescrBuilder = new StringBuilder();

        foreach (SourceStatementContract Statement in Contract.SourceStatements)
        {
            string LineTypeId = Statement.LineTypeId;
            string RawLine = Statement.RawLine;

            if (AccState == AccDescrParseState.Multiline)
            {
                if (LineTypeId.EndsWith(".structClose", StringComparison.Ordinal))
                {
                    AccessibilityDescription = AccDescrBuilder.ToString().Trim();
                    AccState = AccDescrParseState.None;
                    AccDescrBuilder.Clear();
                }
                else if (LineTypeId.EndsWith(".body", StringComparison.Ordinal))
                {
                    if (AccDescrBuilder.Length > 0)
                    {
                        AccDescrBuilder.AppendLine();
                    }

                    AccDescrBuilder.Append(RawLine.Trim());
                }

                continue;
            }

            if (LineTypeId.EndsWith(".accTitle", StringComparison.Ordinal))
            {
                if (ClassAccessibilityParser.TryParseAccTitle(RawLine, out string Title))
                {
                    AccessibilityTitle = Title;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".accDescr", StringComparison.Ordinal))
            {
                if (ClassAccessibilityParser.IsAccDescrBlockStart(RawLine))
                {
                    AccState = AccDescrParseState.Multiline;
                    AccDescrBuilder.Clear();
                }
                else if (ClassAccessibilityParser.TryParseAccDescrInline(RawLine, out string Description))
                {
                    AccessibilityDescription = Description;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".header", StringComparison.Ordinal))
            {
                if (ShowDataHeaderRegex.IsMatch(RawLine.Trim()))
                {
                    Contract.Configuration.RawDirectiveValuesMutable["pie.showData"] = "true";
                }

                continue;
            }

            if (LineTypeId.EndsWith(".title", StringComparison.Ordinal))
            {
                if (SequenceTitleParser.TryParse(RawLine, out string Title))
                {
                    Contract.Configuration.RawDirectiveValuesMutable["pie.title"] = Title;
                }

                continue;
            }

            if (LineTypeId.EndsWith(".section", StringComparison.Ordinal))
            {
                if (TryParseSection(RawLine, out string Label, out string Value))
                {
                    NodeContract Node = new NodeContract
                    {
                        Id = "slice-" + SliceIndex.ToString(),
                        Label = Label,
                        Shape = "slice",
                        ShapeSource = "pie"
                    };
                    Node.MetadataMutable["pieValue"] = new RawMetadata("pieValue", Value);
                    Contract.NodesMutable.Add(Node);
                    SliceIndex++;
                }
            }
        }

        Contract.Accessibility.Title = AccessibilityTitle;
        Contract.Accessibility.Description = AccessibilityDescription;
    }

    static bool TryParseSection(string Line, out string Label, out string Value)
    {
        Label = string.Empty;
        Value = string.Empty;
        Match Match = SectionRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        Label = Match.Groups[1].Success ? Match.Groups[1].Value : Match.Groups[2].Value;
        Value = Match.Groups[3].Value;
        return Label.Length > 0 && Value.Length > 0;
    }

    enum AccDescrParseState
    {
        None,
        Multiline
    }
}
