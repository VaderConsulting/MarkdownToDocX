namespace MermaidRecognition.Contract;

/// <summary>
/// A repeated or merged node declaration in source order.
/// </summary>
public sealed class MermaidDeclaration
{
    public MermaidDeclaration(string Kind, string RawText, MermaidSourceSpan SourceSpan)
    {
        _Kind = Kind;
        _RawText = RawText;
        _SourceSpan = SourceSpan;
    }

    private readonly string _Kind;

    public string Kind => _Kind;

    private readonly string _RawText;

    public string RawText => _RawText;

    private readonly MermaidSourceSpan _SourceSpan;

    public MermaidSourceSpan SourceSpan => _SourceSpan;
}

