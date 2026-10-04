using System.Globalization;
using System.Text;

namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// Decodes Mermaid flowchart entity codes (for example <c>#quot;</c>, <c>#42;</c>) in label text.
/// </summary>
public static class FlowchartEntityCodeDecoder
{
    /// <summary>
    /// Replaces <c>#name;</c> and <c>#digits;</c> sequences with their display characters.
    /// </summary>
    /// <param name="Text">Label text after delimiter quotes are removed.</param>
    public static string Decode(string Text)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return Text;
        }

        StringBuilder Builder = new StringBuilder(Text.Length);
        int Index = 0;
        while (Index < Text.Length)
        {
            if (Text[Index] == '#' && Index + 2 < Text.Length)
            {
                int SemiIndex = Text.IndexOf(';', Index + 1);
                if (SemiIndex > Index + 1)
                {
                    string Code = Text.Substring(Index + 1, SemiIndex - Index - 1);
                    if (TryDecode(Code, out string Decoded))
                    {
                        Builder.Append(Decoded);
                        Index = SemiIndex + 1;
                        continue;
                    }
                }
            }

            Builder.Append(Text[Index]);
            Index++;
        }

        return Builder.ToString();
    }

    static bool TryDecode(string Code, out string Decoded)
    {
        Decoded = string.Empty;
        if (Code.Length == 0)
        {
            return false;
        }

        if (IsNumericEntityCode(Code))
        {
            int Value = int.Parse(Code.TrimStart('+'), CultureInfo.InvariantCulture);
            if (Value >= 0 && Value <= 0x10FFFF)
            {
                Decoded = char.ConvertFromUtf32(Value);
                return true;
            }

            return false;
        }

        switch (Code.ToLowerInvariant())
        {
            case "quot":
                Decoded = "\"";
                return true;
            case "lt":
                Decoded = "<";
                return true;
            case "gt":
                Decoded = ">";
                return true;
            case "amp":
                Decoded = "&";
                return true;
            case "apos":
                Decoded = "'";
                return true;
            case "nbsp":
                Decoded = "\u00A0";
                return true;
            default:
                return false;
        }
    }

    static bool IsNumericEntityCode(string Code)
    {
        if (Code.Length == 0)
        {
            return false;
        }

        int StartIndex = 0;
        if (Code[0] == '+')
        {
            if (Code.Length == 1)
            {
                return false;
            }

            StartIndex = 1;
        }

        for (int Index = StartIndex; Index < Code.Length; Index++)
        {
            if (!char.IsDigit(Code[Index]))
            {
                return false;
            }
        }

        return true;
    }
}
