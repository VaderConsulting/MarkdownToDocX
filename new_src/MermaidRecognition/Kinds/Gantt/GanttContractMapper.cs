using System.Text;
using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Class;

namespace MermaidRecognition.Kinds.Gantt;

/// <summary>
/// Maps classified gantt source statements into <see cref="DiagramContract"/> entities (L3 G4).
/// </summary>
public static class GanttContractMapper
{
    private static readonly Regex TitleRegex = new(
        @"^\s*title\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SectionRegex = new(
        @"^\s*section\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TaskRegex = new(
        @"^\s*(.+?)\s*:\s*(\w+)",
        RegexOptions.Compiled);

    private static readonly Regex DateFormatRegex = new(
        @"^\s*dateFormat\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExcludesRegex = new(
        @"^\s*excludes\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string MILESTONE_TASK_ID = "milestone";

    enum AccDescrParseState
    {
        None,
        Multiline
    }

    /// <summary>
    /// Populates section groups, task nodes, configuration, and accessibility on an L2-classified gantt contract.
    /// </summary>
    public static void Apply(DiagramContract Contract)
    {
        string? CurrentSectionId = null;
        int SectionIndex = 0;
        AccDescrParseState AccState = AccDescrParseState.None;
        StringBuilder AccDescrBuilder = new StringBuilder();

        foreach (SourceStatementContract Statement in Contract.SourceStatements)
        {
            string LineTypeId = Statement.LineTypeId;
            string RawLine = Statement.RawLine;

            if (AccState == AccDescrParseState.Multiline)
            {
                if (RawLine.Trim() == "}")
                {
                    Contract.Accessibility.Description = AccDescrBuilder.ToString().Trim();
                    AccState = AccDescrParseState.None;
                    AccDescrBuilder.Clear();
                }
                else if (LineTypeId == "gantt.body")
                {
                    if (AccDescrBuilder.Length > 0)
                    {
                        AccDescrBuilder.AppendLine();
                    }

                    AccDescrBuilder.Append(RawLine.Trim());
                }

                continue;
            }

            if (LineTypeId == "gantt.title")
            {
                if (TryParseTitle(RawLine, out string Title))
                {
                    Contract.Configuration.RawDirectiveValuesMutable["gantt.title"] = Title;
                }

                continue;
            }

            if (LineTypeId == "gantt.accTitle")
            {
                if (ClassAccessibilityParser.TryParseAccTitle(RawLine, out string AccTitle))
                {
                    Contract.Accessibility.Title = AccTitle;
                }

                continue;
            }

            if (LineTypeId == "gantt.accDescr")
            {
                if (ClassAccessibilityParser.IsAccDescrBlockStart(RawLine))
                {
                    AccState = AccDescrParseState.Multiline;
                    AccDescrBuilder.Clear();
                }
                else if (ClassAccessibilityParser.TryParseAccDescrInline(RawLine, out string Description))
                {
                    Contract.Accessibility.Description = Description;
                }

                continue;
            }

            if (LineTypeId == "gantt.dateFormat")
            {
                Match Match = DateFormatRegex.Match(RawLine);
                if (Match.Success)
                {
                    Contract.Configuration.RawDirectiveValuesMutable["gantt.dateFormat"] = Match.Groups[1].Value.Trim();
                }

                continue;
            }

            if (LineTypeId == "gantt.excludes")
            {
                Match Match = ExcludesRegex.Match(RawLine);
                if (Match.Success)
                {
                    Contract.Configuration.RawDirectiveValuesMutable["gantt.excludes"] = Match.Groups[1].Value.Trim();
                }

                continue;
            }

            if (LineTypeId == "gantt.inclusiveEndDates")
            {
                Contract.Configuration.RawDirectiveValuesMutable["gantt.inclusiveEndDates"] = "true";
                continue;
            }

            if (LineTypeId == "gantt.section")
            {
                Match Match = SectionRegex.Match(RawLine);
                if (Match.Success)
                {
                    string SectionTitle = Match.Groups[1].Value.Trim();
                    GroupContract Group = new GroupContract
                    {
                        Id = "section-" + SectionIndex.ToString(),
                        Title = SectionTitle,
                        ParentGroupId = null
                    };
                    Contract.GroupsMutable.Add(Group);
                    CurrentSectionId = Group.Id;
                    SectionIndex++;
                }

                continue;
            }

            if (LineTypeId == "gantt.task")
            {
                Match Match = TaskRegex.Match(RawLine);
                if (Match.Success)
                {
                    string TaskLabel = Match.Groups[1].Value.Trim();
                    string TaskId = Match.Groups[2].Value.Trim();
                    NodeContract Node = new NodeContract
                    {
                        Id = TaskId,
                        Label = TaskLabel
                    };
                    if (string.Equals(TaskId, MILESTONE_TASK_ID, StringComparison.OrdinalIgnoreCase))
                    {
                        Node.Shape = "ganttMilestone";
                    }
                    else
                    {
                        Node.Shape = "ganttTask";
                    }

                    Node.ParentGroupId = CurrentSectionId;
                    Contract.NodesMutable.Add(Node);
                }
            }
        }
    }

    static bool TryParseTitle(string RawLine, out string Title)
    {
        Title = string.Empty;
        Match Match = TitleRegex.Match(RawLine);
        if (!Match.Success)
        {
            return false;
        }

        Title = Match.Groups[1].Value.Trim();
        return Title.Length > 0;
    }
}
