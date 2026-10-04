namespace MermaidRecognition.Core;

/// <summary>
/// Whether a fenced Mermaid block is active for render gates or catalogue-remarked (§9.1).
/// </summary>
public enum MermaidDiagramRemarkStatus
{
    /// <summary>
    /// At least one substantive (non-remark) line exists; render and recognition gates apply.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Every non-empty line is a full-line <c>%%</c> remark; considered but rejected for render.
    /// Recognition may still parse <see cref="MermaidRemarkAnalyzer.UnremarkCatalogueSource"/>.
    /// </summary>
    CatalogueRemarked = 1
}
