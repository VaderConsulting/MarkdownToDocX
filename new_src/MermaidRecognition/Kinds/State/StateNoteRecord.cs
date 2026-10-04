namespace MermaidRecognition.Kinds.State;

/// <summary>
/// Parse-time state diagram note record.
/// </summary>
public sealed class StateNoteRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get => _Id; set => _Id = value;
    }

    private string _Placement = string.Empty;

    public string Placement
    {
        get => _Placement; set => _Placement = value;
    }

    private string _AnchorIds = string.Empty;

    public string AnchorIds
    {
        get => _AnchorIds; set => _AnchorIds = value;
    }

    private string _Text = string.Empty;

    public string Text
    {
        get => _Text; set => _Text = value;
    }
}
