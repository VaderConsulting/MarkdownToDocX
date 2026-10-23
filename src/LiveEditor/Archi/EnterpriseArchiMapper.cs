using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Archi;

/// <summary>
/// Maps between Mermaid enterprise source and the Archi exchange model.
/// </summary>
internal static class EnterpriseArchiMapper
{
    /// <summary>
    /// Parses Mermaid enterprise source into an exchange model.
    /// </summary>
    /// <param name="Source">Enterprise diagram source.</param>
    /// <param name="Model">Mapped model when the method returns true.</param>
    /// <param name="ErrorMessage">Failure reason when the method returns false.</param>
    /// <returns>True when the source is a usable enterprise model.</returns>
    public static bool TryFromMermaidSource(string? Source, out ArchiExchangeModel Model, out string ErrorMessage)
    {
        Model = new ArchiExchangeModel();
        ErrorMessage = string.Empty;

        if (!EnterpriseParser.IsEnterpriseSource(Source))
        {
            ErrorMessage = "Archi import/export requires an enterprise (ArchiMate) diagram.";
            return false;
        }

        EnterpriseParseResult ParseResult = EnterpriseParser.Parse(Source);
        if (HasBlockingErrors(ParseResult))
        {
            ErrorMessage = FirstErrorMessage(ParseResult) ?? "Enterprise diagram has parse errors.";
            return false;
        }

        EnterpriseDocument Document = ParseResult.Document;
        Model.Version = Document.Version;
        Model.ModelName = string.IsNullOrWhiteSpace(Document.Title) ? "Enterprise model" : Document.Title;
        Model.ModelArchiId = ArchiObjectIds.FromMermaidKey("model", Model.ModelName);

        foreach (KeyValuePair<string, EnterpriseGroup> Pair in Document.Groups)
        {
            EnterpriseGroup Group = Pair.Value;
            ArchiExchangeElement Mapped = new ArchiExchangeElement
            {
                MermaidId = Group.Id,
                ArchiId = ArchiObjectIds.FromMermaidKey("element", Group.Id),
                ElementType = EnterpriseElementType.Grouping,
                Label = string.IsNullOrWhiteSpace(Group.Label) ? Group.Id : Group.Label,
                ParentMermaidId = Group.ParentId
            };
            Model.Elements.Add(Mapped);
        }

        foreach (KeyValuePair<string, EnterpriseElement> Pair in Document.Elements)
        {
            EnterpriseElement Element = Pair.Value;
            ArchiExchangeElement Mapped = new ArchiExchangeElement
            {
                MermaidId = Element.Id,
                ArchiId = ArchiObjectIds.FromMermaidKey("element", Element.Id),
                ElementType = Element.ElementType,
                Label = string.IsNullOrWhiteSpace(Element.Label) ? Element.Id : Element.Label,
                ParentMermaidId = Element.ParentId,
                JunctionKind = Element.JunctionKind,
                PlacementHint = Element.PlacementHint
            };
            Model.Elements.Add(Mapped);
        }

        int RelationshipIndex = 0;
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            string RelKey = Relationship.SourceId
                + "|"
                + EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind)
                + "|"
                + Relationship.TargetId
                + "|"
                + RelationshipIndex.ToString(CultureInfo.InvariantCulture);
            ArchiExchangeRelationship Mapped = new ArchiExchangeRelationship
            {
                ArchiId = ArchiObjectIds.FromMermaidKey("relationship", RelKey),
                SourceMermaidId = Relationship.SourceId,
                TargetMermaidId = Relationship.TargetId,
                Kind = Relationship.Kind,
                Label = Relationship.Label,
                AccessKind = Relationship.AccessKind,
                DirectedAssociation = Relationship.DirectedAssociation
            };
            Model.Relationships.Add(Mapped);
            RelationshipIndex++;
        }

        foreach (EnterpriseDiagnostic Diagnostic in ParseResult.Diagnostics)
        {
            if (Diagnostic.Severity == MermaidDiagnosticSeverity.Warning)
            {
                Model.Warnings.Add(Diagnostic.Message);
            }
        }

        return true;
    }

    /// <summary>
    /// Emits Mermaid enterprise source from an exchange model.
    /// </summary>
    /// <param name="Model">Exchange model.</param>
    /// <returns>Enterprise diagram text.</returns>
    public static string ToMermaidSource(ArchiExchangeModel Model)
    {
        if (Model == null)
        {
            throw new ArgumentNullException(nameof(Model));
        }

        StringBuilder Builder = new StringBuilder();
        Builder.AppendLine("enterprise");
        if (!string.IsNullOrWhiteSpace(Model.ModelName) && !string.Equals(Model.ModelName, "Enterprise model", StringComparison.Ordinal))
        {
            Builder.Append("  title ");
            Builder.AppendLine(Model.ModelName);
        }

        Builder.Append("  version ");
        Builder.AppendLine(Model.Version);

        Dictionary<string, ArchiExchangeElement> ByMermaidId = new Dictionary<string, ArchiExchangeElement>(StringComparer.Ordinal);
        foreach (ArchiExchangeElement Element in Model.Elements)
        {
            if (!string.IsNullOrEmpty(Element.MermaidId))
            {
                ByMermaidId[Element.MermaidId] = Element;
            }
        }

        List<ArchiExchangeElement> Ordered = OrderByParent(Model.Elements);
        foreach (ArchiExchangeElement Element in Ordered)
        {
            string TypeToken = ResolveTypeToken(Element);
            Builder.Append("  ");
            Builder.Append(TypeToken);
            Builder.Append(' ');
            Builder.Append(Element.MermaidId);
            if (!string.IsNullOrWhiteSpace(Element.Label) && !string.Equals(Element.Label, Element.MermaidId, StringComparison.Ordinal))
            {
                Builder.Append(' ');
                Builder.Append(QuoteLabel(Element.Label));
            }

            if (!string.IsNullOrWhiteSpace(Element.ParentMermaidId) && ByMermaidId.ContainsKey(Element.ParentMermaidId))
            {
                Builder.Append(" in ");
                Builder.Append(Element.ParentMermaidId);
            }

            if (LiveEditorFeatureGate.W_ArchiHintRoundTrip)
            {
                string HintSuffix = ArchiHintProperties.FormatPlacementSuffix(Element.PlacementHint);
                if (!string.IsNullOrEmpty(HintSuffix))
                {
                    Builder.Append(HintSuffix);
                }
            }

            Builder.AppendLine();
        }

        foreach (ArchiExchangeRelationship Relationship in Model.Relationships)
        {
            Builder.Append("  ");
            Builder.Append(Relationship.SourceMermaidId);
            Builder.Append(" -");
            Builder.Append(EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind));
            string? Qualifier = BuildQualifier(Relationship);
            if (!string.IsNullOrEmpty(Qualifier))
            {
                Builder.Append('(');
                Builder.Append(Qualifier);
                Builder.Append(')');
            }

            Builder.Append("-> ");
            Builder.Append(Relationship.TargetMermaidId);
            if (!string.IsNullOrWhiteSpace(Relationship.Label))
            {
                Builder.Append(' ');
                Builder.Append(QuoteLabel(Relationship.Label));
            }

            Builder.AppendLine();
        }

        return Builder.ToString();
    }

    private static string ResolveTypeToken(ArchiExchangeElement Element)
    {
        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            if (Element.JunctionKind == EnterpriseJunctionKind.Or)
            {
                return "or-junction";
            }

            return "and-junction";
        }

        return EnterpriseElementCatalog.GetKebabName(Element.ElementType);
    }

    private static string? BuildQualifier(ArchiExchangeRelationship Relationship)
    {
        if (Relationship.Kind == EnterpriseRelationshipKind.Access && Relationship.AccessKind.HasValue)
        {
            return Relationship.AccessKind.Value switch
            {
                EnterpriseAccessKind.Read => "read",
                EnterpriseAccessKind.ReadWrite => "readwrite",
                EnterpriseAccessKind.Access => "access",
                _ => "write"
            };
        }

        if (Relationship.Kind == EnterpriseRelationshipKind.Association && Relationship.DirectedAssociation)
        {
            return "directed";
        }

        return null;
    }

    private static List<ArchiExchangeElement> OrderByParent(List<ArchiExchangeElement> Elements)
    {
        Dictionary<string, ArchiExchangeElement> ById = new Dictionary<string, ArchiExchangeElement>(StringComparer.Ordinal);
        foreach (ArchiExchangeElement Element in Elements)
        {
            ById[Element.MermaidId] = Element;
        }

        List<ArchiExchangeElement> Ordered = new List<ArchiExchangeElement>();
        HashSet<string> Visiting = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> Visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ArchiExchangeElement Element)
        {
            if (Visited.Contains(Element.MermaidId) || Visiting.Contains(Element.MermaidId))
            {
                return;
            }

            Visiting.Add(Element.MermaidId);
            if (!string.IsNullOrEmpty(Element.ParentMermaidId)
                && ById.TryGetValue(Element.ParentMermaidId, out ArchiExchangeElement? Parent))
            {
                Visit(Parent);
            }

            Visiting.Remove(Element.MermaidId);
            Visited.Add(Element.MermaidId);
            Ordered.Add(Element);
        }

        foreach (ArchiExchangeElement Element in Elements)
        {
            Visit(Element);
        }

        return Ordered;
    }

    private static string QuoteLabel(string Label)
    {
        string Escaped = Label
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + Escaped + "\"";
    }

    private static bool HasBlockingErrors(EnterpriseParseResult ParseResult)
    {
        foreach (EnterpriseDiagnostic Diagnostic in ParseResult.Diagnostics)
        {
            if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    private static string? FirstErrorMessage(EnterpriseParseResult ParseResult)
    {
        foreach (EnterpriseDiagnostic Diagnostic in ParseResult.Diagnostics)
        {
            if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error)
            {
                return Diagnostic.Message;
            }
        }

        return null;
    }
}
