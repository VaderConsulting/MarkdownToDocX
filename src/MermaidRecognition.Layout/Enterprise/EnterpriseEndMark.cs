namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// An explicit path or circle drawn at a relationship end (no SVG marker).
/// </summary>
internal sealed class EnterpriseEndMark
{
    private readonly string _Svg;

    private readonly double _Inset;

    internal EnterpriseEndMark(string Svg, double Inset)
    {
        _Svg = Svg ?? string.Empty;
        _Inset = Inset;
    }

    internal string Svg
    {
        get
        {
            return _Svg;
        }
    }

    internal double Inset
    {
        get
        {
            return _Inset;
        }
    }
}
