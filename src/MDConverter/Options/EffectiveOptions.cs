namespace MDConverter.Options;

/// <summary>
/// Fully resolved options after profile defaults and overlay. All sub-option objects are non-null.
/// </summary>
public sealed class EffectiveOptions
{
    public MarkdownOptions Markdown { get; set; } = new MarkdownOptions();

    public DocxOptions Docx { get; set; } = new DocxOptions();

    public ImageOptions Images { get; set; } = new ImageOptions();

    public LinkOptions Links { get; set; } = new LinkOptions();

    public TableOptions Tables { get; set; } = new TableOptions();

    public MermaidOptions Mermaid { get; set; } = new MermaidOptions();
}
