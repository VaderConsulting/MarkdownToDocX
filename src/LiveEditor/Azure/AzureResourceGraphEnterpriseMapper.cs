using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiveEditor.Archi;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Azure;

/// <summary>
/// Maps Azure Resource Graph rows to an Archi exchange model for enterprise rendering.
/// </summary>
internal static class AzureResourceGraphEnterpriseMapper
{
    /// <summary>
    /// Builds an exchange model from Resource Graph records.
    /// </summary>
    public static bool TryMap(
        IReadOnlyList<AzureResourceGraphRecord> Records,
        AzureResourceGraphImportOptions Options,
        out ArchiExchangeModel Model,
        out string ErrorMessage)
    {
        Model = new ArchiExchangeModel();
        ErrorMessage = string.Empty;
        if (Records == null || Records.Count == 0)
        {
            ErrorMessage = "No resources to import.";
            return false;
        }

        if (Options == null)
        {
            ErrorMessage = "Import options are required.";
            return false;
        }

        Model.ModelName = Options.ModelTitle;
        HashSet<string> UsedIds = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, string> MermaidIdByArmId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, AzureResourceGraphRecord> RecordByArmId = new Dictionary<string, AzureResourceGraphRecord>(StringComparer.OrdinalIgnoreCase);
        List<AzureResourceGraphRecord> Kept = [];

        foreach (AzureResourceGraphRecord Record in Records)
        {
            if (ShouldSkip(Record, Options))
            {
                continue;
            }

            Kept.Add(Record);
            RecordByArmId[Record.Id] = Record;
        }

        if (Kept.Count == 0)
        {
            ErrorMessage = "Every row was filtered out. Select more categories in the import dialog or broaden the Resource Graph query.";
            return false;
        }

        HashSet<string> SubscriptionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (AzureResourceGraphRecord Record in Kept)
        {
            if (!string.IsNullOrWhiteSpace(Record.SubscriptionId))
            {
                SubscriptionIds.Add(Record.SubscriptionId);
            }
        }

        bool UseSubscriptionGroups = Options.GroupBySubscription && SubscriptionIds.Count > 1;
        Dictionary<string, string> ResourceGroupMermaidId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (AzureResourceGraphRecord Record in Kept)
        {
            if (UseSubscriptionGroups && !string.IsNullOrWhiteSpace(Record.SubscriptionId))
            {
                EnsureSubscriptionGroup(Model, UsedIds, Record.SubscriptionId);
            }

            string GroupKey = BuildResourceGroupKey(Record);
            if (!ResourceGroupMermaidId.ContainsKey(GroupKey))
            {
                string GroupId = CreateUniqueId(UsedIds, "rg", Record.ResourceGroup);
                ResourceGroupMermaidId.Add(GroupKey, GroupId);
                ArchiExchangeElement GroupElement = new ArchiExchangeElement
                {
                    MermaidId = GroupId,
                    ArchiId = ArchiObjectIds.FromMermaidKey("element", GroupId),
                    ElementType = EnterpriseElementType.Grouping,
                    Label = string.IsNullOrWhiteSpace(Record.ResourceGroup) ? GroupId : Record.ResourceGroup,
                    ParentMermaidId = UseSubscriptionGroups ? SubscriptionGroupId(Record.SubscriptionId) : null
                };
                Model.Elements.Add(GroupElement);
            }

            string ElementId = CreateResourceElementId(UsedIds, Record);
            MermaidIdByArmId[Record.Id] = ElementId;
            string Label = BuildResourceLabel(Record);
            ArchiExchangeElement Element = new ArchiExchangeElement
            {
                MermaidId = ElementId,
                ArchiId = ArchiObjectIds.FromMermaidKey("element", ElementId),
                ElementType = MapResourceType(Record.Type),
                Label = Label,
                ParentMermaidId = ResourceGroupMermaidId[GroupKey]
            };
            Model.Elements.Add(Element);
        }

        if (Options.LinkNestedResources)
        {
            AddNestedComposition(Model, Kept, MermaidIdByArmId, RecordByArmId);
        }

        if (Options.LinkHiddenTagReferences)
        {
            AddHiddenLinkRealizations(Model, Kept, MermaidIdByArmId, RecordByArmId);
        }

        if (Kept.Count < Records.Count)
        {
            int Skipped = Records.Count - Kept.Count;
            Model.Warnings.Add($"Skipped {Skipped.ToString(CultureInfo.InvariantCulture)} rows not included in the import selection.");
        }

        return true;
    }

    private static bool ShouldSkip(AzureResourceGraphRecord Record, AzureResourceGraphImportOptions Options)
    {
        if (string.IsNullOrWhiteSpace(Record.Id) || string.IsNullOrWhiteSpace(Record.Type))
        {
            return true;
        }

        return !AzureResourceGraphIncludeCatalog.IsTypeIncluded(Record.Type, Options);
    }

    private static void EnsureSubscriptionGroup(ArchiExchangeModel Model, HashSet<string> UsedIds, string SubscriptionId)
    {
        string GroupId = SubscriptionGroupId(SubscriptionId);
        foreach (ArchiExchangeElement Existing in Model.Elements)
        {
            if (string.Equals(Existing.MermaidId, GroupId, StringComparison.Ordinal))
            {
                return;
            }
        }

        UsedIds.Add(GroupId);
        string ShortLabel = SubscriptionId.Length > 13
            ? "Subscription " + SubscriptionId.Substring(0, 8) + "…"
            : "Subscription " + SubscriptionId;
        ArchiExchangeElement GroupElement = new ArchiExchangeElement
        {
            MermaidId = GroupId,
            ArchiId = ArchiObjectIds.FromMermaidKey("element", GroupId),
            ElementType = EnterpriseElementType.Grouping,
            Label = ShortLabel
        };
        Model.Elements.Add(GroupElement);
    }

    private static string SubscriptionGroupId(string SubscriptionId)
    {
        string Token = ShortHash(SubscriptionId);
        return "sub_" + Token;
    }

    private static string BuildResourceGroupKey(AzureResourceGraphRecord Record)
    {
        return (Record.SubscriptionId ?? string.Empty).ToLowerInvariant()
            + "|"
            + (Record.ResourceGroup ?? string.Empty).ToLowerInvariant();
    }

    private static string CreateResourceElementId(HashSet<string> UsedIds, AzureResourceGraphRecord Record)
    {
        string Base = "res_" + ShortHash(Record.Id);
        string Candidate = Base;
        int Suffix = 2;
        while (UsedIds.Contains(Candidate))
        {
            Candidate = Base + "_" + Suffix.ToString(CultureInfo.InvariantCulture);
            Suffix++;
        }

        UsedIds.Add(Candidate);
        return Candidate;
    }

    private static string CreateUniqueId(HashSet<string> UsedIds, string Prefix, string Name)
    {
        string Base = Prefix + "_" + ShortHash(Name);
        string Candidate = Base;
        int Suffix = 2;
        while (UsedIds.Contains(Candidate))
        {
            Candidate = Base + "_" + Suffix.ToString(CultureInfo.InvariantCulture);
            Suffix++;
        }

        UsedIds.Add(Candidate);
        return Candidate;
    }

    private static string ShortHash(string Material)
    {
        byte[] Hash = MD5.HashData(Encoding.UTF8.GetBytes(Material ?? string.Empty));
        StringBuilder Builder = new StringBuilder(16);
        for (int Index = 0; Index < 8; Index++)
        {
            Builder.Append(Hash[Index].ToString("x2", CultureInfo.InvariantCulture));
        }

        return Builder.ToString();
    }

    private static string BuildResourceLabel(AzureResourceGraphRecord Record)
    {
        string TypeShort = Record.Type;
        int SlashIndex = TypeShort.LastIndexOf('/');
        if (SlashIndex >= 0 && SlashIndex < TypeShort.Length - 1)
        {
            TypeShort = TypeShort.Substring(SlashIndex + 1);
        }

        string Name = Record.Name;
        if (string.IsNullOrWhiteSpace(Name))
        {
            return TypeShort;
        }

        if (!string.IsNullOrWhiteSpace(Record.Location) && !string.Equals(Record.Location, "global", StringComparison.OrdinalIgnoreCase))
        {
            return Name + "<br>" + TypeShort + "<br>" + Record.Location;
        }

        return Name + "<br>" + TypeShort;
    }

    private static EnterpriseElementType MapResourceType(string Type)
    {
        string Normalised = (Type ?? string.Empty).Trim().ToLowerInvariant();
        if (Normalised.Contains("microsoft.web/sites", StringComparison.Ordinal))
        {
            return EnterpriseElementType.ApplicationComponent;
        }

        if (Normalised.Contains("microsoft.insights/components", StringComparison.Ordinal))
        {
            return EnterpriseElementType.ApplicationComponent;
        }

        if (Normalised.Contains("microsoft.storage/storageaccounts", StringComparison.Ordinal))
        {
            return EnterpriseElementType.Artifact;
        }

        if (Normalised.Contains("microsoft.compute/virtualmachines", StringComparison.Ordinal))
        {
            return EnterpriseElementType.Device;
        }

        if (Normalised.Contains("microsoft.network/virtualnetworks", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.network/virtualwans", StringComparison.Ordinal))
        {
            return EnterpriseElementType.CommunicationNetwork;
        }

        if (Normalised.Contains("microsoft.network/", StringComparison.Ordinal))
        {
            return EnterpriseElementType.TechnologyInterface;
        }

        if (Normalised.Contains("microsoft.sql/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.documentdb/", StringComparison.Ordinal)
            || Normalised.Contains("microsoft.cache/", StringComparison.Ordinal))
        {
            return EnterpriseElementType.Node;
        }

        if (Normalised.Contains("microsoft.keyvault/", StringComparison.Ordinal))
        {
            return EnterpriseElementType.Node;
        }

        return EnterpriseElementType.Node;
    }

    private static void AddNestedComposition(
        ArchiExchangeModel Model,
        List<AzureResourceGraphRecord> Records,
        Dictionary<string, string> MermaidIdByArmId,
        Dictionary<string, AzureResourceGraphRecord> RecordByArmId)
    {
        int RelationshipIndex = 0;
        foreach (AzureResourceGraphRecord Record in Records)
        {
            string? ParentArmId = TryGetParentArmId(Record.Id);
            if (string.IsNullOrWhiteSpace(ParentArmId))
            {
                continue;
            }

            if (!MermaidIdByArmId.TryGetValue(Record.Id, out string ChildId))
            {
                continue;
            }

            if (!MermaidIdByArmId.TryGetValue(ParentArmId, out string ParentId))
            {
                continue;
            }

            string RelKey = ParentId + "|composition|" + ChildId + "|" + RelationshipIndex.ToString(CultureInfo.InvariantCulture);
            ArchiExchangeRelationship Relationship = new ArchiExchangeRelationship
            {
                ArchiId = ArchiObjectIds.FromMermaidKey("relationship", RelKey),
                SourceMermaidId = ParentId,
                TargetMermaidId = ChildId,
                Kind = EnterpriseRelationshipKind.Composition
            };
            Model.Relationships.Add(Relationship);
            RelationshipIndex++;
        }
    }

    private static void AddHiddenLinkRealizations(
        ArchiExchangeModel Model,
        List<AzureResourceGraphRecord> Records,
        Dictionary<string, string> MermaidIdByArmId,
        Dictionary<string, AzureResourceGraphRecord> RecordByArmId)
    {
        int RelationshipIndex = 0;
        foreach (AzureResourceGraphRecord Record in Records)
        {
            if (!MermaidIdByArmId.TryGetValue(Record.Id, out string SourceId))
            {
                continue;
            }

            foreach (string TargetArmId in ReadHiddenLinkTargets(Record.TagsJson))
            {
                string Resolved = ResolveArmId(TargetArmId, RecordByArmId);
                if (!MermaidIdByArmId.TryGetValue(Resolved, out string TargetId))
                {
                    continue;
                }

                if (string.Equals(SourceId, TargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                string RelKey = SourceId + "|realization|" + TargetId + "|" + RelationshipIndex.ToString(CultureInfo.InvariantCulture);
                ArchiExchangeRelationship Relationship = new ArchiExchangeRelationship
                {
                    ArchiId = ArchiObjectIds.FromMermaidKey("relationship", RelKey),
                    SourceMermaidId = SourceId,
                    TargetMermaidId = TargetId,
                    Kind = EnterpriseRelationshipKind.Realization,
                    Label = "hidden-link"
                };
                Model.Relationships.Add(Relationship);
                RelationshipIndex++;
            }
        }
    }

    private static IEnumerable<string> ReadHiddenLinkTargets(string TagsJson)
    {
        List<string> Targets = [];
        if (string.IsNullOrWhiteSpace(TagsJson) || string.Equals(TagsJson, "{}", StringComparison.Ordinal))
        {
            return Targets;
        }

        try
        {
            using JsonDocument Document = JsonDocument.Parse(TagsJson);
            if (Document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Targets;
            }

            foreach (JsonProperty Property in Document.RootElement.EnumerateObject())
            {
                if (!Property.Name.StartsWith("hidden-link", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string Value = Property.Value.GetString() ?? string.Empty;
                if (Value.Length > 0)
                {
                    Targets.Add(Value.Trim());
                }
            }
        }
        catch (JsonException)
        {
        }

        return Targets;
    }

    private static string ResolveArmId(string Reference, Dictionary<string, AzureResourceGraphRecord> RecordByArmId)
    {
        if (RecordByArmId.ContainsKey(Reference))
        {
            return Reference;
        }

        foreach (KeyValuePair<string, AzureResourceGraphRecord> Pair in RecordByArmId)
        {
            if (Pair.Key.EndsWith(Reference, StringComparison.OrdinalIgnoreCase)
                || Reference.EndsWith(Pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return Pair.Key;
            }
        }

        return Reference;
    }

    internal static string? TryGetParentArmId(string ResourceId)
    {
        if (string.IsNullOrWhiteSpace(ResourceId))
        {
            return null;
        }

        string Normalised = ResourceId.TrimEnd('/');
        int SlotsIndex = Normalised.IndexOf("/slots/", StringComparison.OrdinalIgnoreCase);
        if (SlotsIndex >= 0)
        {
            return Normalised.Substring(0, SlotsIndex);
        }

        int ProvidersIndex = Normalised.IndexOf("/providers/", StringComparison.OrdinalIgnoreCase);
        if (ProvidersIndex < 0)
        {
            return null;
        }

        string AfterProviders = Normalised.Substring(ProvidersIndex + "/providers/".Length);
        string[] Segments = AfterProviders.Split('/');
        if (Segments.Length <= 2)
        {
            return null;
        }

        string ParentTail = string.Join("/", Segments, 0, Segments.Length - 2);
        return Normalised.Substring(0, ProvidersIndex + "/providers/".Length) + ParentTail;
    }
}
