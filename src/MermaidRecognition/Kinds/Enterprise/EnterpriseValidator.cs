using System.Globalization;
using System.Text;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Validates relationships and nesting against the embedded enterprise rule tables.
/// </summary>
public static class EnterpriseValidator
{
    private static readonly HashSet<EnterpriseRelationshipKind> NestableKinds = new HashSet<EnterpriseRelationshipKind>
    {
        EnterpriseRelationshipKind.Composition,
        EnterpriseRelationshipKind.Aggregation,
        EnterpriseRelationshipKind.Assignment,
        EnterpriseRelationshipKind.Realization,
        EnterpriseRelationshipKind.Access,
        EnterpriseRelationshipKind.Specialization
    };

    /// <summary>
    /// Text appended when a 3.2 check hits one of the 15 unverified cells.
    /// </summary>
    public const string PROVISIONAL_NOTE = "(provisional 3.2 rule, not yet verified against the 3.2 specification)";

    /// <summary>
    /// Validates a parsed document and marks relationships hidden by nesting.
    /// </summary>
    /// <param name="Document">Parsed document.</param>
    /// <param name="Catalog">Rule tables. When null, the embedded catalog is used.</param>
    /// <returns>Validation diagnostics.</returns>
    public static List<EnterpriseDiagnostic> Validate(EnterpriseDocument Document, EnterpriseRulesCatalog? Catalog)
    {
        if (Document == null)
        {
            throw new ArgumentNullException(nameof(Document));
        }

        EnterpriseRulesCatalog Rules = Catalog ?? EnterpriseRulesCatalog.Instance;
        List<EnterpriseDiagnostic> Diagnostics = new List<EnterpriseDiagnostic>();
        if (string.Equals(Document.Version, "3.2", StringComparison.Ordinal))
        {
            Diagnostics.Add(new EnterpriseDiagnostic(
                MermaidDiagnosticSeverity.Info,
                0,
                "version 3.2: 15 Application/Technology cells that differ from 3.1, and every cell that involves Business, Motivation, Strategy, Physical, Implementation, composite or junction types, use provisional values not yet verified against the 3.2 specification text."));
        }

        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (!Document.Elements.TryGetValue(Relationship.SourceId, out EnterpriseElement? Source)
                || !Document.Elements.TryGetValue(Relationship.TargetId, out EnterpriseElement? Target))
            {
                continue;
            }

            string SourceType = EnterpriseElementCatalog.GetKebabName(Source.ElementType);
            string TargetType = EnterpriseElementCatalog.GetKebabName(Target.ElementType);
            string AllowedLetters = Rules.GetAllowedLetters(Document.Version, SourceType, TargetType);
            char Letter = EnterpriseElementCatalog.GetLetter(Relationship.Kind);
            // Archi allows every relationship letter into a junction (3.2) or a large subset (3.1).
            // Those cells stay in the JSON as Archi published them. And/or polarity is not a letter
            // restriction; it is carried on the junction element in the parser and renderer.
            bool Provisional = string.Equals(Document.Version, "3.2", StringComparison.Ordinal)
                && Rules.IsProvisionalCell(SourceType, TargetType);
            if (AllowedLetters.IndexOf(Letter) < 0)
            {
                Diagnostics.Add(new EnterpriseDiagnostic(
                    MermaidDiagnosticSeverity.Error,
                    Relationship.LineNumber,
                    FormatDisallowedMessage(Document.Version, Relationship, Source, Target, AllowedLetters, Provisional)));
            }
            else if (Provisional)
            {
                Diagnostics.Add(new EnterpriseDiagnostic(
                    MermaidDiagnosticSeverity.Info,
                    Relationship.LineNumber,
                    "line " + Relationship.LineNumber.ToString(CultureInfo.InvariantCulture)
                    + ": " + EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind)
                    + " from " + EnterpriseElementCatalog.GetSpecName(Source.ElementType)
                    + " \"" + Source.Label + "\" to "
                    + EnterpriseElementCatalog.GetSpecName(Target.ElementType)
                    + " \"" + Target.Label + "\" " + PROVISIONAL_NOTE + "."));
            }

            if (IsNestingRelationship(Document, Relationship, Source, Target))
            {
                Relationship.HiddenByNesting = true;
            }
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (string.IsNullOrEmpty(Element.ParentId) || !Document.Elements.ContainsKey(Element.ParentId))
            {
                continue;
            }

            if (Document.Elements.TryGetValue(Element.ParentId, out EnterpriseElement? Parent)
                && Parent.ElementType == EnterpriseElementType.Grouping)
            {
                continue;
            }

            bool HasNestable = false;
            foreach (EnterpriseRelationship Relationship in Document.Relationships)
            {
                if (Relationship.SourceId == Element.ParentId
                    && Relationship.TargetId == Element.Id
                    && NestableKinds.Contains(Relationship.Kind))
                {
                    HasNestable = true;
                    break;
                }
            }

            if (!HasNestable)
            {
                Diagnostics.Add(new EnterpriseDiagnostic(
                    MermaidDiagnosticSeverity.Warning,
                    Element.LineNumber,
                    "line " + Element.LineNumber.ToString(CultureInfo.InvariantCulture)
                    + ": \"" + Element.Id + "\" is nested in element \"" + Element.ParentId
                    + "\" but no composition/aggregation/assignment/realization/access/specialization from \""
                    + Element.ParentId + "\" to \"" + Element.Id + "\" is declared; the nesting is ambiguous."));
            }
        }

        return Diagnostics;
    }

    /// <summary>
    /// Parses source and validates it in one step.
    /// </summary>
    /// <param name="Source">Diagram source.</param>
    /// <returns>Parse result plus validation diagnostics.</returns>
    public static EnterpriseAnalysisResult Analyze(string Source)
    {
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Source);
        List<EnterpriseDiagnostic> Combined = new List<EnterpriseDiagnostic>(Parsed.Diagnostics);
        if (!Parsed.HasErrors)
        {
            Combined.AddRange(Validate(Parsed.Document, null));
        }

        return new EnterpriseAnalysisResult(Parsed.Document, Combined);
    }

    private static bool IsNestingRelationship(
        EnterpriseDocument Document,
        EnterpriseRelationship Relationship,
        EnterpriseElement Source,
        EnterpriseElement Target)
    {
        if (!NestableKinds.Contains(Relationship.Kind))
        {
            return false;
        }

        if (Target.ParentId == Source.Id)
        {
            return true;
        }

        return false;
    }

    private static string FormatDisallowedMessage(
        string Version,
        EnterpriseRelationship Relationship,
        EnterpriseElement Source,
        EnterpriseElement Target,
        string AllowedLetters,
        bool Provisional)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.Append("line ");
        Builder.Append(Relationship.LineNumber.ToString(CultureInfo.InvariantCulture));
        Builder.Append(": ");
        Builder.Append(EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind));
        Builder.Append(" from ");
        Builder.Append(EnterpriseElementCatalog.GetSpecName(Source.ElementType));
        Builder.Append(" \"");
        Builder.Append(Source.Label);
        Builder.Append("\" to ");
        Builder.Append(EnterpriseElementCatalog.GetSpecName(Target.ElementType));
        Builder.Append(" \"");
        Builder.Append(Target.Label);
        Builder.Append("\" is not allowed in Enterprise ");
        Builder.Append(Version);
        Builder.Append(". Allowed: ");
        Builder.Append(FormatAllowedList(AllowedLetters));
        Builder.Append('.');
        if (Provisional)
        {
            Builder.Append(' ');
            Builder.Append(PROVISIONAL_NOTE);
        }

        return Builder.ToString();
    }

    private static string FormatAllowedList(string AllowedLetters)
    {
        if (string.IsNullOrEmpty(AllowedLetters))
        {
            return "none";
        }

        List<string> Names = new List<string>();
        foreach (char Letter in AllowedLetters)
        {
            if (EnterpriseElementCatalog.TryResolveLetter(Letter, out EnterpriseRelationshipKind Kind))
            {
                Names.Add(EnterpriseElementCatalog.GetRelationshipName(Kind));
            }
        }

        if (Names.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", Names);
    }
}
