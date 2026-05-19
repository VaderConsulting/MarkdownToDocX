namespace MDConverter.Options;

/// <summary>
/// Resolves effective options: apply profile defaults, then overlay explicit sub-options (property-by-property).
/// </summary>
public static class OptionsResolver
{
    private const string DEFAULT_PROFILE = "default";
    private const double DEFAULT_MAX_WIDTH_CM = 16.0;
    private const string DEFAULT_MISSING_IMAGE_FORMAT = "[Missing image: {AltText}]";

    /// <summary>
    /// Returns fully resolved options. Profile (or "default") supplies defaults; non-null sub-options are overlaid property-by-property.
    /// </summary>
    public static EffectiveOptions Resolve(MarkdownToDocxOptions Options)
    {
        string Profile = string.IsNullOrWhiteSpace(Options?.Profile)
            ? DEFAULT_PROFILE
            : Options.Profile.Trim();

        EffectiveOptions Effective = ApplyProfileDefaults(Profile);

        if (Options?.Markdown != null)
        {
            OverlayMarkdown(Effective.Markdown, Options.Markdown);
        }

        if (Options?.Docx != null)
        {
            OverlayDocx(Effective.Docx, Options.Docx);
        }

        if (Options?.Images != null)
        {
            OverlayImages(Effective.Images, Options.Images);
        }

        if (Options?.Links != null)
        {
            OverlayLinks(Effective.Links, Options.Links);
        }

        if (Options?.Tables != null)
        {
            OverlayTables(Effective.Tables, Options.Tables);
        }

        return Effective;
    }

    private static EffectiveOptions ApplyProfileDefaults(string Profile)
    {
        EffectiveOptions Effective = new EffectiveOptions
        {
            Markdown = new MarkdownOptions(),
            Docx = new DocxOptions(),
            Images = new ImageOptions(),
            Links = new LinkOptions(),
            Tables = new TableOptions()
        };

        switch (Profile.ToLowerInvariant())
        {
            case "strict-commonmark":
                Effective.Markdown.Profile = "CommonMark";
                Effective.Markdown.SetResolvedValues(false, false, false, false, false);
                break;
            case "docsite":
                Effective.Markdown.Profile = "Extended";
                Effective.Markdown.SetResolvedValues(true, true, true, true, false);
                break;
            default:
                Effective.Markdown.Profile = "Extended";
                Effective.Markdown.SetResolvedValues(true, true, true, true, false);
                break;
        }

        Effective.Docx.UseBuiltinTemplate = true;
        Effective.Docx.TemplatePath = string.Empty;
        Effective.Docx.Styles = new StyleMapping();

        Effective.Images.SetResolvedValues(DEFAULT_MAX_WIDTH_CM, true, DEFAULT_MISSING_IMAGE_FORMAT);

        Effective.Links.SetResolvedValues(true, true);

        Effective.Tables.SetResolvedValues(true);

        return Effective;
    }

    private static void OverlayMarkdown(MarkdownOptions Target, MarkdownOptions Source)
    {
        if (Source.Profile != null)
        {
            Target.Profile = Source.Profile;
        }

        if (Source.HasEnableTables) Target.EnableTables = Source.EnableTables;
        if (Source.HasEnableTaskLists) Target.EnableTaskLists = Source.EnableTaskLists;
        if (Source.HasEnableAutoLinks) Target.EnableAutoLinks = Source.EnableAutoLinks;
        if (Source.HasEnableFootnotes) Target.EnableFootnotes = Source.EnableFootnotes;
        if (Source.HasEnableInlineHtml) Target.EnableInlineHtml = Source.EnableInlineHtml;
    }

    private static void OverlayDocx(DocxOptions Target, DocxOptions Source)
    {
        Target.UseBuiltinTemplate = Source.UseBuiltinTemplate;
        Target.TemplatePath = Source.TemplatePath ?? Target.TemplatePath;
        Target.Styles = Source.Styles ?? Target.Styles;
    }

    private static void OverlayImages(ImageOptions Target, ImageOptions Source)
    {
        if (Source.HasMaxWidthCm) Target.MaxWidthCm = Source.MaxWidthCm;
        if (Source.HasScaleDownOnly) Target.ScaleDownOnly = Source.ScaleDownOnly;
        Target.MissingImagePlaceholderFormat = Source.MissingImagePlaceholderFormat ?? Target.MissingImagePlaceholderFormat;
    }

    private static void OverlayLinks(LinkOptions Target, LinkOptions Source)
    {
        if (Source.HasEnableInternalLinks) Target.EnableInternalLinks = Source.EnableInternalLinks;
        if (Source.HasEnableExternalLinks) Target.EnableExternalLinks = Source.EnableExternalLinks;
    }

    private static void OverlayTables(TableOptions Target, TableOptions Source)
    {
        if (Source.HasEnableAlignment) Target.EnableAlignment = Source.EnableAlignment;
    }
}
