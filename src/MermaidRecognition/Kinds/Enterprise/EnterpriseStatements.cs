using System.Text;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>Splits source statements without splitting quoted text or changing diagnostic lines.</summary>
internal static class EnterpriseStatements
{
    internal static IEnumerable<(string Text, int Line)> Read(string Source)
    {
        string[] Lines = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        StringBuilder Current = new();
        char Quote = '\0';
        int StartLine = 1;
        for (int Row = 0; Row < Lines.Length; Row++)
        {
            string Line = Lines[Row];
            if (Current.Length == 0) StartLine = Row + 1;
            bool Remainder = Quote == '\0' && System.Text.RegularExpressions.Regex.IsMatch(
                Line.TrimStart(), @"^(title\s|accTitle\s*:|accDescr\s*:)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            for (int Index = 0; Index < Line.Length; Index++)
            {
                char Character = Line[Index];
                if (Remainder) { Current.Append(Character); continue; }
                if (Quote != '\0' && Character == '\\' && Index + 1 < Line.Length)
                {
                    Current.Append(Character).Append(Line[++Index]);
                    continue;
                }
                if (Quote != '\0')
                {
                    Current.Append(Character);
                    if (Character == Quote) Quote = '\0';
                    continue;
                }
                if (Character == '%' && Index + 1 < Line.Length && Line[Index + 1] == '%') break;
                if (Character is '"' or '\'') Quote = Character;
                if (Character == ';')
                {
                    yield return (Current.ToString(), StartLine);
                    Current.Clear();
                    StartLine = Row + 1;
                    // Rest-of-statement directives retain punctuation as text.
                    Remainder = System.Text.RegularExpressions.Regex.IsMatch(Line[(Index + 1)..].TrimStart(),
                        @"^(title\s|accTitle\s*:|accDescr\s*:)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                else Current.Append(Character);
            }
            if (Quote == '\0')
            {
                yield return (Current.ToString(), StartLine);
                Current.Clear();
            }
            else Current.Append('\n');
        }
        if (Current.Length > 0) yield return (Current.ToString(), StartLine);
    }
}
