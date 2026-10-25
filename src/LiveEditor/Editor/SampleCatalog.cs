using System;
using System.Collections.Generic;

namespace LiveEditor.Editor;

/// <summary>
/// One built-in diagram, with a short sample and a sample that uses the type's options.
/// </summary>
internal sealed class DiagramSampleGroup
{
    /// <summary>
    /// Creates a pair of samples for one diagram type.
    /// </summary>
    /// <param name="KindId">Kind id from the diagram catalogue.</param>
    /// <param name="MenuTitle">Sub-menu caption.</param>
    /// <param name="SimpleTitle">Menu caption for the short sample, ending in (Simple).</param>
    /// <param name="SimpleSource">A small diagram of this type.</param>
    /// <param name="CompleteTitle">Menu caption for the full sample, ending in (Complete).</param>
    /// <param name="FullSource">A diagram that uses the type's options.</param>
    public DiagramSampleGroup(string KindId, string MenuTitle, string SimpleTitle, string SimpleSource, string CompleteTitle, string FullSource)
    {
        _KindId = KindId ?? string.Empty;
        _MenuTitle = MenuTitle ?? string.Empty;
        _SimpleTitle = SimpleTitle ?? string.Empty;
        _SimpleSource = SimpleSource ?? string.Empty;
        _CompleteTitle = CompleteTitle ?? string.Empty;
        _FullSource = FullSource ?? string.Empty;
    }

    private readonly string _KindId;

    private readonly string _MenuTitle;

    private readonly string _SimpleTitle;

    private readonly string _SimpleSource;

    private readonly string _CompleteTitle;

    private readonly string _FullSource;

    /// <summary>Gets the catalogue kind id.</summary>
    public string KindId
    {
        get
        {
            return _KindId;
        }
    }

    /// <summary>Gets the sub-menu caption.</summary>
    public string MenuTitle
    {
        get
        {
            return _MenuTitle;
        }
    }

    /// <summary>Gets the menu caption for the short sample.</summary>
    public string SimpleTitle
    {
        get
        {
            return _SimpleTitle;
        }
    }

    /// <summary>Gets the short sample.</summary>
    public string SimpleSource
    {
        get
        {
            return _SimpleSource;
        }
    }

    /// <summary>Gets the menu caption for the full sample.</summary>
    public string CompleteTitle
    {
        get
        {
            return _CompleteTitle;
        }
    }

    /// <summary>Gets the sample that uses the type's options.</summary>
    public string FullSource
    {
        get
        {
            return _FullSource;
        }
    }
}

/// <summary>
/// Built-in Mermaid samples for the Samples menu.
/// </summary>
internal static class SampleCatalog
{
    private static readonly List<DiagramSampleGroup> _Groups = BuildGroups();

    /// <summary>Gets one pair of samples for every diagram type, including types the toolbar does not offer yet.</summary>
    public static IReadOnlyList<DiagramSampleGroup> Groups
    {
        get
        {
            return _Groups;
        }
    }

    /// <summary>
    /// Finds the sample pair for a kind id.
    /// </summary>
    /// <param name="KindId">Catalogue kind id stored on the Samples menu item.</param>
    /// <returns>The group, or null when that kind has no sample.</returns>
    public static DiagramSampleGroup? Find(string KindId)
    {
        for (int Index = 0; Index < _Groups.Count; Index++)
        {
            if (string.Equals(_Groups[Index].KindId, KindId, StringComparison.OrdinalIgnoreCase))
            {
                return _Groups[Index];
            }
        }

        return null;
    }

    private static string SampleTitle(string Description, bool Simple)
    {
        if (Simple)
        {
            return Description + " (Simple)";
        }

        return Description + " (Complete)";
    }

    private static List<DiagramSampleGroup> BuildGroups()
    {
        List<DiagramSampleGroup> Groups = new List<DiagramSampleGroup>();
        Groups.Add(new DiagramSampleGroup(
            "enterprise",
            "Enterprise",
            SampleTitle("Web shop", true),
            "enterprise\r\n"
            + "  title Web shop\r\n"
            + "  application-component webShop \"Web Shop\"\r\n"
            + "  application-service orderApi \"Order API\"\r\n"
            + "  data-object order \"Order\"\r\n"
            + "  webShop -serving-> orderApi\r\n"
            + "  orderApi -access(write)-> order\r\n",
            SampleTitle("Clauses and relationships", false),
            "enterprise\r\n"
            + "  title All options\r\n"
            + "  version 3.2\r\n"
            + "  direction TB\r\n"
            + "  accTitle: All options\r\n"
            + "  accDescr: Every enterprise clause\r\n"
            + "  classDef emphasis fill:#fff3cd,stroke:#333333\r\n"
            + "  group shop \"Shop\" {\r\n"
            + "    direction LR\r\n"
            + "    application-component webShop \"Web Shop\":::emphasis\r\n"
            + "  }\r\n"
            + "  grouping box \"Box\" in shop\r\n"
            + "  application-component part \"Part\" in shop\r\n"
            + "  application-service orderApi \"Order API\" in box\r\n"
            + "  data-object order \"Order\" below orderApi\r\n"
            + "  data-object ledger \"Ledger\" above order +1\r\n"
            + "  business-actor customer \"Customer\" left-of webShop\r\n"
            + "  application-component neighbour \"Neighbour\" right-of webShop +1\r\n"
            + "  business-process checkout \"Checkout\" push E\r\n"
            + "  business-process review \"Review\" push W\r\n"
            + "  business-process delay \"Delay\" push N\r\n"
            + "  business-process cancel \"Cancel\" push S\r\n"
            + "  application-interface contract \"Contract\"\r\n"
            + "  application-service baseApi \"Base API\"\r\n"
            + "  application-component shared \"Shared\"\r\n"
            + "  data-object audit \"Audit\"\r\n"
            + "  business-process rating \"Rating\"\r\n"
            + "  application-component partner \"Partner\"\r\n"
            + "  goal ship \"Ship on time\"\r\n"
            + "  outcome late \"Late orders\"\r\n"
            + "  driver cost \"Cost\"\r\n"
            + "  assessment risk \"Risk\"\r\n"
            + "  requirement mustStore \"Must store\"\r\n"
            + "  note margin \"A note beside the order\" below order +2\r\n"
            + "  and-junction split \"Split\" above checkout\r\n"
            + "  or-junction choice \"Choice\" below checkout +1\r\n"
            + "  style order fill:#eef\r\n"
            + "  class webShop emphasis\r\n"
            + "  customer -assignment-> checkout \"Assigned\" [leave E, travel E, enter W]\r\n"
            + "  webShop -composition-> part\r\n"
            + "  webShop -aggregation-> shared\r\n"
            + "  webShop -realization-> contract\r\n"
            + "  webShop -serving-> orderApi \"Serves\"\r\n"
            + "  orderApi -access(read)-> order\r\n"
            + "  orderApi -access(write)-> ledger\r\n"
            + "  orderApi -access(readwrite)-> audit\r\n"
            + "  orderApi -access(access)-> order\r\n"
            + "  customer -influence(+)-> ship\r\n"
            + "  customer -influence(++)-> late\r\n"
            + "  customer -influence(-)-> cost\r\n"
            + "  customer -influence(--)-> risk\r\n"
            + "  customer -influence(2)-> mustStore\r\n"
            + "  webShop -triggering-> checkout \"Starts\"\r\n"
            + "  orderApi -flow-> checkout \"Moves\"\r\n"
            + "  orderApi -specialization-> baseApi\r\n"
            + "  webShop -association-> neighbour\r\n"
            + "  webShop -association(directed)-> partner \"Directed\"\r\n"
            + "  margin --- order\r\n"
            + "  linkStyle 0 stroke:#333333\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "flowchart",
            "Flowchart",
            SampleTitle("Start to done", true),
            "flowchart TD\r\n"
            + "  Start([Start]) --> Done([Done])\r\n",
            SampleTitle("Shapes, links, and groups", false),
            "flowchart TD\r\n"
            + "  accTitle: Flow\r\n"
            + "  accDescr: Shapes, links, and groups\r\n"
            + "  direction TB\r\n"
            + "  subgraph box [Group]\r\n"
            + "    direction LR\r\n"
            + "    Start([Start]) --> Decision{Ready?}\r\n"
            + "    Decision -->|Yes| Work[Do work]\r\n"
            + "    Decision -->|No| Wait[Wait]\r\n"
            + "    Work --> Store[(Store)]\r\n"
            + "    Store --> Round(Round)\r\n"
            + "    Round --> Dot((Dot))\r\n"
            + "    Dot --> Call[[Call]]\r\n"
            + "    Call --> Prep{{Prepare}}\r\n"
            + "    Prep --> Slant[/Slant/]\r\n"
            + "    Slant --> Trap[\\Trap\\]\r\n"
            + "    Trap --> Flag>Flag]\r\n"
            + "  end\r\n"
            + "  Work -.-> Done([Done])\r\n"
            + "  Wait ==> Decision\r\n"
            + "  Start --- Round\r\n"
            + "  classDef good fill:#efe,stroke:#333\r\n"
            + "  class Done good\r\n"
            + "  style Start fill:#eef\r\n"
            + "  click Done \"https://example.com\"\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "sequence",
            "Sequence",
            SampleTitle("Request and response", true),
            "sequenceDiagram\r\n"
            + "  participant C as Client\r\n"
            + "  participant S as Service\r\n"
            + "  C->>S: Request\r\n"
            + "  S-->>C: Response\r\n",
            SampleTitle("Messages and blocks", false),
            "sequenceDiagram\r\n"
            + "  title Request\r\n"
            + "  autonumber\r\n"
            + "  participant C as Client\r\n"
            + "  actor U as User\r\n"
            + "  participant S as Service\r\n"
            + "  U->>C: Ask\r\n"
            + "  C->>+S: Request\r\n"
            + "  S-->>-C: Response\r\n"
            + "  C-xS: Cancel\r\n"
            + "  Note over C,S: Done\r\n"
            + "  loop Retry\r\n"
            + "    C->>S: Again\r\n"
            + "  end\r\n"
            + "  alt Ready\r\n"
            + "    C->>S: Go\r\n"
            + "  else Waiting\r\n"
            + "    C->>S: Hold\r\n"
            + "  end\r\n"
            + "  opt Extra\r\n"
            + "    C->>S: More\r\n"
            + "  end\r\n"
            + "  par Side\r\n"
            + "    C->>S: One\r\n"
            + "  and Other\r\n"
            + "    C->>S: Two\r\n"
            + "  end\r\n"
            + "  rect rgb(240, 240, 240)\r\n"
            + "    C->>S: Inside\r\n"
            + "  end\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "class",
            "Class",
            SampleTitle("Inheritance", true),
            "classDiagram\r\n"
            + "  class Animal\r\n"
            + "  class Duck\r\n"
            + "  Animal <|-- Duck\r\n",
            SampleTitle("Members and relations", false),
            "classDiagram\r\n"
            + "  direction LR\r\n"
            + "  class Animal {\r\n"
            + "    +String name\r\n"
            + "    +eat()\r\n"
            + "  }\r\n"
            + "  class Duck {\r\n"
            + "    +swim()\r\n"
            + "  }\r\n"
            + "  Animal <|-- Duck\r\n"
            + "  class Farm\r\n"
            + "  Farm *-- Animal : owns\r\n"
            + "  Farm o-- Duck : keeps\r\n"
            + "  Animal --> Duck : knows\r\n"
            + "  Animal ..> Duck : uses\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "state",
            "State",
            SampleTitle("Start and end", true),
            "stateDiagram-v2\r\n"
            + "  [*] --> Idle\r\n"
            + "  Idle --> [*]\r\n",
            SampleTitle("Direction and a note", false),
            "stateDiagram-v2\r\n"
            + "  direction TB\r\n"
            + "  accTitle: States\r\n"
            + "  [*] --> Idle\r\n"
            + "  Idle --> Running : start\r\n"
            + "  Running --> [*]\r\n"
            + "  note right of Idle : Waiting\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "er",
            "Entity relationship",
            SampleTitle("Customer places an order", true),
            "erDiagram\r\n"
            + "  CUSTOMER ||--o{ ORDER : places\r\n",
            SampleTitle("Keys and cardinalities", false),
            "erDiagram\r\n"
            + "  CUSTOMER ||--o{ ORDER : places\r\n"
            + "  ORDER ||--|{ LINE : contains\r\n"
            + "  CUSTOMER {\r\n"
            + "    string id PK\r\n"
            + "    string email UK\r\n"
            + "  }\r\n"
            + "  ORDER {\r\n"
            + "    string id PK\r\n"
            + "    string customerId FK\r\n"
            + "  }\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "gantt",
            "Gantt",
            SampleTitle("One task", true),
            "gantt\r\n"
            + "  title Plan\r\n"
            + "  section Phase\r\n"
            + "  Task :a1, 2026-01-01, 3d\r\n",
            SampleTitle("Dates and sections", false),
            "gantt\r\n"
            + "  title Plan\r\n"
            + "  dateFormat YYYY-MM-DD\r\n"
            + "  axisFormat %m-%d\r\n"
            + "  excludes weekends\r\n"
            + "  section Phase\r\n"
            + "  Task :a1, 2026-01-05, 3d\r\n"
            + "  Next :after a1, 2d\r\n"
            + "  section Later\r\n"
            + "  Review :crit, 2026-01-12, 1d\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "pie",
            "Pie",
            SampleTitle("Two slices", true),
            "pie\r\n"
            + "  title Share\r\n"
            + "  \"Orders\" : 40\r\n"
            + "  \"Other\" : 60\r\n",
            SampleTitle("Values beside the slices", false),
            "pie showData\r\n"
            + "  title Share\r\n"
            + "  \"Orders\" : 40\r\n"
            + "  \"Stock\" : 35\r\n"
            + "  \"Other\" : 25\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "gitGraph",
            "Git graph",
            SampleTitle("Two commits", true),
            "gitGraph\r\n"
            + "  commit\r\n"
            + "  commit\r\n",
            SampleTitle("Branch and merge", false),
            "gitGraph\r\n"
            + "  commit\r\n"
            + "  commit\r\n"
            + "  branch develop\r\n"
            + "  checkout develop\r\n"
            + "  commit\r\n"
            + "  checkout main\r\n"
            + "  merge develop\r\n"
            + "  commit\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "mindmap",
            "Mindmap",
            SampleTitle("Shop and orders", true),
            "mindmap\r\n"
            + "  root((Shop))\r\n"
            + "    Orders\r\n",
            SampleTitle("Nested topics", false),
            "mindmap\r\n"
            + "  root((Shop))\r\n"
            + "    Orders\r\n"
            + "      New\r\n"
            + "      Paid\r\n"
            + "    Stock\r\n"
            + "      Low\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "timeline",
            "Timeline",
            SampleTitle("One year", true),
            "timeline\r\n"
            + "  title History\r\n"
            + "  section 2026\r\n"
            + "    Start : Idea\r\n",
            SampleTitle("Two sections", false),
            "timeline\r\n"
            + "  title History\r\n"
            + "  section 2025\r\n"
            + "    Start : Idea\r\n"
            + "    : Sketch\r\n"
            + "  section 2026\r\n"
            + "    Build : First order\r\n"
            + "    : Ship\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "journey",
            "Journey",
            SampleTitle("Sign in", true),
            "journey\r\n"
            + "  title Visit\r\n"
            + "  section Arrive\r\n"
            + "    Sign in: 5: Customer\r\n",
            SampleTitle("Arrive and buy", false),
            "journey\r\n"
            + "  title Visit\r\n"
            + "  section Arrive\r\n"
            + "    Sign in: 5: Customer\r\n"
            + "    Browse: 3: Customer\r\n"
            + "  section Buy\r\n"
            + "    Pay: 4: Customer, Shop\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "quadrantChart",
            "Quadrant chart",
            SampleTitle("One item", true),
            "quadrantChart\r\n"
            + "  title Priority\r\n"
            + "  Item A: [0.3, 0.6]\r\n",
            SampleTitle("Axes and quadrants", false),
            "quadrantChart\r\n"
            + "  title Priority\r\n"
            + "  x-axis Low --> High\r\n"
            + "  y-axis Low --> High\r\n"
            + "  quadrant-1 Do now\r\n"
            + "  quadrant-2 Plan\r\n"
            + "  quadrant-3 Drop\r\n"
            + "  quadrant-4 Delegate\r\n"
            + "  Item A: [0.3, 0.6]\r\n"
            + "  Item B: [0.8, 0.7]\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "xychart",
            "XY chart",
            SampleTitle("Bars", true),
            "xychart-beta\r\n"
            + "  title Results\r\n"
            + "  bar [2, 5, 8]\r\n",
            SampleTitle("Bars and a line", false),
            "xychart-beta\r\n"
            + "  title Results\r\n"
            + "  x-axis [jan, feb, mar]\r\n"
            + "  y-axis \"Count\" 0 --> 10\r\n"
            + "  bar [2, 5, 8]\r\n"
            + "  line [1, 4, 7]\r\n"));
        // Requirement does not render yet, so it stays off the Samples menu.
        // Groups.Add(new DiagramSampleGroup(
        //     "requirement",
        //     "Requirement",
        //     SampleTitle("One requirement", true),
        //     "requirementDiagram\r\n"
        //     + "  requirement req1 {\r\n"
        //     + "    id: 1\r\n"
        //     + "    text: the system stores orders\r\n"
        //     + "  }\r\n",
        //     SampleTitle("Requirement and element", false),
        //     "requirementDiagram\r\n"
        //     + "  requirement req1 {\r\n"
        //     + "    id: 1\r\n"
        //     + "    text: the system stores orders\r\n"
        //     + "    risk: low\r\n"
        //     + "    verifymethod: test\r\n"
        //     + "  }\r\n"
        //     + "  element orderApi {\r\n"
        //     + "    type: service\r\n"
        //     + "  }\r\n"
        //     + "  orderApi - satisfies -> req1\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "architecture",
            "Architecture",
            SampleTitle("One service", true),
            "architecture-beta\r\n"
            + "  service api(server)[API]\r\n",
            SampleTitle("Group and services", false),
            "architecture-beta\r\n"
            + "  group shop(cloud)[Shop]\r\n"
            + "  service api(server)[API] in shop\r\n"
            + "  service db(database)[DB] in shop\r\n"
            + "  junction join in shop\r\n"
            + "  api:R --> L:db\r\n"));
        // Kanban does not render yet, so it stays off the Samples menu.
        // Groups.Add(new DiagramSampleGroup(
        //     "kanban",
        //     "Kanban",
        //     SampleTitle("One card", true),
        //     "kanban\r\n"
        //     + "  Todo\r\n"
        //     + "    [Write the brief]\r\n",
        //     SampleTitle("Three columns", false),
        //     "kanban\r\n"
        //     + "  Todo\r\n"
        //     + "    [Write the brief]\r\n"
        //     + "    [Draw the flow]\r\n"
        //     + "  Doing\r\n"
        //     + "    [Build the shop]\r\n"
        //     + "  Done\r\n"
        //     + "    [Open the till]\r\n"));
        // Sankey does not render yet, so it stays off the Samples menu.
        // Groups.Add(new DiagramSampleGroup(
        //     "sankey",
        //     "Sankey",
        //     SampleTitle("One flow", true),
        //     "sankey-beta\r\n"
        //     + "source,target,value\r\n"
        //     + "A,B,10\r\n",
        //     SampleTitle("Several flows", false),
        //     "sankey-beta\r\n"
        //     + "source,target,value\r\n"
        //     + "A,B,10\r\n"
        //     + "A,C,4\r\n"
        //     + "B,D,6\r\n"
        //     + "C,D,4\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "block",
            "Block",
            SampleTitle("One block", true),
            "block-beta\r\n"
            + "  A[\"One\"]\r\n",
            SampleTitle("Columns and a span", false),
            "block-beta\r\n"
            + "  columns 2\r\n"
            + "  A[\"One\"] B[\"Two\"]\r\n"
            + "  C[\"Three\"]:2\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "packet",
            "Packet",
            SampleTitle("One field", true),
            "packet-beta\r\n"
            + "  0-15: \"Source\"\r\n",
            SampleTitle("Three fields", false),
            "packet-beta\r\n"
            + "  0-15: \"Source\"\r\n"
            + "  16-31: \"Destination\"\r\n"
            + "  32-63: \"Payload\"\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "radar",
            "Radar",
            SampleTitle("Axes", true),
            "radar-beta\r\n"
            + "  title Skills\r\n"
            + "  axis Design, Build, Test\r\n",
            SampleTitle("Two curves", false),
            "radar-beta\r\n"
            + "  title Skills\r\n"
            + "  axis Design, Build, Test\r\n"
            + "  curve Team{3, 4, 5}\r\n"
            + "  curve Plan{2, 3, 4}\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "treemap",
            "Treemap",
            SampleTitle("One area", true),
            "treemap-beta\r\n"
            + "  \"Shop\"\r\n"
            + "    \"Orders\": 40\r\n",
            SampleTitle("Two areas", false),
            "treemap-beta\r\n"
            + "  \"Shop\"\r\n"
            + "    \"Orders\": 40\r\n"
            + "    \"Stock\": 60:::emphasis\r\n"));
        Groups.Add(new DiagramSampleGroup(
            "c4",
            "C4",
            SampleTitle("Person and system", true),
            "C4Context\r\n"
            + "  Person(customer, \"Customer\")\r\n"
            + "  System(shop, \"Web shop\")\r\n"
            + "  Rel(customer, shop, \"Orders\")\r\n",
            SampleTitle("Context and an external system", false),
            "C4Context\r\n"
            + "  title System context\r\n"
            + "  Person(customer, \"Customer\", \"Places orders\")\r\n"
            + "  System(shop, \"Web shop\", \"Takes orders\")\r\n"
            + "  System_Ext(pay, \"Payments\", \"Takes payment\")\r\n"
            + "  Rel_U(customer, shop, \"Orders\")\r\n"
            + "  Rel(shop, pay, \"Charges\", \"HTTPS\")\r\n"));
        return Groups;
    }
}
