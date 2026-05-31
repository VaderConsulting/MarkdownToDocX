namespace MermaidRecognition.Contract;

/// <summary>
/// Mermaid <c>linkStyle</c> directive targeting default or indexed edges by creation index (§7.1.1).
/// </summary>
public sealed class LinkStyleDirective
{
    private bool _IsDefault;

    public bool IsDefault
    {
        get
        {
            return _IsDefault;
        }
    }

    private readonly List<int> _EdgeIndexes = new List<int>();

    public IReadOnlyList<int> EdgeIndexes
    {
        get
        {
            return _EdgeIndexes;
        }
    }

    public List<int> EdgeIndexesMutable
    {
        get
        {
            return _EdgeIndexes;
        }
    }

    private readonly RecognitionStyle _Style = new RecognitionStyle();

    public RecognitionStyle Style
    {
        get
        {
            return _Style;
        }
    }

    private string? _Curve;

    public string? Curve
    {
        get
        {
            return _Curve;
        }
        set
        {
            _Curve = value;
        }
    }

    /// <summary>
    /// Indexed <c>linkStyle N</c> mutation.
    /// </summary>
    public static LinkStyleDirective ForIndex(int EdgeIndex, RecognitionStyle Style, string? Curve)
    {
        LinkStyleDirective Directive = new LinkStyleDirective();
        Directive._IsDefault = false;
        Directive._EdgeIndexes.Add(EdgeIndex);
        CopyStyle(Style, Directive._Style);
        Directive._Curve = Curve;
        return Directive;
    }

    /// <summary>
    /// Indexed <c>linkStyle N,M</c> mutation.
    /// </summary>
    public static LinkStyleDirective ForIndexes(IReadOnlyList<int> EdgeIndexes, RecognitionStyle Style, string? Curve)
    {
        LinkStyleDirective Directive = new LinkStyleDirective();
        Directive._IsDefault = false;
        foreach (int EdgeIndex in EdgeIndexes)
        {
            Directive._EdgeIndexes.Add(EdgeIndex);
        }

        CopyStyle(Style, Directive._Style);
        Directive._Curve = Curve;
        return Directive;
    }

    /// <summary>
    /// <c>linkStyle default</c> mutation.
    /// </summary>
    public static LinkStyleDirective ForDefault(RecognitionStyle Style, string? Curve)
    {
        LinkStyleDirective Directive = new LinkStyleDirective();
        Directive._IsDefault = true;
        CopyStyle(Style, Directive._Style);
        Directive._Curve = Curve;
        return Directive;
    }

    private LinkStyleDirective()
    {
    }

    private static void CopyStyle(RecognitionStyle Source, RecognitionStyle Target)
    {
        Target.Fill = Source.Fill;
        Target.Stroke = Source.Stroke;
        Target.StrokeWidth = Source.StrokeWidth;
        Target.StrokeDasharray = Source.StrokeDasharray;
        Target.FontFamily = Source.FontFamily;
        Target.FontSize = Source.FontSize;
        Target.FontWeight = Source.FontWeight;
        Target.TextColor = Source.TextColor;
    }
}
