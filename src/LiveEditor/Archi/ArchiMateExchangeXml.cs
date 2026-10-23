using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Archi;

/// <summary>
/// Reads and writes Open Group ArchiMate Model Exchange XML (3.0).
/// </summary>
internal static class ArchiMateExchangeXml
{
    private static readonly XNamespace ExchangeNs = "http://www.opengroup.org/xsd/archimate/3.0/";

    private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Parses Model Exchange XML into an exchange model.
    /// </summary>
    /// <param name="Xml">Exchange XML text.</param>
    /// <param name="Model">Mapped model when successful.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when supported content was found.</returns>
    public static bool TryRead(string Xml, out ArchiExchangeModel Model, out string ErrorMessage)
    {
        Model = new ArchiExchangeModel();
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Xml))
        {
            ErrorMessage = "Model Exchange XML is empty.";
            return false;
        }

        XDocument Document;
        try
        {
            Document = XDocument.Parse(Xml);
        }
        catch (Exception Ex)
        {
            ErrorMessage = "Could not parse Model Exchange XML: " + Ex.Message;
            return false;
        }

        XElement? Root = Document.Root;
        if (Root == null)
        {
            ErrorMessage = "Model Exchange XML has no root element.";
            return false;
        }

        Model.ModelArchiId = (string?)Root.Attribute("identifier")
            ?? (string?)Root.Attribute("id")
            ?? ArchiObjectIds.FromMermaidKey("model", "exchange");
        Model.ModelName = ReadName(Root) ?? (string?)Root.Attribute("name") ?? "Enterprise model";

        Dictionary<string, string> ArchiIdToMermaidId = new Dictionary<string, string>(StringComparer.Ordinal);
        HashSet<string> UsedMermaidIds = new HashSet<string>(StringComparer.Ordinal);

        XElement? ElementsNode = Root.Element(ExchangeNs + "elements") ?? Root.Element("elements");
        if (ElementsNode != null)
        {
            foreach (XElement Element in ElementsNode.Elements())
            {
                if (!string.Equals(Element.Name.LocalName, "element", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? TypeName = GetTypeLocalName(Element);
                if (string.IsNullOrEmpty(TypeName)
                    || !TryMapElementType(TypeName, out EnterpriseElementType ElementType, out EnterpriseJunctionKind? JunctionKind))
                {
                    if (!string.IsNullOrEmpty(TypeName))
                    {
                        Model.Warnings.Add("Skipped unsupported exchange element type: " + TypeName);
                    }

                    continue;
                }

                string ArchiId = (string?)Element.Attribute("identifier")
                    ?? (string?)Element.Attribute("id")
                    ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                string Name = ReadName(Element) ?? string.Empty;
                string MermaidId = ArchiObjectIds.ToMermaidId(ArchiId, Name, UsedMermaidIds);
                ArchiIdToMermaidId[ArchiId] = MermaidId;
                Model.Elements.Add(new ArchiExchangeElement
                {
                    ArchiId = ArchiId,
                    MermaidId = MermaidId,
                    ElementType = ElementType,
                    Label = string.IsNullOrWhiteSpace(Name) ? MermaidId : Name,
                    JunctionKind = JunctionKind
                });
            }
        }

        XElement? RelationshipsNode = Root.Element(ExchangeNs + "relationships") ?? Root.Element("relationships");
        if (RelationshipsNode != null)
        {
            foreach (XElement Relationship in RelationshipsNode.Elements())
            {
                if (!string.Equals(Relationship.Name.LocalName, "relationship", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? TypeName = GetTypeLocalName(Relationship);
                if (string.IsNullOrEmpty(TypeName) || !TryMapRelationshipType(TypeName, out EnterpriseRelationshipKind Kind))
                {
                    if (!string.IsNullOrEmpty(TypeName))
                    {
                        Model.Warnings.Add("Skipped unsupported exchange relationship type: " + TypeName);
                    }

                    continue;
                }

                string ArchiId = (string?)Relationship.Attribute("identifier")
                    ?? (string?)Relationship.Attribute("id")
                    ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                string SourceArchiId = (string?)Relationship.Attribute("source") ?? string.Empty;
                string TargetArchiId = (string?)Relationship.Attribute("target") ?? string.Empty;
                if (!ArchiIdToMermaidId.TryGetValue(SourceArchiId, out string? SourceMermaidId)
                    || !ArchiIdToMermaidId.TryGetValue(TargetArchiId, out string? TargetMermaidId))
                {
                    Model.Warnings.Add("Skipped exchange relationship with unmapped endpoints: " + ArchiId);
                    continue;
                }

                Model.Relationships.Add(new ArchiExchangeRelationship
                {
                    ArchiId = ArchiId,
                    SourceMermaidId = SourceMermaidId,
                    TargetMermaidId = TargetMermaidId,
                    Kind = Kind,
                    Label = ReadName(Relationship) ?? string.Empty
                });
            }
        }

        if (Model.Elements.Count == 0 && Model.Relationships.Count == 0)
        {
            ErrorMessage = "No enterprise/ArchiMate content was found in the Model Exchange file.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes Open Group Model Exchange XML.
    /// </summary>
    /// <param name="Model">Exchange model.</param>
    /// <returns>XML text.</returns>
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
            ExchangeNs + "model",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute("identifier", ModelId),
            new XElement(ExchangeNs + "name", Model.ModelName));

        XElement ElementsNode = new XElement(ExchangeNs + "elements");
        Dictionary<string, string> MermaidIdToArchiId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ArchiExchangeElement Element in Model.Elements)
        {
            MermaidIdToArchiId[Element.MermaidId] = Element.ArchiId;
            string TypeName = ToExchangeElementTypeName(Element);
            XElement Node = new XElement(
                ExchangeNs + "element",
                new XAttribute("identifier", Element.ArchiId),
                new XAttribute(XsiNs + "type", TypeName),
                new XElement(ExchangeNs + "name", string.IsNullOrWhiteSpace(Element.Label) ? Element.MermaidId : Element.Label));
            ElementsNode.Add(Node);
        }

        Root.Add(ElementsNode);

        XElement RelationshipsNode = new XElement(ExchangeNs + "relationships");
        foreach (ArchiExchangeRelationship Relationship in Model.Relationships)
        {
            if (!MermaidIdToArchiId.TryGetValue(Relationship.SourceMermaidId, out string? SourceArchiId)
                || !MermaidIdToArchiId.TryGetValue(Relationship.TargetMermaidId, out string? TargetArchiId))
            {
                continue;
            }

            XElement Node = new XElement(
                ExchangeNs + "relationship",
                new XAttribute("identifier", Relationship.ArchiId),
                new XAttribute("source", SourceArchiId),
                new XAttribute("target", TargetArchiId),
                new XAttribute(XsiNs + "type", ToExchangeRelationshipTypeName(Relationship.Kind)));
            if (!string.IsNullOrWhiteSpace(Relationship.Label))
            {
                Node.Add(new XElement(ExchangeNs + "name", Relationship.Label));
            }

            RelationshipsNode.Add(Node);
        }

        Root.Add(RelationshipsNode);

        XDocument Document = new XDocument(new XDeclaration("1.0", "utf-8", null), Root);
        return Document.Declaration!.ToString() + Environment.NewLine + Document.ToString(SaveOptions.None);
    }

    private static string? ReadName(XElement Element)
    {
        XElement? NameNode = Element.Element(ExchangeNs + "name") ?? Element.Element("name");
        if (NameNode == null)
        {
            return null;
        }

        return NameNode.Value;
    }

    private static string? GetTypeLocalName(XElement Element)
    {
        string? Typed = (string?)Element.Attribute(XsiNs + "type");
        if (string.IsNullOrWhiteSpace(Typed))
        {
            Typed = (string?)Element.Attribute("xsi:type") ?? (string?)Element.Attribute("type");
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

        return EnterpriseElementCatalog.TryResolveElementType(TypeName, out ElementType);
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

    private static string ToExchangeElementTypeName(ArchiExchangeElement Element)
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

    private static string ToExchangeRelationshipTypeName(EnterpriseRelationshipKind Kind)
    {
        string Name = EnterpriseElementCatalog.GetRelationshipName(Kind);
        if (string.IsNullOrEmpty(Name))
        {
            return "Association";
        }

        return char.ToUpperInvariant(Name[0]) + Name.Substring(1);
    }
}
