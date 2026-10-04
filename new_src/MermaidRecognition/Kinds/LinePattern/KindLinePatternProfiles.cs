using System.Text.RegularExpressions;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Kinds.LinePattern;

/// <summary>
/// L1 line patterns derived from vendored Mermaid diagram headers and common body forms.
/// </summary>
public static class KindLinePatternProfiles
{
    /// <summary>
    /// Builds the default L1 recogniser for a programme kind.
    /// </summary>
    public static LinePatternKindRecognizer CreateDefault(
        string ProgrammeKindId,
        string HeaderRegexPattern,
        params KindLinePattern[] ExtraPatterns) => CreateDefault(ProgrammeKindId, HeaderRegexPattern, null, ExtraPatterns);

    public static LinePatternKindRecognizer CreateDefault(
        string ProgrammeKindId,
        string HeaderRegexPattern,
        Action<DiagramContract>? PostParseMapper,
        KindLinePattern[] ExtraPatterns)
    {
        Regex Header = new(HeaderRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return new LinePatternKindRecognizer(ProgrammeKindId, Header, ExtraPatterns, PostParseMapper);
    }

    /// <summary>
    /// Title directive used by several diagram kinds.
    /// </summary>
    public static KindLinePattern TitlePattern(string ProgrammeKindId)
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = ProgrammeKindId + ".title",
            Pattern = new Regex(@"^\s*title\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    /// <summary>
    /// Accessibility title line (flowchart family and others).
    /// </summary>
    public static KindLinePattern AccTitlePattern(string ProgrammeKindId)
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = ProgrammeKindId + ".accTitle",
            Pattern = new Regex(@"^\s*accTitle\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    /// <summary>
    /// Accessibility description line (inline or multiline block open).
    /// </summary>
    public static KindLinePattern AccDescrPattern(string ProgrammeKindId)
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = ProgrammeKindId + ".accDescr",
            Pattern = new Regex(@"^\s*accDescr(\s*:|\s*\{)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }
}
