namespace MDConverter.Options;

/// <summary>
/// Options for DOCX output (template and style mapping).
/// </summary>
public sealed class DocxOptions
{
    /// <summary>
    /// When true, use an embedded template shipped with the converter.
    /// </summary>
    public bool UseBuiltinTemplate { get; set; } = true;

    /// <summary>
    /// When provided, load template from this path instead of built-in.
    /// </summary>
    public string TemplatePath { get; set; } = string.Empty;

    /// <summary>
    /// Mapping from Markdown elements to Word style names.
    /// </summary>
    public StyleMapping? Styles { get; set; }
}
