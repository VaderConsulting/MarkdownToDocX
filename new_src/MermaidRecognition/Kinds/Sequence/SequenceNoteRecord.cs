namespace MermaidRecognition.Sequence;

/// <summary>
/// Parse-time sequence diagram note record.
/// </summary>
public sealed class SequenceNoteRecord
{
    private string _Id = string.Empty;

    public string Id
    {
        get
        {
            return _Id;
        }
        set
        {
            _Id = value;
        }
    }

    private string _Placement = string.Empty;

    public string Placement
    {
        get
        {
            return _Placement;
        }
        set
        {
            _Placement = value;
        }
    }

    private string _AnchorIds = string.Empty;

    public string AnchorIds
    {
        get
        {
            return _AnchorIds;
        }
        set
        {
            _AnchorIds = value;
        }
    }

    private string _Text = string.Empty;

    public string Text
    {
        get
        {
            return _Text;
        }
        set
        {
            _Text = value;
        }
    }
}
