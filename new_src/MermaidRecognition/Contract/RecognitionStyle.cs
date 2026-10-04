namespace MermaidRecognition.Contract;

/// <summary>
/// CSS-oriented style facts for classDef, style, and linkStyle directives (recognition contract).
/// </summary>
public sealed class RecognitionStyle
{
    private string? _Fill;

    public string? Fill
    {
        get => _Fill; set => _Fill = value;
    }

    private string? _Stroke;

    public string? Stroke
    {
        get => _Stroke; set => _Stroke = value;
    }

    private double? _StrokeWidth;

    public double? StrokeWidth
    {
        get => _StrokeWidth; set => _StrokeWidth = value;
    }

    private string? _StrokeDasharray;

    public string? StrokeDasharray
    {
        get => _StrokeDasharray; set => _StrokeDasharray = value;
    }

    private string? _FontFamily;

    public string? FontFamily
    {
        get => _FontFamily; set => _FontFamily = value;
    }

    private double? _FontSize;

    public double? FontSize
    {
        get => _FontSize; set => _FontSize = value;
    }

    private string? _FontWeight;

    public string? FontWeight
    {
        get => _FontWeight; set => _FontWeight = value;
    }

    private string? _TextColor;

    public string? TextColor
    {
        get => _TextColor; set => _TextColor = value;
    }

    private string? _Animation;

    /// <summary>
    /// CSS <c>animation</c> value from <c>classDef</c> (F88 edge class styling).
    /// </summary>
    public string? Animation
    {
        get => _Animation; set => _Animation = value;
    }
}
