using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Turns an element and its relationships into a short paragraph, written from that element's point of view.
/// </summary>
internal static class ElementStory
{
    /// <summary>
    /// Describes an element, including relationships that start or end on it.
    /// </summary>
    /// <param name="Document">Parsed diagram.</param>
    /// <param name="Element">The element under the pointer or selection.</param>
    /// <returns>The paragraph. With no relationships it is the type sentence and has no final stop.</returns>
    public static string DescribeElement(EnterpriseDocument Document, EnterpriseElement Element)
    {
        string TypeName = TypeNameOf(Element);
        string Opening = DisplayName(Element.Label, Element.Id) + " is " + Article(TypeName) + " " + TypeName;
        return Compose(Document, Element.Id, Opening);
    }

    /// <summary>
    /// Describes one relationship line.
    /// </summary>
    /// <param name="Document">Parsed diagram.</param>
    /// <param name="SourceId">Source element id.</param>
    /// <param name="TargetId">Target element id.</param>
    /// <param name="KindName">Kind drawn on the line, or empty.</param>
    /// <returns>A single sentence with no final stop.</returns>
    public static string DescribeRelationship(EnterpriseDocument Document, string SourceId, string TargetId, string KindName)
    {
        EnterpriseRelationship? Match = null;
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            if (!string.Equals(Relationship.SourceId, SourceId, StringComparison.Ordinal)
                || !string.Equals(Relationship.TargetId, TargetId, StringComparison.Ordinal))
            {
                continue;
            }

            if (KindName.Length > 0
                && !string.Equals(EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind), KindName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Match = Relationship;
            break;
        }

        string SourceName = ParticipantName(Document, SourceId);
        string TargetName = ParticipantName(Document, TargetId);
        if (Match == null)
        {
            return SourceName + " connects to " + TargetName;
        }

        string Sentence = SourceName + " " + Verb(Match) + " " + TargetName;
        if (Match.Label.Length > 0 && !LabelOnlyRepeatsRelationshipWording(Match))
        {
            Sentence = Sentence + ": " + Match.Label;
        }

        return Sentence;
    }

    /// <summary>
    /// Keeps a hover note short. A long paragraph keeps its first sentence and points at the selection panel.
    /// </summary>
    /// <param name="Full">The full description.</param>
    /// <returns>Text that fits a small note.</returns>
    public static string ForPopup(string Full)
    {
        if (string.IsNullOrEmpty(Full) || Full.Length <= 220)
        {
            return Full ?? string.Empty;
        }

        int Stop = Full.IndexOf(". ", StringComparison.Ordinal);
        string First = Stop < 0 ? Full : Full.Substring(0, Stop + 1);
        return First + " The selection panel has the rest.";
    }

    private static string Compose(EnterpriseDocument Document, string Id, string Opening)
    {
        List<StoryGroup> Leading = new List<StoryGroup>();
        List<StoryGroup> Following = new List<StoryGroup>();
        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            bool Incoming = string.Equals(Relationship.TargetId, Id, StringComparison.Ordinal);
            bool Outgoing = string.Equals(Relationship.SourceId, Id, StringComparison.Ordinal);
            if (!Incoming && !Outgoing)
            {
                continue;
            }

            string OtherId = Incoming ? Relationship.SourceId : Relationship.TargetId;
            StoryGroup Group = FindGroup(Leading, Following, Relationship, Incoming, OtherType(Document, OtherId));
            Group.Names.Add(Mention(Document, Relationship, OtherId));
        }

        if (Leading.Count == 0 && Following.Count == 0)
        {
            return Opening;
        }

        SortGroups(Leading);
        SortGroups(Following);
        StringBuilder Paragraph = new StringBuilder();
        Paragraph.Append(Opening);
        bool Started = false;
        foreach (StoryGroup Group in Leading)
        {
            string Clause = LeadingClause(Group);
            if (!Started)
            {
                Paragraph.Append(' ');
            }
            else if (Clause.StartsWith("with ", StringComparison.Ordinal))
            {
                Paragraph.Append(' ');
            }
            else
            {
                Paragraph.Append(", ");
            }

            Paragraph.Append(Clause);
            Started = true;
        }

        Paragraph.Append('.');
        foreach (StoryGroup Group in Following)
        {
            Paragraph.Append(' ');
            Paragraph.Append(FollowingSentence(Group));
        }

        return Paragraph.ToString();
    }

    private static StoryGroup FindGroup(List<StoryGroup> Leading, List<StoryGroup> Following, EnterpriseRelationship Relationship, bool Incoming, string OtherTypeName)
    {
        bool Structural = IsStructural(Relationship.Kind);
        string TypeKey = Structural && Relationship.Kind != EnterpriseRelationshipKind.Serving ? string.Empty : OtherTypeName;
        if (Relationship.Kind == EnterpriseRelationshipKind.Serving && !Incoming)
        {
            TypeKey = string.Empty;
        }

        List<StoryGroup> Target = Structural ? Leading : Following;
        foreach (StoryGroup Existing in Target)
        {
            if (Existing.Incoming == Incoming
                && Existing.Kind == Relationship.Kind
                && Existing.Access == Relationship.AccessKind
                && Existing.Directed == Relationship.DirectedAssociation
                && string.Equals(Existing.Influence, Relationship.InfluenceStrength, StringComparison.Ordinal)
                && string.Equals(Existing.OtherType, TypeKey, StringComparison.Ordinal))
            {
                return Existing;
            }
        }

        StoryGroup Created = new StoryGroup
        {
            Incoming = Incoming,
            Kind = Relationship.Kind,
            Access = Relationship.AccessKind,
            Influence = Relationship.InfluenceStrength,
            Directed = Relationship.DirectedAssociation,
            OtherType = TypeKey,
            Rank = Rank(Relationship.Kind, Incoming)
        };
        Target.Add(Created);
        return Created;
    }

    private static bool IsStructural(EnterpriseRelationshipKind Kind)
    {
        switch (Kind)
        {
            case EnterpriseRelationshipKind.Composition:
            case EnterpriseRelationshipKind.Aggregation:
            case EnterpriseRelationshipKind.Assignment:
            case EnterpriseRelationshipKind.Realization:
            case EnterpriseRelationshipKind.Serving:
            case EnterpriseRelationshipKind.Specialization:
            case EnterpriseRelationshipKind.Association:
                return true;
            default:
                return false;
        }
    }

    private static int Rank(EnterpriseRelationshipKind Kind, bool Incoming)
    {
        switch (Kind)
        {
            case EnterpriseRelationshipKind.Realization:
                return Incoming ? 0 : 8;
            case EnterpriseRelationshipKind.Serving:
                return Incoming ? 1 : 9;
            case EnterpriseRelationshipKind.Composition:
                return Incoming ? 3 : 2;
            case EnterpriseRelationshipKind.Aggregation:
                return Incoming ? 5 : 4;
            case EnterpriseRelationshipKind.Assignment:
                return Incoming ? 7 : 6;
            case EnterpriseRelationshipKind.Specialization:
                return Incoming ? 11 : 10;
            case EnterpriseRelationshipKind.Association:
                return 12;
            case EnterpriseRelationshipKind.Access:
                return Incoming ? 21 : 20;
            case EnterpriseRelationshipKind.Flow:
                return Incoming ? 23 : 22;
            case EnterpriseRelationshipKind.Triggering:
                return Incoming ? 25 : 24;
            default:
                return Incoming ? 27 : 26;
        }
    }

    private static void SortGroups(List<StoryGroup> Groups)
    {
        Groups.Sort(CompareGroups);
        foreach (StoryGroup Group in Groups)
        {
            Group.Names.Sort(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static int CompareGroups(StoryGroup Left, StoryGroup Right)
    {
        int RankOrder = Left.Rank.CompareTo(Right.Rank);
        if (RankOrder != 0)
        {
            return RankOrder;
        }

        return string.Compare(Left.OtherType, Right.OtherType, StringComparison.OrdinalIgnoreCase);
    }

    private static string LeadingClause(StoryGroup Group)
    {
        string Names = JoinNames(Group.Names);
        if (Group.Kind == EnterpriseRelationshipKind.Realization && Group.Incoming)
        {
            return "realised by " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Realization)
        {
            return "realising " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Serving && Group.Incoming)
        {
            if (Group.Names.Count == 1)
            {
                return "with " + Article(Group.OtherType) + " " + Group.OtherType + " provided by " + Names;
            }

            return "with " + Plural(Group.OtherType) + " provided by " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Serving)
        {
            return "serving " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Composition && Group.Incoming)
        {
            return "and is part of " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Composition)
        {
            return "composed of " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Aggregation && Group.Incoming)
        {
            return "and belongs to " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Aggregation)
        {
            return "aggregating " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Assignment && Group.Incoming)
        {
            return "with " + Names + " assigned to it";
        }

        if (Group.Kind == EnterpriseRelationshipKind.Assignment)
        {
            return "assigned to " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Specialization && Group.Incoming)
        {
            return "specialised by " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Specialization)
        {
            return "specialising " + Names;
        }

        if (Group.Kind == EnterpriseRelationshipKind.Association && Group.Directed && Group.Incoming)
        {
            return "with " + Names + " directed toward it";
        }

        if (Group.Kind == EnterpriseRelationshipKind.Association && Group.Directed)
        {
            return "directed toward " + Names;
        }

        return "associated with " + Names;
    }

    private static string FollowingSentence(StoryGroup Group)
    {
        string Lead = FollowingLead(Group);
        if (Group.Names.Count == 1)
        {
            return "It " + Lead + " " + Group.Names[0] + ".";
        }

        return "It " + Lead + " " + Group.Names.Count.ToString(CultureInfo.InvariantCulture) + " " + Plural(Group.OtherType) + " - " + JoinNames(Group.Names) + ".";
    }

    private static string FollowingLead(StoryGroup Group)
    {
        if (Group.Kind == EnterpriseRelationshipKind.Access)
        {
            EnterpriseAccessKind Access = Group.Access ?? EnterpriseAccessKind.Write;
            if (Group.Incoming)
            {
                switch (Access)
                {
                    case EnterpriseAccessKind.Read:
                        return "is read by";
                    case EnterpriseAccessKind.ReadWrite:
                        return "is read and written by";
                    case EnterpriseAccessKind.Access:
                        return "is accessed by";
                    default:
                        return "is written by";
                }
            }

            switch (Access)
            {
                case EnterpriseAccessKind.Read:
                    return "reads";
                case EnterpriseAccessKind.ReadWrite:
                    return "reads and writes";
                case EnterpriseAccessKind.Access:
                    return "accesses";
                default:
                    return "relies on";
            }
        }

        if (Group.Kind == EnterpriseRelationshipKind.Flow)
        {
            return Group.Incoming ? "has a flow from" : "flows to";
        }

        if (Group.Kind == EnterpriseRelationshipKind.Triggering)
        {
            return Group.Incoming ? "is triggered by" : "triggers";
        }

        if (Group.Incoming)
        {
            switch (Group.Influence)
            {
                case "+":
                    return "is positively influenced by";
                case "++":
                    return "is strongly influenced by";
                case "-":
                    return "is negatively influenced by";
                case "--":
                    return "is strongly opposed by";
                default:
                    return "is influenced by";
            }
        }

        switch (Group.Influence)
        {
            case "+":
                return "positively influences";
            case "++":
                return "strongly influences";
            case "-":
                return "negatively influences";
            case "--":
                return "strongly opposes";
            default:
                return "influences";
        }
    }

    private static string JoinNames(List<string> Names)
    {
        if (Names.Count == 0)
        {
            return string.Empty;
        }

        if (Names.Count == 1)
        {
            return Names[0];
        }

        if (Names.Count == 2)
        {
            return Names[0] + " and " + Names[1];
        }

        StringBuilder Builder = new StringBuilder();
        for (int Index = 0; Index < Names.Count; Index++)
        {
            if (Index > 0)
            {
                Builder.Append(Index == Names.Count - 1 ? " and " : ", ");
            }

            Builder.Append(Names[Index]);
        }

        return Builder.ToString();
    }

    private static string Mention(EnterpriseDocument Document, EnterpriseRelationship Relationship, string OtherId)
    {
        string Name = ParticipantName(Document, OtherId);
        if (Relationship.Label.Length == 0 || LabelOnlyRepeatsRelationshipWording(Relationship))
        {
            return Name;
        }

        return Name + " (" + Relationship.Label + ")";
    }

    private static bool LabelOnlyRepeatsRelationshipWording(EnterpriseRelationship Relationship)
    {
        string Label = Relationship.Label.Trim();
        if (Label.Length == 0)
        {
            return true;
        }

        foreach (string Token in RedundantLabelTokens(Relationship))
        {
            if (string.Equals(Label, Token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> RedundantLabelTokens(EnterpriseRelationship Relationship)
    {
        yield return EnterpriseElementCatalog.GetRelationshipName(Relationship.Kind);
        switch (Relationship.Kind)
        {
            case EnterpriseRelationshipKind.Composition:
                yield return "composition";
                yield return "composed";
                yield return "composes";
                break;
            case EnterpriseRelationshipKind.Aggregation:
                yield return "aggregation";
                yield return "aggregates";
                yield return "aggregate";
                break;
            case EnterpriseRelationshipKind.Assignment:
                yield return "assignment";
                yield return "assigned";
                yield return "assigns";
                break;
            case EnterpriseRelationshipKind.Realization:
                yield return "realization";
                yield return "realisation";
                yield return "realises";
                yield return "realizes";
                break;
            case EnterpriseRelationshipKind.Serving:
                yield return "serves";
                yield return "serve";
                yield return "serving";
                break;
            case EnterpriseRelationshipKind.Specialization:
                yield return "specialization";
                yield return "specialisation";
                yield return "specialises";
                yield return "specializes";
                break;
            case EnterpriseRelationshipKind.Association:
                if (Relationship.DirectedAssociation)
                {
                    yield return "directed";
                    yield return "direction";
                }
                else
                {
                    yield return "associated";
                    yield return "association";
                }

                break;
            case EnterpriseRelationshipKind.Access:
                yield return "access";
                yield return "accesses";
                yield return "reads";
                yield return "writes";
                break;
            case EnterpriseRelationshipKind.Flow:
                yield return "flow";
                yield return "flows";
                break;
            case EnterpriseRelationshipKind.Triggering:
                yield return "triggering";
                yield return "triggers";
                yield return "trigger";
                yield return "starts";
                yield return "start";
                yield return "triggered";
                break;
            case EnterpriseRelationshipKind.Influence:
                yield return "influence";
                yield return "influences";
                yield return "influenced";
                break;
        }
    }

    private static string OtherType(EnterpriseDocument Document, string Id)
    {
        if (Document.Elements.TryGetValue(Id, out EnterpriseElement? Element))
        {
            return TypeNameOf(Element);
        }

        if (Document.Groups.ContainsKey(Id))
        {
            return "group";
        }

        if (Document.Notes.ContainsKey(Id))
        {
            return "note";
        }

        return "element";
    }

    private static string ParticipantName(EnterpriseDocument Document, string Id)
    {
        if (Document.Elements.TryGetValue(Id, out EnterpriseElement? Element))
        {
            return DisplayName(Element.Label, Id);
        }

        if (Document.Groups.TryGetValue(Id, out EnterpriseGroup? Group))
        {
            return DisplayName(Group.Label, Id);
        }

        if (Document.Notes.TryGetValue(Id, out EnterpriseNote? Note))
        {
            return DisplayName(Note.Label, Id);
        }

        return Id;
    }

    private static string TypeNameOf(EnterpriseElement Element)
    {
        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            if (Element.JunctionKind == EnterpriseJunctionKind.Or)
            {
                return "or junction";
            }

            return "and junction";
        }

        return ElementShapePainter.DisplayName(EnterpriseElementCatalog.GetKebabName(Element.ElementType)).ToLowerInvariant();
    }

    private static string DisplayName(string Label, string Id)
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            return Id;
        }

        return Label;
    }

    private static string Article(string TypeName)
    {
        if (TypeName.Length == 0)
        {
            return "a";
        }

        char First = char.ToLowerInvariant(TypeName[0]);
        if (First == 'a' || First == 'e' || First == 'i' || First == 'o' || First == 'u')
        {
            return "an";
        }

        return "a";
    }

    private static string Plural(string TypeName)
    {
        if (TypeName.EndsWith("s", StringComparison.Ordinal)
            || TypeName.EndsWith("x", StringComparison.Ordinal)
            || TypeName.EndsWith("ch", StringComparison.Ordinal)
            || TypeName.EndsWith("sh", StringComparison.Ordinal))
        {
            return TypeName + "es";
        }

        return TypeName + "s";
    }

    private static string Verb(EnterpriseRelationship Relationship)
    {
        switch (Relationship.Kind)
        {
            case EnterpriseRelationshipKind.Composition:
                return "is composed of";
            case EnterpriseRelationshipKind.Aggregation:
                return "aggregates";
            case EnterpriseRelationshipKind.Assignment:
                return "is assigned to";
            case EnterpriseRelationshipKind.Realization:
                return "realises";
            case EnterpriseRelationshipKind.Serving:
                return "serves";
            case EnterpriseRelationshipKind.Access:
                return AccessVerb(Relationship.AccessKind);
            case EnterpriseRelationshipKind.Influence:
                return InfluenceVerb(Relationship.InfluenceStrength);
            case EnterpriseRelationshipKind.Triggering:
                return "triggers";
            case EnterpriseRelationshipKind.Flow:
                return "flows to";
            case EnterpriseRelationshipKind.Specialization:
                return "specialises";
            case EnterpriseRelationshipKind.Association:
                if (Relationship.DirectedAssociation)
                {
                    return "is directed toward";
                }

                return "is associated with";
            default:
                return "connects to";
        }
    }

    private static string AccessVerb(EnterpriseAccessKind? Kind)
    {
        switch (Kind)
        {
            case EnterpriseAccessKind.Read:
                return "reads";
            case EnterpriseAccessKind.ReadWrite:
                return "reads and writes";
            case EnterpriseAccessKind.Access:
                return "accesses";
            default:
                return "writes";
        }
    }

    private static string InfluenceVerb(string? Strength)
    {
        switch (Strength)
        {
            case "+":
                return "positively influences";
            case "++":
                return "strongly influences";
            case "-":
                return "negatively influences";
            case "--":
                return "strongly opposes";
            default:
                return "influences";
        }
    }

    private sealed class StoryGroup
    {
        public bool Incoming;

        public EnterpriseRelationshipKind Kind;

        public EnterpriseAccessKind? Access;

        public string? Influence;

        public bool Directed;

        public string OtherType = string.Empty;

        public int Rank;

        public List<string> Names = new List<string>();
    }
}
