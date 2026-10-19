namespace MermaidRecognition.Kinds.Enterprise;

using MermaidRecognition.Contract;

/// <summary>A free-text drawing item with no Enterprise model meaning.</summary>
public sealed class EnterpriseNote
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool IsMarkdownLabel { get; set; }

    public string? ParentId { get; set; }

    public int LineNumber { get; set; }

    public PlacementHintContract? PlacementHint { get; set; }
}
