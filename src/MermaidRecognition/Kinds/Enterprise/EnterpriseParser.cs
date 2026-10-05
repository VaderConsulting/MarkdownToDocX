using System.Text.RegularExpressions;

using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Parses the native <c>enterprise</c> diagram syntax.
/// </summary>
public static class EnterpriseParser
{
    /// <summary>Default rule-table version when the source omits <c>version</c>.</summary>
    public const string DEFAULT_VERSION = "3.2";

    private const string ID_PATTERN = @"[A-Za-z_][\w.]*";

    private const string STRING_PATTERN = @"""((?:[^""\\]|\\.)*)""";

    private static readonly Regex HeaderRegex = new Regex(
        @"^enterprise(-beta)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new Regex(
        @"^title\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VersionRegex = new Regex(
        @"^version\s+(3\.1|3\.2)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DirectionRegex = new Regex(
        @"^direction\s+(TB|LR)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GroupRegex = new Regex(
        @"^group\s+(" + ID_PATTERN + @")(?:\s+" + STRING_PATTERN + @")?(?:\s+in\s+(" + ID_PATTERN + @"))?$",
        RegexOptions.Compiled);

    private static readonly Regex RelationshipRegex = new Regex(
        @"^(" + ID_PATTERN + @")\s+-([A-Za-z]+)(?:\(([^)]*)\))?->\s+(" + ID_PATTERN + @")(?:\s+" + STRING_PATTERN + @")?$",
        RegexOptions.Compiled);

    private static readonly Regex ElementRegex = new Regex(
        @"^([A-Za-z-]+)\s+(" + ID_PATTERN + @")(?:\s+" + STRING_PATTERN + @")?(?:\s+in\s+(" + ID_PATTERN + @"))?$",
        RegexOptions.Compiled);

    private static readonly Regex InfluenceQualifierRegex = new Regex(
        @"^[+-]{1,2}$|^[+-]?\d+$",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true when the first non-blank, non-comment line is <c>enterprise</c>.
    /// </summary>
    /// <param name="Source">Diagram source, optionally including leading init blocks.</param>
    /// <returns>True when the source is an enterprise diagram.</returns>
    public static bool IsEnterpriseSource(string? Source)
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            return false;
        }

        string DetectionText = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        string[] Lines = DetectionText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        foreach (string Line in Lines)
        {
            string Trimmed = Line.Trim();
            if (Trimmed.Length == 0)
            {
                continue;
            }

            return HeaderRegex.IsMatch(Trimmed);
        }

        return false;
    }

    /// <summary>
    /// Parses an enterprise diagram block.
    /// </summary>
    /// <param name="Source">Diagram source. Line 1 is the first line of the block.</param>
    /// <returns>Document and parser diagnostics.</returns>
    public static EnterpriseParseResult Parse(string? Source)
    {
        EnterpriseDocument Document = new EnterpriseDocument();
        List<EnterpriseDiagnostic> Diagnostics = new List<EnterpriseDiagnostic>();
        if (string.IsNullOrWhiteSpace(Source))
        {
            Diagnostics.Add(CreateError(1, "empty diagram"));
            return new EnterpriseParseResult(Document, Diagnostics);
        }

        string Normalised = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] Lines = Normalised.Split('\n');
        bool SeenHeader = false;

        for (int Index = 0; Index < Lines.Length; Index++)
        {
            int LineNumber = Index + 1;
            string RawLine = Lines[Index];
            string WithoutComment = StripTrailingComment(RawLine);
            string Line = WithoutComment.Trim();
            if (Line.Length == 0)
            {
                continue;
            }

            if (!SeenHeader)
            {
                if (!HeaderRegex.IsMatch(Line))
                {
                    Diagnostics.Add(CreateError(LineNumber, "expected diagram header \"enterprise\""));
                    return new EnterpriseParseResult(Document, Diagnostics);
                }

                SeenHeader = true;
                continue;
            }

            Match TitleMatch = TitleRegex.Match(Line);
            if (TitleMatch.Success)
            {
                Document.Title = TitleMatch.Groups[1].Value.Trim();
                continue;
            }

            Match VersionMatch = VersionRegex.Match(Line);
            if (VersionMatch.Success)
            {
                Document.Version = VersionMatch.Groups[1].Value;
                continue;
            }

            Match DirectionMatch = DirectionRegex.Match(Line);
            if (DirectionMatch.Success)
            {
                string DirectionText = DirectionMatch.Groups[1].Value.ToUpperInvariant();
                Document.Direction = DirectionText == "LR" ? EnterpriseDiagramDirection.Lr : EnterpriseDiagramDirection.Tb;
                continue;
            }

            Match GroupMatch = GroupRegex.Match(Line);
            if (GroupMatch.Success)
            {
                AddGroup(Document, Diagnostics, GroupMatch, LineNumber);
                continue;
            }

            Match RelationshipMatch = RelationshipRegex.Match(Line);
            if (RelationshipMatch.Success)
            {
                AddRelationship(Document, Diagnostics, RelationshipMatch, LineNumber);
                continue;
            }

            Match ElementMatch = ElementRegex.Match(Line);
            if (ElementMatch.Success)
            {
                AddElement(Document, Diagnostics, ElementMatch, LineNumber);
                continue;
            }

            Diagnostics.Add(CreateError(LineNumber, "cannot parse \"" + Line + "\""));
        }

        if (!SeenHeader)
        {
            Diagnostics.Add(CreateError(1, "empty diagram"));
            return new EnterpriseParseResult(Document, Diagnostics);
        }

        ValidateReferences(Document, Diagnostics);
        return new EnterpriseParseResult(Document, Diagnostics);
    }

    private static void AddGroup(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match GroupMatch,
        int LineNumber)
    {
        string Id = GroupMatch.Groups[1].Value;
        if (Document.HasContainer(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "duplicate id \"" + Id + "\""));
            return;
        }

        string? QuotedLabel = GroupMatch.Groups[2].Success ? Unquote(GroupMatch.Groups[2].Value) : null;
        string? ParentId = GroupMatch.Groups[3].Success ? GroupMatch.Groups[3].Value : null;
        EnterpriseGroup Group = new EnterpriseGroup
        {
            Id = Id,
            Label = QuotedLabel ?? Id,
            ParentId = ParentId,
            LineNumber = LineNumber
        };
        Document.Groups[Id] = Group;
    }

    private static void AddElement(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match ElementMatch,
        int LineNumber)
    {
        string TypeToken = ElementMatch.Groups[1].Value;
        if (!EnterpriseElementCatalog.TryResolveElementType(TypeToken, out EnterpriseElementType ElementType))
        {
            Diagnostics.Add(CreateError(
                LineNumber,
                "unknown element type \"" + TypeToken + "\" (in scope: Application and Technology layer elements)"));
            return;
        }

        string Id = ElementMatch.Groups[2].Value;
        if (Document.HasContainer(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "duplicate element id \"" + Id + "\""));
            return;
        }

        string? QuotedLabel = ElementMatch.Groups[3].Success ? Unquote(ElementMatch.Groups[3].Value) : null;
        string? ParentId = ElementMatch.Groups[4].Success ? ElementMatch.Groups[4].Value : null;
        EnterpriseElement Element = new EnterpriseElement
        {
            Id = Id,
            ElementType = ElementType,
            Label = QuotedLabel ?? Id,
            ParentId = ParentId,
            LineNumber = LineNumber
        };
        Document.Elements[Id] = Element;
    }

    private static void AddRelationship(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match RelationshipMatch,
        int LineNumber)
    {
        string SourceId = RelationshipMatch.Groups[1].Value;
        string KindToken = RelationshipMatch.Groups[2].Value;
        if (!EnterpriseElementCatalog.TryResolveRelationship(KindToken, out EnterpriseRelationshipKind Kind))
        {
            Diagnostics.Add(CreateError(LineNumber, "unknown relationship type \"" + KindToken + "\""));
            return;
        }

        string TargetId = RelationshipMatch.Groups[4].Value;
        string? QuotedLabel = RelationshipMatch.Groups[5].Success ? Unquote(RelationshipMatch.Groups[5].Value) : null;
        EnterpriseRelationship Relationship = new EnterpriseRelationship
        {
            SourceId = SourceId,
            TargetId = TargetId,
            Kind = Kind,
            Label = QuotedLabel ?? string.Empty,
            LineNumber = LineNumber
        };

        if (RelationshipMatch.Groups[3].Success)
        {
            string Qualifier = RelationshipMatch.Groups[3].Value.Trim();
            if (!TryApplyQualifier(Relationship, Kind, Qualifier, LineNumber, Diagnostics))
            {
                return;
            }
        }

        if (Kind == EnterpriseRelationshipKind.Access && !Relationship.AccessKind.HasValue)
        {
            Relationship.AccessKind = EnterpriseAccessKind.Write;
        }

        Document.Relationships.Add(Relationship);
    }

    private static bool TryApplyQualifier(
        EnterpriseRelationship Relationship,
        EnterpriseRelationshipKind Kind,
        string Qualifier,
        int LineNumber,
        List<EnterpriseDiagnostic> Diagnostics)
    {
        string Normalised = Qualifier.ToLowerInvariant();
        if (Kind == EnterpriseRelationshipKind.Access)
        {
            if (Normalised == "read")
            {
                Relationship.AccessKind = EnterpriseAccessKind.Read;
                return true;
            }

            if (Normalised == "write")
            {
                Relationship.AccessKind = EnterpriseAccessKind.Write;
                return true;
            }

            if (Normalised == "readwrite")
            {
                Relationship.AccessKind = EnterpriseAccessKind.ReadWrite;
                return true;
            }

            if (Normalised == "access")
            {
                Relationship.AccessKind = EnterpriseAccessKind.Access;
                return true;
            }
        }
        else if (Kind == EnterpriseRelationshipKind.Influence)
        {
            if (InfluenceQualifierRegex.IsMatch(Qualifier.Trim()))
            {
                Relationship.InfluenceStrength = Qualifier.Trim();
                return true;
            }
        }
        else if (Kind == EnterpriseRelationshipKind.Association)
        {
            if (Normalised == "directed")
            {
                Relationship.DirectedAssociation = true;
                return true;
            }
        }

        Diagnostics.Add(CreateError(
            LineNumber,
            "qualifier \"(" + Qualifier + ")\" is not valid for " + EnterpriseElementCatalog.GetRelationshipName(Kind)));
        return false;
    }

    private static void ValidateReferences(EnterpriseDocument Document, List<EnterpriseDiagnostic> Diagnostics)
    {
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (!Document.Elements.ContainsKey(Relationship.SourceId))
            {
                Diagnostics.Add(CreateError(
                    Relationship.LineNumber,
                    "relationship source \"" + Relationship.SourceId + "\" is not a declared element"));
            }

            if (!Document.Elements.ContainsKey(Relationship.TargetId))
            {
                Diagnostics.Add(CreateError(
                    Relationship.LineNumber,
                    "relationship target \"" + Relationship.TargetId + "\" is not a declared element"));
            }
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            ValidateParent(Document, Diagnostics, Element.ParentId, Element.LineNumber);
        }

        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            ValidateParent(Document, Diagnostics, Group.ParentId, Group.LineNumber);
        }
    }

    private static void ValidateParent(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        string? ParentId,
        int LineNumber)
    {
        if (string.IsNullOrEmpty(ParentId))
        {
            return;
        }

        if (!Document.HasContainer(ParentId))
        {
            Diagnostics.Add(CreateError(LineNumber, "\"in " + ParentId + "\": no such group or element"));
        }
    }

    private static string StripTrailingComment(string Line)
    {
        int CommentIndex = Line.IndexOf("%%", StringComparison.Ordinal);
        if (CommentIndex < 0)
        {
            return Line;
        }

        return Line.Substring(0, CommentIndex);
    }

    private static string Unquote(string Value)
    {
        return Regex.Replace(Value, @"\\(.)", "$1");
    }

    private static EnterpriseDiagnostic CreateError(int LineNumber, string Message)
    {
        return new EnterpriseDiagnostic(
            MermaidDiagnosticSeverity.Error,
            LineNumber,
            "line " + LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + Message);
    }
}
