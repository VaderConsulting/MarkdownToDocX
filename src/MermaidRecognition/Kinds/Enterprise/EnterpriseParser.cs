using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Parses the native <c>enterprise</c> diagram syntax.
/// </summary>
public static class EnterpriseParser
{
    /// <summary>Default rule-table version when the source omits <c>version</c>.</summary>
    public const string DEFAULT_VERSION = "3.2";

    private const string ID_PATTERN = @"[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*";

    private const string STRING_PATTERN = @"(""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')";

    private static readonly HashSet<string> ReservedIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "version", "direction", "group", "in", "classDef", "class", "style", "linkStyle", "accTitle", "accDescr"
    };

    private static readonly Regex HeaderRegex = new Regex(
        @"^enterprise(-beta)?(?:\s+(TB|TD|BT|LR|RL))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleRegex = new Regex(
        @"^title\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VersionRegex = new Regex(
        @"^version\s+(3\.1|3\.2)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DirectionRegex = new Regex(
        @"^direction\s+(TB|TD|BT|LR|RL)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string PLACEMENT_HINT_PATTERN = @"(?:(?<relative>above|below|left-of|right-of)\s+(?<peer>" + ID_PATTERN + @")(?:\s+\+(?<offset>\d+))?|push\s+(?<push>[NSEW]))";

    private static readonly Regex GroupRegex = new Regex(
        @"^group\s+(?<id>" + ID_PATTERN + @")(?:\s+(?<label>" + STRING_PATTERN + @"))?(?:\s+in\s+(?<parent>" + ID_PATTERN + @"))?(?:\s+(?<placement>" + PLACEMENT_HINT_PATTERN + @"))?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex RelationshipRegex = new Regex(
        @"^(?<source>" + ID_PATTERN + @")\s+-(?<kind>[A-Za-z]+)(?:\((?<qualifier>[^)]*)\))?(?<arrow>-+)>\s+(?<target>" + ID_PATTERN + @")(?:\s+(?<label>" + STRING_PATTERN + @"))?(?:\s+\[(?<routing>[^\]]+)\])?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex PlainConnectionRegex = new Regex(
        @"^(?<source>" + ID_PATTERN + @")\s+(?<dashes>-{3,})\s+(?<target>" + ID_PATTERN + @")(?:\s+\[(?<routing>[^\]]+)\])?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NoteRegex = new Regex(
        @"^note\s+(?<id>" + ID_PATTERN + @")\s+(?<label>" + STRING_PATTERN + @")(?:\s+in\s+(?<parent>" + ID_PATTERN + @"))?(?:\s+(?<placement>" + PLACEMENT_HINT_PATTERN + @"))?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ElementRegex = new Regex(
        @"^(?<type>[A-Za-z-]+)\s+(?<id>" + ID_PATTERN + @")(?:\s+(?<label>" + STRING_PATTERN + @"))?(?:\s+in\s+(?<parent>" + ID_PATTERN + @"))?(?:\s+(?<placement>" + PLACEMENT_HINT_PATTERN + @"))?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

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
        foreach ((string Line, _) in EnterpriseStatements.Read(DetectionText))
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

        string Normalised = EnterpriseConfiguration.Prepare(Source, Document).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        Document.LayoutMode = MermaidRecognition.Config.DiagramLayoutSettings.Resolve(Document.Configuration,
            Message => Diagnostics.Add(new EnterpriseDiagnostic(MermaidDiagnosticSeverity.Warning, 0, Message)));
        string[] Lines = Normalised.Split('\n');
        bool SeenHeader = false;
        bool InDescription = false;
        List<string> DescriptionLines = new();
        Stack<string> GroupScope = new();

        foreach ((string RawLine, int LineNumber) in EnterpriseStatements.Read(Normalised))
        {
            if (InDescription)
            {
                if (RawLine.Trim() == "}")
                {
                    Document.AccessibilityDescription = string.Join("\n", DescriptionLines).Trim();
                    InDescription = false;
                }
                else DescriptionLines.Add(RawLine.Trim());
                continue;
            }
            string WithoutComment = StripTrailingComment(RawLine);
            string Line = WithoutComment.Trim();
            if (Line.Length == 0)
            {
                continue;
            }

            if (SeenHeader && (Line == "}" || Line.Equals("end", StringComparison.OrdinalIgnoreCase)) && GroupScope.Count > 0)
            {
                GroupScope.Pop();
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
                Match HeaderMatch = HeaderRegex.Match(Line);
                if (HeaderMatch.Groups[2].Success)
                    Document.Direction = ParseDirection(HeaderMatch.Groups[2].Value);
                continue;
            }

            if (MermaidRecognition.Kinds.Class.ClassAccessibilityParser.TryParseAccTitle(Line, out string AccessibleTitle))
            {
                Document.AccessibilityTitle = AccessibleTitle;
                continue;
            }
            if (MermaidRecognition.Kinds.Class.ClassAccessibilityParser.TryParseAccDescrInline(Line, out string Description))
            {
                Document.AccessibilityDescription = Description;
                continue;
            }
            if (MermaidRecognition.Kinds.Class.ClassAccessibilityParser.IsAccDescrBlockStart(Line))
            {
                InDescription = true;
                DescriptionLines.Clear();
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
                var Direction = ParseDirection(DirectionMatch.Groups[1].Value);
                if (GroupScope.TryPeek(out string? GroupId)) Document.Groups[GroupId].Direction = Direction;
                else Document.Direction = Direction;
                continue;
            }

            if (Document.Styles.Read(Line, LineNumber)) continue;
            string? TrailingClass = null;
            Line = Regex.Replace(Line, STRING_PATTERN + @"|(?<id>" + ID_PATTERN + @"):::(?<class>[\w-]+)", Match =>
            {
                if (!Match.Groups["id"].Success) return Match.Value;
                Document.Styles.AddInlineClass(Match.Groups["id"].Value, Match.Groups["class"].Value);
                return Match.Groups["id"].Value;
            }, RegexOptions.Singleline);
            Match SuffixClass = Regex.Match(Line, @":::([\w-]+)$");
            if (SuffixClass.Success)
            {
                TrailingClass = SuffixClass.Groups[1].Value;
                Line = Line[..SuffixClass.Index].TrimEnd();
            }

            bool OpensGroup = Line.EndsWith('{');
            Match GroupMatch = GroupRegex.Match(OpensGroup ? Line[..^1].TrimEnd() : Line);
            if (GroupMatch.Success)
            {
                AddGroup(Document, Diagnostics, GroupMatch, LineNumber);
                string GroupId = GroupMatch.Groups["id"].Value;
                if (GroupScope.TryPeek(out string? ParentId) && Document.Groups.TryGetValue(GroupId, out var Group)
                    && Group.ParentId == null)
                {
                    Group.ParentId = ParentId;
                    Document.Elements[GroupId].ParentId = ParentId;
                }
                if (OpensGroup && Document.Groups.ContainsKey(GroupId)) GroupScope.Push(GroupId);
                continue;
            }

            Match RelationshipMatch = RelationshipRegex.Match(Line);
            if (RelationshipMatch.Success)
            {
                AddRelationship(Document, Diagnostics, RelationshipMatch, LineNumber);
                continue;
            }

            Match PlainConnectionMatch = PlainConnectionRegex.Match(Line);
            if (PlainConnectionMatch.Success)
            {
                AddPlainConnection(Document, Diagnostics, PlainConnectionMatch, LineNumber);
                continue;
            }

            Match NoteMatch = NoteRegex.Match(Line);
            if (NoteMatch.Success)
            {
                AddNote(Document, Diagnostics, NoteMatch, LineNumber);
                if (TrailingClass != null) Document.Styles.AddInlineClass(NoteMatch.Groups["id"].Value, TrailingClass);
                if (GroupScope.TryPeek(out string? ParentId)
                    && Document.Notes.TryGetValue(NoteMatch.Groups["id"].Value, out EnterpriseNote? Note) && Note.ParentId == null)
                    Note.ParentId = ParentId;
                continue;
            }

            Match ElementMatch = ElementRegex.Match(Line);
            if (ElementMatch.Success)
            {
                AddElement(Document, Diagnostics, ElementMatch, LineNumber);
                if (TrailingClass != null) Document.Styles.AddInlineClass(ElementMatch.Groups["id"].Value, TrailingClass);
                if (GroupScope.TryPeek(out string? ParentId)
                    && Document.Elements.TryGetValue(ElementMatch.Groups["id"].Value, out var Element) && Element.ParentId == null)
                    Element.ParentId = ParentId;
                continue;
            }

            if (IsIncompleteElementDeclaration(Line))
            {
                continue;
            }

            Diagnostics.Add(CreateError(LineNumber, "cannot parse \"" + Line + "\""));
        }

        if (!SeenHeader)
        {
            Diagnostics.Add(CreateError(1, "empty diagram"));
            return new EnterpriseParseResult(Document, Diagnostics);
        }

        if (InDescription)
        {
            Diagnostics.Add(CreateError(Lines.Length, "unterminated accessibility description"));
        }
        ValidateReferences(Document, Diagnostics);
        ValidatePlacementHints(Document, Diagnostics);
        Document.Styles.Resolve(Document, Diagnostics);
        return new EnterpriseParseResult(Document, Diagnostics);
    }

    private static void AddGroup(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match GroupMatch,
        int LineNumber)
    {
        string Id = GroupMatch.Groups["id"].Value;
        if (ReservedIds.Contains(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "reserved word cannot be an id: \"" + Id + "\""));
            return;
        }
        if (Document.HasDrawingItem(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "duplicate id \"" + Id + "\""));
            return;
        }

        string? QuotedLabel = GroupMatch.Groups["label"].Success ? Unquote(GroupMatch.Groups["label"].Value) : null;
        string? ParentId = GroupMatch.Groups["parent"].Success ? GroupMatch.Groups["parent"].Value : null;
        PlacementHintContract? PlacementHint = ParsePlacementHint(GroupMatch, LineNumber);
        EnterpriseGroup Group = new EnterpriseGroup
        {
            Id = Id,
            Label = QuotedLabel ?? Id,
            IsMarkdownLabel = IsMarkdown(GroupMatch.Groups["label"].Value),
            ParentId = ParentId,
            LineNumber = LineNumber,
            PlacementHint = PlacementHint
        };
        Document.Groups[Id] = Group;
        Document.Elements[Id] = new EnterpriseElement
        {
            Id = Id,
            ElementType = EnterpriseElementType.Grouping,
            Label = QuotedLabel ?? Id,
            IsMarkdownLabel = Group.IsMarkdownLabel,
            ParentId = ParentId,
            LineNumber = LineNumber,
            PlacementHint = PlacementHint
        };
    }

    private static void AddElement(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match ElementMatch,
        int LineNumber)
    {
        string TypeToken = ElementMatch.Groups["type"].Value;
        if (!EnterpriseElementCatalog.TryResolveElementType(TypeToken, out EnterpriseElementType ElementType))
        {
            Diagnostics.Add(CreateError(
                LineNumber,
                "unknown element type \"" + TypeToken + "\" (in scope: Enterprise 3.2 Application, Technology, Physical, Business, Motivation, Strategy, Implementation, composite and junction elements)"));
            return;
        }

        string Id = ElementMatch.Groups["id"].Value;
        if (ReservedIds.Contains(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "reserved word cannot be an id: \"" + Id + "\""));
            return;
        }
        if (Document.HasDrawingItem(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "duplicate element id \"" + Id + "\""));
            return;
        }

        string? QuotedLabel = ElementMatch.Groups["label"].Success ? Unquote(ElementMatch.Groups["label"].Value) : null;
        string? ParentId = ElementMatch.Groups["parent"].Success ? ElementMatch.Groups["parent"].Value : null;
        PlacementHintContract? PlacementHint = ParsePlacementHint(ElementMatch, LineNumber);
        EnterpriseElement Element = new EnterpriseElement
        {
            Id = Id,
            ElementType = ElementType,
            Label = QuotedLabel ?? Id,
            IsMarkdownLabel = IsMarkdown(ElementMatch.Groups["label"].Value),
            ParentId = ParentId,
            LineNumber = LineNumber,
            PlacementHint = PlacementHint
        };
        if (ElementType == EnterpriseElementType.Junction)
        {
            Element.JunctionKind = ResolveJunctionKind(TypeToken);
        }

        Document.Elements[Id] = Element;
        if (ElementType == EnterpriseElementType.Grouping && !Document.Groups.ContainsKey(Id))
        {
            Document.Groups[Id] = new EnterpriseGroup
            {
                Id = Id,
                Label = Element.Label,
                ParentId = ParentId,
                LineNumber = LineNumber,
                PlacementHint = PlacementHint
            };
        }
    }

    private static void AddNote(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match NoteMatch,
        int LineNumber)
    {
        string Id = NoteMatch.Groups["id"].Value;
        if (ReservedIds.Contains(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "reserved word cannot be an id: \"" + Id + "\""));
            return;
        }
        if (Document.HasDrawingItem(Id))
        {
            Diagnostics.Add(CreateError(LineNumber, "duplicate id \"" + Id + "\""));
            return;
        }

        Document.Notes[Id] = new EnterpriseNote
        {
            Id = Id,
            Label = Unquote(NoteMatch.Groups["label"].Value),
            IsMarkdownLabel = IsMarkdown(NoteMatch.Groups["label"].Value),
            ParentId = NoteMatch.Groups["parent"].Success ? NoteMatch.Groups["parent"].Value : null,
            LineNumber = LineNumber,
            PlacementHint = ParsePlacementHint(NoteMatch, LineNumber)
        };
    }

    private static void AddPlainConnection(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match ConnectionMatch,
        int LineNumber)
    {
        Document.PlainConnections.Add(new EnterprisePlainConnection
        {
            SourceId = ConnectionMatch.Groups["source"].Value,
            TargetId = ConnectionMatch.Groups["target"].Value,
            LineNumber = LineNumber,
            MinLength = MermaidLinkLength.FromStrokeRun(ConnectionMatch.Groups["dashes"].Value.Length - 1),
            RoutingHint = ParseRoutingHint(ConnectionMatch, LineNumber, Diagnostics)
        });
    }

    private static EnterpriseJunctionKind ResolveJunctionKind(string TypeToken)
    {
        string NormalisedType = TypeToken.Trim().ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);
        if (NormalisedType == "orjunction" || NormalisedType == "junctionor")
        {
            return EnterpriseJunctionKind.Or;
        }

        return EnterpriseJunctionKind.And;
    }

    private static void AddRelationship(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics,
        Match RelationshipMatch,
        int LineNumber)
    {
        string SourceId = RelationshipMatch.Groups["source"].Value;
        string KindToken = RelationshipMatch.Groups["kind"].Value;
        if (!EnterpriseElementCatalog.TryResolveRelationship(KindToken, out EnterpriseRelationshipKind Kind))
        {
            Diagnostics.Add(CreateError(LineNumber, "unknown relationship type \"" + KindToken + "\""));
            return;
        }

        string TargetId = RelationshipMatch.Groups["target"].Value;
        string? QuotedLabel = RelationshipMatch.Groups["label"].Success ? Unquote(RelationshipMatch.Groups["label"].Value) : null;
        EnterpriseRelationship Relationship = new EnterpriseRelationship
        {
            SourceId = SourceId,
            TargetId = TargetId,
            Kind = Kind,
            Label = QuotedLabel ?? string.Empty,
            IsMarkdownLabel = IsMarkdown(RelationshipMatch.Groups["label"].Value),
            LineNumber = LineNumber,
            MinLength = MermaidLinkLength.FromStrokeRun(RelationshipMatch.Groups["arrow"].Value.Length),
            RoutingHint = ParseRoutingHint(RelationshipMatch, LineNumber, Diagnostics)
        };

        if (RelationshipMatch.Groups["qualifier"].Success)
        {
            string Qualifier = RelationshipMatch.Groups["qualifier"].Value.Trim();
            if (!TryApplyQualifier(Relationship, Kind, Qualifier, LineNumber, Diagnostics))
            {
                return;
            }
        }

        if (Kind == EnterpriseRelationshipKind.Access && !Relationship.AccessKind.HasValue)
        {
            Relationship.AccessKind = EnterpriseAccessKind.Write;
        }

        foreach (EnterpriseRelationship Existing in Document.Relationships)
        {
            if (IsSameRelationship(Existing, Relationship))
            {
                Diagnostics.Add(CreateError(LineNumber, "duplicate relationship \"" + SourceId + " -" + EnterpriseElementCatalog.GetRelationshipName(Kind) + MermaidLinkLength.Arrow(Relationship.MinLength, 1) + " " + TargetId + "\""));
                return;
            }
        }

        Document.Relationships.Add(Relationship);
    }

    /// <summary>
    /// True when both lines connect the same elements with the same type and qualifier.
    /// A different qualifier is a different relationship.
    /// </summary>
    private static bool IsSameRelationship(EnterpriseRelationship Existing, EnterpriseRelationship Candidate)
    {
        if (!string.Equals(Existing.SourceId, Candidate.SourceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(Existing.TargetId, Candidate.TargetId, StringComparison.Ordinal))
        {
            return false;
        }

        if (Existing.Kind != Candidate.Kind)
        {
            return false;
        }

        if (Existing.AccessKind != Candidate.AccessKind)
        {
            return false;
        }

        if (!string.Equals(Existing.InfluenceStrength, Candidate.InfluenceStrength, StringComparison.Ordinal))
        {
            return false;
        }

        return Existing.DirectedAssociation == Candidate.DirectedAssociation;
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

    private static PlacementHintContract? ParsePlacementHint(Match Match, int LineNumber)
    {
        if (!Match.Groups["placement"].Success)
        {
            return null;
        }

        PlacementHintContract Hint = new PlacementHintContract { SourceLine = LineNumber };
        if (Match.Groups["push"].Success)
        {
            Hint.Kind = PlacementHintKind.Push;
            Hint.Direction = ParseHintDirection(Match.Groups["push"].Value);
            return Hint;
        }

        Hint.Kind = Match.Groups["relative"].Value.ToLowerInvariant() switch
        {
            "above" => PlacementHintKind.Above,
            "below" => PlacementHintKind.Below,
            "left-of" => PlacementHintKind.LeftOf,
            _ => PlacementHintKind.RightOf
        };
        Hint.PeerId = Match.Groups["peer"].Value;
        if (Match.Groups["offset"].Success)
        {
            Hint.Offset = int.Parse(Match.Groups["offset"].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        return Hint;
    }

    private static RoutingHintContract? ParseRoutingHint(
        Match Match,
        int LineNumber,
        List<EnterpriseDiagnostic> Diagnostics)
    {
        if (!Match.Groups["routing"].Success)
        {
            return null;
        }

        RoutingHintContract Hint = new RoutingHintContract { SourceLine = LineNumber };
        HashSet<string> Seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string RawPart in Match.Groups["routing"].Value.Split(','))
        {
            Match Part = Regex.Match(RawPart.Trim(), @"^(leave|enter|travel)\s+([NSEW])$", RegexOptions.IgnoreCase);
            if (!Part.Success)
            {
                DeactivateHint(Hint, "invalid line hint \"" + RawPart.Trim() + "\"");
                continue;
            }

            string Name = Part.Groups[1].Value.ToLowerInvariant();
            if (!Seen.Add(Name))
            {
                DeactivateHint(Hint, "conflicting duplicate " + Name + " hint");
                continue;
            }

            LayoutHintDirection Direction = ParseHintDirection(Part.Groups[2].Value);
            if (Name == "leave") Hint.Leave = Direction;
            else if (Name == "enter") Hint.Enter = Direction;
            else Hint.Travel = Direction;
        }

        if (!Hint.IsActive)
        {
            Diagnostics.Add(CreateWarning(LineNumber, Hint.UnhonouredReason!));
        }
        return Hint;
    }

    private static LayoutHintDirection ParseHintDirection(string Value) => Value.ToUpperInvariant() switch
    {
        "N" => LayoutHintDirection.North,
        "S" => LayoutHintDirection.South,
        "E" => LayoutHintDirection.East,
        _ => LayoutHintDirection.West
    };

    private static void ValidatePlacementHints(
        EnterpriseDocument Document,
        List<EnterpriseDiagnostic> Diagnostics)
    {
        List<(string Id, string? ParentId, PlacementHintContract Hint)> Hints = new();
        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            if (Element.PlacementHint != null && !Document.Groups.ContainsKey(Element.Id))
                Hints.Add((Element.Id, Element.ParentId, Element.PlacementHint));
        }
        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            if (Group.PlacementHint != null)
                Hints.Add((Group.Id, Group.ParentId, Group.PlacementHint));
        }
        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            if (Note.PlacementHint != null)
                Hints.Add((Note.Id, Note.ParentId, Note.PlacementHint));
        }

        Dictionary<string, string> AcceptedPeers = new(StringComparer.Ordinal);
        foreach ((string Id, string? ParentId, PlacementHintContract Hint) in Hints.OrderBy(Item => Item.Hint.SourceLine))
        {
            if (!Hint.IsActive || Hint.Kind == PlacementHintKind.Push)
                continue;

            string PeerId = Hint.PeerId ?? string.Empty;
            if (!Document.HasDrawingItem(PeerId))
            {
                RejectPlacementHint(Hint, Diagnostics, "placement hint peer \"" + PeerId + "\" does not exist");
                continue;
            }

            string? PeerParentId = ParentOf(Document, PeerId);
            if (!string.Equals(ParentId, PeerParentId, StringComparison.Ordinal))
            {
                RejectPlacementHint(Hint, Diagnostics, "placement hint peer \"" + PeerId + "\" is in another group");
                continue;
            }

            string Cursor = PeerId;
            HashSet<string> Visited = new(StringComparer.Ordinal);
            while (AcceptedPeers.TryGetValue(Cursor, out string? Next) && Visited.Add(Cursor))
                Cursor = Next;
            if (string.Equals(Cursor, Id, StringComparison.Ordinal))
            {
                RejectPlacementHint(Hint, Diagnostics, "placement hint conflicts with earlier hints by closing a peer circle");
                continue;
            }

            AcceptedPeers[Id] = PeerId;
        }
    }

    private static string? ParentOf(EnterpriseDocument Document, string Id)
    {
        if (Document.Groups.TryGetValue(Id, out EnterpriseGroup? Group)) return Group.ParentId;
        if (Document.Elements.TryGetValue(Id, out EnterpriseElement? Element)) return Element.ParentId;
        return Document.Notes.TryGetValue(Id, out EnterpriseNote? Note) ? Note.ParentId : null;
    }

    private static void RejectPlacementHint(
        PlacementHintContract Hint,
        List<EnterpriseDiagnostic> Diagnostics,
        string Reason)
    {
        Hint.IsActive = false;
        Hint.UnhonouredReason = Reason;
        Diagnostics.Add(CreateWarning(Hint.SourceLine, Reason));
    }

    private static void DeactivateHint(RoutingHintContract Hint, string Reason)
    {
        Hint.IsActive = false;
        Hint.UnhonouredReason = Reason;
    }

    private static void ValidateReferences(EnterpriseDocument Document, List<EnterpriseDiagnostic> Diagnostics)
    {
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            bool MissingSource = !Document.Elements.ContainsKey(Relationship.SourceId);
            bool MissingTarget = !Document.Elements.ContainsKey(Relationship.TargetId);
            // Each undeclared endpoint is the source error. Reporting the
            // consequent dropped relationship would duplicate that fault. Keep the
            // relationship identity on the same visible line so rendering can
            // explain exactly which connection cannot be drawn.
            if (MissingSource)
            {
                Diagnostics.Add(CreateError(
                    Relationship.LineNumber,
                    "relationship source \"" + Relationship.SourceId + "\" is not a declared element ("
                    + Relationship.SourceId + "->" + Relationship.TargetId + ")"));
            }

            if (MissingTarget)
            {
                Diagnostics.Add(CreateError(
                    Relationship.LineNumber,
                    "relationship target \"" + Relationship.TargetId + "\" is not a declared element ("
                    + Relationship.SourceId + "->" + Relationship.TargetId + ")"));
            }
        }

        foreach (EnterprisePlainConnection Connection in Document.PlainConnections)
        {
            bool SourceExists = Document.HasDrawingItem(Connection.SourceId);
            bool TargetExists = Document.HasDrawingItem(Connection.TargetId);
            if (!SourceExists)
                Diagnostics.Add(CreateError(Connection.LineNumber, "plain connection source \"" + Connection.SourceId + "\" is not declared"));
            if (!TargetExists)
                Diagnostics.Add(CreateError(Connection.LineNumber, "plain connection target \"" + Connection.TargetId + "\" is not declared"));
            if (SourceExists && TargetExists
                && !Document.Notes.ContainsKey(Connection.SourceId)
                && !Document.Notes.ContainsKey(Connection.TargetId))
                Diagnostics.Add(CreateError(Connection.LineNumber, "a plain connection must have a note at one or both ends"));
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            ValidateParent(Document, Diagnostics, Element.ParentId, Element.LineNumber);
        }

        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            ValidateParent(Document, Diagnostics, Group.ParentId, Group.LineNumber);
        }

        foreach (EnterpriseNote Note in Document.Notes.Values)
        {
            ValidateParent(Document, Diagnostics, Note.ParentId, Note.LineNumber);
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
        // Titles are the rest of the line, not quoted-label statements.
        bool IsTitle = TitleRegex.IsMatch(Line.TrimStart());
        char Quote = '\0';
        for (int Index = 0; Index < Line.Length; Index++)
        {
            if (!IsTitle && Quote != '\0' && Line[Index] == '\\')
            {
                Index++;
                continue;
            }
            if (!IsTitle && (Line[Index] == '"' || Line[Index] == '\''))
            {
                if (Quote == '\0') Quote = Line[Index];
                else if (Quote == Line[Index]) Quote = '\0';
            }
            if (Quote == '\0' && Line[Index] == '%' && Index + 1 < Line.Length && Line[Index + 1] == '%')
                return Line.Substring(0, Index);
        }
        return Line;
    }

    private static string Unquote(string Value)
    {
        string Body = Value[1..^1];
        if (IsMarkdown(Value)) Body = Body[1..^1];
        return EnterpriseLabelText.DecodeQuoted(Body);
    }

    private static bool IsMarkdown(string Value) => Value.Length >= 4 && Value[1] == '`' && Value[^2] == '`';

    private static EnterpriseDiagramDirection ParseDirection(string Value) => Value.ToUpperInvariant() switch
    {
        "LR" => EnterpriseDiagramDirection.Lr,
        "RL" => EnterpriseDiagramDirection.Rl,
        "BT" => EnterpriseDiagramDirection.Bt,
        _ => EnterpriseDiagramDirection.Tb
    };

    private static bool IsIncompleteElementDeclaration(string Line)
    {
        if (Line.IndexOf(' ') >= 0 || Line.IndexOf('\t') >= 0)
        {
            return false;
        }

        if (EnterpriseElementCatalog.TryResolveElementType(Line, out _))
        {
            return true;
        }

        foreach (EnterpriseElementMetadata Metadata in EnterpriseElementCatalog.Elements.Values)
        {
            if (Metadata.KebabName.Length > Line.Length && Metadata.KebabName.StartsWith(Line, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static EnterpriseDiagnostic CreateError(int LineNumber, string Message)
    {
        return new EnterpriseDiagnostic(
            MermaidDiagnosticSeverity.Error,
            LineNumber,
            "line " + LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + Message);
    }

    private static EnterpriseDiagnostic CreateWarning(int LineNumber, string Message)
    {
        return new EnterpriseDiagnostic(
            MermaidDiagnosticSeverity.Warning,
            LineNumber,
            "line " + LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + Message);
    }
}
