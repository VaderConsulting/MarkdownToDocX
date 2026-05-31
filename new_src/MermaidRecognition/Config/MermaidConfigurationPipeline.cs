namespace MermaidRecognition.Config;

/// <summary>
/// Central configuration merge for all diagram recognisers (spec section 4.1, 3.17).
/// </summary>
public static class MermaidConfigurationPipeline
{
    /// <summary>
    /// Parses leading <c>%%{init}%%</c> blocks into <see cref="DiagramConfigContract.RawDirectiveValues"/>.
    /// </summary>
    public static void ParseLeadingInitDirectives(string Source, DiagramConfigContract Configuration)
    {
        Dictionary<string, string> InitValues = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(Source);
        foreach (KeyValuePair<string, string> Entry in InitValues)
        {
            Configuration.RawDirectiveValuesMutable[Entry.Key] = Entry.Value;
        }
    }

    /// <summary>
    /// Parses YAML front-matter and init blocks (flowchart F86 path).
    /// </summary>
    public static string ParseFlowchartLeadingDirectives(string Source, DiagramConfigContract Configuration)
    {
        string AfterFrontMatter = Source;
        MermaidYamlFrontMatterFlattener.TryExtractLeadingFrontMatter(
            Source,
            Configuration.RawDirectiveValuesMutable,
            out AfterFrontMatter);

        Dictionary<string, string> InitValues = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(AfterFrontMatter);
        foreach (KeyValuePair<string, string> Entry in InitValues)
        {
            Configuration.RawDirectiveValuesMutable[Entry.Key] = Entry.Value;
        }

        return AfterFrontMatter;
    }

    /// <summary>
    /// Applies pinned defaults, directive overrides, and computed effective keys.
    /// </summary>
    public static void BuildEffectiveConfiguration(DiagramConfigContract Configuration, string ProgrammeKindId)
    {
        MermaidPinnedDefaults.ApplyTo(Configuration, ProgrammeKindId);
        Configuration.ApplyRawDirectivesToEffective(ConfigValueSource.DirectiveOverride);
        MermaidEffectiveConfigBuilder.ResolveEffectiveHtmlLabels(Configuration);
        MermaidEffectiveConfigBuilder.SynchronizeThemeTypographyFromRoot(Configuration);
    }

    /// <summary>
    /// Applies global defaults only (unknown diagram kind).
    /// </summary>
    public static void BuildEffectiveConfiguration(DiagramConfigContract Configuration)
    {
        MermaidPinnedDefaults.ApplyTo(Configuration, null);
        Configuration.ApplyRawDirectivesToEffective(ConfigValueSource.DirectiveOverride);
        MermaidEffectiveConfigBuilder.SynchronizeThemeTypographyFromRoot(Configuration);
    }
}
