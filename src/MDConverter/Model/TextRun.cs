namespace MDConverter.Model;

/// <summary>Plain text inline.</summary>
public sealed class TextRun : InlineNode
{
    public string Text { get; }

    public TextRun(string Text) => this.Text = Text ?? string.Empty;
}
