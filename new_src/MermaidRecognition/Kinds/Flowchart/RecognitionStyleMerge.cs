namespace MermaidRecognition.Flowchart;

/// <summary>
/// Merges <see cref="RecognitionStyle"/> facts onto parse-time records.
/// </summary>
public static class RecognitionStyleMerge
{
    /// <summary>
    /// Copies non-null style properties from <paramref name="Source"/> onto <paramref name="Target"/>.
    /// </summary>
    public static void Apply(RecognitionStyle Source, RecognitionStyle Target)
    {
        if (Source.Fill is not null)
        {
            Target.Fill = Source.Fill;
        }

        if (Source.Stroke is not null)
        {
            Target.Stroke = Source.Stroke;
        }

        if (Source.StrokeWidth.HasValue)
        {
            Target.StrokeWidth = Source.StrokeWidth;
        }

        if (Source.StrokeDasharray is not null)
        {
            Target.StrokeDasharray = Source.StrokeDasharray;
        }

        if (Source.FontFamily is not null)
        {
            Target.FontFamily = Source.FontFamily;
        }

        if (Source.FontSize.HasValue)
        {
            Target.FontSize = Source.FontSize;
        }

        if (Source.FontWeight is not null)
        {
            Target.FontWeight = Source.FontWeight;
        }

        if (Source.TextColor is not null)
        {
            Target.TextColor = Source.TextColor;
        }

        if (Source.Animation is not null)
        {
            Target.Animation = Source.Animation;
        }
    }
}
