using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Completion;

/// <summary>
/// Parameter Info for the statement under the caret.
/// </summary>
internal sealed class ParameterHelp
{
    /// <summary>Gets or sets the one-line signature.</summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>Gets or sets the start of the active parameter in <see cref="Signature"/>.</summary>
    public int BoldStart { get; set; }

    /// <summary>Gets or sets the length of the active parameter.</summary>
    public int BoldLength { get; set; }

    /// <summary>Gets or sets the parameter heading.</summary>
    public string Heading { get; set; } = string.Empty;

    /// <summary>Gets or sets the explanation under the heading.</summary>
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Shared element, relationship, and hint text for completion, Parameter Info, and Quick Info.
/// </summary>
internal static class EnterpriseHelp
{
    private const int NOTE_WIDTH = 64;

    private static readonly Dictionary<string, string> ELEMENT_SUMMARY = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application-collaboration"] = "Declares two or more application components working together to perform collective application behavior.",
        ["application-component"] = "Declares a modular, replaceable part of an application that encapsulates its behaviour and data.",
        ["application-service"] = "Declares behaviour the application layer exposes to its users.",
        ["data-object"] = "Declares data used or produced by application behaviour.",
        ["business-actor"] = "Declares a person, organisation, or system that performs business behaviour.",
        ["business-process"] = "Declares a sequence of business behaviour that achieves a result.",
        ["grouping"] = "Declares a visual frame around elements that belong together. It is not an ArchiMate layer."
    };

    /// <summary>Builds the documentation panel for an element type.</summary>
    public static string ElementTypeDocumentation(EnterpriseElementMetadata Metadata)
    {
        string Name = Metadata.KebabName;
        string Summary = ELEMENT_SUMMARY.TryGetValue(Name, out string? Written)
            ? Written
            : "Declares a " + Metadata.Layer.ToString().ToLowerInvariant() + " " + AspectWord(Metadata.Aspect) + " element (" + ElementShapePainterName(Name) + ").";
        string ExampleId = ExampleIdentifier(ElementShapePainterName(Name));
        List<string> Lines = new List<string>();
        AppendSignature(Lines, Name + " <id> [\"label\"]", "[in <container>] [<placement>]");
        Lines.Add(string.Empty);
        Lines.AddRange(Wrap(Summary, NOTE_WIDTH));
        Lines.Add(string.Empty);
        int NameWidth = FieldWidth("id", "\"label\"", "container", "placement");
        AppendField(Lines, "id", "Required. Identifier unique within this diagram.", NameWidth);
        AppendField(Lines, "\"label\"", "Optional. Displayed name; defaults to the identifier.", NameWidth);
        AppendField(Lines, "container", "Optional. Identifier of the containing group.", NameWidth);
        AppendField(Lines, "placement", "Optional. Position relative to another element, or an outward push direction.", NameWidth);
        Lines.Add(string.Empty);
        Lines.Add("Example");
        Lines.Add(Name + " " + ExampleId + " \"" + ElementShapePainterName(Name) + "\"");
        Lines.Add(string.Empty);
        Lines.Add("Tab to insert. Esc to dismiss.");
        return Compose(Name, "Element", Lines);
    }

    /// <summary>Documentation for one relationship name.</summary>
    public static string RelationshipDocumentation(EnterpriseRelationshipMetadata Metadata, string SourceType)
    {
        List<string> Lines = new List<string>();
        Lines.Add("<source> -" + Metadata.Name + "-> <target> [\"label\"]");
        Lines.Add(string.Empty);
        Lines.AddRange(Wrap(RelationshipSummary(Metadata.Name), NOTE_WIDTH));
        Lines.Add(string.Empty);
        Lines.AddRange(Wrap("Shown because " + SourceType + " can use it with an element already in this diagram.", NOTE_WIDTH));
        Lines.Add("The target list narrows further once the other end is known.");
        Lines.Add(string.Empty);
        Lines.Add("Tab to insert. Esc to dismiss.");
        return Compose(Metadata.Name, "Relationship", Lines);
    }

    /// <summary>Documentation for an access, influence, or association qualifier.</summary>
    public static string QualifierDocumentation(string Kind, string Option)
    {
        List<string> Lines = new List<string>();
        if (string.Equals(Kind, "access", StringComparison.OrdinalIgnoreCase))
        {
            Lines.Add("source -access(qualifier)-> target [\"label\"]");
            Lines.Add(string.Empty);
            Lines.AddRange(Wrap("Describes access to a passive structure element, such as a data object.", NOTE_WIDTH));
            Lines.Add(string.Empty);
            Lines.AddRange(Wrap(AccessOptionLine(Option), NOTE_WIDTH));
            Lines.Add(string.Empty);
            int NameWidth = FieldWidth("read", "write", "readwrite", "access");
            AppendField(Lines, "read", "reads information", NameWidth);
            AppendField(Lines, "write", "creates or changes information", NameWidth);
            AppendField(Lines, "readwrite", "reads and writes information", NameWidth);
            AppendField(Lines, "access", "access without specifying read or write", NameWidth);
            Lines.Add(string.Empty);
            Lines.Add("Default when omitted: write.");
            return Compose(Option, Kind + " qualifier", Lines);
        }

        if (string.Equals(Kind, "influence", StringComparison.OrdinalIgnoreCase))
        {
            Lines.Add("How strongly the source influences the target.");
            Lines.Add("+ or ++ strengthens. - or -- weakens.");
            Lines.Add("There is no default strength.");
            return Compose(Option, Kind + " qualifier", Lines);
        }

        int DirectedWidth = FieldWidth("directed");
        AppendField(Lines, "directed", "the association has a direction.", DirectedWidth);
        Lines.Add("Without directed, the association is undirected.");
        return Compose(Option, Kind + " qualifier", Lines);
    }

    /// <summary>Documentation for a placement word.</summary>
    public static string PlacementDocumentation(string Hint)
    {
        List<string> Lines = new List<string>();
        if (string.Equals(Hint, "push", StringComparison.Ordinal))
        {
            Lines.Add("push <N|S|E|W>");
            Lines.Add(string.Empty);
            Lines.Add("Pushes this element outward, just past its siblings.");
            Lines.Add("N is toward the top of the page, S the bottom, E the right, W the left.");
            Lines.Add(string.Empty);
            Lines.Add("Tab to insert. Esc to dismiss.");
            return Compose(Hint, "Placement", Lines);
        }

        Lines.Add(Hint + " <peer> [+n]");
        Lines.Add(string.Empty);
        Lines.AddRange(Wrap(PlacementSentence(Hint), NOTE_WIDTH));
        int NameWidth = FieldWidth("peer", "+n");
        AppendField(Lines, "peer", "Required. An element, group, or note in the same group.", NameWidth);
        AppendField(Lines, "+n", "Optional. Extra whole cells beyond the normal gap.", NameWidth);
        Lines.Add(string.Empty);
        Lines.Add("Tab to insert. Esc to dismiss.");
        return Compose(Hint, "Placement", Lines);
    }

    /// <summary>Parameter Info for the caret, or null when the line is not inside a statement.</summary>
    public static ParameterHelp? ParameterHelpFor(string Source, int CaretIndex, EnterpriseDocument? Document)
    {
        if (Document == null || string.IsNullOrEmpty(Source))
        {
            return null;
        }

        GetLine(Source, CaretIndex, out int LineStart, out int LineEnd, out int LineNumber);
        string Line = Source.Substring(LineStart, LineEnd - LineStart);
        int Column = CaretIndex - LineStart;
        ParameterHelp? Direction = DirectionHelp(Line, Column);
        if (Direction != null)
        {
            return Direction;
        }

        ParameterHelp? Relationship = RelationshipHelp(Line, Column, Document);
        if (Relationship != null)
        {
            return Relationship;
        }

        return ElementHelp(Line, Column, LineNumber, Document);
    }

    /// <summary>Quick Info for the token under the caret, or null.</summary>
    public static string? QuickInfoFor(string Source, int CaretIndex, EnterpriseDocument? Document)
    {
        if (string.IsNullOrEmpty(Source) || Document == null)
        {
            return null;
        }

        GetLine(Source, CaretIndex, out int LineStart, out int LineEnd, out _);
        string Line = Source.Substring(LineStart, LineEnd - LineStart);
        int Column = Math.Max(0, Math.Min(CaretIndex - LineStart, Line.Length));
        string Token = TokenAt(Line, Column);
        if (Token.Length == 0)
        {
            return null;
        }

        if (EnterpriseElementCatalog.TryResolveElementType(Token, out EnterpriseElementType ElementType)
            && EnterpriseElementCatalog.Elements.TryGetValue(ElementType, out EnterpriseElementMetadata? Metadata))
        {
            return ElementTypeDocumentation(Metadata);
        }

        if (EnterpriseElementCatalog.TryResolveRelationship(Token, out EnterpriseRelationshipKind Kind))
        {
            return RelationshipDocumentation(EnterpriseElementCatalog.Relationships[Kind], "the source");
        }

        if (IsPlacementWord(Token))
        {
            return PlacementDocumentation(Token);
        }

        if (Document.Elements.TryGetValue(Token, out EnterpriseElement? Element))
        {
            return ElementQuickInfo(Element);
        }

        if (Document.Groups.TryGetValue(Token, out EnterpriseGroup? Group))
        {
            return "group " + Group.Id + "\n\nDisplayed name: " + Group.Label + "\n\nF12 goes to this declaration.";
        }

        if (Document.Notes.TryGetValue(Token, out EnterpriseNote? Note))
        {
            return "note " + Note.Id + "\n\n" + Note.Label + "\n\nF12 goes to this declaration.";
        }

        return null;
    }

    /// <summary>Compact row for an element that already exists.</summary>
    public static string ElementRow(EnterpriseElement Element)
    {
        string TypeName = EnterpriseElementCatalog.GetKebabName(Element.ElementType);
        return Element.Id + "    " + Element.Label + "    " + TypeName;
    }

    /// <summary>Text for a reference row, including connections already drawn to it.</summary>
    public static string ConnectionNote(EnterpriseDocument Document, string SourceId, string TargetId)
    {
        List<string> Notes = new List<string>();
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            bool SameEnds = string.Equals(Relationship.SourceId, SourceId, StringComparison.Ordinal)
                && string.Equals(Relationship.TargetId, TargetId, StringComparison.Ordinal);
            if (!SameEnds)
            {
                continue;
            }

            string Name = EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind);
            if (Relationship.AccessKind.HasValue)
            {
                Name = Name + "(" + AccessWord(Relationship.AccessKind.Value) + ")";
            }

            Notes.Add(Name);
        }

        if (Notes.Count == 0)
        {
            return string.Empty;
        }

        return "Already connected: " + string.Join(", ", Notes);
    }

    private static ParameterHelp? DirectionHelp(string Line, int Column)
    {
        Match Match = Regex.Match(Line, @"^\s*direction\b", RegexOptions.IgnoreCase);
        if (!Match.Success || Column < Match.Length)
        {
            return null;
        }

        string Signature = "direction <TB|TD|BT|LR|RL>";
        return Make(Signature, "<TB|TD|BT|LR|RL>", "direction - flow of the page", "TB and TD run top to bottom. BT runs bottom to top. LR runs left to right. RL runs right to left.");
    }

    private static ParameterHelp? RelationshipHelp(string Line, int Column, EnterpriseDocument Document)
    {
        Match Match = Regex.Match(Line, @"^(?<indent>\s*)(?<src>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)\s*-");
        if (!Match.Success)
        {
            return null;
        }

        if (!Document.Elements.ContainsKey(Match.Groups["src"].Value))
        {
            return null;
        }

        string SourceId = Match.Groups["src"].Value;
        int Dash = Line.IndexOf('-', Match.Groups["src"].Index);
        if (Dash < 0 || Column <= Dash)
        {
            return null;
        }

        string Signature = SourceId + " -<relationship>-> <target> [\"label\"] [[routing]]";
        int QualifierStart = Line.IndexOf('(', Dash);
        int Arrow = Line.IndexOf("->", Dash, StringComparison.Ordinal);
        if (QualifierStart >= 0 && (Arrow < 0 || QualifierStart < Arrow) && Column > QualifierStart)
        {
            int QualifierEnd = Line.IndexOf(')', QualifierStart);
            if (QualifierEnd < 0 || Column <= QualifierEnd)
            {
                string Kind = KindToken(Line, Dash);
                return Make(
                    SourceId + " -" + Kind + "(<qualifier>)-> <target> [\"label\"]",
                    "<qualifier>",
                    "qualifier - " + Kind + " mode",
                    QualifierBody(Kind));
            }
        }

        if (Arrow >= 0 && Column > Arrow + 1)
        {
            string Kind = KindToken(Line, Dash);
            return Make(
                SourceId + " -" + Kind + "-> <target> [\"label\"] [[routing]]",
                "<target>",
                "target - existing element",
                "An element already declared in this diagram. It must be a legal " + Kind + " target for " + SourceId + ".");
        }

        return Make(Signature, "<relationship>", "relationship - permitted type", "A relationship this source can have with an element already in the diagram.");
    }

    private static ParameterHelp? ElementHelp(string Line, int Column, int LineNumber, EnterpriseDocument Document)
    {
        Match Match = Regex.Match(Line, @"^(?<indent>\s*)(?<type>[A-Za-z][A-Za-z-]*)(?<gap>\s*)(?<id>[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*)?");
        if (!Match.Success)
        {
            return null;
        }

        string TypeName = Match.Groups["type"].Value;
        if (!EnterpriseElementCatalog.TryResolveElementType(TypeName, out _))
        {
            return null;
        }

        int TypeEnd = Match.Groups["type"].Index + Match.Groups["type"].Length;
        if (Column <= TypeEnd)
        {
            return null;
        }

        string Signature = TypeName + " <id> [\"label\"] [in <container>] [<placement>]";
        string AfterId = string.Empty;
        int IdEnd = TypeEnd;
        if (Match.Groups["id"].Success)
        {
            IdEnd = Match.Groups["id"].Index + Match.Groups["id"].Length;
            if (IdEnd < Line.Length)
            {
                AfterId = Line.Substring(IdEnd);
            }
        }

        if (!Match.Groups["id"].Success || Column <= IdEnd)
        {
            string Id = Match.Groups["id"].Success ? Match.Groups["id"].Value : string.Empty;
            return Make(Signature, "<id>", "id - required identifier", IdentifierBody(Document, Id, LineNumber));
        }

        if (IsInsideQuotes(Line, Column))
        {
            return Make(Signature, "[\"label\"]", "label - optional displayed name", "Enclose the text in quotes. If omitted, the identifier is displayed. The statement can end before this.");
        }

        if (Regex.IsMatch(AfterId, @"\bin\s+\S*$", RegexOptions.IgnoreCase) && Column > IdEnd)
        {
            return Make(Signature, "[in <container>]", "container - optional group", "The identifier of the group this element belongs to. The statement can end before this.");
        }

        if (Regex.IsMatch(AfterId, @"\b(above|below|left-of|right-of)\s+\S*$", RegexOptions.IgnoreCase))
        {
            return Make(Signature, "[<placement>]", "peer - element to place against", "An element, group, or note in the same group. Optional +n adds whole cells beyond the normal gap.");
        }

        if (Regex.IsMatch(AfterId, @"\bpush\s+\S*$", RegexOptions.IgnoreCase))
        {
            return Make(Signature, "[<placement>]", "push - outward direction", "N toward the top of the page, S the bottom, E the right, W the left.");
        }

        if (Regex.IsMatch(AfterId, @"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'"))
        {
            return null;
        }

        return Make(Signature, "[\"label\"]", "label - optional displayed name", "Enclose the text in quotes. If omitted, the identifier is displayed. The statement can end before this.");
    }

    private static string IdentifierBody(EnterpriseDocument Document, string Id, int LineNumber)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.AppendLine("Must be unique within this diagram. Used to reference this element in relationships and other statements. Identifiers are case-sensitive. Example: ac.");
        if (Id.Length > 0 && Document.Elements.TryGetValue(Id, out EnterpriseElement? Existing) && Existing.LineNumber != LineNumber)
        {
            string TypeName = EnterpriseElementCatalog.GetKebabName(Existing.ElementType);
            Builder.AppendLine();
            Builder.AppendLine("Identifier '" + Id + "' is already in use.");
            Builder.AppendLine("Declared as " + TypeName + " \"" + Existing.Label + "\".");
            Builder.Append("Choose another identifier. F12 goes to the declaration.");
        }

        return Builder.ToString().TrimEnd();
    }

    private static string ElementQuickInfo(EnterpriseElement Element)
    {
        string TypeName = EnterpriseElementCatalog.GetKebabName(Element.ElementType);
        List<string> Lines = new List<string>();
        Lines.Add("Displayed name: " + Element.Label);
        if (!string.IsNullOrEmpty(Element.ParentId))
        {
            Lines.Add("Container: " + Element.ParentId);
        }

        Lines.Add("Declared on line " + Element.LineNumber.ToString(CultureInfo.InvariantCulture) + ".");
        Lines.Add("F12 goes to this declaration.");
        return Compose(Element.Id, TypeName, Lines);
    }

    private static string Compose(string Title, string Kind, List<string> Lines)
    {
        int Width = Title.Length + 2 + Kind.Length;
        foreach (string Line in Lines)
        {
            if (Line.Length > Width)
            {
                Width = Line.Length;
            }
        }

        StringBuilder Builder = new StringBuilder();
        Builder.Append(Title);
        Builder.Append(' ', Math.Max(2, Width - Title.Length - Kind.Length));
        Builder.AppendLine(Kind);
        Builder.AppendLine();
        foreach (string Line in Lines)
        {
            Builder.AppendLine(Line);
        }

        return Builder.ToString().TrimEnd();
    }

    private static void AppendSignature(List<string> Lines, string First, string Continuation)
    {
        Lines.Add(First);
        int Bracket = First.IndexOf('[');
        if (Bracket < 0)
        {
            Bracket = 0;
        }

        Lines.Add(new string(' ', Bracket) + Continuation);
    }

    private static int FieldWidth(params string[] Names)
    {
        int Width = 0;
        foreach (string Name in Names)
        {
            if (Name.Length > Width)
            {
                Width = Name.Length;
            }
        }

        return Width + 2;
    }

    private static void AppendField(List<string> Lines, string Name, string Text, int NameWidth)
    {
        List<string> Wrapped = Wrap(Text, Math.Max(24, NOTE_WIDTH - NameWidth));
        for (int Index = 0; Index < Wrapped.Count; Index++)
        {
            if (Index == 0)
            {
                Lines.Add(Name.PadRight(NameWidth) + Wrapped[Index]);
            }
            else
            {
                Lines.Add(new string(' ', NameWidth) + Wrapped[Index]);
            }
        }
    }

    private static List<string> Wrap(string Text, int Width)
    {
        List<string> Lines = new List<string>();
        if (string.IsNullOrEmpty(Text))
        {
            return Lines;
        }

        string[] Words = Text.Split(' ');
        StringBuilder Line = new StringBuilder();
        foreach (string Word in Words)
        {
            if (Word.Length == 0)
            {
                continue;
            }

            if (Line.Length == 0)
            {
                Line.Append(Word);
            }
            else if (Line.Length + 1 + Word.Length <= Width)
            {
                Line.Append(' ');
                Line.Append(Word);
            }
            else
            {
                Lines.Add(Line.ToString());
                Line.Clear();
                Line.Append(Word);
            }
        }

        if (Line.Length > 0)
        {
            Lines.Add(Line.ToString());
        }

        return Lines;
    }

    private static ParameterHelp Make(string Signature, string Active, string Heading, string Body)
    {
        int Start = Signature.IndexOf(Active, StringComparison.Ordinal);
        ParameterHelp Help = new ParameterHelp();
        Help.Signature = Signature;
        Help.BoldStart = Math.Max(0, Start);
        Help.BoldLength = Start < 0 ? 0 : Active.Length;
        Help.Heading = Heading;
        Help.Body = Body;
        return Help;
    }

    private static string QualifierBody(string Kind)
    {
        if (string.Equals(Kind, "access", StringComparison.OrdinalIgnoreCase))
        {
            return "read reads information. write creates or changes information. readwrite does both. access does not say which. Default when omitted: write.";
        }

        if (string.Equals(Kind, "influence", StringComparison.OrdinalIgnoreCase))
        {
            return "+ and ++ strengthen the influence. - and -- weaken it.";
        }

        return "directed gives the association a direction. Omit it for an undirected association.";
    }

    private static string KindToken(string Line, int Dash)
    {
        int Start = Dash + 1;
        int End = Start;
        while (End < Line.Length && char.IsLetter(Line[End]))
        {
            End++;
        }

        if (End == Start)
        {
            return "relationship";
        }

        return Line.Substring(Start, End - Start);
    }

    private static bool IsInsideQuotes(string Line, int Column)
    {
        bool Open = false;
        int Limit = Math.Min(Column, Line.Length);
        for (int Index = 0; Index < Limit; Index++)
        {
            if (Line[Index] == '"' && (Index == 0 || Line[Index - 1] != '\\'))
            {
                Open = !Open;
            }
        }

        return Open;
    }

    private static bool IsPlacementWord(string Token)
    {
        return Token == "above" || Token == "below" || Token == "left-of" || Token == "right-of" || Token == "push";
    }

    private static string PlacementSentence(string Hint)
    {
        switch (Hint)
        {
            case "above":
                return "Places this element above the named element.";
            case "below":
                return "Places this element below the named element.";
            case "left-of":
                return "Places this element to the left of the named element.";
            case "right-of":
                return "Places this element to the right of the named element.";
            default:
                return "Places this element relative to the named element.";
        }
    }

    private static string RelationshipSummary(string Name)
    {
        switch (Name)
        {
            case "access":
                return "The source accesses a passive structure element, such as a data object.";
            case "serving":
                return "The source provides its behaviour to the target.";
            case "composition":
                return "The source is composed of the target.";
            case "aggregation":
                return "The source groups the target, which can also stand alone.";
            case "assignment":
                return "The source is assigned to perform or carry the target.";
            case "realization":
                return "The source realises the target.";
            case "triggering":
                return "The source starts or follows on to the target.";
            case "flow":
                return "The source passes information or material to the target.";
            default:
                return "Connects the source element to the target element.";
        }
    }

    private static string AccessOptionLine(string Option)
    {
        switch (Option)
        {
            case "read":
                return "read reads information.";
            case "write":
                return "write creates or changes information. This is also the default when the qualifier is omitted.";
            case "readwrite":
                return "readwrite reads and writes information.";
            default:
                return "access names the relationship without saying read or write.";
        }
    }

    private static string AccessWord(EnterpriseAccessKind Kind)
    {
        switch (Kind)
        {
            case EnterpriseAccessKind.Read:
                return "read";
            case EnterpriseAccessKind.ReadWrite:
                return "readwrite";
            case EnterpriseAccessKind.Access:
                return "access";
            default:
                return "write";
        }
    }

    private static string AspectWord(EnterpriseAspect Aspect)
    {
        switch (Aspect)
        {
            case EnterpriseAspect.Behaviour:
                return "behaviour";
            case EnterpriseAspect.Passive:
                return "passive";
            default:
                return "active";
        }
    }

    private static string ElementShapePainterName(string KebabName)
    {
        return ElementShapePainter.DisplayName(KebabName);
    }

    private static string ExampleIdentifier(string DisplayName)
    {
        string[] Parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (Parts.Length == 1)
        {
            return Parts[0].Length <= 2 ? Parts[0].ToLowerInvariant() : Parts[0].Substring(0, 2).ToLowerInvariant();
        }

        StringBuilder Builder = new StringBuilder();
        for (int Index = 0; Index < Parts.Length && Builder.Length < 3; Index++)
        {
            Builder.Append(char.ToLowerInvariant(Parts[Index][0]));
        }

        return Builder.ToString();
    }

    private static string TokenAt(string Line, int Column)
    {
        if (Line.Length == 0)
        {
            return string.Empty;
        }

        int Index = Math.Min(Column, Line.Length - 1);
        if (Index > 0 && (Index == Line.Length || !IsTokenChar(Line[Index])) && IsTokenChar(Line[Index - 1]))
        {
            Index--;
        }

        if (!IsTokenChar(Line[Index]))
        {
            return string.Empty;
        }

        int Start = Index;
        while (Start > 0 && IsTokenChar(Line[Start - 1]))
        {
            Start--;
        }

        int End = Index + 1;
        while (End < Line.Length && IsTokenChar(Line[End]))
        {
            End++;
        }

        return Line.Substring(Start, End - Start);
    }

    private static bool IsTokenChar(char Character)
    {
        return char.IsLetterOrDigit(Character) || Character == '-' || Character == '_';
    }

    private static void GetLine(string Source, int CaretIndex, out int LineStart, out int LineEnd, out int LineNumber)
    {
        if (CaretIndex < 0)
        {
            CaretIndex = 0;
        }

        if (CaretIndex > Source.Length)
        {
            CaretIndex = Source.Length;
        }

        LineStart = CaretIndex;
        while (LineStart > 0 && Source[LineStart - 1] != '\n')
        {
            LineStart--;
        }

        LineEnd = CaretIndex;
        while (LineEnd < Source.Length && Source[LineEnd] != '\n' && Source[LineEnd] != '\r')
        {
            LineEnd++;
        }

        LineNumber = 1;
        for (int Index = 0; Index < LineStart; Index++)
        {
            if (Source[Index] == '\n')
            {
                LineNumber++;
            }
        }
    }
}
