namespace MDConverter.Model;

/// <summary>Plain text inline.</summary>
public sealed class TextRun(string Text) : InlineNode
{
    public string Text { get; } = Text ?? string.Empty;
}
