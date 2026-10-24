using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LiveEditor.Completion;

/// <summary>
/// List Members, Parameter Info, and Quick Info for the Mermaid diagrams LiveEditor highlights.
/// </summary>
internal sealed class MermaidDiagramAssist
{
    private static readonly Regex HeaderRegex = new Regex(
        @"^\s*(enterprise(?:-beta)?|flowchart|graph|sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|journey|gantt|pie|mindmap|timeline|sankey-beta|architecture-beta)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, StatementWord[]> Words = BuildWords();

    private static readonly StatementWord[] DiagramOpeners = new StatementWord[]
    {
        new StatementWord("enterprise\r\n", "Diagram", "Archimate-style enterprise diagram."),
        new StatementWord("flowchart TD\r\n", "Diagram", "Flowchart, top to bottom."),
        new StatementWord("flowchart LR\r\n", "Diagram", "Flowchart, left to right."),
        new StatementWord("sequenceDiagram\r\n", "Diagram", "Messages between participants."),
        new StatementWord("classDiagram\r\n", "Diagram", "Classes and relations."),
        new StatementWord("stateDiagram-v2\r\n", "Diagram", "States and transitions."),
        new StatementWord("erDiagram\r\n", "Diagram", "Entities and relationships."),
        new StatementWord("journey\r\n", "Diagram", "Tasks scored across sections."),
        new StatementWord("gantt\r\n", "Diagram", "Tasks on a calendar."),
        new StatementWord("pie\r\n", "Diagram", "Slices of a whole."),
        new StatementWord("mindmap\r\n", "Diagram", "Indented topics."),
        new StatementWord("timeline\r\n", "Diagram", "Events in sections."),
        new StatementWord("sankey-beta\r\n", "Diagram", "Flows from source to target."),
        new StatementWord("architecture-beta\r\n", "Diagram", "Groups, services, and junctions.")
    };

    private static readonly StatementWord[] Directions = new StatementWord[]
    {
        new StatementWord("TB", "Direction", "Top to bottom."),
        new StatementWord("TD", "Direction", "Top to bottom."),
        new StatementWord("BT", "Direction", "Bottom to top."),
        new StatementWord("LR", "Direction", "Left to right."),
        new StatementWord("RL", "Direction", "Right to left.")
    };

    /// <summary>Suggestions for the token being typed.</summary>
    public CompletionContext BuildMembers(string Source, int CaretIndex)
    {
        CompletionContext Context = new CompletionContext();
        string Text = Source ?? string.Empty;
        int Caret = Clamp(Text, CaretIndex);
        GetLine(Text, Caret, out int LineStart, out int LineEnd);
        string Line = Text.Substring(LineStart, LineEnd - LineStart);
        int Column = Caret - LineStart;
        string Kind = DetectKind(Text, LineStart);
        if (Kind.Length == 0)
        {
            Fill(Context, Line, Column, LineStart, DiagramOpeners, "Diagram");
            return Context;
        }

        if (IsDirectionValue(Line, Column, out int ValueStart, out string ValuePrefix))
        {
            Context.TokenKind = CompletionTokenKind.Direction;
            Context.Prefix = ValuePrefix;
            Context.ReplaceStart = LineStart + ValueStart;
            Context.ReplaceEnd = Caret;
            AddMatching(Context, Directions, ValuePrefix);
            return Context;
        }

        if (TryLinkToken(Kind, Line, Column, out int LinkStart, out string LinkPrefix, out StatementWord[] Links))
        {
            Context.TokenKind = CompletionTokenKind.RelationshipKind;
            Context.Prefix = LinkPrefix;
            Context.ReplaceStart = LineStart + LinkStart;
            Context.ReplaceEnd = Caret;
            AddMatching(Context, Links, LinkPrefix);
            return Context;
        }

        Fill(Context, Line, Column, LineStart, WordsFor(Kind), "Statement");
        return Context;
    }

    /// <summary>Parameter Info when the line has a known shape.</summary>
    public ParameterHelp? BuildParameter(string Source, int CaretIndex)
    {
        string Text = Source ?? string.Empty;
        int Caret = Clamp(Text, CaretIndex);
        GetLine(Text, Caret, out int LineStart, out int LineEnd);
        string Line = Text.Substring(LineStart, LineEnd - LineStart);
        int Column = Caret - LineStart;
        string Kind = DetectKind(Text, LineStart);
        if (Kind.Length == 0)
        {
            return null;
        }

        if (Regex.IsMatch(Line, @"^\s*direction\b", RegexOptions.IgnoreCase) && Column >= IndexOfWordEnd(Line, "direction"))
        {
            return Make("direction <TB|TD|BT|LR|RL>", "<TB|TD|BT|LR|RL>", "direction", "TB and TD run top to bottom. BT runs bottom to top. LR runs left to right. RL runs right to left.");
        }

        if (string.Equals(Kind, "flowchart", StringComparison.Ordinal))
        {
            if (Regex.IsMatch(Line, @"^\s*subgraph\b", RegexOptions.IgnoreCase))
            {
                return Make("subgraph <id> [title]", "<id>", "subgraph", "An id, then an optional title in brackets. Close the group with end.");
            }

            if (Regex.IsMatch(Line, @"^\s*click\b", RegexOptions.IgnoreCase))
            {
                return Make("click <id> \"<url>\"", "<id>", "click", "The node id, then a quoted address or call <callback>().");
            }
        }

        if (string.Equals(Kind, "sequence", StringComparison.Ordinal))
        {
            if (Regex.IsMatch(Line, @"^\s*participant\b", RegexOptions.IgnoreCase))
            {
                return Make("participant <id> as <label>", "<id>", "participant", "The id is used in messages. as sets the displayed name.");
            }

            if (Regex.IsMatch(Line, @"^\s*note\b", RegexOptions.IgnoreCase))
            {
                return Make("note <left|right|over> of <id> : <text>", "<left|right|over>", "note", "left of, right of, or over a participant.");
            }
        }

        if (string.Equals(Kind, "class", StringComparison.Ordinal) && Regex.IsMatch(Line, @"^\s*class\b", RegexOptions.IgnoreCase))
        {
            return Make("class <name>", "<name>", "class", "A class name. Members can follow inside braces.");
        }

        if (string.Equals(Kind, "gantt", StringComparison.Ordinal) && Regex.IsMatch(Line, @"^\s*dateFormat\b", RegexOptions.IgnoreCase))
        {
            return Make("dateFormat <format>", "<format>", "dateFormat", "A date pattern such as YYYY-MM-DD.");
        }

        if (CaretIsInsideParentheses(Line, Column))
        {
            return Make(Line.Trim() + "  (arguments)", "(arguments)", "arguments", "The caret is inside parentheses on this line.");
        }

        return null;
    }

    /// <summary>Quick Info for the word at the caret.</summary>
    public string? BuildQuickInfo(string Source, int CaretIndex)
    {
        string Text = Source ?? string.Empty;
        int Caret = Clamp(Text, CaretIndex);
        GetLine(Text, Caret, out int LineStart, out int LineEnd);
        string Line = Text.Substring(LineStart, LineEnd - LineStart);
        int Column = Caret - LineStart;
        if (!TryTokenAt(Line, Column, out _, out _, out string Token))
        {
            return null;
        }

        string Kind = DetectKind(Text, LineStart);
        foreach (StatementWord Word in WordsFor(Kind.Length == 0 ? "*" : Kind))
        {
            string Name = Word.Insert.Trim();
            if (string.Equals(Name, Token, StringComparison.OrdinalIgnoreCase))
            {
                return Name + "\r\n\r\n" + Word.Documentation;
            }
        }

        foreach (StatementWord Word in Directions)
        {
            if (string.Equals(Word.Insert, Token, StringComparison.OrdinalIgnoreCase))
            {
                return Word.Insert + "\r\n\r\n" + Word.Documentation;
            }
        }

        string? Declared = FindDeclaration(Text, Kind, Token);
        if (Declared != null)
        {
            return Token + "\r\n\r\nDeclared in this diagram:\r\n" + Declared;
        }

        return null;
    }

    private static void Fill(CompletionContext Context, string Line, int Column, int LineStart, StatementWord[] Words, string DetailKind)
    {
        if (!TryTokenAt(Line, Math.Max(0, Column - 1), out int TokenStart, out int TokenEnd, out string Token) || Column < TokenStart || Column > TokenEnd)
        {
            if (Column > 0 && !char.IsWhiteSpace(Line[Column - 1]))
            {
                return;
            }

            if (Line.Substring(0, Column).Trim().Length > 0)
            {
                return;
            }

            Token = string.Empty;
            TokenStart = Column;
            TokenEnd = Column;
        }

        if (TokenStart > 0 && !char.IsWhiteSpace(Line[TokenStart - 1]))
        {
            return;
        }

        Context.TokenKind = CompletionTokenKind.Snippet;
        Context.Prefix = Token;
        Context.ReplaceStart = LineStart + TokenStart;
        Context.ReplaceEnd = LineStart + TokenEnd;
        AddMatching(Context, Words, Token);
        if (Context.Items.Count == 0)
        {
            Context.StatusMessage = "No " + DetailKind.ToLowerInvariant() + " suggestions.";
        }
    }

    private static void AddMatching(CompletionContext Context, StatementWord[] Words, string Prefix)
    {
        foreach (StatementWord Word in Words)
        {
            string Name = Word.Insert.Trim();
            if (!Name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Context.Items.Add(new CompletionItem(Word.Insert, Name, Word.Detail, Word.Documentation));
        }

        Context.Items.Sort(CompareItems);
    }

    private static int CompareItems(CompletionItem Left, CompletionItem Right)
    {
        return string.Compare(Left.DisplayText, Right.DisplayText, StringComparison.OrdinalIgnoreCase);
    }

    private static StatementWord[] WordsFor(string Kind)
    {
        if (Words.TryGetValue(Kind, out StatementWord[]? Found))
        {
            return Found;
        }

        return Array.Empty<StatementWord>();
    }

    private static bool IsDirectionValue(string Line, int Column, out int ValueStart, out string Prefix)
    {
        ValueStart = 0;
        Prefix = string.Empty;
        Match Match = Regex.Match(Line, @"^\s*direction\s+(\S*)", RegexOptions.IgnoreCase);
        if (!Match.Success)
        {
            return false;
        }

        ValueStart = Match.Groups[1].Index;
        int ValueEnd = ValueStart + Match.Groups[1].Length;
        if (Column < ValueStart || Column > ValueEnd)
        {
            return false;
        }

        Prefix = Line.Substring(ValueStart, Column - ValueStart);
        return true;
    }

    private static bool TryLinkToken(string Kind, string Line, int Column, out int LinkStart, out string Prefix, out StatementWord[] Links)
    {
        LinkStart = 0;
        Prefix = string.Empty;
        Links = Array.Empty<StatementWord>();
        StatementWord[]? Found = null;
        if (string.Equals(Kind, "flowchart", StringComparison.Ordinal))
        {
            Found = FlowLinks();
        }
        else if (string.Equals(Kind, "sequence", StringComparison.Ordinal))
        {
            Found = SequenceLinks();
        }
        else if (string.Equals(Kind, "class", StringComparison.Ordinal))
        {
            Found = ClassLinks();
        }
        else if (string.Equals(Kind, "state", StringComparison.Ordinal))
        {
            Found = StateLinks();
        }
        else if (string.Equals(Kind, "er", StringComparison.Ordinal))
        {
            Found = ErLinks();
        }

        if (Found == null)
        {
            return false;
        }

        int Index = Column;
        while (Index > 0 && IsLinkChar(Line[Index - 1]))
        {
            Index--;
        }

        if (Index == Column)
        {
            return false;
        }

        if (Index > 0 && char.IsLetterOrDigit(Line[Index - 1]))
        {
            LinkStart = Index;
            Prefix = Line.Substring(Index, Column - Index);
            Links = Found;
            return true;
        }

        return false;
    }

    private static bool IsLinkChar(char Value)
    {
        return Value == '-' || Value == '.' || Value == '=' || Value == '>' || Value == '<' || Value == 'o' || Value == 'x' || Value == '|' || Value == '{' || Value == '}' || Value == '*';
    }

    private static string DetectKind(string Source, int BeforeIndex)
    {
        int End = Math.Max(0, Math.Min(BeforeIndex, Source.Length));
        string Head = Source.Substring(0, End);
        MatchCollection Matches = HeaderRegex.Matches(Head);
        if (Matches.Count == 0)
        {
            Match OnLine = HeaderRegex.Match(Source.Substring(End));
            if (OnLine.Success && OnLine.Index == 0)
            {
                return MapKind(OnLine.Groups[1].Value);
            }

            return string.Empty;
        }

        return MapKind(Matches[Matches.Count - 1].Groups[1].Value);
    }

    private static string MapKind(string Header)
    {
        if (Header.StartsWith("enterprise", StringComparison.OrdinalIgnoreCase))
        {
            return "enterprise";
        }

        if (Header.Equals("flowchart", StringComparison.OrdinalIgnoreCase) || Header.Equals("graph", StringComparison.OrdinalIgnoreCase))
        {
            return "flowchart";
        }

        if (Header.Equals("sequenceDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "sequence";
        }

        if (Header.Equals("classDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "class";
        }

        if (Header.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "state";
        }

        if (Header.Equals("erDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "er";
        }

        if (Header.Equals("journey", StringComparison.OrdinalIgnoreCase))
        {
            return "journey";
        }

        if (Header.Equals("gantt", StringComparison.OrdinalIgnoreCase))
        {
            return "gantt";
        }

        if (Header.Equals("pie", StringComparison.OrdinalIgnoreCase))
        {
            return "pie";
        }

        if (Header.Equals("mindmap", StringComparison.OrdinalIgnoreCase))
        {
            return "mindmap";
        }

        if (Header.Equals("timeline", StringComparison.OrdinalIgnoreCase))
        {
            return "timeline";
        }

        if (Header.StartsWith("sankey", StringComparison.OrdinalIgnoreCase))
        {
            return "sankey";
        }

        if (Header.StartsWith("architecture", StringComparison.OrdinalIgnoreCase))
        {
            return "architecture";
        }

        return string.Empty;
    }

    private static string? FindDeclaration(string Source, string Kind, string Token)
    {
        string[] Lines = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (int Index = 0; Index < Lines.Length; Index++)
        {
            string Line = Lines[Index].Trim();
            if (Line.Length == 0 || Line.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (LineDeclares(Kind, Line, Token))
            {
                return Line;
            }
        }

        return null;
    }

    private static bool LineDeclares(string Kind, string Line, string Token)
    {
        if (Regex.IsMatch(Line, @"(^|\s)" + Regex.Escape(Token) + @"(\s|\[|\(|\{|$)", RegexOptions.IgnoreCase))
        {
            if (string.Equals(Kind, "sequence", StringComparison.Ordinal))
            {
                return Line.StartsWith("participant ", StringComparison.OrdinalIgnoreCase)
                    || Line.StartsWith("actor ", StringComparison.OrdinalIgnoreCase)
                    || Line.StartsWith(Token, StringComparison.Ordinal);
            }

            if (string.Equals(Kind, "class", StringComparison.Ordinal))
            {
                return Line.StartsWith("class " + Token, StringComparison.OrdinalIgnoreCase) || Line.StartsWith(Token, StringComparison.Ordinal);
            }

            if (string.Equals(Kind, "state", StringComparison.Ordinal))
            {
                return Line.StartsWith("state " + Token, StringComparison.OrdinalIgnoreCase) || Line.StartsWith(Token, StringComparison.Ordinal);
            }

            if (string.Equals(Kind, "architecture", StringComparison.Ordinal))
            {
                return Line.StartsWith("service " + Token, StringComparison.OrdinalIgnoreCase)
                    || Line.StartsWith("group " + Token, StringComparison.OrdinalIgnoreCase)
                    || Line.StartsWith("junction " + Token, StringComparison.OrdinalIgnoreCase);
            }

            return !HeaderRegex.IsMatch(Line);
        }

        return false;
    }

    private static bool TryTokenAt(string Line, int Column, out int Start, out int End, out string Token)
    {
        Start = 0;
        End = 0;
        Token = string.Empty;
        if (Line.Length == 0)
        {
            return false;
        }

        int Index = Math.Max(0, Math.Min(Column, Line.Length - 1));
        if (!IsWordChar(Line[Index]))
        {
            return false;
        }

        Start = Index;
        while (Start > 0 && IsWordChar(Line[Start - 1]))
        {
            Start--;
        }

        End = Index + 1;
        while (End < Line.Length && IsWordChar(Line[End]))
        {
            End++;
        }

        Token = Line.Substring(Start, End - Start);
        return Token.Length > 0;
    }

    private static bool IsWordChar(char Value)
    {
        return char.IsLetterOrDigit(Value) || Value == '_' || Value == '-';
    }

    private static bool CaretIsInsideParentheses(string Line, int Column)
    {
        int Depth = 0;
        int End = Math.Min(Column, Line.Length);
        for (int Index = 0; Index < End; Index++)
        {
            if (Line[Index] == '(')
            {
                Depth++;
            }
            else if (Line[Index] == ')' && Depth > 0)
            {
                Depth--;
            }
        }

        return Depth > 0;
    }

    private static int IndexOfWordEnd(string Line, string Word)
    {
        Match Match = Regex.Match(Line, @"^\s*" + Word + @"\b", RegexOptions.IgnoreCase);
        if (!Match.Success)
        {
            return Line.Length;
        }

        return Match.Length;
    }

    private static ParameterHelp Make(string Signature, string BoldText, string Heading, string Body)
    {
        int BoldStart = Signature.IndexOf(BoldText, StringComparison.Ordinal);
        ParameterHelp Help = new ParameterHelp();
        Help.Signature = Signature;
        Help.BoldStart = Math.Max(0, BoldStart);
        Help.BoldLength = BoldStart < 0 ? 0 : BoldText.Length;
        Help.Heading = Heading;
        Help.Body = Body;
        return Help;
    }

    private static int Clamp(string Text, int CaretIndex)
    {
        if (CaretIndex < 0)
        {
            return 0;
        }

        if (CaretIndex > Text.Length)
        {
            return Text.Length;
        }

        return CaretIndex;
    }

    private static void GetLine(string Source, int Caret, out int LineStart, out int LineEnd)
    {
        LineStart = Caret;
        while (LineStart > 0 && Source[LineStart - 1] != '\n')
        {
            LineStart--;
        }

        LineEnd = Caret;
        while (LineEnd < Source.Length && Source[LineEnd] != '\n' && Source[LineEnd] != '\r')
        {
            LineEnd++;
        }
    }

    private static Dictionary<string, StatementWord[]> BuildWords()
    {
        Dictionary<string, StatementWord[]> Result = new Dictionary<string, StatementWord[]>(StringComparer.Ordinal);
        Result["flowchart"] = new StatementWord[]
        {
            new StatementWord("subgraph ", "Statement", "Starts a group of nodes. Close it with end."),
            new StatementWord("end", "Statement", "Closes the current subgraph."),
            new StatementWord("direction ", "Statement", "Sets TB, TD, BT, LR, or RL for the current group."),
            new StatementWord("classDef ", "Statement", "Defines a style class."),
            new StatementWord("class ", "Statement", "Applies a class to node ids."),
            new StatementWord("style ", "Statement", "Sets a style on one node."),
            new StatementWord("linkStyle ", "Statement", "Sets a style on a link by index."),
            new StatementWord("click ", "Statement", "Opens an address or calls a function when a node is clicked."),
            new StatementWord("accTitle ", "Statement", "Accessible title."),
            new StatementWord("accDescr ", "Statement", "Accessible description.")
        };
        Result["sequence"] = new StatementWord[]
        {
            new StatementWord("participant ", "Statement", "Declares a participant."),
            new StatementWord("actor ", "Statement", "Declares a participant drawn as an actor."),
            new StatementWord("box ", "Statement", "Groups the following participants until end."),
            new StatementWord("end", "Statement", "Closes a box, loop, alt, opt, par, or critical."),
            new StatementWord("activate ", "Statement", "Starts an activation on a participant."),
            new StatementWord("deactivate ", "Statement", "Ends an activation."),
            new StatementWord("note ", "Statement", "A note left of, right of, or over a participant."),
            new StatementWord("loop ", "Statement", "Repeats until end."),
            new StatementWord("alt ", "Statement", "A choice. else adds a branch. end closes it."),
            new StatementWord("else ", "Statement", "Another branch of alt or par."),
            new StatementWord("opt ", "Statement", "An optional block until end."),
            new StatementWord("par ", "Statement", "Parallel branches until end."),
            new StatementWord("and ", "Statement", "Another parallel branch."),
            new StatementWord("critical ", "Statement", "A critical block until end."),
            new StatementWord("break ", "Statement", "A break block until end."),
            new StatementWord("rect ", "Statement", "A coloured rectangle around the following messages."),
            new StatementWord("autonumber", "Statement", "Numbers the messages."),
            new StatementWord("accTitle ", "Statement", "Accessible title."),
            new StatementWord("accDescr ", "Statement", "Accessible description.")
        };
        Result["class"] = new StatementWord[]
        {
            new StatementWord("class ", "Statement", "Declares a class."),
            new StatementWord("namespace ", "Statement", "Groups classes until end."),
            new StatementWord("note ", "Statement", "A note for a class."),
            new StatementWord("direction ", "Statement", "Sets TB, TD, BT, LR, or RL."),
            new StatementWord("accTitle ", "Statement", "Accessible title."),
            new StatementWord("accDescr ", "Statement", "Accessible description.")
        };
        Result["state"] = new StatementWord[]
        {
            new StatementWord("state ", "Statement", "Declares a state."),
            new StatementWord("[*]", "Statement", "The start or end state."),
            new StatementWord("note ", "Statement", "A note on a state."),
            new StatementWord("direction ", "Statement", "Sets TB, TD, BT, LR, or RL."),
            new StatementWord("accTitle ", "Statement", "Accessible title."),
            new StatementWord("accDescr ", "Statement", "Accessible description.")
        };
        Result["er"] = new StatementWord[]
        {
            new StatementWord("PK", "Attribute", "Primary key."),
            new StatementWord("FK", "Attribute", "Foreign key."),
            new StatementWord("UK", "Attribute", "Unique key."),
            new StatementWord("accTitle ", "Statement", "Accessible title."),
            new StatementWord("accDescr ", "Statement", "Accessible description.")
        };
        Result["journey"] = new StatementWord[]
        {
            new StatementWord("title ", "Statement", "Title of the journey."),
            new StatementWord("section ", "Statement", "A group of scored tasks.")
        };
        Result["gantt"] = new StatementWord[]
        {
            new StatementWord("title ", "Statement", "Title of the chart."),
            new StatementWord("dateFormat ", "Statement", "How dates are written, for example YYYY-MM-DD."),
            new StatementWord("axisFormat ", "Statement", "How dates are drawn on the axis."),
            new StatementWord("section ", "Statement", "A group of tasks."),
            new StatementWord("excludes ", "Statement", "Days that are not working days."),
            new StatementWord("todayMarker ", "Statement", "How today is marked.")
        };
        Result["pie"] = new StatementWord[]
        {
            new StatementWord("title ", "Statement", "Title of the pie."),
            new StatementWord("showData", "Statement", "Prints the values beside the slices.")
        };
        Result["mindmap"] = new StatementWord[]
        {
            new StatementWord("accTitle ", "Statement", "Accessible title."),
            new StatementWord("accDescr ", "Statement", "Accessible description.")
        };
        Result["timeline"] = new StatementWord[]
        {
            new StatementWord("title ", "Statement", "Title of the timeline."),
            new StatementWord("section ", "Statement", "A group of events.")
        };
        Result["sankey"] = new StatementWord[]
        {
            new StatementWord("source,target,value", "Row", "Each following row is a source, a target, and a number.")
        };
        Result["architecture"] = new StatementWord[]
        {
            new StatementWord("group ", "Statement", "A named group. Other items can sit in it."),
            new StatementWord("service ", "Statement", "A service, with an icon and a label."),
            new StatementWord("junction ", "Statement", "A junction between edges.")
        };
        Result["*"] = Array.Empty<StatementWord>();
        return Result;
    }

    private static StatementWord[] FlowLinks()
    {
        return new StatementWord[]
        {
            new StatementWord("--> ", "Link", "Arrow to the next node."),
            new StatementWord("--- ", "Link", "Line with no arrow."),
            new StatementWord("-.-> ", "Link", "Dotted arrow."),
            new StatementWord("==> ", "Link", "Thick arrow."),
            new StatementWord("--o ", "Link", "Line ending in a circle."),
            new StatementWord("--x ", "Link", "Line ending in a cross.")
        };
    }

    private static StatementWord[] SequenceLinks()
    {
        return new StatementWord[]
        {
            new StatementWord("->>", "Message", "Solid arrow."),
            new StatementWord("-->>", "Message", "Dotted arrow."),
            new StatementWord("->", "Message", "Solid line without an arrow head."),
            new StatementWord("-->", "Message", "Dotted line without an arrow head."),
            new StatementWord("-x", "Message", "Solid line with a cross."),
            new StatementWord("--x", "Message", "Dotted line with a cross."),
            new StatementWord("-)", "Message", "Async message.")
        };
    }

    private static StatementWord[] ClassLinks()
    {
        return new StatementWord[]
        {
            new StatementWord("<|--", "Relation", "Inheritance."),
            new StatementWord("*--", "Relation", "Composition."),
            new StatementWord("o--", "Relation", "Aggregation."),
            new StatementWord("-->", "Relation", "Association."),
            new StatementWord("<..", "Relation", "Dashed inheritance."),
            new StatementWord("..>", "Relation", "Dependency.")
        };
    }

    private static StatementWord[] StateLinks()
    {
        return new StatementWord[]
        {
            new StatementWord("-->", "Transition", "Transition to the next state.")
        };
    }

    private static StatementWord[] ErLinks()
    {
        return new StatementWord[]
        {
            new StatementWord("||--o{", "Relationship", "Exactly one to zero or many."),
            new StatementWord("||--|{", "Relationship", "Exactly one to one or many."),
            new StatementWord("}o--o{", "Relationship", "Zero or many to zero or many."),
            new StatementWord("|o--o{", "Relationship", "Zero or one to zero or many.")
        };
    }

    private sealed class StatementWord
    {
        private readonly string _Insert;

        private readonly string _Detail;

        private readonly string _Documentation;

        public StatementWord(string Insert, string Detail, string Documentation)
        {
            _Insert = Insert;
            _Detail = Detail;
            _Documentation = Documentation;
        }

        public string Insert
        {
            get
            {
                return _Insert;
            }
        }

        public string Detail
        {
            get
            {
                return _Detail;
            }
        }

        public string Documentation
        {
            get
            {
                return _Documentation;
            }
        }
    }
}
