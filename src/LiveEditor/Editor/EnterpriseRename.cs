using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Editor;

/// <summary>
/// Renames one declared element, group or note and every reference to it. References are found by their grammatical
/// role (declaration, parent, placement peer, relationship end, class/style target), never by matching text, so
/// labels, comments, class names and longer ids that merely contain the old id are untouched. The result is parsed
/// again and must describe exactly the same objects, with only the id changed, or nothing is changed.
/// </summary>
internal static class EnterpriseRename
{
    private const string ID = @"[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*";
    private const string HINT_WORDS = @"above|below|left-of|right-of|NW|NE|SE|SW";

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "enterprise", "enterprise-beta", "title", "version", "direction", "group", "note", "in", "end", "class", "classDef",
        "style", "linkStyle", "accTitle", "accDescr", "at", "push", "default", "above", "below", "left-of", "right-of",
        "NW", "NE", "SE", "SW", "TB", "TD", "BT", "LR", "RL"
    };

    internal sealed record Result(string? Updated, int References, string? Failure);

    internal static Result Rename(string Source, string OldId, string NewId)
    {
        if (string.IsNullOrEmpty(Source) || string.IsNullOrWhiteSpace(OldId)) return Fail("There is nothing to rename.");
        NewId = NewId?.Trim() ?? string.Empty;
        if (!Regex.IsMatch(NewId, "^" + ID + "$")) return Fail("The new id must start with a letter, digit or underscore and contain only letters, digits, '_', '.' and '-'.");
        if (!LiveEditor.Archi.ArchiObjectIds.IsValidMermaidId(NewId)) return Fail("The new id is not a valid Mermaid identifier.");
        if (Reserved.Contains(NewId) || EnterpriseElementCatalog.TryResolveElementType(NewId, out _))
            return Fail("'" + NewId + "' is a keyword or element type and cannot be an id.");
        var Before = EnterpriseValidator.Analyze(Source).Document;
        if (!IsDeclared(Before, OldId)) return Fail("'" + OldId + "' is not a declared element, group or note.");
        if (IsDeclared(Before, NewId)) return Fail("'" + NewId + "' is already used by another object.");
        if (string.Equals(OldId, NewId, StringComparison.Ordinal)) return Fail("The new id is the same as the old id.");

        var Edits = new List<(int Start, int Length)>();
        int Offset = 0;
        foreach (string RawLine in Source.Split('\n'))
        {
            string Line = RawLine.EndsWith('\r') ? RawLine[..^1] : RawLine;
            foreach ((int Start, int Length) in References(Mask(Line), OldId)) Edits.Add((Offset + Start, Length));
            Offset += RawLine.Length + 1;
        }
        if (Edits.Count == 0) return Fail("No reference to '" + OldId + "' was found in a place that can be renamed.");

        var Builder = new StringBuilder(Source);
        foreach (var (Start, Length) in Edits.OrderByDescending(Edit => Edit.Start)) Builder.Remove(Start, Length).Insert(Start, NewId);
        string Updated = Builder.ToString();

        var After = EnterpriseValidator.Analyze(Updated).Document;
        string Expected = Projection(Before, Id => Id == OldId ? NewId : Id);
        if (!string.Equals(Expected, Projection(After, Id => Id), StringComparison.Ordinal))
            return Fail("The script could not be renamed safely because the renamed script would describe different objects.");
        return new(Updated, Edits.Count, null);

        static Result Fail(string Reason) => new(null, 0, Reason);
    }

    /// <summary>True when the id names a declared element, group or note.</summary>
    internal static bool IsRenameable(string Source, string Id)
        => !string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(Id) && IsDeclared(EnterpriseValidator.Analyze(Source).Document, Id);

    private static bool IsDeclarationKeyword(string Word)
        => Word.Equals("group", StringComparison.OrdinalIgnoreCase) || Word.Equals("note", StringComparison.OrdinalIgnoreCase)
            || EnterpriseElementCatalog.TryResolveElementType(Word, out _);

    private static bool IsDeclared(EnterpriseDocument Document, string Id)
        => Document.Elements.ContainsKey(Id) || Document.Groups.ContainsKey(Id) || Document.Notes.ContainsKey(Id);

    // Strings and comments become spaces of the same length, so positions survive and no label can be mistaken for code.
    private static string Mask(string Line)
    {
        var Masked = new StringBuilder(Line);
        char Quote = '\0';
        for (int I = 0; I < Line.Length; I++)
        {
            char C = Line[I];
            if (Quote != '\0')
            {
                Masked[I] = ' ';
                if (C == '\\' && I + 1 < Line.Length) { Masked[++I] = ' '; }
                else if (C == Quote) Quote = '\0';
                continue;
            }
            if (C is '"' or '\'') { Quote = C; Masked[I] = ' '; continue; }
            if (C == '%' && I + 1 < Line.Length && Line[I + 1] == '%')
            {
                for (int J = I; J < Line.Length; J++) Masked[J] = ' ';
                break;
            }
        }
        return Masked.ToString();
    }

    private static IEnumerable<(int Start, int Length)> References(string Line, string OldId)
    {
        var Found = new List<(int, int)>();
        void Add(Group Group) { if (Group.Success && Group.Value == OldId) Found.Add((Group.Index, Group.Length)); }

        Match Directive = Regex.Match(Line, @"^\s*(?<keyword>classDef|class|style|linkStyle)\s+(?<targets>\S+)", RegexOptions.IgnoreCase);
        if (Directive.Success)
        {
            string Keyword = Directive.Groups["keyword"].Value;
            if (Keyword.Equals("class", StringComparison.OrdinalIgnoreCase) || Keyword.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                Group Targets = Directive.Groups["targets"];
                int Position = Targets.Index;
                foreach (string Target in Targets.Value.Split(','))
                {
                    if (Target == OldId) Found.Add((Position, Target.Length));
                    Position += Target.Length + 1;
                }
            }
            return Found;
        }

        Match Relationship = Regex.Match(Line, @"^\s*(?<source>" + ID + @")\s+(?:-[A-Za-z]+(?:\([^)]*\))?-+>|-{3,})\s+(?<target>" + ID + ")");
        if (Relationship.Success)
        {
            Add(Relationship.Groups["source"]); Add(Relationship.Groups["target"]);
            return Found;
        }

        Match Declaration = Regex.Match(Line, @"^\s*(?<type>[A-Za-z-]+)\s+(?<id>" + ID + ")(?<rest>.*)$");
        if (Declaration.Success && IsDeclarationKeyword(Declaration.Groups["type"].Value))
        {
            Add(Declaration.Groups["id"]);
            Group Rest = Declaration.Groups["rest"];
            // The parent follows `in`; a placement peer follows a relative hint word. Label text is already masked.
            foreach (Match Parent in Regex.Matches(Rest.Value, @"(?<![\p{L}\p{N}_.-])in\s+(?<parent>" + ID + ")"))
                if (Parent.Groups["parent"].Value == OldId) Found.Add((Rest.Index + Parent.Groups["parent"].Index, OldId.Length));
            foreach (Match Peer in Regex.Matches(Rest.Value, @"(?<![\p{L}\p{N}_.-])(?:" + HINT_WORDS + @")\s+(?<peer>" + ID + ")", RegexOptions.IgnoreCase))
                if (Peer.Groups["peer"].Value == OldId) Found.Add((Rest.Index + Peer.Groups["peer"].Index, OldId.Length));
        }
        // `id:::class` applies a class to this id wherever it appears.
        foreach (Match Inline in Regex.Matches(Line, @"(?<id>" + ID + @"):::"))
            if (Inline.Groups["id"].Value == OldId && !Found.Contains((Inline.Groups["id"].Index, OldId.Length)))
                Found.Add((Inline.Groups["id"].Index, OldId.Length));
        return Found.Distinct().OrderBy(Item => Item.Item1).Select(Item => (Item.Item1, Item.Item2));
    }

    // Everything the parser knows about the objects, keyed by the (possibly mapped) id, in a stable order.
    private static string Projection(EnterpriseDocument Document, Func<string, string> Map)
    {
        var Nodes = new SortedDictionary<string, JsonNode?>(StringComparer.Ordinal);
        JsonNode? Describe(object Value, params string[] IdFields)
        {
            var Node = JsonSerializer.SerializeToNode(Value, Value.GetType(), new JsonSerializerOptions { ReferenceHandler = null, MaxDepth = 8 });
            if (Node is JsonObject Object)
            {
                foreach (string Field in IdFields)
                {
                    string[] Path = Field.Split('.');
                    JsonObject? Holder = Object;
                    for (int I = 0; I < Path.Length - 1 && Holder != null; I++) Holder = Holder[Path[I]] as JsonObject;
                    if (Holder != null && Holder[Path[^1]] is JsonValue Id && Id.TryGetValue(out string? Text) && Text != null) Holder[Path[^1]] = Map(Text);
                }
                // Parser explanations quote ids ("peer \"order\" is in another group"), so they follow the rename too.
                foreach (string Hint in new[] { "PlacementHint", "RoutingHint" })
                    if (Object[Hint] is JsonObject Owner && Owner["UnhonouredReason"] is JsonValue Reason && Reason.TryGetValue(out string? Why) && Why != null)
                        Owner["UnhonouredReason"] = Regex.Replace(Why, "\"(" + ID + ")\"", Match => "\"" + Map(Match.Groups[1].Value) + "\"");
            }
            return Node;
        }
        foreach (var Element in Document.Elements.Values) Nodes["e:" + Map(Element.Id)] = Describe(Element, "Id", "ParentId", "PlacementHint.PeerId");
        foreach (var Group in Document.Groups.Values) Nodes["g:" + Map(Group.Id)] = Describe(Group, "Id", "ParentId", "PlacementHint.PeerId");
        foreach (var Note in Document.Notes.Values) Nodes["n:" + Map(Note.Id)] = Describe(Note, "Id", "ParentId", "PlacementHint.PeerId");
        var Builder = new StringBuilder();
        // Text that is not an id must be identical; a rename that altered a title or description is wrong.
        Builder.Append("t:").Append(Document.Title).Append('|').Append(Document.AccessibilityTitle).Append('|').Append(Document.AccessibilityDescription).Append('\n');
        foreach (var Pair in Document.Styles.Classes.OrderBy(Pair => Pair.Key, StringComparer.Ordinal))
            Builder.Append("k:").Append(Pair.Key).Append('=').Append(string.Join(";", Pair.Value.OrderBy(Item => Item.Key, StringComparer.Ordinal).Select(Item => Item.Key + ":" + Item.Value))).Append('\n');
        foreach (var Pair in Nodes) Builder.Append(Pair.Key).Append('=').Append(Pair.Value?.ToJsonString()).Append('\n');
        foreach (var Relationship in Document.Relationships)
            Builder.Append("r:").Append(Describe(Relationship, "SourceId", "TargetId")?.ToJsonString()).Append('\n');
        foreach (var Connection in Document.PlainConnections)
            Builder.Append("c:").Append(Describe(Connection, "SourceId", "TargetId")?.ToJsonString()).Append('\n');
        foreach (var Style in Document.Styles.Elements.OrderBy(Pair => Map(Pair.Key), StringComparer.Ordinal))
            Builder.Append("s:").Append(Map(Style.Key)).Append('=').Append(string.Join(";", Style.Value.OrderBy(Pair => Pair.Key, StringComparer.Ordinal).Select(Pair => Pair.Key + ":" + Pair.Value))).Append('\n');
        return Builder.ToString();
    }
}
