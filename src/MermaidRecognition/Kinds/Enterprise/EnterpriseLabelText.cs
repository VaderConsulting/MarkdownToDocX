using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>Decodes quoted label escapes once and wraps each explicit line independently.</summary>
public static class EnterpriseLabelText
{
    public static string DecodeQuoted(string Text) => DecodeEntities(Regex.Replace(
        Regex.Replace(Text, @"\\(.)", Match => Match.Groups[1].Value switch
        {
            "n" => "\n",
            "\"" => "\"",
            "'" => "'",
            "\\" => "\\",
            _ => Match.Value
        }), @"<br\s*/?>", "\n", RegexOptions.IgnoreCase));

    private static string DecodeEntities(string Text) => Regex.Replace(Text,
        @"#([A-Za-z][A-Za-z0-9]*|\+?[0-9]+);", Match =>
        {
            string Code = Match.Groups[1].Value;
            if (Code[0] == '+' || char.IsDigit(Code[0]))
            {
                if (int.TryParse(Code, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int Scalar)
                    && (Scalar is 9 or 10 or 13 || Scalar >= 0x20)
                    && Scalar <= 0x10FFFF && !(Scalar >= 0xD800 && Scalar <= 0xDFFF)
                    && Scalar != 0xFFFE && Scalar != 0xFFFF)
                    return char.ConvertFromUtf32(Scalar);
                return Match.Value;
            }
            string Entity = "&" + Code + ";";
            string Decoded = WebUtility.HtmlDecode(Entity);
            return Decoded == Entity ? Match.Value : Decoded;
        });

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
