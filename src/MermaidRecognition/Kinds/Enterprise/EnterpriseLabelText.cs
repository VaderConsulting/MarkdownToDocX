using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>Decodes quoted label escapes once and wraps each explicit line independently.</summary>
public static class EnterpriseLabelText
{
    public static string DecodeQuoted(string Text) => Regex.Replace(
        Regex.Replace(Text, @"\\(.)", Match => Match.Groups[1].Value switch
        {
            "n" => "\n",
            "\"" => "\"",
            "'" => "'",
            "\\" => "\\",
            _ => Match.Value
        }), @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

    public static List<string> Wrap(string Text, int Width)
    {
        List<string> Lines = new();
        foreach (string ExplicitLine in Text.Replace("\r\n", "\n").Split('\n'))
        {
            string Current = string.Empty;
            foreach (string Word in ExplicitLine.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                string Candidate = Current.Length == 0 ? Word : Current + " " + Word;
                if (Current.Length > 0 && Candidate.Length > Width)
                {
                    Lines.Add(Current);
                    Current = Word;
                }
                else Current = Candidate;
            }
            Lines.Add(Current);
        }
        return Lines;
    }
}
