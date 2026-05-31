namespace MermaidRecognition.Config;

/// <summary>
/// Computed effective-configuration rules beyond static defaults (spec section 3.17.4).
/// </summary>
public static class MermaidEffectiveConfigBuilder
{
    /// <summary>
    /// Applies Mermaid <c>getEffectiveHtmlLabels</c> precedence: root <c>htmlLabels</c> when set in init,
    /// otherwise diagram <c>flowchart.htmlLabels</c>, otherwise the default already on <paramref name="Configuration"/>.
    /// </summary>
    public static void ResolveEffectiveHtmlLabels(DiagramConfigContract Configuration)
    {
        if (Configuration.RawDirectiveValues.ContainsKey("htmlLabels"))
        {
            return;
        }

        if (Configuration.RawDirectiveValues.TryGetValue("flowchart.htmlLabels", out string FlowchartHtmlLabels))
        {
            Configuration.EffectiveValuesMutable["htmlLabels"] = FlowchartHtmlLabels;
            Configuration.ValueSourcesMutable["htmlLabels"] = ConfigValueSource.DirectiveOverride;
        }
    }

    /// <summary>
    /// When <c>fontFamily</c> is overridden but <c>themeVariables.fontFamily</c> is not, mirror Mermaid config.ts behaviour.
    /// </summary>
    public static void SynchronizeThemeTypographyFromRoot(DiagramConfigContract Configuration)
    {
        if (Configuration.RawDirectiveValues.ContainsKey("themeVariables.fontFamily"))
        {
            return;
        }

        if (!Configuration.TryGetEffective("fontFamily", out string FontFamily))
        {
            return;
        }

        string ThemeFontFamily = FontFamily.Trim().TrimEnd(';');
        Configuration.EffectiveValuesMutable["themeVariables.fontFamily"] = ThemeFontFamily;
        if (!Configuration.ValueSources.ContainsKey("themeVariables.fontFamily"))
        {
            Configuration.ValueSourcesMutable["themeVariables.fontFamily"] = ConfigValueSource.ThemeResolved;
        }
    }
}
