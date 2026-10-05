using System;
using System.Globalization;
using System.Text;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// SVG number and text escaping helpers for enterprise diagrams.
/// </summary>
internal static class EnterpriseSvgFormat
{
    internal static string Number(double Value)
    {
        double Rounded = Math.Round(Value * 100.0) / 100.0;
        return Rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    internal static string Escape(string? Value)
    {
        if (string.IsNullOrEmpty(Value))
        {
            return string.Empty;
        }

        StringBuilder Builder = new StringBuilder(Value.Length);
        foreach (char Character in Value)
        {
            switch (Character)
            {
                case '&':
                    Builder.Append("&amp;");
                    break;
                case '<':
                    Builder.Append("&lt;");
                    break;
                case '>':
                    Builder.Append("&gt;");
                    break;
                case '"':
                    Builder.Append("&quot;");
                    break;
                default:
                    Builder.Append(Character);
                    break;
            }
        }

        return Builder.ToString();
    }
}
