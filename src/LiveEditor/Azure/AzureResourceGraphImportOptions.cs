using System.Collections.Generic;

namespace LiveEditor.Azure;

/// <summary>
/// Controls how Resource Graph rows become an enterprise diagram.
/// </summary>
internal sealed class AzureResourceGraphImportOptions
{
    private readonly HashSet<string> _EnabledCategories = new HashSet<string>(System.StringComparer.Ordinal);

    /// <summary>Gets or sets the diagram title when the file name is not used.</summary>
    public string ModelTitle { get; set; } = "Azure resources";

    /// <summary>Gets or sets whether subscription groups are created when multiple subscriptions appear.</summary>
    public bool GroupBySubscription { get; set; } = true;

    /// <summary>Gets or sets whether nested ARM children (for example deployment slots) get composition links.</summary>
    public bool LinkNestedResources { get; set; } = true;

    /// <summary>Gets or sets whether hidden-link tags become realization relationships.</summary>
    public bool LinkHiddenTagReferences { get; set; } = true;

    /// <summary>Gets category keys to include (see <see cref="AzureResourceGraphIncludeCatalog"/>).</summary>
    public HashSet<string> EnabledCategories
    {
        get
        {
            return _EnabledCategories;
        }
    }

    /// <summary>
    /// Creates options with enterprise-focused category defaults.
    /// </summary>
    public static AzureResourceGraphImportOptions CreateEnterpriseDefaults()
    {
        AzureResourceGraphImportOptions Options = new AzureResourceGraphImportOptions();
        AzureResourceGraphIncludeCatalog.ApplyEnterpriseDefaults(Options);
        return Options;
    }
}
