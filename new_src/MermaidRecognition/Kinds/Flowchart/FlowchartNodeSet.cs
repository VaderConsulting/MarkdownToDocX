namespace MermaidRecognition.Flowchart;

/// <summary>
/// One side of a link (possibly several node ids joined with <c>&</c>).
/// </summary>
public sealed class FlowchartNodeSet
{
    private readonly List<string> _Items = new List<string>();

    public IReadOnlyList<string> Items
    {
        get
        {
            return _Items;
        }
    }

    public List<string> ItemsMutable
    {
        get
        {
            return _Items;
        }
    }

    public string? First
    {
        get
        {
            if (_Items.Count == 0)
            {
                return null;
            }

            return _Items[0];
        }
    }

    public string? Last
    {
        get
        {
            if (_Items.Count == 0)
            {
                return null;
            }

            return _Items[_Items.Count - 1];
        }
    }
}
