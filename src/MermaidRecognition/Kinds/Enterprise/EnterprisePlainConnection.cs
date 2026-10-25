namespace MermaidRecognition.Kinds.Enterprise;

using MermaidRecognition.Contract;

/// <summary>An undirected drawing line with no Enterprise relationship meaning.</summary>
public sealed class EnterprisePlainConnection
{
    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    /// <summary>Gets or sets the minimum rank span. Three dashes stay at 1; each further dash adds one, up to 10.</summary>
    public int MinLength { get; set; } = 1;

    public RoutingHintContract? RoutingHint { get; set; }
}
