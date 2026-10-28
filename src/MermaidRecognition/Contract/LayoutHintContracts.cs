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
    Push,
    NorthWest,
    NorthEast,
    SouthEast,
    SouthWest,
    At
}

/// <summary>Parsed placement hint attached to one node or group declaration.</summary>
public sealed class PlacementHintContract
{
    public PlacementHintKind Kind { get; set; }

    public string? PeerId { get; set; }

    public int Offset { get; set; }

    /// <summary>Horizontal gap for diagonal placement; omitted uses Offset on both axes.</summary>
    public int? ColumnOffset { get; set; }

    /// <summary>Absolute logical-cell origin for an At hint, independent of placement peers.</summary>
    public int CellRow { get; set; }
    public int CellColumn { get; set; }

    public bool IsDiagonal => Kind is PlacementHintKind.NorthWest or PlacementHintKind.NorthEast
        or PlacementHintKind.SouthEast or PlacementHintKind.SouthWest;

    public LayoutHintDirection? Direction { get; set; }

    public int SourceLine { get; set; }

    public bool IsActive { get; set; } = true;

    public string? UnhonouredReason { get; set; }
}

/// <summary>Parsed side and track directions attached to one relationship.</summary>
public sealed class RoutingHintContract
{
    public LayoutHintDirection? Leave { get; set; }

    /// <summary>Signed connection-grid steps from the source side's centre; requires Leave.</summary>
    public int? LeavePoint { get; set; }

    public LayoutHintDirection? Enter { get; set; }

    /// <summary>Signed connection-grid steps from the target side's centre; requires Enter.</summary>
    public int? EnterPoint { get; set; }

    public LayoutHintDirection? Travel { get; set; }

    public int SourceLine { get; set; }

    public bool IsActive { get; set; } = true;

    public string? UnhonouredReason { get; set; }
}
