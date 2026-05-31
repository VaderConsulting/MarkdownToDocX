namespace MermaidRecognition.Config;

/// <summary>
/// Per-diagram Mermaid configuration: raw init directives and merged effective values (§4.1).
/// Keys use dotted paths (for example <c>flowchart.nodeSpacing</c>, <c>themeVariables.fontFamily</c>).
/// </summary>
public sealed class DiagramConfigContract
{
    private readonly Dictionary<string, string> _RawDirectiveValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _EffectiveValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ConfigValueSource> _ValueSources =
        new Dictionary<string, ConfigValueSource>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Values from <c>%%{init: ...}%%</c> blocks only, flattened with dotted keys.
    /// </summary>
    public IReadOnlyDictionary<string, string> RawDirectiveValues
    {
        get
        {
            return _RawDirectiveValues;
        }
    }

    /// <summary>
    /// Merged effective configuration (defaults, directives, resolved theme variables).
    /// </summary>
    public IReadOnlyDictionary<string, string> EffectiveValues
    {
        get
        {
            return _EffectiveValues;
        }
    }

    /// <summary>
    /// Optional provenance per effective key.
    /// </summary>
    public IReadOnlyDictionary<string, ConfigValueSource> ValueSources
    {
        get
        {
            return _ValueSources;
        }
    }

    /// <summary>
    /// Mutable raw directive bag used while parsing init blocks.
    /// </summary>
    public Dictionary<string, string> RawDirectiveValuesMutable
    {
        get
        {
            return _RawDirectiveValues;
        }
    }

    /// <summary>
    /// Mutable effective bag used while merging defaults and directives.
    /// </summary>
    public Dictionary<string, string> EffectiveValuesMutable
    {
        get
        {
            return _EffectiveValues;
        }
    }

    /// <summary>
    /// Mutable provenance bag.
    /// </summary>
    public Dictionary<string, ConfigValueSource> ValueSourcesMutable
    {
        get
        {
            return _ValueSources;
        }
    }

    /// <summary>
    /// Copies all raw directive keys into effective values when not already set (directive override wins).
    /// </summary>
    public void ApplyRawDirectivesToEffective(ConfigValueSource DirectiveSource)
    {
        foreach (KeyValuePair<string, string> Entry in _RawDirectiveValues)
        {
            _EffectiveValues[Entry.Key] = Entry.Value;
            _ValueSources[Entry.Key] = DirectiveSource;
        }
    }

    /// <summary>
    /// Sets an effective value only when the key is not already present.
    /// </summary>
    public void SetDefaultIfMissing(string Key, string Value)
    {
        if (_EffectiveValues.ContainsKey(Key))
        {
            return;
        }

        _EffectiveValues[Key] = Value;
        _ValueSources[Key] = ConfigValueSource.Default;
    }

    /// <summary>
    /// Builds effective configuration from pinned Mermaid defaults (section 3.17) plus raw directives.
    /// </summary>
    public void BuildEffectiveConfiguration(string ProgrammeKindId)
    {
        MermaidConfigurationPipeline.BuildEffectiveConfiguration(this, ProgrammeKindId);
    }

    /// <summary>
    /// Tries to read an effective value.
    /// </summary>
    public bool TryGetEffective(string Key, out string Value)
    {
        return _EffectiveValues.TryGetValue(Key, out Value!);
    }
}

