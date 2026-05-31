using System.Text;
using MermaidRecognition.Config;

namespace MermaidRecognition;

/// <summary>
/// Analyses Mermaid <c>%%</c> remarks: full-line comments, trailing comments, init directives, and catalogue-remarked blocks.
/// </summary>
public static class MermaidRemarkAnalyzer
{
    /// <summary>
    /// Result of analysing diagram source for remark semantics.
    /// </summary>
    public sealed class MermaidRemarkAnalysis
    {
        public MermaidDiagramRemarkStatus Status { get; init; }

        public string CatalogueUnremarkedSource { get; init; } = string.Empty;

        public int FullLineRemarkCount { get; init; }

        public int TrailingRemarkCount { get; init; }

        public int InitDirectiveCount { get; init; }
    }

    /// <summary>
    /// Returns true when the trimmed line is a leading <c>%%{init:...}%%</c> directive (not a comment).
    /// </summary>
    public static bool IsInitDirectiveLine(string Line)
    {
        string Trimmed = Line.TrimStart();
        if (!Trimmed.StartsWith("%%{", StringComparison.Ordinal))
        {
            return false;
        }

        return MermaidJsonConfigFlattener.TryExtractNextInitBlock(Trimmed, out _, out _);
    }

    /// <summary>
    /// Returns true when the line is entirely a <c>%%</c> comment (after optional whitespace, excluding init directives).
    /// </summary>
    public static bool IsFullLineRemark(string Line)
    {
        if (IsInitDirectiveLine(Line))
        {
            return false;
        }

        string Active = StripTrailingRemark(Line);
        return Active.Length == 0 && FindRemarkStartIndex(Line) >= 0;
    }

    /// <summary>
    /// Removes a trailing <c>%%</c> comment from a line, preserving init directives and code before the remark.
    /// </summary>
    public static string StripTrailingRemark(string Line)
    {
        int RemarkIndex = FindRemarkStartIndex(Line);
        if (RemarkIndex < 0)
        {
            return Line;
        }

        string Before = Line.Substring(0, RemarkIndex);
        if (string.IsNullOrWhiteSpace(Before))
        {
            return string.Empty;
        }

        return Before.TrimEnd();
    }

    /// <summary>
    /// Analyses source for catalogue-remark status and optional unremarked shadow source for recognition.
    /// </summary>
    public static MermaidRemarkAnalysis Analyze(string Source)
    {
        string Normalized = Source.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] Lines = Normalized.Split('\n');
        bool HasActiveSubstantiveLine = false;
        bool HasFullLineRemark = false;
        int FullLineRemarkCount = 0;
        int TrailingRemarkCount = 0;
        int InitDirectiveCount = 0;

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line))
            {
                continue;
            }

            if (IsInitDirectiveLine(Line))
            {
                InitDirectiveCount++;
                HasActiveSubstantiveLine = true;
                continue;
            }

            if (IsFullLineRemark(Line))
            {
                HasFullLineRemark = true;
                FullLineRemarkCount++;
                continue;
            }

            string Active = StripTrailingRemark(Line);
            if (!string.IsNullOrWhiteSpace(Active))
            {
                HasActiveSubstantiveLine = true;
            }

            if (!string.Equals(Active, Line, StringComparison.Ordinal))
            {
                TrailingRemarkCount++;
            }
        }

        MermaidDiagramRemarkStatus Status = MermaidDiagramRemarkStatus.Active;
        string Unremarked = string.Empty;
        if (HasFullLineRemark && !HasActiveSubstantiveLine)
        {
            Status = MermaidDiagramRemarkStatus.CatalogueRemarked;
            Unremarked = UnremarkCatalogueSource(Source);
        }

        return new MermaidRemarkAnalysis
        {
            Status = Status,
            CatalogueUnremarkedSource = Unremarked,
            FullLineRemarkCount = FullLineRemarkCount,
            TrailingRemarkCount = TrailingRemarkCount,
            InitDirectiveCount = InitDirectiveCount
        };
    }

    /// <summary>
    /// Strips catalogue remark prefixes (<c>%%</c> at line start) to recover shadow source for recognition-only passes.
    /// </summary>
    public static string UnremarkCatalogueSource(string Source)
    {
        string Normalized = Source.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] Lines = Normalized.Split('\n');
        StringBuilder Builder = new StringBuilder();

        for (int LineIndex = 0; LineIndex < Lines.Length; LineIndex++)
        {
            string Line = Lines[LineIndex];
            if (string.IsNullOrWhiteSpace(Line))
            {
                if (Builder.Length > 0)
                {
                    Builder.AppendLine();
                }

                continue;
            }

            if (IsInitDirectiveLine(Line))
            {
                Builder.AppendLine(Line.Trim());
                continue;
            }

            string Unremarked = UnremarkLinePrefix(Line);
            if (Unremarked.Length == 0)
            {
                continue;
            }

            Builder.AppendLine(Unremarked);
        }

        return Builder.ToString().TrimEnd();
    }

    static string UnremarkLinePrefix(string Line)
    {
        int Index = 0;
        while (Index < Line.Length && char.IsWhiteSpace(Line[Index]))
        {
            Index++;
        }

        if (Index + 1 >= Line.Length || Line[Index] != '%' || Line[Index + 1] != '%')
        {
            return Line.Trim();
        }

        if (Index + 2 < Line.Length && Line[Index + 2] == '{')
        {
            return Line.Trim();
        }

        Index += 2;
        while (Index < Line.Length && char.IsWhiteSpace(Line[Index]))
        {
            Index++;
        }

        if (Index >= Line.Length)
        {
            return string.Empty;
        }

        return Line.Substring(Index).TrimEnd();
    }

    static int FindRemarkStartIndex(string Line)
    {
        for (int Index = 0; Index < Line.Length - 1; Index++)
        {
            if (Line[Index] != '%' || Line[Index + 1] != '%')
            {
                continue;
            }

            if (Index + 2 < Line.Length && Line[Index + 2] == '{')
            {
                continue;
            }

            return Index;
        }

        return -1;
    }
}
