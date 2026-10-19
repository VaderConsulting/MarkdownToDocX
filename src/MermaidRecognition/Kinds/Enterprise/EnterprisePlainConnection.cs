namespace MermaidRecognition.Kinds.Enterprise;

using MermaidRecognition.Contract;

/// <summary>An undirected drawing line with no Enterprise relationship meaning.</summary>
public sealed class EnterprisePlainConnection
{
    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public RoutingHintContract? RoutingHint { get; set; }
}
