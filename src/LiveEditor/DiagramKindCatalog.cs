using System;
using System.Collections.Generic;

namespace LiveEditor;

/// <summary>
/// One toolbar button for a diagram type.
/// </summary>
internal sealed class DiagramToolbarItem
{
    /// <summary>
    /// Creates a toolbar button.
    /// </summary>
    /// <param name="Tab">Tab caption.</param>
    /// <param name="Caption">Button caption.</param>
    /// <param name="Tip">Short description.</param>
    /// <param name="Snippet">Text to insert. <c>{id}</c> and <c>{label}</c> are filled by the prompt.</param>
    /// <param name="AsksForName">When true, the button asks for a label and an identifier.</param>
    public DiagramToolbarItem(string Tab, string Caption, string Tip, string Snippet, bool AsksForName)
    {
        _Tab = Tab ?? string.Empty;
        _Caption = Caption ?? string.Empty;
        _Tip = Tip ?? string.Empty;
        _Snippet = Snippet ?? string.Empty;
        _AsksForName = AsksForName;
    }

    private readonly string _Tab;

    private readonly string _Caption;

    private readonly string _Tip;

    private readonly string _Snippet;

    private readonly bool _AsksForName;

    /// <summary>Gets the tab caption.</summary>
    public string Tab
    {
        get
        {
            return _Tab;
        }
    }

    /// <summary>Gets the button caption.</summary>
    public string Caption
    {
        get
        {
            return _Caption;
        }
    }

    /// <summary>Gets the short description.</summary>
    public string Tip
    {
        get
        {
            return _Tip;
        }
    }

    /// <summary>Gets the text to insert.</summary>
    public string Snippet
    {
        get
        {
            return _Snippet;
        }
    }

    /// <summary>Gets whether the button asks for a label and an identifier.</summary>
    public bool AsksForName
    {
        get
        {
            return _AsksForName;
        }
    }
}

/// <summary>
/// A Mermaid diagram the New command can start.
/// </summary>
internal sealed class DiagramKindDefinition
{
    /// <summary>
    /// Creates a diagram kind.
    /// </summary>
    /// <param name="Id">Stable id used by the toolbar.</param>
    /// <param name="Group">Tab on the New dialog.</param>
    /// <param name="Title">Name shown on the button.</param>
    /// <param name="Starter">Source written into a new file.</param>
    /// <param name="Description">Sentence shown under the button.</param>
    /// <param name="Toolbar">Buttons for this kind. Empty when the kind has its own catalogue.</param>
    public DiagramKindDefinition(string Id, string Group, string Title, string Starter, string Description, DiagramToolbarItem[] Toolbar)
    {
        _Id = Id ?? string.Empty;
        _Group = Group ?? string.Empty;
        _Title = Title ?? string.Empty;
        _Starter = Starter ?? string.Empty;
        _Description = Description ?? string.Empty;
        _Toolbar = Toolbar ?? Array.Empty<DiagramToolbarItem>();
    }

    private readonly string _Id;

    private readonly string _Group;

    private readonly string _Title;

    private readonly string _Starter;

    private readonly string _Description;

    private readonly DiagramToolbarItem[] _Toolbar;

    /// <summary>Gets the stable id.</summary>
    public string Id
    {
        get
        {
            return _Id;
        }
    }

    /// <summary>Gets the New-dialog tab.</summary>
    public string Group
    {
        get
        {
            return _Group;
        }
    }

    /// <summary>Gets the button name.</summary>
    public string Title
    {
        get
        {
            return _Title;
        }
    }

    /// <summary>Gets the source for a new file.</summary>
    public string Starter
    {
        get
        {
            return _Starter;
        }
    }

    /// <summary>Gets the sentence under the button.</summary>
    public string Description
    {
        get
        {
            return _Description;
        }
    }

    /// <summary>Gets the toolbar buttons.</summary>
    public DiagramToolbarItem[] Toolbar
    {
        get
        {
            return _Toolbar;
        }
    }
}

/// <summary>
/// Diagram types LiveEditor can start, and the header each source uses.
/// </summary>
internal static class DiagramKindCatalog
{
    private static readonly Dictionary<string, string> TITLES = BuildTitles();

    /// <summary>Gets the diagram types that have a toolbar.</summary>
    public static IReadOnlyList<DiagramKindDefinition> Supported { get; } = BuildSupported();

    /// <summary>
    /// Reads the diagram type from the first real line.
    /// </summary>
    /// <param name="Source">Editor text.</param>
    /// <returns>A kind id, or empty when the file has no diagram header.</returns>
    public static string Detect(string Source)
    {
        if (string.IsNullOrEmpty(Source))
        {
            return string.Empty;
        }

        string Text = Source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] Lines = Text.Split('\n');
        for (int Index = 0; Index < Lines.Length; Index++)
        {
            string Line = Lines[Index].Trim();
            if (Line.Length == 0 || Line.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            return MapHeader(Line);
        }

        return string.Empty;
    }

    /// <summary>
    /// Finds a supported kind by id.
    /// </summary>
    /// <param name="KindId">Kind id from <see cref="Detect"/>.</param>
    /// <returns>The kind, or null when that type has no toolbar.</returns>
    public static DiagramKindDefinition? Find(string KindId)
    {
        if (string.IsNullOrEmpty(KindId))
        {
            return null;
        }

        for (int Index = 0; Index < Supported.Count; Index++)
        {
            if (string.Equals(Supported[Index].Id, KindId, StringComparison.OrdinalIgnoreCase))
            {
                return Supported[Index];
            }
        }

        return null;
    }

    /// <summary>
    /// Title for a detected kind, including kinds whose toolbar is still commented out.
    /// </summary>
    /// <param name="KindId">Kind id.</param>
    /// <returns>A display name.</returns>
    public static string TitleFor(string KindId)
    {
        if (string.IsNullOrEmpty(KindId))
        {
            return "Diagram";
        }

        if (TITLES.TryGetValue(KindId, out string? Title))
        {
            return Title;
        }

        return KindId;
    }

    private static string MapHeader(string Line)
    {
        if (Line.StartsWith("enterprise", StringComparison.OrdinalIgnoreCase))
        {
            return "enterprise";
        }

        if (Line.StartsWith("flowchart", StringComparison.OrdinalIgnoreCase) || Line.StartsWith("graph", StringComparison.OrdinalIgnoreCase))
        {
            return "flowchart";
        }

        if (Line.StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "sequence";
        }

        if (Line.StartsWith("classDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "class";
        }

        if (Line.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "state";
        }

        if (Line.StartsWith("erDiagram", StringComparison.OrdinalIgnoreCase))
        {
            return "er";
        }

        if (Line.StartsWith("gantt", StringComparison.OrdinalIgnoreCase))
        {
            return "gantt";
        }

        if (Line.StartsWith("pie", StringComparison.OrdinalIgnoreCase))
        {
            return "pie";
        }

        if (Line.StartsWith("gitGraph", StringComparison.OrdinalIgnoreCase))
        {
            return "gitGraph";
        }

        if (Line.StartsWith("mindmap", StringComparison.OrdinalIgnoreCase))
        {
            return "mindmap";
        }

        if (Line.StartsWith("timeline", StringComparison.OrdinalIgnoreCase))
        {
            return "timeline";
        }

        if (Line.StartsWith("journey", StringComparison.OrdinalIgnoreCase))
        {
            return "journey";
        }

        if (Line.StartsWith("quadrantChart", StringComparison.OrdinalIgnoreCase))
        {
            return "quadrantChart";
        }

        if (Line.StartsWith("xychart", StringComparison.OrdinalIgnoreCase))
        {
            return "xychart";
        }

        if (Line.StartsWith("requirement", StringComparison.OrdinalIgnoreCase))
        {
            return "requirement";
        }

        if (Line.StartsWith("architecture", StringComparison.OrdinalIgnoreCase))
        {
            return "architecture";
        }

        if (Line.StartsWith("kanban", StringComparison.OrdinalIgnoreCase))
        {
            return "kanban";
        }

        if (Line.StartsWith("sankey", StringComparison.OrdinalIgnoreCase))
        {
            return "sankey";
        }

        if (Line.StartsWith("block", StringComparison.OrdinalIgnoreCase))
        {
            return "block";
        }

        if (Line.StartsWith("packet", StringComparison.OrdinalIgnoreCase))
        {
            return "packet";
        }

        if (Line.StartsWith("radar", StringComparison.OrdinalIgnoreCase))
        {
            return "radar";
        }

        if (Line.StartsWith("treemap", StringComparison.OrdinalIgnoreCase))
        {
            return "treemap";
        }

        if (Line.StartsWith("C4", StringComparison.OrdinalIgnoreCase))
        {
            return "c4";
        }

        return string.Empty;
    }

    private static Dictionary<string, string> BuildTitles()
    {
        Dictionary<string, string> Titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Titles["enterprise"] = "Enterprise";
        Titles["flowchart"] = "Flowchart";
        Titles["sequence"] = "Sequence";
        Titles["class"] = "Class";
        Titles["state"] = "State";
        Titles["er"] = "Entity relationship";
        Titles["gantt"] = "Gantt";
        Titles["pie"] = "Pie";
        Titles["gitGraph"] = "Git graph";
        Titles["mindmap"] = "Mindmap";
        Titles["timeline"] = "Timeline";
        Titles["journey"] = "Journey";
        Titles["quadrantChart"] = "Quadrant chart";
        Titles["xychart"] = "XY chart";
        Titles["requirement"] = "Requirement";
        Titles["architecture"] = "Architecture";
        Titles["kanban"] = "Kanban";
        Titles["sankey"] = "Sankey";
        Titles["block"] = "Block";
        Titles["packet"] = "Packet";
        Titles["radar"] = "Radar";
        Titles["treemap"] = "Treemap";
        Titles["c4"] = "C4";
        return Titles;
    }

    private static DiagramToolbarItem Item(string Tab, string Caption, string Tip, string Snippet, bool AsksForName)
    {
        return new DiagramToolbarItem(Tab, Caption, Tip, Snippet, AsksForName);
    }

    private static List<DiagramKindDefinition> BuildSupported()
    {
        List<DiagramKindDefinition> Kinds = new List<DiagramKindDefinition>();
        Kinds.Add(new DiagramKindDefinition(
            "enterprise",
            "Model",
            "Enterprise",
            "enterprise\r\n",
            "Archimate-style enterprise diagram. The toolbar lists every element type.",
            Array.Empty<DiagramToolbarItem>()));
        Kinds.Add(new DiagramKindDefinition(
            "flowchart",
            "Flow",
            "Flowchart",
            "flowchart TD\r\n",
            "Flowchart, top to bottom. Change TD to LR, RL, or BT when the flow runs another way.",
            new DiagramToolbarItem[]
            {
                Item("Shapes", "Rectangle", "A process step.", "{id}[\"{label}\"]", true),
                Item("Shapes", "Rounded", "A start, end, or softer step.", "{id}(\"{label}\")", true),
                Item("Shapes", "Stadium", "A terminal step.", "{id}([\"{label}\"])", true),
                Item("Shapes", "Subroutine", "A step that calls another process.", "{id}[[\"{label}\"]]", true),
                Item("Shapes", "Cylinder", "A database or store.", "{id}[(\"{label}\")]", true),
                Item("Shapes", "Circle", "A small connector.", "{id}((\"{label}\"))", true),
                Item("Shapes", "Diamond", "A decision.", "{id}{\"{label}\"}", true),
                Item("Shapes", "Hexagon", "A preparation step.", "{id}{{\"{label}\"}}", true),
                Item("Links", "Arrow", "Arrow to the next node.", "Start --> Next", false),
                Item("Links", "Open", "Line with no arrow.", "Start --- Next", false),
                Item("Links", "Dotted", "Dotted arrow.", "Start -.-> Next", false),
                Item("Links", "Thick", "Thick arrow.", "Start ==> Next", false),
                Item("Statements", "Subgraph", "Starts a group. Close it with end.", "subgraph group1 [Group]", false),
                Item("Statements", "End", "Closes the current subgraph.", "end", false),
                Item("Statements", "Direction", "TB, TD, BT, LR, or RL for the current group.", "direction LR", false)
            }));
        Kinds.Add(new DiagramKindDefinition(
            "sequence",
            "Interaction",
            "Sequence",
            "sequenceDiagram\r\n",
            "Messages between participants.",
            new DiagramToolbarItem[]
            {
                Item("Participants", "Participant", "A participant.", "participant {id} as {label}", true),
                Item("Participants", "Actor", "A participant drawn as an actor.", "actor {id} as {label}", true),
                Item("Messages", "Solid", "A solid arrow.", "Alice->>Bob: Message", false),
                Item("Messages", "Dashed", "A dashed reply.", "Bob-->>Alice: Reply", false),
                Item("Messages", "Cross", "An arrow that ends in a cross.", "Alice-xBob: Stop", false),
                Item("Blocks", "Note", "A note over a participant.", "note over Alice: Note", false),
                Item("Blocks", "Loop", "Repeats until end.", "loop Every time", false),
                Item("Blocks", "Alt", "A choice. else adds a branch.", "alt Ready", false),
                Item("Blocks", "Else", "Another branch.", "else Waiting", false),
                Item("Blocks", "Opt", "An optional block.", "opt When needed", false),
                Item("Blocks", "End", "Closes a box, loop, alt, opt, or par.", "end", false),
                Item("Blocks", "Autonumber", "Numbers the messages.", "autonumber", false)
            }));

        // These kinds are recognised, but the toolbar does not offer them yet.
        // Remove the comment markers and Add the definition when that toolbar is ready.
        //
        // Kinds.Add(new DiagramKindDefinition("class", "Structure", "Class", "classDiagram\r\n", "Classes and relations.", new DiagramToolbarItem[]
        // {
        //     Item("Types", "Class", "Declares a class.", "class {id}~{label}~ {{}}", true),
        //     Item("Types", "Namespace", "Groups classes until end.", "namespace Group", false),
        //     Item("Types", "Note", "A note for a class.", "note for Class \"Note\"", false),
        //     Item("Types", "Direction", "TB, TD, BT, LR, or RL.", "direction TB", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("state", "Structure", "State", "stateDiagram-v2\r\n", "States and transitions.", new DiagramToolbarItem[]
        // {
        //     Item("States", "State", "Declares a state.", "state \"{label}\" as {id}", true),
        //     Item("States", "Start", "The start state.", "[*] --> State1", false),
        //     Item("States", "Note", "A note on a state.", "note right of State1: Note", false),
        //     Item("States", "Direction", "TB, TD, BT, LR, or RL.", "direction LR", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("er", "Structure", "Entity relationship", "erDiagram\r\n", "Entities and relationships.", new DiagramToolbarItem[]
        // {
        //     Item("Entities", "Entity", "An entity and one attribute.", "{id} {{\r\n  string name\r\n}}", true),
        //     Item("Keys", "PK", "Primary key.", "string id PK", false),
        //     Item("Keys", "FK", "Foreign key.", "string parentId FK", false),
        //     Item("Keys", "UK", "Unique key.", "string code UK", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("gantt", "Charts", "Gantt", "gantt\r\n", "Tasks on a calendar.", new DiagramToolbarItem[]
        // {
        //     Item("Chart", "Title", "Title of the chart.", "title Plan", false),
        //     Item("Chart", "Date format", "How dates are written.", "dateFormat YYYY-MM-DD", false),
        //     Item("Chart", "Section", "A group of tasks.", "section Phase", false),
        //     Item("Chart", "Excludes", "Days that are not working days.", "excludes weekends", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("pie", "Charts", "Pie", "pie\r\n", "Slices of a whole.", new DiagramToolbarItem[]
        // {
        //     Item("Chart", "Title", "Title of the pie.", "title Share", false),
        //     Item("Chart", "Show data", "Prints the values beside the slices.", "showData", false),
        //     Item("Chart", "Slice", "One labelled slice.", "\"{label}\" : 10", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("gitGraph", "Charts", "Git graph", "gitGraph\r\n", "Commits and branches.", new DiagramToolbarItem[]
        // {
        //     Item("Git", "Commit", "A commit on the current branch.", "commit", false),
        //     Item("Git", "Branch", "Starts a branch.", "branch feature", false),
        //     Item("Git", "Checkout", "Moves to a branch.", "checkout feature", false),
        //     Item("Git", "Merge", "Merges a branch.", "merge feature", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("mindmap", "Notes", "Mindmap", "mindmap\r\n", "Indented topics.", new DiagramToolbarItem[]
        // {
        //     Item("Topics", "Root", "The root topic.", "root(({label}))", true),
        //     Item("Topics", "Child", "An indented topic.", "  {label}", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("timeline", "Notes", "Timeline", "timeline\r\n", "Events in sections.", new DiagramToolbarItem[]
        // {
        //     Item("Events", "Title", "Title of the timeline.", "title History", false),
        //     Item("Events", "Section", "A group of events.", "section Year", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("journey", "Notes", "Journey", "journey\r\n", "Tasks scored across sections.", new DiagramToolbarItem[]
        // {
        //     Item("Journey", "Title", "Title of the journey.", "title Visit", false),
        //     Item("Journey", "Section", "A group of scored tasks.", "section Arrive", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("quadrantChart", "Charts", "Quadrant chart", "quadrantChart\r\n", "Points on two axes.", new DiagramToolbarItem[]
        // {
        //     Item("Chart", "Title", "Title of the chart.", "title Priority", false),
        //     Item("Chart", "X axis", "The horizontal axis.", "x-axis Low --> High", false),
        //     Item("Chart", "Y axis", "The vertical axis.", "y-axis Low --> High", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("xychart", "Charts", "XY chart", "xychart-beta\r\n", "Bars or a line on an x axis.", new DiagramToolbarItem[]
        // {
        //     Item("Chart", "Title", "Title of the chart.", "title Results", false),
        //     Item("Chart", "X axis", "Category labels.", "x-axis [a, b, c]", false),
        //     Item("Chart", "Bar", "Bar values.", "bar [1, 2, 3]", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("requirement", "Structure", "Requirement", "requirementDiagram\r\n", "Requirements and elements.", new DiagramToolbarItem[]
        // {
        //     Item("Items", "Requirement", "A requirement.", "requirement {id} {{\r\n  id: 1\r\n  text: {label}\r\n}}", true),
        //     Item("Items", "Element", "An element that satisfies a requirement.", "element {id} {{\r\n  type: \"{label}\"\r\n}}", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("architecture", "Model", "Architecture", "architecture-beta\r\n", "Groups, services, and junctions.", new DiagramToolbarItem[]
        // {
        //     Item("Items", "Group", "A named group.", "group {id}({label})", true),
        //     Item("Items", "Service", "A service with a label.", "service {id}({label}) in group", true),
        //     Item("Items", "Junction", "A junction between edges.", "junction {id}", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("kanban", "Charts", "Kanban", "kanban\r\n", "Cards in columns.", new DiagramToolbarItem[]
        // {
        //     Item("Board", "Column", "A column.", "{label}", true),
        //     Item("Board", "Card", "A card under the current column.", "  {label}", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("sankey", "Charts", "Sankey", "sankey-beta\r\n", "Flows from source to target.", new DiagramToolbarItem[]
        // {
        //     Item("Rows", "Header", "Column names.", "source,target,value", false),
        //     Item("Rows", "Flow", "One flow.", "A,B,10", false)
        // }));
        // Kinds.Add(new DiagramKindDefinition("block", "Charts", "Block", "block-beta\r\n", "Blocks in columns.", new DiagramToolbarItem[]
        // {
        //     Item("Blocks", "Columns", "Starts a row of columns.", "columns 2", false),
        //     Item("Blocks", "Block", "One block.", "{id}[\"{label}\"]", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("packet", "Charts", "Packet", "packet-beta\r\n", "Bits in a packet.", new DiagramToolbarItem[]
        // {
        //     Item("Fields", "Field", "A field and its bit width.", "0-7: \"{label}\"", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("radar", "Charts", "Radar", "radar-beta\r\n", "Scores around a centre.", new DiagramToolbarItem[]
        // {
        //     Item("Chart", "Title", "Title of the chart.", "title Skills", false),
        //     Item("Chart", "Axis", "One spoke.", "axis {label}", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("treemap", "Charts", "Treemap", "treemap-beta\r\n", "Nested weighted areas.", new DiagramToolbarItem[]
        // {
        //     Item("Areas", "Title", "Title of the treemap.", "title Area", false),
        //     Item("Areas", "Leaf", "A weighted leaf.", "{label}: 10", true)
        // }));
        // Kinds.Add(new DiagramKindDefinition("c4", "Model", "C4", "C4Context\r\n", "A C4 context, container, component, or deployment diagram.", new DiagramToolbarItem[]
        // {
        //     Item("Context", "Person", "A person.", "Person({id}, \"{label}\")", true),
        //     Item("Context", "System", "A software system.", "System({id}, \"{label}\")", true),
        //     Item("Context", "Relationship", "A relationship.", "Rel({id}, other, \"Uses\")", false)
        // }));
        return Kinds;
    }
}
