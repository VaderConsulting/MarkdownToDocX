namespace MermaidRecognition.Layout;

/// <summary>
/// Layout-space bounds of a subgraph cluster box (Y-up, same axes as Sugiyama output).
/// </summary>
public sealed class FlowchartSubgraphLayoutRect
{
    public string GroupId
    {
        get
        {
            return _GroupId;
        }
        set
        {
            _GroupId = value;
        }
    }

    private string _GroupId = string.Empty;

    public double Left
    {
        get
        {
            return _Left;
        }
        set
        {
            _Left = value;
        }
    }

    private double _Left;

    public double Right
    {
        get
        {
            return _Right;
        }
        set
        {
            _Right = value;
        }
    }

    private double _Right;

    public double Bottom
    {
        get
        {
            return _Bottom;
        }
        set
        {
            _Bottom = value;
        }
    }

    private double _Bottom;

    public double Top
    {
        get
        {
            return _Top;
        }
        set
        {
            _Top = value;
        }
    }

    private double _Top;

    public string? Title
    {
        get
        {
            return _Title;
        }
        set
        {
            _Title = value;
        }
    }

    private string? _Title;
}
