namespace MermaidRecognition.Contract;

/// <summary>Screen direction used by explicit placement and routing hints.</summary>
public enum LayoutHintDirection
{
    North,
    South,
    East,
    West
}

/// <summary>Kind of relative or boundary placement requested for a node or group.</summary>
public enum PlacementHintKind
{
    Above,
    Below,
    LeftOf,
    RightOf,
    Push
}

/// <summary>Parsed placement hint attached to one node or group declaration.</summary>
public sealed class PlacementHintContract
{
    public PlacementHintKind Kind { get; set; }

    public string? PeerId { get; set; }

    public int Offset { get; set; }

    public LayoutHintDirection? Direction { get; set; }

    public int SourceLine { get; set; }

    public bool IsActive { get; set; } = true;

    public string? UnhonouredReason { get; set; }
}

/// <summary>Parsed side and track directions attached to one relationship.</summary>
public sealed class RoutingHintContract
{
    public LayoutHintDirection? Leave { get; set; }

    public LayoutHintDirection? Enter { get; set; }

    public LayoutHintDirection? Travel { get; set; }

    public int SourceLine { get; set; }

    public bool IsActive { get; set; } = true;

    public string? UnhonouredReason { get; set; }
}
