namespace LiveEditor.Azure;

/// <summary>
/// One row from an Azure Resource Graph export (CSV or JSON).
/// </summary>
internal sealed class AzureResourceGraphRecord
{
    private string _SubscriptionId = string.Empty;

    private string _ResourceGroup = string.Empty;

    private string _Name = string.Empty;

    private string _Type = string.Empty;

    private string _Location = string.Empty;

    private string _Kind = string.Empty;

    private string _Sku = string.Empty;

    private string _TagsJson = string.Empty;

    private string _Id = string.Empty;

    /// <summary>Gets or sets the subscription id.</summary>
    public string SubscriptionId
    {
        get
        {
            return _SubscriptionId;
        }
        set
        {
            _SubscriptionId = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the resource group name.</summary>
    public string ResourceGroup
    {
        get
        {
            return _ResourceGroup;
        }
        set
        {
            _ResourceGroup = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the resource name.</summary>
    public string Name
    {
        get
        {
            return _Name;
        }
        set
        {
            _Name = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the ARM resource type (for example microsoft.web/sites).</summary>
    public string Type
    {
        get
        {
            return _Type;
        }
        set
        {
            _Type = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the Azure region.</summary>
    public string Location
    {
        get
        {
            return _Location;
        }
        set
        {
            _Location = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the optional kind string.</summary>
    public string Kind
    {
        get
        {
            return _Kind;
        }
        set
        {
            _Kind = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the optional SKU text (often JSON from export).</summary>
    public string Sku
    {
        get
        {
            return _Sku;
        }
        set
        {
            _Sku = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets tags as JSON object text from the export.</summary>
    public string TagsJson
    {
        get
        {
            return _TagsJson;
        }
        set
        {
            _TagsJson = value ?? string.Empty;
        }
    }

    /// <summary>Gets or sets the full ARM resource id.</summary>
    public string Id
    {
        get
        {
            return _Id;
        }
        set
        {
            _Id = value ?? string.Empty;
        }
    }
}
