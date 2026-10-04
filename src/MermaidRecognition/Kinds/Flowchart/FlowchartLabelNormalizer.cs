namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Normalises flowchart labels when bracket contents use Mermaid quoted delimiter syntax.
/// </summary>
public static class FlowchartLabelNormalizer
{
    /// <summary>
    /// Returns display text without outer delimiter quotes and with escape sequences resolved.
    /// </summary>
    /// <param name="Text">Raw text captured between shape delimiters.</param>
    public static string Normalize(string Text)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return Text;
        }

        string Trimmed = Text.Trim();
        string Stripped = StripOuterDelimiterQuotes(Trimmed);
        string Unescaped = Stripped.Replace("\\\"", "\"").Replace("\\\\", "\\");
        string Decoded = FlowchartEntityCodeDecoder.Decode(Unescaped);
        return Decoded.Trim();
    }

    static string StripOuterDelimiterQuotes(string Text)
    {
        string Working = Text;
        while (Working.Length >= 2)
        {
            char Open = Working[0];
            char Close = Working[Working.Length - 1];
            bool IsAsciiDouble = Open == '"' && Close == '"';
            bool IsAsciiSingle = Open == '\'' && Close == '\'';
            bool IsCurlyDouble = Open == '\u201c' && Close == '\u201d';
            bool IsCurlySingle = Open == '\u2018' && Close == '\u2019';
            if (IsAsciiDouble || IsAsciiSingle || IsCurlyDouble || IsCurlySingle)
            {
                Working = Working.Substring(1, Working.Length - 2).Trim();
                continue;
            }

            break;
        }

        return Working;
    }
}
