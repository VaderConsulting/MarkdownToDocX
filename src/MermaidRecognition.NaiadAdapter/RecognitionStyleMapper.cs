namespace MermaidRecognition.NaiadAdapter;

/// <summary>
/// Maps recognition <see cref="RecognitionStyle"/> to Naiad <see cref="Style"/>.
/// </summary>
public static class RecognitionStyleMapper
{
    /// <summary>
    /// Converts a recognition style to a Naiad style instance.
    /// </summary>
    public static Style ToNaiadStyle(RecognitionStyle Source)
    {
        Style Target = new Style
        {
            Fill = NormalizeCssColor(Source.Fill),
            Stroke = NormalizeCssColor(Source.Stroke),
            StrokeWidth = Source.StrokeWidth,
            StrokeDasharray = Source.StrokeDasharray,
            FontFamily = Source.FontFamily,
            FontSize = Source.FontSize,
            FontWeight = Source.FontWeight,
            TextColor = NormalizeCssColor(Source.TextColor)
        };
        return Target;
    }

    static string? NormalizeCssColor(string? Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            return Value;
        }

        string Trimmed = Value.Trim();
        if (Trimmed.Length == 4 && Trimmed[0] == '#')
        {
            // Expand CSS shorthand (#rgb) to #rrggbb for renderers that do not support shorthand.
            char r = Trimmed[1];
            char g = Trimmed[2];
            char b = Trimmed[3];
            return "#" + r + r + g + g + b + b;
        }

        return Trimmed;
    }
}
