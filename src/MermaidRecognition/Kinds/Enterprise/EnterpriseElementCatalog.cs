using System;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Catalogue of in-scope element types, relationship letters, and display names.
/// </summary>
public static class EnterpriseElementCatalog
{
    private static readonly Dictionary<EnterpriseElementType, EnterpriseElementMetadata> _ByType = BuildByType();

    private static readonly Dictionary<string, EnterpriseElementType> _ByToken = BuildByToken();

    private static readonly Dictionary<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> _Relationships = BuildRelationships();

    private static readonly Dictionary<string, EnterpriseRelationshipKind> _RelationshipByToken = BuildRelationshipTokens();

    /// <summary>
    /// Metadata for every in-scope element type.
    /// </summary>
    public static IReadOnlyDictionary<EnterpriseElementType, EnterpriseElementMetadata> Elements
    {
        get
        {
            return _ByType;
        }
    }

    /// <summary>
    /// Metadata for every relationship kind.
    /// </summary>
    public static IReadOnlyDictionary<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> Relationships
    {
        get
        {
            return _Relationships;
        }
    }

    /// <summary>
    /// Resolves a kebab-case or specification CamelCase type token.
    /// </summary>
    /// <param name="Token">Type word from the source line.</param>
    /// <param name="ElementType">Resolved type when the method returns true.</param>
    /// <returns>True when the token is an in-scope element type.</returns>
    public static bool TryResolveElementType(string Token, out EnterpriseElementType ElementType)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ElementType = default;
            return false;
        }

        string Normalised = Token.Trim().ToLowerInvariant();
        if (_ByToken.TryGetValue(Normalised, out ElementType))
        {
            return true;
        }

        string Compact = Normalised.Replace("-", string.Empty, StringComparison.Ordinal);
        return _ByToken.TryGetValue(Compact, out ElementType);
    }

    /// <summary>
    /// Resolves a relationship name or single-letter code.
    /// </summary>
    /// <param name="Token">Relationship word or letter.</param>
    /// <param name="Kind">Resolved kind when the method returns true.</param>
    /// <returns>True when the token is a known relationship.</returns>
    public static bool TryResolveRelationship(string Token, out EnterpriseRelationshipKind Kind)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            Kind = default;
            return false;
        }

        return _RelationshipByToken.TryGetValue(Token.Trim().ToLowerInvariant(), out Kind);
    }

    /// <summary>
    /// Returns the kebab-case type id used in the rules JSON.
    /// </summary>
    /// <param name="ElementType">Element type.</param>
    /// <returns>Kebab-case identifier.</returns>
    public static string GetKebabName(EnterpriseElementType ElementType)
    {
        return _ByType[ElementType].KebabName;
    }

    /// <summary>
    /// Returns the type token written in source for an element. Junctions keep and-junction or or-junction.
    /// </summary>
    /// <param name="Element">Declared element.</param>
    /// <returns>Source type token.</returns>
    public static string GetSourceToken(EnterpriseElement Element)
    {
        if (Element == null)
        {
            throw new ArgumentNullException(nameof(Element));
        }

        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            if (Element.JunctionKind == EnterpriseJunctionKind.Or)
            {
                return "or-junction";
            }

            return "and-junction";
        }

        return GetKebabName(Element.ElementType);
    }

    /// <summary>
    /// Returns the specification CamelCase type name used in diagnostics.
    /// </summary>
    /// <param name="ElementType">Element type.</param>
    /// <returns>Specification type name.</returns>
    public static string GetSpecName(EnterpriseElementType ElementType)
    {
        return _ByType[ElementType].SpecName;
    }

    /// <summary>
    /// Returns the relationship letter used in the rules matrix.
    /// </summary>
    /// <param name="Kind">Relationship kind.</param>
    /// <returns>Single-letter code.</returns>
    public static char GetLetter(EnterpriseRelationshipKind Kind)
    {
        return _Relationships[Kind].Letter;
    }

    /// <summary>
    /// Returns the canonical relationship name.
    /// </summary>
    /// <param name="Kind">Relationship kind.</param>
    /// <returns>Lower-case relationship name.</returns>
    public static string GetRelationshipName(EnterpriseRelationshipKind Kind)
    {
        return _Relationships[Kind].Name;
    }

    /// <summary>
    /// Returns the layer fill colour used by specification example figures.
    /// </summary>
    /// <param name="Layer">Layer or aspect.</param>
    /// <returns>Hex fill.</returns>
    public static string GetLayerFill(EnterpriseLayer Layer)
    {
        switch (Layer)
        {
            case EnterpriseLayer.Motivation:
                return "#CCCCFF";
            case EnterpriseLayer.Strategy:
                return "#F8D0C8";
            case EnterpriseLayer.Business:
                return "#FFFFAF";
            case EnterpriseLayer.Application:
                return "#AFFFFF";
            case EnterpriseLayer.Technology:
                return "#AFFFAF";
            case EnterpriseLayer.Implementation:
                return "#FFE0E0";
            case EnterpriseLayer.Composite:
                return "#FFFFFF";
            case EnterpriseLayer.Junction:
                return "#FFFFFF";
            default:
                return "#FFFFFF";
        }
    }

    /// <summary>
    /// Returns the layout rank so Motivation/Strategy sit above Business, Application, then Technology.
    /// </summary>
    /// <param name="Layer">Layer or aspect.</param>
    /// <returns>Smaller values are placed higher in TB layout.</returns>
    public static int GetLayoutRank(EnterpriseLayer Layer)
    {
        switch (Layer)
        {
            case EnterpriseLayer.Motivation:
                return 0;
            case EnterpriseLayer.Strategy:
                return 1;
            case EnterpriseLayer.Business:
                return 2;
            case EnterpriseLayer.Application:
                return 3;
            case EnterpriseLayer.Technology:
                return 4;
            case EnterpriseLayer.Implementation:
                return 5;
            default:
                return 3;
        }
    }

    /// <summary>
    /// Resolves a relationship letter to its kind.
    /// </summary>
    /// <param name="Letter">Matrix letter.</param>
    /// <param name="Kind">Resolved kind when the method returns true.</param>
    /// <returns>True when the letter is known.</returns>
    public static bool TryResolveLetter(char Letter, out EnterpriseRelationshipKind Kind)
    {
        foreach (KeyValuePair<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> Entry in _Relationships)
        {
            if (Entry.Value.Letter == Letter)
            {
                Kind = Entry.Key;
                return true;
            }
        }

        Kind = default;
        return false;
    }

    private static Dictionary<EnterpriseElementType, EnterpriseElementMetadata> BuildByType()
    {
        Dictionary<EnterpriseElementType, EnterpriseElementMetadata> Map = new Dictionary<EnterpriseElementType, EnterpriseElementMetadata>();
        Add(Map, EnterpriseElementType.ApplicationComponent, "application-component", "ApplicationComponent", EnterpriseLayer.Application, EnterpriseAspect.Active, "component", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ApplicationCollaboration, "application-collaboration", "ApplicationCollaboration", EnterpriseLayer.Application, EnterpriseAspect.Active, "collaboration", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ApplicationInterface, "application-interface", "ApplicationInterface", EnterpriseLayer.Application, EnterpriseAspect.Active, "interface", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ApplicationFunction, "application-function", "ApplicationFunction", EnterpriseLayer.Application, EnterpriseAspect.Behaviour, "function", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ApplicationInteraction, "application-interaction", "ApplicationInteraction", EnterpriseLayer.Application, EnterpriseAspect.Behaviour, "interaction", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ApplicationProcess, "application-process", "ApplicationProcess", EnterpriseLayer.Application, EnterpriseAspect.Behaviour, "process", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ApplicationEvent, "application-event", "ApplicationEvent", EnterpriseLayer.Application, EnterpriseAspect.Behaviour, "event", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ApplicationService, "application-service", "ApplicationService", EnterpriseLayer.Application, EnterpriseAspect.Behaviour, "service", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.DataObject, "data-object", "DataObject", EnterpriseLayer.Application, EnterpriseAspect.Passive, null, EnterpriseBoxKind.Object);
        Add(Map, EnterpriseElementType.Node, "node", "Node", EnterpriseLayer.Technology, EnterpriseAspect.Active, "node", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Device, "device", "Device", EnterpriseLayer.Technology, EnterpriseAspect.Active, "device", EnterpriseBoxKind.Box3d);
        Add(Map, EnterpriseElementType.SystemSoftware, "system-software", "SystemSoftware", EnterpriseLayer.Technology, EnterpriseAspect.Active, "systemSoftware", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.TechnologyCollaboration, "technology-collaboration", "TechnologyCollaboration", EnterpriseLayer.Technology, EnterpriseAspect.Active, "collaboration", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.TechnologyInterface, "technology-interface", "TechnologyInterface", EnterpriseLayer.Technology, EnterpriseAspect.Active, "interface", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Path, "path", "Path", EnterpriseLayer.Technology, EnterpriseAspect.Active, "path", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.CommunicationNetwork, "communication-network", "CommunicationNetwork", EnterpriseLayer.Technology, EnterpriseAspect.Active, "network", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.TechnologyFunction, "technology-function", "TechnologyFunction", EnterpriseLayer.Technology, EnterpriseAspect.Behaviour, "function", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.TechnologyProcess, "technology-process", "TechnologyProcess", EnterpriseLayer.Technology, EnterpriseAspect.Behaviour, "process", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.TechnologyInteraction, "technology-interaction", "TechnologyInteraction", EnterpriseLayer.Technology, EnterpriseAspect.Behaviour, "interaction", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.TechnologyEvent, "technology-event", "TechnologyEvent", EnterpriseLayer.Technology, EnterpriseAspect.Behaviour, "event", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.TechnologyService, "technology-service", "TechnologyService", EnterpriseLayer.Technology, EnterpriseAspect.Behaviour, "service", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Artifact, "artifact", "Artifact", EnterpriseLayer.Technology, EnterpriseAspect.Passive, "artifact", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Stakeholder, "stakeholder", "Stakeholder", EnterpriseLayer.Motivation, EnterpriseAspect.Active, "stakeholder", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Driver, "driver", "Driver", EnterpriseLayer.Motivation, EnterpriseAspect.Active, "driver", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Assessment, "assessment", "Assessment", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "assessment", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Goal, "goal", "Goal", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "goal", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Outcome, "outcome", "Outcome", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "outcome", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Principle, "principle", "Principle", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "principle", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Requirement, "requirement", "Requirement", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "requirement", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Constraint, "constraint", "Constraint", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "constraint", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Meaning, "meaning", "Meaning", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "meaning", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Value, "value", "Value", EnterpriseLayer.Motivation, EnterpriseAspect.Passive, "value", EnterpriseBoxKind.Ellipse);
        Add(Map, EnterpriseElementType.Resource, "resource", "Resource", EnterpriseLayer.Strategy, EnterpriseAspect.Passive, "resource", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Capability, "capability", "Capability", EnterpriseLayer.Strategy, EnterpriseAspect.Behaviour, "capability", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ValueStream, "value-stream", "ValueStream", EnterpriseLayer.Strategy, EnterpriseAspect.Behaviour, "valueStream", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.CourseOfAction, "course-of-action", "CourseOfAction", EnterpriseLayer.Strategy, EnterpriseAspect.Behaviour, "courseOfAction", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessActor, "business-actor", "BusinessActor", EnterpriseLayer.Business, EnterpriseAspect.Active, "actor", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessRole, "business-role", "BusinessRole", EnterpriseLayer.Business, EnterpriseAspect.Active, "role", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessCollaboration, "business-collaboration", "BusinessCollaboration", EnterpriseLayer.Business, EnterpriseAspect.Active, "collaboration", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessInterface, "business-interface", "BusinessInterface", EnterpriseLayer.Business, EnterpriseAspect.Active, "interface", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessFunction, "business-function", "BusinessFunction", EnterpriseLayer.Business, EnterpriseAspect.Behaviour, "function", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessInteraction, "business-interaction", "BusinessInteraction", EnterpriseLayer.Business, EnterpriseAspect.Behaviour, "interaction", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessProcess, "business-process", "BusinessProcess", EnterpriseLayer.Business, EnterpriseAspect.Behaviour, "process", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessEvent, "business-event", "BusinessEvent", EnterpriseLayer.Business, EnterpriseAspect.Behaviour, "event", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessService, "business-service", "BusinessService", EnterpriseLayer.Business, EnterpriseAspect.Behaviour, "service", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.BusinessObject, "business-object", "BusinessObject", EnterpriseLayer.Business, EnterpriseAspect.Passive, null, EnterpriseBoxKind.Object);
        Add(Map, EnterpriseElementType.Contract, "contract", "Contract", EnterpriseLayer.Business, EnterpriseAspect.Passive, null, EnterpriseBoxKind.Contract);
        Add(Map, EnterpriseElementType.Representation, "representation", "Representation", EnterpriseLayer.Business, EnterpriseAspect.Passive, "representation", EnterpriseBoxKind.Representation);
        Add(Map, EnterpriseElementType.Product, "product", "Product", EnterpriseLayer.Business, EnterpriseAspect.Passive, "product", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Equipment, "equipment", "Equipment", EnterpriseLayer.Technology, EnterpriseAspect.Active, "equipment", EnterpriseBoxKind.Box3d);
        Add(Map, EnterpriseElementType.Facility, "facility", "Facility", EnterpriseLayer.Technology, EnterpriseAspect.Active, "facility", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.DistributionNetwork, "distribution-network", "DistributionNetwork", EnterpriseLayer.Technology, EnterpriseAspect.Active, "distributionNetwork", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Material, "material", "Material", EnterpriseLayer.Technology, EnterpriseAspect.Passive, "material", EnterpriseBoxKind.Object);
        Add(Map, EnterpriseElementType.WorkPackage, "work-package", "WorkPackage", EnterpriseLayer.Implementation, EnterpriseAspect.Behaviour, "workPackage", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Deliverable, "deliverable", "Deliverable", EnterpriseLayer.Implementation, EnterpriseAspect.Passive, "deliverable", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.ImplementationEvent, "implementation-event", "ImplementationEvent", EnterpriseLayer.Implementation, EnterpriseAspect.Behaviour, "event", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Plateau, "plateau", "Plateau", EnterpriseLayer.Implementation, EnterpriseAspect.Passive, "plateau", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Gap, "gap", "Gap", EnterpriseLayer.Implementation, EnterpriseAspect.Passive, "gap", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Grouping, "grouping", "Grouping", EnterpriseLayer.Composite, EnterpriseAspect.Active, null, EnterpriseBoxKind.Grouping);
        Add(Map, EnterpriseElementType.Location, "location", "Location", EnterpriseLayer.Composite, EnterpriseAspect.Active, "location", EnterpriseBoxKind.Rectangle);
        Add(Map, EnterpriseElementType.Junction, "junction", "Junction", EnterpriseLayer.Junction, EnterpriseAspect.Active, null, EnterpriseBoxKind.Junction);
        return Map;
    }

    private static void Add(
        Dictionary<EnterpriseElementType, EnterpriseElementMetadata> Map,
        EnterpriseElementType ElementType,
        string KebabName,
        string SpecName,
        EnterpriseLayer Layer,
        EnterpriseAspect Aspect,
        string? Icon,
        EnterpriseBoxKind BoxKind)
    {
        Map[ElementType] = new EnterpriseElementMetadata(ElementType, KebabName, SpecName, Layer, Aspect, Icon, BoxKind);
    }

    private static Dictionary<string, EnterpriseElementType> BuildByToken()
    {
        Dictionary<string, EnterpriseElementType> Map = new Dictionary<string, EnterpriseElementType>(StringComparer.OrdinalIgnoreCase);
        foreach (EnterpriseElementMetadata Metadata in _ByType.Values)
        {
            Map[Metadata.KebabName] = Metadata.ElementType;
            Map[Metadata.KebabName.Replace("-", string.Empty, StringComparison.Ordinal)] = Metadata.ElementType;
            Map[Metadata.SpecName.ToLowerInvariant()] = Metadata.ElementType;
        }

        Map["group"] = EnterpriseElementType.Grouping;
        Map["and-junction"] = EnterpriseElementType.Junction;
        Map["or-junction"] = EnterpriseElementType.Junction;
        Map["junction-and"] = EnterpriseElementType.Junction;
        Map["junction-or"] = EnterpriseElementType.Junction;
        Map["andjunction"] = EnterpriseElementType.Junction;
        Map["orjunction"] = EnterpriseElementType.Junction;
        return Map;
    }

    private static Dictionary<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> BuildRelationships()
    {
        Dictionary<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> Map = new Dictionary<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata>();
        Map[EnterpriseRelationshipKind.Composition] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Composition, "composition", 'c', "solid", "diamondFilled", null);
        Map[EnterpriseRelationshipKind.Aggregation] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Aggregation, "aggregation", 'g', "solid", "diamondHollow", null);
        Map[EnterpriseRelationshipKind.Assignment] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Assignment, "assignment", 'i', "solid", "dot", "arrowFilled");
        Map[EnterpriseRelationshipKind.Realization] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Realization, "realization", 'r', "dotted", null, "triangleHollow");
        Map[EnterpriseRelationshipKind.Serving] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Serving, "serving", 'v', "solid", null, "arrowOpen");
        Map[EnterpriseRelationshipKind.Access] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Access, "access", 'a', "dotted", null, "arrowOpenSmall");
        Map[EnterpriseRelationshipKind.Influence] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Influence, "influence", 'n', "dashed", null, "arrowOpen");
        Map[EnterpriseRelationshipKind.Triggering] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Triggering, "triggering", 't', "solid", null, "arrowFilled");
        Map[EnterpriseRelationshipKind.Flow] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Flow, "flow", 'f', "dashed", null, "arrowFilled");
        Map[EnterpriseRelationshipKind.Specialization] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Specialization, "specialization", 's', "solid", null, "triangleHollow");
        Map[EnterpriseRelationshipKind.Association] = new EnterpriseRelationshipMetadata(EnterpriseRelationshipKind.Association, "association", 'o', "solid", null, null);
        return Map;
    }

    private static Dictionary<string, EnterpriseRelationshipKind> BuildRelationshipTokens()
    {
        Dictionary<string, EnterpriseRelationshipKind> Map = new Dictionary<string, EnterpriseRelationshipKind>(StringComparer.OrdinalIgnoreCase);
        foreach (EnterpriseRelationshipMetadata Metadata in _Relationships.Values)
        {
            Map[Metadata.Name] = Metadata.Kind;
            Map[Metadata.Letter.ToString()] = Metadata.Kind;
        }

        return Map;
    }
}
