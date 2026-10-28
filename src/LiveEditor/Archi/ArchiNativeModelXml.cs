using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Archi;

/// <summary>
/// Reads and writes Archi's native <c>model.xml</c> / coArchi <c>model.archimate</c> XML.
/// </summary>
internal static class ArchiNativeModelXml
{
    private static readonly XNamespace ArchimateNs = "http://www.archimatetool.com/archimate";

    private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Parses Archi native model XML into an exchange model.
    /// </summary>
    /// <param name="Xml">Model XML text.</param>
    /// <param name="Model">Mapped model when successful.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when at least one supported enterprise element or relationship was mapped.</returns>
    public static bool TryRead(string Xml, out ArchiExchangeModel Model, out string ErrorMessage)
    {
        Model = new ArchiExchangeModel();
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Xml))
        {
            ErrorMessage = "Archi model XML is empty.";
            return false;
        }

        XDocument Document;
        try
        {
            Document = XDocument.Parse(Xml);
        }
        catch (Exception Ex)
        {
            ErrorMessage = "Could not parse Archi model XML: " + Ex.Message;
            return false;
        }

        XElement? Root = Document.Root;
        if (Root == null)
        {
            ErrorMessage = "Archi model XML has no root element.";
            return false;
        }

        Model.ModelName = (string?)Root.Attribute("name") ?? "Enterprise model";
        Model.ModelArchiId = (string?)Root.Attribute("id") ?? ArchiObjectIds.FromMermaidKey("model", Model.ModelName);

        Dictionary<string, string> ArchiIdToMermaidId = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> UsedMermaidIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (XElement Element in Root.Descendants("element"))
        {
            string? TypeName = GetTypeLocalName(Element);
            if (string.IsNullOrEmpty(TypeName))
            {
                continue;
            }

            if (TryMapElementType(TypeName, out EnterpriseElementType ElementType, out EnterpriseJunctionKind? JunctionKind))
            {
                string ArchiId = (string?)Element.Attribute("id") ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                string Name = (string?)Element.Attribute("name") ?? string.Empty;
                string MermaidId = ArchiObjectIds.ToMermaidId(ArchiId, Name, UsedMermaidIds);
                ArchiIdToMermaidId[ArchiId] = MermaidId;
                ArchiExchangeElement MappedElement = new ArchiExchangeElement
                {
                    ArchiId = ArchiId,
                    MermaidId = MermaidId,
                    ElementType = ElementType,
                    Label = string.IsNullOrWhiteSpace(Name) ? MermaidId : Name,
                    JunctionKind = JunctionKind
                };
                if (LiveEditorFeatureGate.W_ArchiHintRoundTrip)
                {
                    MappedElement.PlacementHint = ArchiHintProperties.ReadPlacement(Element);
                }

                Model.Elements.Add(MappedElement);
                continue;
            }

            if (TryMapRelationshipType(TypeName, out EnterpriseRelationshipKind Kind))
            {
                string ArchiId = (string?)Element.Attribute("id") ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                string SourceArchiId = (string?)Element.Attribute("source") ?? string.Empty;
                string TargetArchiId = (string?)Element.Attribute("target") ?? string.Empty;
                if (string.IsNullOrEmpty(SourceArchiId) || string.IsNullOrEmpty(TargetArchiId))
                {
                    Model.Warnings.Add("Skipped relationship without source or target: " + ArchiId);
                    continue;
                }

                Model.Relationships.Add(new ArchiExchangeRelationship
                {
                    ArchiId = ArchiId,
                    SourceMermaidId = SourceArchiId,
                    TargetMermaidId = TargetArchiId,
                    Kind = Kind,
                    Label = (string?)Element.Attribute("name") ?? string.Empty,
                    RoutingHint = LiveEditorFeatureGate.W_ArchiHintRoundTrip ? ArchiHintProperties.ReadRouting(Element) : null
                });
            }
            else
            {
                Model.Warnings.Add("Skipped unsupported Archi type: " + TypeName);
            }
        }

        foreach (ArchiExchangeRelationship Relationship in Model.Relationships)
        {
            if (!ArchiIdToMermaidId.TryGetValue(Relationship.SourceMermaidId, out string? SourceMermaidId)
                || !ArchiIdToMermaidId.TryGetValue(Relationship.TargetMermaidId, out string? TargetMermaidId))
            {
                Model.Warnings.Add("Skipped relationship with unmapped endpoints: " + Relationship.ArchiId);
                Relationship.SourceMermaidId = string.Empty;
                continue;
            }

            Relationship.SourceMermaidId = SourceMermaidId;
            Relationship.TargetMermaidId = TargetMermaidId;
        }

        Model.Relationships.RemoveAll(Item => string.IsNullOrEmpty(Item.SourceMermaidId) || string.IsNullOrEmpty(Item.TargetMermaidId));

        if (Model.Elements.Count == 0 && Model.Relationships.Count == 0)
        {
            ErrorMessage = "No enterprise/ArchiMate elements or relationships were found in the Archi model.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes Archi native model XML.
    /// </summary>
    /// <param name="Model">Exchange model.</param>
    /// <returns>XML document text.</returns>
    public static string Write(ArchiExchangeModel Model)
    {
        if (Model == null)
        {
            throw new ArgumentNullException(nameof(Model));
        }

        string ModelId = string.IsNullOrWhiteSpace(Model.ModelArchiId)
            ? ArchiObjectIds.FromMermaidKey("model", Model.ModelName)
            : Model.ModelArchiId;

        XElement Root = new XElement(
            ArchimateNs + "model",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "archimate", ArchimateNs.NamespaceName),
            new XAttribute("name", Model.ModelName),
            new XAttribute("id", ModelId),
            new XAttribute("version", "5.0.0"));

        Dictionary<string, XElement> FoldersByType = CreateStandardFolders(Root);
        Dictionary<string, string> MermaidIdToArchiId = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ArchiExchangeElement Element in Model.Elements)
        {
            MermaidIdToArchiId[Element.MermaidId] = Element.ArchiId;
            string FolderKey = FolderKeyFor(Element.ElementType);
            if (!FoldersByType.TryGetValue(FolderKey, out XElement? Folder))
            {
                Folder = FoldersByType["other"];
            }

            string TypeName = ToArchiElementTypeName(Element);
            XElement Node = new XElement(
                "element",
                new XAttribute(XsiNs + "type", "archimate:" + TypeName),
                new XAttribute("name", string.IsNullOrWhiteSpace(Element.Label) ? Element.MermaidId : Element.Label),
                new XAttribute("id", Element.ArchiId));
            if (LiveEditorFeatureGate.W_ArchiHintRoundTrip)
            {
                ArchiHintProperties.WritePlacement(Node, Element.PlacementHint);
            }

            Folder.Add(Node);
        }

        XElement RelationsFolder = FoldersByType["relations"];
        foreach (ArchiExchangeRelationship Relationship in Model.Relationships)
        {
            if (!MermaidIdToArchiId.TryGetValue(Relationship.SourceMermaidId, out string? SourceArchiId)
                || !MermaidIdToArchiId.TryGetValue(Relationship.TargetMermaidId, out string? TargetArchiId))
            {
                continue;
            }

            string TypeName = ToArchiRelationshipTypeName(Relationship.Kind);
            XElement Node = new XElement(
                "element",
                new XAttribute(XsiNs + "type", "archimate:" + TypeName),
                new XAttribute("id", Relationship.ArchiId),
                new XAttribute("source", SourceArchiId),
                new XAttribute("target", TargetArchiId));
            if (!string.IsNullOrWhiteSpace(Relationship.Label))
            {
                Node.SetAttributeValue("name", Relationship.Label);
            }

            if (LiveEditorFeatureGate.W_ArchiHintRoundTrip) ArchiHintProperties.WriteRouting(Node, Relationship.RoutingHint);
            RelationsFolder.Add(Node);
        }

        XDocument Document = new XDocument(new XDeclaration("1.0", "utf-8", null), Root);
        return Document.Declaration!.ToString() + Environment.NewLine + Document.ToString(SaveOptions.None);
    }

    private static Dictionary<string, XElement> CreateStandardFolders(XElement Root)
    {
        Dictionary<string, XElement> Map = new Dictionary<string, XElement>(StringComparer.Ordinal);
        AddFolder(Root, Map, "Strategy", "strategy", "folder-strategy");
        AddFolder(Root, Map, "Business", "business", "folder-business");
        AddFolder(Root, Map, "Application", "application", "folder-application");
        AddFolder(Root, Map, "Technology & Physical", "technology", "folder-technology");
        AddFolder(Root, Map, "Motivation", "motivation", "folder-motivation");
        AddFolder(Root, Map, "Implementation & Migration", "implementation_migration", "folder-implementation");
        AddFolder(Root, Map, "Other", "other", "folder-other");
        AddFolder(Root, Map, "Relations", "relations", "folder-relations");
        AddFolder(Root, Map, "Views", "diagrams", "folder-views");
        return Map;
    }

    private static void AddFolder(
        XElement Root,
        Dictionary<string, XElement> Map,
        string Name,
        string Type,
        string IdKey)
    {
        XElement Folder = new XElement(
            "folder",
            new XAttribute("name", Name),
            new XAttribute("id", ArchiObjectIds.FromMermaidKey("folder", IdKey)),
            new XAttribute("type", Type));
        Root.Add(Folder);
        Map[Type] = Folder;
    }

    private static string FolderKeyFor(EnterpriseElementType ElementType)
    {
        if (ElementType == EnterpriseElementType.Grouping || ElementType == EnterpriseElementType.Location)
        {
            return "other";
        }

        if (ElementType == EnterpriseElementType.Junction)
        {
            return "other";
        }

        EnterpriseLayer Layer = EnterpriseElementCatalog.Elements[ElementType].Layer;
        return Layer switch
        {
            EnterpriseLayer.Strategy => "strategy",
            EnterpriseLayer.Business => "business",
            EnterpriseLayer.Application => "application",
            EnterpriseLayer.Technology => "technology",
            EnterpriseLayer.Motivation => "motivation",
            EnterpriseLayer.Implementation => "implementation_migration",
            _ => "other"
        };
    }

    private static string? GetTypeLocalName(XElement Element)
    {
        string? Typed = (string?)Element.Attribute(XsiNs + "type");
        if (string.IsNullOrWhiteSpace(Typed))
        {
            Typed = (string?)Element.Attributes().FirstOrDefault(Attribute => Attribute.Name.LocalName == "type");
        }

        if (string.IsNullOrWhiteSpace(Typed))
        {
            return null;
        }

        int Colon = Typed.IndexOf(':');
        if (Colon >= 0 && Colon < Typed.Length - 1)
        {
            return Typed.Substring(Colon + 1);
        }

        return Typed;
    }

    private static bool TryMapElementType(
        string TypeName,
        out EnterpriseElementType ElementType,
        out EnterpriseJunctionKind? JunctionKind)
    {
        JunctionKind = null;
        if (string.Equals(TypeName, "AndJunction", StringComparison.OrdinalIgnoreCase)
            || string.Equals(TypeName, "Junction", StringComparison.OrdinalIgnoreCase))
        {
            ElementType = EnterpriseElementType.Junction;
            JunctionKind = EnterpriseJunctionKind.And;
            return true;
        }

        if (string.Equals(TypeName, "OrJunction", StringComparison.OrdinalIgnoreCase))
        {
            ElementType = EnterpriseElementType.Junction;
            JunctionKind = EnterpriseJunctionKind.Or;
            return true;
        }

        if (EnterpriseElementCatalog.TryResolveElementType(TypeName, out ElementType))
        {
            return true;
        }

        ElementType = default;
        return false;
    }

    private static bool TryMapRelationshipType(string TypeName, out EnterpriseRelationshipKind Kind)
    {
        string Trimmed = TypeName;
        if (Trimmed.EndsWith("Relationship", StringComparison.OrdinalIgnoreCase))
        {
            Trimmed = Trimmed.Substring(0, Trimmed.Length - "Relationship".Length);
        }

        return EnterpriseElementCatalog.TryResolveRelationship(Trimmed, out Kind);
    }

    private static string ToArchiElementTypeName(ArchiExchangeElement Element)
    {
        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            if (Element.JunctionKind == EnterpriseJunctionKind.Or)
            {
                return "OrJunction";
            }

            return "AndJunction";
        }

        return EnterpriseElementCatalog.GetSpecName(Element.ElementType);
    }

    private static string ToArchiRelationshipTypeName(EnterpriseRelationshipKind Kind)
    {
        string Name = EnterpriseElementCatalog.GetRelationshipName(Kind);
        if (string.IsNullOrEmpty(Name))
        {
            return "AssociationRelationship";
        }

        return char.ToUpperInvariant(Name[0]) + Name.Substring(1) + "Relationship";
    }
}
