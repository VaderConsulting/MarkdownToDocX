using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Maps an enterprise diagram onto the shared recognition contract.
/// </summary>
public sealed class EnterpriseKindRecognizer : IKindRecognizer
{
    /// <inheritdoc />
    public DiagramContract Parse(string Source, DiagramDetectionResult Detection)
    {
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source ?? string.Empty);
        DiagramContract Contract = new DiagramContract
        {
            Kind = "enterprise",
            Header = "enterprise",
            LayoutMode = Analysis.Document.LayoutMode,
            Direction = Analysis.Document.Direction.ToString().ToUpperInvariant()
        };
        Contract.Accessibility.Title = Analysis.Document.AccessibilityTitle;
        Contract.Accessibility.Description = Analysis.Document.AccessibilityDescription;

        foreach (EnterpriseGroup Group in Analysis.Document.Groups.Values)
        {
            GroupContract GroupContract = new GroupContract
            {
                Id = Group.Id,
                Title = Group.Label,
                ParentGroupId = Group.ParentId,
                Direction = Group.Direction?.ToString().ToUpperInvariant(),
                PlacementHint = Group.PlacementHint
            };
            Contract.GroupsMutable.Add(GroupContract);
        }

        foreach (EnterpriseElement Element in Analysis.Document.Elements.Values)
        {
            if (Element.ElementType == EnterpriseElementType.Grouping
                && Analysis.Document.Groups.ContainsKey(Element.Id))
            {
                continue;
            }

            NodeContract Node = new NodeContract
            {
                Id = Element.Id,
                Label = Element.Label,
                Shape = EnterpriseElementCatalog.GetKebabName(Element.ElementType),
                PlacementHint = Element.PlacementHint
            };
            Contract.NodesMutable.Add(Node);
            if (!string.IsNullOrEmpty(Element.ParentId))
            {
                foreach (GroupContract GroupContract in Contract.GroupsMutable)
                {
                    if (GroupContract.Id == Element.ParentId)
                    {
                        GroupContract.ChildNodeIdsMutable.Add(Element.Id);
                        break;
                    }
                }
            }
        }

        foreach (EnterpriseRelationship Relationship in Analysis.Document.Relationships)
        {
            EdgeContract Edge = new EdgeContract
            {
                SourceId = Relationship.SourceId,
                TargetId = Relationship.TargetId,
                Label = Relationship.Label,
                RoutingHint = Relationship.RoutingHint
            };
            Contract.EdgesMutable.Add(Edge);
        }

        foreach (EnterpriseDiagnostic Diagnostic in Analysis.Diagnostics)
        {
            MermaidSourceSpan? Span = Diagnostic.LineNumber <= 0
                ? null
                : new MermaidSourceSpan(0, 0, Diagnostic.LineNumber, 1, Diagnostic.LineNumber, 1);
            Contract.DiagnosticsMutable.Add(new MermaidDiagnostic(
                Diagnostic.Severity,
                "enterprise.diagnostic",
                Diagnostic.Message,
                Span,
                null));
        }

        return Contract;
    }
}
