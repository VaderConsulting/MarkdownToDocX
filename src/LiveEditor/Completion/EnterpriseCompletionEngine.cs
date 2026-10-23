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
    private static readonly Regex KindContextRegex = new Regex(
        @"^(?<indent>\s*)(?<src>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)\s*-(?<kind>[A-Za-z]*)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex TargetContextRegex = new Regex(
        @"^(?<indent>\s*)(?<src>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)\s*-(?<kind>[A-Za-z]+)(?:\([^)]*\))?->\s*(?<tgt>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex QualifierContextRegex = new Regex(
        @"^(?<indent>\s*)(?<src>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)\s*-(?<kind>access|influence|association)\((?<qual>[A-Za-z+-]*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ElementTypeContextRegex = new Regex(
        @"^(?<indent>\s*)(?<type>[A-Za-z-]*)$",
        RegexOptions.Compiled);

    private static readonly Regex PlacementContextRegex = new Regex(
        @"^(?<indent>\s*)(?:[A-Za-z-]+)\s+(?<id>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)(?:\s+(?<label>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'))?(?:\s+in\s+(?<parent>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*))?\s+(?<hint>[A-Za-z-]*)$",
        RegexOptions.Compiled);

    private EnterpriseDocument? _CachedDocument;

    private string _CachedSource = string.Empty;

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

        Match PlacementMatch = PlacementContextRegex.Match(LinePrefix);
        if (PlacementMatch.Success && LiveEditorFeatureGate.D_PlacementHintCompletion)
        {
            FillPlacementSuggestions(Context, PlacementMatch, LineStart);
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

            Context.Items.Add(new CompletionItem(Name + " ", Name, Pair.Value.SpecName));
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

            Context.Items.Add(new CompletionItem(Option + ")-> ", Option, Kind + " qualifier"));
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

            string Insert = Hint == "push" ? "push N" : Hint + " ";
            Context.Items.Add(new CompletionItem(Insert, Hint, "placement hint"));
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
            string Detail = Pair.Value.Letter.ToString(CultureInfo.InvariantCulture) + " · from " + SourceType;
            Items.Add(new CompletionItem(Insert, Name, Detail));
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
            string Detail = TargetType + " · " + Label;
            Items.Add(new CompletionItem(Pair.Key, Pair.Key, Detail));
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
        EnterpriseRulesCatalog Rules = EnterpriseRulesCatalog.Instance;
        foreach (KeyValuePair<string, EnterpriseElement> Pair in Document.Elements)
        {
            string TargetType = EnterpriseElementCatalog.GetKebabName(Pair.Value.ElementType);
            string Allowed = Rules.GetAllowedLetters(Document.Version, SourceType, TargetType);
            foreach (char Letter in Allowed)
            {
                Letters.Add(Letter);
            }
        }

        return Letters;
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
