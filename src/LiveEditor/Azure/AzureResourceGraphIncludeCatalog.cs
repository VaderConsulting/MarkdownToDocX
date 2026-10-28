using System;
using System.Collections.Generic;

namespace LiveEditor.Azure;

/// <summary>
/// Resource-type categories for filtering Resource Graph imports.
/// </summary>
internal static class AzureResourceGraphIncludeCatalog
{
    internal static class CategoryKeys
    {
        public const string WebApplications = "webApplications";

        public const string KeyVaults = "keyVaults";

        public const string StorageAccounts = "storageAccounts";

        public const string ApplicationInsights = "applicationInsights";

        public const string Databases = "databases";

        public const string VirtualMachines = "virtualMachines";

        public const string PrivateEndpoints = "privateEndpoints";

        public const string ManagedIdentities = "managedIdentities";

        public const string Containers = "containers";

        public const string VmExtensions = "vmExtensions";

        public const string Disks = "disks";

        public const string NetworkInterfaces = "networkInterfaces";

        public const string MonitoringAlerts = "monitoringAlerts";

        public const string Backup = "backup";

        public const string VirtualNetworks = "virtualNetworks";

        public const string NetworkSecurity = "networkSecurity";

        public const string Other = "other";
    }

    private static readonly List<AzureResourceGraphIncludeCategoryDefinition> _Definitions = BuildDefinitions();

    /// <summary>Gets all categories in display order.</summary>
    public static IReadOnlyList<AzureResourceGraphIncludeCategoryDefinition> Definitions
    {
        get
        {
            return _Definitions;
        }
    }

    /// <summary>
    /// Gets categories shown in the import dialog (enterprise-focused by default).
    /// </summary>
    /// <param name="IncludeInfrastructureDetail">When true, includes disks, NICs, alerts, and similar low-level types.</param>
    public static IReadOnlyList<AzureResourceGraphIncludeCategoryDefinition> GetDialogDefinitions(bool IncludeInfrastructureDetail)
    {
        List<AzureResourceGraphIncludeCategoryDefinition> Visible = [];
        foreach (AzureResourceGraphIncludeCategoryDefinition Definition in _Definitions)
        {
            if (Definition.ShowInImportDialog || IncludeInfrastructureDetail)
            {
                Visible.Add(Definition);
            }
        }

        return Visible;
    }

    /// <summary>
    /// Applies enterprise diagram defaults to import options.
    /// </summary>
    public static void ApplyEnterpriseDefaults(AzureResourceGraphImportOptions Options)
    {
        Options.EnabledCategories.Clear();
        foreach (AzureResourceGraphIncludeCategoryDefinition Definition in _Definitions)
        {
            if (Definition.DefaultEnabledForEnterprise)
            {
                Options.EnabledCategories.Add(Definition.Key);
            }
        }
    }

    /// <summary>
    /// Returns the category key for an ARM type string.
    /// </summary>
    public static string ClassifyType(string? Type)
    {
        string Normalised = (Type ?? string.Empty).Trim().ToLowerInvariant();
        if (Normalised.Length == 0)
        {
            return CategoryKeys.Other;
        }

        if (Normalised.Contains("microsoft.compute/virtualmachines/extensions", StringComparison.Ordinal))
        {
            return CategoryKeys.VmExtensions;
        }

        if (Normalised == "microsoft.compute/virtualmachines")
        {
            return CategoryKeys.VirtualMachines;
        }

        if (Normalised.Contains("microsoft.compute/disks", StringComparison.Ordinal))
        {
            return CategoryKeys.Disks;
        }

        if (Normalised.Contains("microsoft.web/sites", StringComparison.Ordinal))
        {
            return CategoryKeys.WebApplications;
        }

        if (Normalised.Contains("microsoft.keyvault/vaults", StringComparison.Ordinal))
        {
            return CategoryKeys.KeyVaults;
        }

        if (Normalised.Contains("microsoft.storage/storageaccounts", StringComparison.Ordinal))
        {
            return CategoryKeys.StorageAccounts;
        }

        if (Normalised == "microsoft.insights/components")
        {
            return CategoryKeys.ApplicationInsights;
        }

        if (Normalised.Contains("microsoft.insights/metricalerts", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.insights/activitylogalerts", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.insights/scheduledqueryrules", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.alertsmanagement/", StringComparison.Ordinal))
        {
            return CategoryKeys.MonitoringAlerts;
        }

        if (Normalised.Contains("microsoft.sql/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.documentdb/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.cache/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.dbforpostgresql/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.dbformysql/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.synapse/", StringComparison.Ordinal))
        {
            return CategoryKeys.Databases;
        }

        if (Normalised.Contains("microsoft.network/privateendpoints", StringComparison.Ordinal))
        {
            return CategoryKeys.PrivateEndpoints;
        }

        if (Normalised.Contains("microsoft.network/networkinterfaces", StringComparison.Ordinal))
        {
            return CategoryKeys.NetworkInterfaces;
        }

        if (Normalised.Contains("microsoft.network/virtualnetworks", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.network/networkwatchers", StringComparison.Ordinal))
        {
            return CategoryKeys.VirtualNetworks;
        }

        if (Normalised.Contains("microsoft.network/networksecuritygroups", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.network/azurefirewalls", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.network/routetables", StringComparison.Ordinal))
        {
            return CategoryKeys.NetworkSecurity;
        }

        if (Normalised.Contains("microsoft.managedidentity/userassignedidentities", StringComparison.Ordinal))
        {
            return CategoryKeys.ManagedIdentities;
        }

        if (Normalised.Contains("microsoft.app/containerapps", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.containerinstance/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.containerservice/managedclusters", StringComparison.Ordinal))
        {
            return CategoryKeys.Containers;
        }

        if (Normalised.Contains("microsoft.compute/restorepointcollections", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.recoveryservices/", StringComparison.Ordinal))
        {
            return CategoryKeys.Backup;
        }

        return CategoryKeys.Other;
    }

    /// <summary>
    /// Counts rows per category for preview in the import dialog.
    /// </summary>
    public static Dictionary<string, int> CountByCategory(IReadOnlyList<AzureResourceGraphRecord> Records)
    {
        Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (AzureResourceGraphIncludeCategoryDefinition Definition in _Definitions)
        {
            Counts[Definition.Key] = 0;
        }

        foreach (AzureResourceGraphRecord Record in Records)
        {
            string Key = ClassifyType(Record.Type);
            Counts[Key] = Counts[Key] + 1;
        }

        return Counts;
    }

    /// <summary>
    /// Returns true when the type should be imported under the given options.
    /// </summary>
    public static bool IsTypeIncluded(string? Type, AzureResourceGraphImportOptions Options)
    {
        if (Options == null || Options.EnabledCategories.Count == 0)
        {
            return false;
        }

        string Key = ClassifyType(Type);
        return Options.EnabledCategories.Contains(Key);
    }

    private static List<AzureResourceGraphIncludeCategoryDefinition> BuildDefinitions()
    {
        List<AzureResourceGraphIncludeCategoryDefinition> List =
        [
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.WebApplications, "Web applications", "App Services, deployment slots, and similar web workloads.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.KeyVaults, "Key Vaults", "Secrets, keys, and certificate stores.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.StorageAccounts, "Storage accounts", "Blob, file, queue, and table storage.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.ApplicationInsights, "Application Insights", "Application components only (not alert rules).", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.Databases, "Databases", "SQL, Cosmos DB, cache, and related data services.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.VirtualMachines, "Virtual machines", "VMs without extensions or disks.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.PrivateEndpoints, "Private endpoints", "Private Link endpoints.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.ManagedIdentities, "Managed identities", "User-assigned identities.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.Containers, "Containers", "Container Apps, instances, and AKS clusters.", true, true),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.VmExtensions, "VM extensions", "Guest extensions and custom script handlers.", false, false),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.Disks, "Managed disks", "OS and data disks.", false, false),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.NetworkInterfaces, "Network interfaces", "NICs (often noise from private endpoints).", false, false),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.MonitoringAlerts, "Monitoring and alerts", "Metric, activity, scheduled query, and smart detector rules.", false, false),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.Backup, "Backup and recovery", "Restore points and recovery services.", false, false),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.VirtualNetworks, "Virtual networks", "VNets, subnets, and network watchers.", false, false),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.NetworkSecurity, "Network security", "NSGs, firewalls, and route tables.", false, false),
            new AzureResourceGraphIncludeCategoryDefinition(CategoryKeys.Other, "Other resources", "Anything not listed above.", false, false),
        ];
        return List;
    }
}

/// <summary>
/// One selectable import category.
/// </summary>
internal sealed class AzureResourceGraphIncludeCategoryDefinition
{
    public AzureResourceGraphIncludeCategoryDefinition(string Key, string DisplayName, string Description, bool DefaultEnabledForEnterprise, bool ShowInImportDialog)
    {
        this.Key = Key;
        this.DisplayName = DisplayName;
        this.Description = Description;
        this.DefaultEnabledForEnterprise = DefaultEnabledForEnterprise;
        this.ShowInImportDialog = ShowInImportDialog;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool DefaultEnabledForEnterprise { get; }

    /// <summary>Gets whether the category appears in the import dialog without turning on infrastructure detail.</summary>
    public bool ShowInImportDialog { get; }
}
