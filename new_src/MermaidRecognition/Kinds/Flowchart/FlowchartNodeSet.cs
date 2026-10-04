namespace MermaidRecognition.Kinds.Flowchart;

/// <summary>
/// One side of a link (possibly several node ids).
/// </summary>
public sealed class FlowchartNodeSet
{
    private readonly List<string> _Items = [];

    public IReadOnlyList<string> Items => _Items;

    public List<string> ItemsMutable => _Items;

    public string? First => _Items.Count == 0 ? null : _Items[0];

    public string? Last => _Items.Count == 0 ? null : _Items[_Items.Count - 1];
}
