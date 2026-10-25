using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Completion;

/// <summary>
/// Builds relationship IntelliSense suggestions from the enterprise rules catalogue.
/// </summary>
internal sealed class EnterpriseCompletionEngine
{
    // The relationship dash is written after a space ("id -kind"). A dash inside a type name, as in "application-", is still part of that name.
    private static readonly Regex KindContextRegex = new Regex(
        @"^(?<indent>\s*)(?<src>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)\s+-(?<kind>[A-Za-z]*)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex TargetContextRegex = new Regex(
        @"^(?<indent>\s*)(?<src>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)\s+-(?<kind>[A-Za-z]+)(?:\([^)]*\))?-+>\s*(?<tgt>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex QualifierContextRegex = new Regex(
        @"^(?<indent>\s*)(?<src>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)\s+-(?<kind>access|influence|association)\((?<qual>[A-Za-z+-]*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ElementTypeContextRegex = new Regex(
        @"^(?<indent>\s*)(?<type>[A-Za-z-]*)$",
        RegexOptions.Compiled);

    private static readonly Regex PlacementContextRegex = new Regex(
        @"^(?<indent>\s*)(?<etype>[A-Za-z-]+)\s+(?<id>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)(?:\s+(?<label>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'))?(?:\s+in\s+(?<parent>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*))?\s+(?<hint>[A-Za-z-]*)$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> NonElementLineStarters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "title",
        "version",
        "direction",
        "enterprise",
        "enterprise-beta",
        "note",
        "classDef",
        "class",
        "style",
        "linkStyle",
        "accTitle",
        "accDescr"
    };

    private EnterpriseDocument? _CachedDocument;

    private string _CachedSource = string.Empty;

    /// <summary>
    /// True when the caret sits on a line that already declares an element, group, note, or relationship.
    /// </summary>
    public bool IsDeclaredLine(string Source, int CaretIndex)
    {
        RefreshDocument(Source);
        if (_CachedDocument == null || string.IsNullOrEmpty(Source))
        {
            return false;
        }

        int Limit = Math.Max(0, Math.Min(CaretIndex, Source.Length));
        int Line = 1;
        for (int Index = 0; Index < Limit; Index++)
        {
            if (Source[Index] == '\n')
            {
                Line++;
            }
        }

        foreach (EnterpriseElement Element in _CachedDocument.Elements.Values)
        {
            if (Element.LineNumber == Line)
            {
                return true;
            }
        }

        foreach (EnterpriseGroup Group in _CachedDocument.Groups.Values)
        {
            if (Group.LineNumber == Line)
            {
                return true;
            }
        }

        foreach (EnterpriseNote Note in _CachedDocument.Notes.Values)
        {
            if (Note.LineNumber == Line)
            {
                return true;
            }
        }

        foreach (EnterpriseRelationship Relationship in _CachedDocument.Relationships)
        {
            if (Relationship.LineNumber == Line && !Relationship.HiddenByNesting)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Refreshes the cached enterprise document when the source changed.
    /// </summary>
    /// <param name="Source">Full editor text.</param>
    public void RefreshDocument(string Source)
    {
        string Normalised = Source ?? string.Empty;
        if (string.Equals(Normalised, _CachedSource, StringComparison.Ordinal))
        {
            return;
        }

        _CachedSource = Normalised;
        if (!EnterpriseParser.IsEnterpriseSource(Normalised))
        {
            _CachedDocument = null;
            return;
        }

        EnterpriseParseResult ParseResult = EnterpriseParser.Parse(Normalised);
        _CachedDocument = ParseResult.Document;
    }

    /// <summary>
    /// Builds completion context for the caret in the given source.
    /// </summary>
    /// <param name="Source">Full editor text.</param>
    /// <param name="CaretIndex">Caret character index.</param>
    /// <returns>Context with items, or TokenKind None.</returns>
    public CompletionContext Build(string Source, int CaretIndex)
    {
        CompletionContext Context = new CompletionContext();
        RefreshDocument(Source);
        if (_CachedDocument == null)
        {
            Context.StatusMessage = "Relationship suggestions are available in enterprise diagrams.";
            return Context;
        }

        if (CaretIndex < 0)
        {
            CaretIndex = 0;
        }

        if (CaretIndex > Source.Length)
        {
            CaretIndex = Source.Length;
        }

        GetLineBounds(Source, CaretIndex, out int LineStart, out int LineEnd);
        string LinePrefix = Source.Substring(LineStart, CaretIndex - LineStart);

        Match QualifierMatch = QualifierContextRegex.Match(LinePrefix);
        if (QualifierMatch.Success && LiveEditorFeatureGate.C_QualifierCompletion)
        {
            FillQualifierSuggestions(Context, QualifierMatch, LineStart);
            return Context;
        }

        Match TargetMatch = TargetContextRegex.Match(LinePrefix);
        if (TargetMatch.Success)
        {
            FillTargetSuggestions(Context, _CachedDocument, TargetMatch, LineStart);
            return Context;
        }

        Match KindMatch = KindContextRegex.Match(LinePrefix);
        if (KindMatch.Success)
        {
            FillKindSuggestions(Context, _CachedDocument, KindMatch, LineStart);
            return Context;
        }

        Match DirectionMatch = Regex.Match(LinePrefix, @"^(?<indent>\s*)direction\s+(?<dir>[A-Za-z]*)$", RegexOptions.IgnoreCase);
        if (DirectionMatch.Success)
        {
            FillDirectionSuggestions(Context, DirectionMatch, LineStart);
            return Context;
        }

        Match PushMatch = Regex.Match(LinePrefix, @"^(?<indent>\s*)(?<etype>[A-Za-z-]+)\s+(?<id>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)(?:\s+(?<label>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'))?(?:\s+in\s+(?<parent>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*))?\s+push\s+(?<dir>[NSEW]?)$", RegexOptions.IgnoreCase);
        if (PushMatch.Success && IsKnownElementTypeToken(PushMatch.Groups["etype"].Value))
        {
            FillPushSuggestions(Context, PushMatch, LineStart);
            return Context;
        }

        Match PeerMatch = Regex.Match(LinePrefix, @"^(?<indent>\s*)(?<etype>[A-Za-z-]+)\s+(?<id>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)(?:\s+(?<label>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'))?(?:\s+in\s+(?<parent>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*))?\s+(?<hint>above|below|left-of|right-of)\s+(?<peer>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)?$", RegexOptions.IgnoreCase);
        if (PeerMatch.Success && IsKnownElementTypeToken(PeerMatch.Groups["etype"].Value))
        {
            FillPeerSuggestions(Context, _CachedDocument, PeerMatch, LineStart);
            return Context;
        }

        Match ContainerMatch = Regex.Match(LinePrefix, @"^(?<indent>\s*)(?<etype>[A-Za-z-]+)\s+(?<id>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)(?:\s+(?<label>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'))?\s+in\s+(?<container>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)?$", RegexOptions.IgnoreCase);
        if (ContainerMatch.Success && IsKnownElementTypeToken(ContainerMatch.Groups["etype"].Value))
        {
            FillContainerSuggestions(Context, _CachedDocument, ContainerMatch, LineStart);
            return Context;
        }

        Match PlacementMatch = PlacementContextRegex.Match(LinePrefix);
        if (PlacementMatch.Success && LiveEditorFeatureGate.D_PlacementHintCompletion)
        {
            string Hint = PlacementMatch.Groups["hint"].Value;
            string DeclaredType = PlacementMatch.Groups["etype"].Value;
            if (Hint.Length > 0 && !NonElementLineStarters.Contains(DeclaredType) && IsKnownElementTypeToken(DeclaredType))
            {
                FillPlacementSuggestions(Context, PlacementMatch, LineStart);
                return Context;
            }
        }

        Match ClauseMatch = Regex.Match(
            LinePrefix,
            @"^(?<indent>\s*)(?<etype>[A-Za-z-]+)\s+(?<id>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)(?:\s+(?<label>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'))(?:\s+in\s+(?<parent>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*))?\s+$",
            RegexOptions.IgnoreCase);
        if (ClauseMatch.Success && IsKnownElementTypeToken(ClauseMatch.Groups["etype"].Value))
        {
            FillClauseSuggestions(Context, ClauseMatch, LineStart);
            return Context;
        }

        Match ElementTypeMatch = ElementTypeContextRegex.Match(LinePrefix);
        if (ElementTypeMatch.Success)
        {
            string TypedType = ElementTypeMatch.Groups["type"].Value;
            // Only offer element types while the line is still a type token (no id yet).
            // Require a non-empty prefix so blank lines do not open the full catalogue.
            if (!string.IsNullOrEmpty(TypedType) && LiveEditorFeatureGate.B_ElementTypeCompletion)
            {
                FillElementTypeSuggestions(Context, ElementTypeMatch, LineStart);
                FillSourceSuggestions(Context, _CachedDocument, TypedType, LineStart, ElementTypeMatch.Groups["type"]);
            }

            if (Context.Items.Count == 0 && LiveEditorFeatureGate.J_Snippets && TypedType.StartsWith("snippet", StringComparison.OrdinalIgnoreCase))
            {
                FillSnippetSuggestions(Context, ElementTypeMatch, LineStart);
            }

            return Context;
        }

        return Context;
    }

    /// <summary>
    /// Returns signature-help text for the relationship being typed, when both ends can be inferred.
    /// </summary>
    public string BuildSignatureHelp(string Source, int CaretIndex)
    {
        RefreshDocument(Source);
        if (_CachedDocument == null)
        {
            return string.Empty;
        }

        GetLineBounds(Source, CaretIndex, out int LineStart, out _);
        string LinePrefix = Source.Substring(LineStart, Math.Max(0, CaretIndex - LineStart));
        Match KindMatch = KindContextRegex.Match(LinePrefix);
        if (!KindMatch.Success)
        {
            return string.Empty;
        }

        string SourceId = KindMatch.Groups["src"].Value;
        if (!_CachedDocument.Elements.TryGetValue(SourceId, out EnterpriseElement? SourceElement))
        {
            return string.Empty;
        }

        string SourceType = EnterpriseElementCatalog.GetKebabName(SourceElement.ElementType);
        HashSet<char> Letters = CollectOutgoingLetters(_CachedDocument, SourceType);
        List<string> Names = new List<string>();
        foreach (KeyValuePair<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> Pair in EnterpriseElementCatalog.Relationships)
        {
            if (Letters.Contains(Pair.Value.Letter))
            {
                Names.Add(Pair.Value.Name);
            }
        }

        Names.Sort(StringComparer.OrdinalIgnoreCase);
        return Names.Count == 0
            ? string.Empty
            : SourceId + " -{" + string.Join("|", Names) + "}-> target";
    }

    /// <summary>
    /// Returns ghost-text insert when exactly one suggestion exists.
    /// </summary>
    public string? TryGetGhostText(CompletionContext Context)
    {
        if (Context == null || Context.Items.Count != 1)
        {
            return null;
        }

        string Insert = Context.Items[0].InsertText;
        if (Insert.StartsWith(Context.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Insert.Substring(Context.Prefix.Length);
        }

        return Insert;
    }

    /// <summary>Parameter Info for the statement under the caret.</summary>
    public ParameterHelp? BuildParameterHelp(string Source, int CaretIndex)
    {
        RefreshDocument(Source);
        return EnterpriseHelp.ParameterHelpFor(Source, CaretIndex, _CachedDocument);
    }

    /// <summary>Quick Info for the token under the caret.</summary>
    public string? BuildQuickInfo(string Source, int CaretIndex)
    {
        RefreshDocument(Source);
        return EnterpriseHelp.QuickInfoFor(Source, CaretIndex, _CachedDocument);
    }

    /// <summary>Gets the cached enterprise document, if any.</summary>
    public EnterpriseDocument? CachedDocument
    {
        get
        {
            return _CachedDocument;
        }
    }

    private static void FillElementTypeSuggestions(CompletionContext Context, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.ElementType;
        string Prefix = Match.Groups["type"].Value;
        Context.Prefix = Prefix;
        Group TypeGroup = Match.Groups["type"];
        Context.ReplaceStart = LineStart + TypeGroup.Index;
        Context.ReplaceEnd = LineStart + TypeGroup.Index + TypeGroup.Length;
        foreach (KeyValuePair<EnterpriseElementType, EnterpriseElementMetadata> Pair in EnterpriseElementCatalog.Elements)
        {
            string Name = Pair.Value.KebabName;
            if (!StartsWithPrefix(Name, Prefix))
            {
                continue;
            }

            Context.Items.Add(new CompletionItem(Name + " ", Name, "Element", EnterpriseHelp.ElementTypeDocumentation(Pair.Value)));
        }

        AddAliasType(Context, Prefix, "and-junction", EnterpriseElementType.Junction);
        AddAliasType(Context, Prefix, "or-junction", EnterpriseElementType.Junction);
        AddAliasType(Context, Prefix, "junction-and", EnterpriseElementType.Junction);
        AddAliasType(Context, Prefix, "junction-or", EnterpriseElementType.Junction);
        AddAliasType(Context, Prefix, "group", EnterpriseElementType.Grouping);
        Context.Items.Sort(CompareItems);
    }

    private static void AddAliasType(CompletionContext Context, string Prefix, string Name, EnterpriseElementType ElementType)
    {
        if (!StartsWithPrefix(Name, Prefix))
        {
            return;
        }

        if (!EnterpriseElementCatalog.Elements.TryGetValue(ElementType, out EnterpriseElementMetadata? Metadata) || Metadata == null)
        {
            return;
        }

        Context.Items.Add(new CompletionItem(Name + " ", Name, "Element", EnterpriseHelp.ElementTypeDocumentation(Metadata, Name)));
    }

    private static void FillClauseSuggestions(CompletionContext Context, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.PlacementHint;
        Context.Prefix = string.Empty;
        Context.ReplaceStart = LineStart + Match.Length;
        Context.ReplaceEnd = Context.ReplaceStart;
        if (!Match.Groups["parent"].Success)
        {
            Context.Items.Add(new CompletionItem("in ", "in", "Container", "in <container>\n\nOptional. Identifier of the containing group. The statement can end before this."));
        }

        string[] Hints = new string[] { "above", "below", "left-of", "right-of", "push" };
        foreach (string Hint in Hints)
        {
            Context.Items.Add(new CompletionItem(Hint + " ", Hint, "Placement", EnterpriseHelp.PlacementDocumentation(Hint)));
        }

        Context.Items.Sort(CompareItems);
    }

    private static void FillQualifierSuggestions(CompletionContext Context, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.Qualifier;
        string Kind = Match.Groups["kind"].Value.ToLowerInvariant();
        string Prefix = Match.Groups["qual"].Value;
        Context.Prefix = Prefix;
        Group QualGroup = Match.Groups["qual"];
        Context.ReplaceStart = LineStart + QualGroup.Index;
        Context.ReplaceEnd = LineStart + QualGroup.Index + QualGroup.Length;
        string[] Options = Kind switch
        {
            "access" => new[] { "read", "write", "readwrite", "access" },
            "influence" => new[] { "+", "-", "++", "--" },
            "association" => new[] { "directed" },
            _ => Array.Empty<string>()
        };
        foreach (string Option in Options)
        {
            if (!StartsWithPrefix(Option, Prefix))
            {
                continue;
            }

            Context.Items.Add(new CompletionItem(Option + ")-> ", Option, Kind + " qualifier", EnterpriseHelp.QualifierDocumentation(Kind, Option)));
        }
    }

    private static void FillPlacementSuggestions(CompletionContext Context, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.PlacementHint;
        string Prefix = Match.Groups["hint"].Value;
        Context.Prefix = Prefix;
        Group HintGroup = Match.Groups["hint"];
        Context.ReplaceStart = LineStart + HintGroup.Index;
        Context.ReplaceEnd = LineStart + HintGroup.Index + HintGroup.Length;
        string[] Hints = { "above", "below", "left-of", "right-of", "push" };
        foreach (string Hint in Hints)
        {
            if (!StartsWithPrefix(Hint, Prefix))
            {
                continue;
            }

            string Insert = Hint + " ";
            Context.Items.Add(new CompletionItem(Insert, Hint, "Placement", EnterpriseHelp.PlacementDocumentation(Hint)));
        }
    }

    private static void FillSnippetSuggestions(CompletionContext Context, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.Snippet;
        string Prefix = Match.Groups["type"].Value;
        Context.Prefix = Prefix;
        Group TypeGroup = Match.Groups["type"];
        Context.ReplaceStart = LineStart + TypeGroup.Index;
        Context.ReplaceEnd = LineStart + TypeGroup.Index + TypeGroup.Length;
        (string Name, string Insert, string Detail)[] Snippets =
        {
            ("snippet-serving",
                "application-component app \"App\"\r\n  application-service svc \"Service\"\r\n  app -serving-> svc\r\n",
                "component serving service"),
            ("snippet-access",
                "application-component app \"App\"\r\n  data-object data \"Data\"\r\n  app -access(write)-> data\r\n",
                "component access data")
        };
        foreach ((string Name, string Insert, string Detail) in Snippets)
        {
            if (!StartsWithPrefix(Name, Prefix) && !StartsWithPrefix("snippet", Prefix))
            {
                continue;
            }

            Context.Items.Add(new CompletionItem(Insert, Name, Detail));
        }
    }

    private static void FillDirectionSuggestions(CompletionContext Context, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.Direction;
        string Prefix = Match.Groups["dir"].Value;
        Context.Prefix = Prefix;
        Group DirectionGroup = Match.Groups["dir"];
        Context.ReplaceStart = LineStart + DirectionGroup.Index;
        Context.ReplaceEnd = LineStart + DirectionGroup.Index + DirectionGroup.Length;
        (string Name, string Meaning)[] Values =
        {
            ("TB", "Top to bottom."),
            ("TD", "Top to bottom, same as TB."),
            ("BT", "Bottom to top."),
            ("LR", "Left to right."),
            ("RL", "Right to left.")
        };
        foreach ((string Name, string Meaning) in Values)
        {
            if (!StartsWithPrefix(Name, Prefix))
            {
                continue;
            }

            Context.Items.Add(new CompletionItem(Name, Name + "    " + Meaning, "Direction", "direction " + Name + "\n\n" + Meaning));
        }
    }

    private static void FillPushSuggestions(CompletionContext Context, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.PlacementHint;
        string Prefix = Match.Groups["dir"].Value;
        Context.Prefix = Prefix;
        Group DirectionGroup = Match.Groups["dir"];
        if (DirectionGroup.Success && DirectionGroup.Length > 0)
        {
            Context.ReplaceStart = LineStart + DirectionGroup.Index;
            Context.ReplaceEnd = LineStart + DirectionGroup.Index + DirectionGroup.Length;
        }
        else
        {
            Context.ReplaceStart = LineStart + Match.Length;
            Context.ReplaceEnd = Context.ReplaceStart;
        }

        (string Name, string Meaning)[] Values =
        {
            ("N", "Toward the top of the page."),
            ("S", "Toward the bottom of the page."),
            ("E", "Toward the right of the page."),
            ("W", "Toward the left of the page.")
        };
        foreach ((string Name, string Meaning) in Values)
        {
            if (!StartsWithPrefix(Name, Prefix))
            {
                continue;
            }

            Context.Items.Add(new CompletionItem(Name, Name + "    " + Meaning, "Push", "push " + Name + "\n\n" + Meaning));
        }
    }

    private static void FillPeerSuggestions(CompletionContext Context, EnterpriseDocument Document, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.PlacementHint;
        string Self = Match.Groups["id"].Value;
        string Hint = Match.Groups["hint"].Value.ToLowerInvariant();
        Group PeerGroup = Match.Groups["peer"];
        string Prefix = PeerGroup.Success ? PeerGroup.Value : string.Empty;
        Context.Prefix = Prefix;
        if (PeerGroup.Success && PeerGroup.Length > 0)
        {
            Context.ReplaceStart = LineStart + PeerGroup.Index;
            Context.ReplaceEnd = LineStart + PeerGroup.Index + PeerGroup.Length;
        }
        else
        {
            Context.ReplaceStart = LineStart + Match.Length;
            Context.ReplaceEnd = Context.ReplaceStart;
        }

        foreach (KeyValuePair<string, EnterpriseElement> Pair in Document.Elements)
        {
            if (string.Equals(Pair.Key, Self, StringComparison.Ordinal) || !StartsWithPrefix(Pair.Key, Prefix))
            {
                continue;
            }

            string Row = EnterpriseHelp.ElementRow(Pair.Value);
            string Docs = "Place this element " + Hint + " " + Pair.Key + ".\n\n" + Pair.Value.Label + "\nThe peer must be in the same group.";
            Context.Items.Add(new CompletionItem(Pair.Key, Row, "Peer", Docs));
        }

        Context.Items.Sort(CompareItems);
    }

    private static void FillContainerSuggestions(CompletionContext Context, EnterpriseDocument Document, Match Match, int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.ElementType;
        string Self = Match.Groups["id"].Value;
        Group ContainerGroup = Match.Groups["container"];
        string Prefix = ContainerGroup.Success ? ContainerGroup.Value : string.Empty;
        Context.Prefix = Prefix;
        if (ContainerGroup.Success && ContainerGroup.Length > 0)
        {
            Context.ReplaceStart = LineStart + ContainerGroup.Index;
            Context.ReplaceEnd = LineStart + ContainerGroup.Index + ContainerGroup.Length;
        }
        else
        {
            Context.ReplaceStart = LineStart + Match.Length;
            Context.ReplaceEnd = Context.ReplaceStart;
        }

        foreach (KeyValuePair<string, EnterpriseElement> Pair in Document.Elements)
        {
            if (string.Equals(Pair.Key, Self, StringComparison.Ordinal) || !StartsWithPrefix(Pair.Key, Prefix))
            {
                continue;
            }

            string Docs = "Put this element in " + Pair.Key + " (" + Pair.Value.Label + ").";
            Context.Items.Add(new CompletionItem(Pair.Key, EnterpriseHelp.ElementRow(Pair.Value), "Container", Docs));
        }

        Context.Items.Sort(CompareItems);
    }

    private static void FillSourceSuggestions(CompletionContext Context, EnterpriseDocument Document, string Prefix, int LineStart, Group TypeGroup)
    {
        foreach (KeyValuePair<string, EnterpriseElement> Pair in Document.Elements)
        {
            if (!StartsWithPrefix(Pair.Key, Prefix) && !StartsWithPrefix(Pair.Value.Label, Prefix))
            {
                continue;
            }

            string Docs = EnterpriseHelp.ElementRow(Pair.Value) + "\n\nExisting element. Accepting inserts the identifier only.\nF12 goes to the declaration.";
            Context.Items.Add(new CompletionItem(Pair.Key, EnterpriseHelp.ElementRow(Pair.Value), "Element", Docs));
        }

        if (TypeGroup.Success)
        {
            Context.ReplaceStart = LineStart + TypeGroup.Index;
            Context.ReplaceEnd = LineStart + TypeGroup.Index + TypeGroup.Length;
        }
    }

    private static void FillKindSuggestions(
        CompletionContext Context,
        EnterpriseDocument Document,
        Match Match,
        int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.RelationshipKind;
        string SourceId = Match.Groups["src"].Value;
        string Prefix = Match.Groups["kind"].Value;
        Group KindGroup = Match.Groups["kind"];
        Context.Prefix = Prefix;
        Context.ReplaceStart = LineStart + KindGroup.Index;
        Context.ReplaceEnd = LineStart + KindGroup.Index + KindGroup.Length;

        if (!Document.Elements.TryGetValue(SourceId, out EnterpriseElement? SourceElement))
        {
            Context.StatusMessage = "Unknown source element '" + SourceId + "'.";
            return;
        }

        string SourceType = EnterpriseElementCatalog.GetKebabName(SourceElement.ElementType);
        HashSet<char> AllowedLetters = CollectOutgoingLetters(Document, SourceType);
        if (AllowedLetters.Count == 0)
        {
            Context.StatusMessage = "No legal relationships from " + SourceType + " to declared elements.";
            return;
        }

        List<CompletionItem> Items = new List<CompletionItem>();
        foreach (KeyValuePair<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> Pair in EnterpriseElementCatalog.Relationships)
        {
            if (!AllowedLetters.Contains(Pair.Value.Letter))
            {
                continue;
            }

            string Name = Pair.Value.Name;
            if (!StartsWithPrefix(Name, Prefix) && !StartsWithPrefix(Pair.Value.Letter.ToString(), Prefix))
            {
                continue;
            }

            string Insert = Name + "-> ";
            string Detail = Pair.Value.Letter.ToString(CultureInfo.InvariantCulture) + " from " + SourceType;
            Items.Add(new CompletionItem(Insert, Name, Detail, EnterpriseHelp.RelationshipDocumentation(Pair.Value, SourceType)));
        }

        Items.Sort(CompareItems);
        Context.Items.AddRange(Items);
        if (Context.Items.Count == 0)
        {
            Context.StatusMessage = "No relationship kinds match '" + Prefix + "'.";
        }
    }

    private static void FillTargetSuggestions(
        CompletionContext Context,
        EnterpriseDocument Document,
        Match Match,
        int LineStart)
    {
        Context.TokenKind = CompletionTokenKind.RelationshipTarget;
        string SourceId = Match.Groups["src"].Value;
        string KindToken = Match.Groups["kind"].Value;
        Group TargetGroup = Match.Groups["tgt"];
        string Prefix = TargetGroup.Success ? TargetGroup.Value : string.Empty;
        Context.Prefix = Prefix;
        if (TargetGroup.Success)
        {
            Context.ReplaceStart = LineStart + TargetGroup.Index;
            Context.ReplaceEnd = LineStart + TargetGroup.Index + TargetGroup.Length;
        }
        else
        {
            Context.ReplaceStart = LineStart + Match.Length;
            Context.ReplaceEnd = Context.ReplaceStart;
        }

        if (!Document.Elements.TryGetValue(SourceId, out EnterpriseElement? SourceElement))
        {
            Context.StatusMessage = "Unknown source element '" + SourceId + "'.";
            return;
        }

        if (!EnterpriseElementCatalog.TryResolveRelationship(KindToken, out EnterpriseRelationshipKind Kind))
        {
            Context.StatusMessage = "Unknown relationship '" + KindToken + "'.";
            return;
        }

        char Letter = EnterpriseElementCatalog.GetLetter(Kind);
        string SourceType = EnterpriseElementCatalog.GetKebabName(SourceElement.ElementType);
        EnterpriseRulesCatalog Rules = EnterpriseRulesCatalog.Instance;
        List<CompletionItem> Items = new List<CompletionItem>();
        foreach (KeyValuePair<string, EnterpriseElement> Pair in Document.Elements)
        {
            if (string.Equals(Pair.Key, SourceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!StartsWithPrefix(Pair.Key, Prefix))
            {
                continue;
            }

            string TargetType = EnterpriseElementCatalog.GetKebabName(Pair.Value.ElementType);
            string Allowed = Rules.GetAllowedLetters(Document.Version, SourceType, TargetType);
            if (Allowed.IndexOf(Letter) < 0)
            {
                continue;
            }

            string Label = string.IsNullOrWhiteSpace(Pair.Value.Label) ? Pair.Key : Pair.Value.Label;
            string Row = Pair.Key + "    " + Label + "    " + TargetType;
            string Connected = EnterpriseHelp.ConnectionNote(Document, SourceId, Pair.Key);
            if (Connected.Length > 0)
            {
                Row = Row + "    " + Connected;
            }

            string Docs = Row + "\n\nLegal " + KindToken + " target for " + SourceId + ".";
            if (Connected.Length > 0)
            {
                Docs = Docs + "\n" + Connected + ". A different qualifier is a separate relationship. The same type and qualifier is a duplicate.";
            }

            Items.Add(new CompletionItem(Pair.Key, Row, TargetType, Docs));
        }

        Items.Sort(CompareItems);
        Context.Items.AddRange(Items);
        if (Context.Items.Count == 0)
        {
            Context.StatusMessage = "No legal targets for " + KindToken + " from " + SourceId + ".";
        }
    }

    private static HashSet<char> CollectOutgoingLetters(EnterpriseDocument Document, string SourceType)
    {
        HashSet<char> Letters = new HashSet<char>();
        foreach (string Name in EnterpriseHelp.OutgoingRelationshipNames(Document.Version, SourceType))
        {
            if (EnterpriseElementCatalog.TryResolveRelationship(Name, out EnterpriseRelationshipKind Kind))
            {
                Letters.Add(EnterpriseElementCatalog.GetLetter(Kind));
            }
        }

        return Letters;
    }

    private static bool IsKnownElementTypeToken(string Token)
    {
        return EnterpriseElementCatalog.TryResolveElementType(Token, out _);
    }

    private static bool StartsWithPrefix(string Value, string Prefix)
    {
        if (string.IsNullOrEmpty(Prefix))
        {
            return true;
        }

        return Value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareItems(CompletionItem Left, CompletionItem Right)
    {
        return string.Compare(Left.DisplayText, Right.DisplayText, StringComparison.OrdinalIgnoreCase);
    }

    private static void GetLineBounds(string Source, int CaretIndex, out int LineStart, out int LineEnd)
    {
        LineStart = CaretIndex;
        while (LineStart > 0 && Source[LineStart - 1] != '\n')
        {
            LineStart--;
        }

        LineEnd = CaretIndex;
        while (LineEnd < Source.Length && Source[LineEnd] != '\n')
        {
            LineEnd++;
        }
    }
}
