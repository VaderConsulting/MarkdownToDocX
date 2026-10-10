using System.Text.RegularExpressions;
using MermaidRecognition.Config;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>Reads optional configuration while preserving original source line numbers.</summary>
internal static class EnterpriseConfiguration
{
    internal static string Prepare(string Source, EnterpriseDocument Document)
    {
        if (MermaidYamlFrontMatterFlattener.TryExtractLeadingFrontMatter(Source, Document.Configuration, out string Remaining))
        {
            string Preamble = Source[..(Source.Length - Remaining.Length)];
            Match Title = Regex.Match(Preamble, @"^title:\s*(.*?)\s*$", RegexOptions.Multiline);
            if (Title.Success) Document.Title = Title.Groups[1].Value.Trim().Trim('"', '\'');
            Source = PreserveLines(Preamble) + Remaining;
        }
        return Regex.Replace(Source, @"^[\t ]*%%\{.*?}%%", Match =>
        {
            MermaidJsonConfigFlattener.TryParseInitBlock(Match.Value.TrimStart(), Document.Configuration);
            return PreserveLines(Match.Value);
        }, RegexOptions.Multiline | RegexOptions.Singleline);
    }

    private static string PreserveLines(string Text) => new(Text.Where(Character => Character is '\r' or '\n').ToArray());
}
