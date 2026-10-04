namespace MermaidRecognition.Contract;

/// <summary>
/// A repeated or merged node declaration in source order.
/// </summary>
public sealed class MermaidDeclaration(string Kind, string RawText, MermaidSourceSpan SourceSpan)
{
    private readonly string _Kind = Kind;

    public string Kind => _Kind;

    private readonly string _RawText = RawText;

    public string RawText => _RawText;

    private readonly MermaidSourceSpan _SourceSpan = SourceSpan;

    public MermaidSourceSpan SourceSpan => _SourceSpan;
}

