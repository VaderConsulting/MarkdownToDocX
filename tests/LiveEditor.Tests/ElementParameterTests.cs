using System;
using System.Collections.Generic;
using LiveEditor.Completion;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Every element type must offer the same declaration parameters, and the relationship kinds its rules row allows.
/// </summary>
[TestFixture]
public sealed class ElementParameterTests
{
    private static readonly string[] DeclarationParameters = new string[]
    {
        "<id>",
        "[\"label\"]",
        "[in <container>]",
        "above",
        "below",
        "left-of",
        "right-of",
        "<peer>",
        "[+n]",
        "push",
        "<N|S|E|W>"
    };

    private static readonly string[] ClauseWords = new string[] { "in", "above", "below", "left-of", "right-of", "push" };

    private static readonly string[] PushDirections = new string[] { "N", "S", "E", "W" };

    [Test]
    public void EveryNodeTypeDocumentsAndOffersItsParameters()
    {
        EnterpriseCompletionEngine Engine = new EnterpriseCompletionEngine();
        foreach (string TypeName in TypeNames())
        {
            string RulesType = RulesTypeName(TypeName);
            HashSet<string> ExpectedRelationships = RelationshipNamesFromRules(RulesType);
            string Documentation = DocumentationFor(TypeName);
            foreach (string Parameter in DeclarationParameters)
            {
                Assert.That(Documentation, Does.Contain(Parameter), TypeName + " documentation is missing " + Parameter);
            }

            Assert.That(RelationshipLines(Documentation), Is.Empty, TypeName + " documentation must not list relationships");
            Assert.That(Documentation, Does.Contain("Tab to insert"), TypeName + " suggestion help keeps its keyboard hint");

            string Hover = EnterpriseHelp.QuickInfoFor("enterprise\n  " + TypeName + " item", "enterprise\n  ".Length + 1, new EnterpriseDocument()) ?? string.Empty;
            Assert.That(Hover, Does.Contain("<id>"), TypeName + " hover");
            Assert.That(Hover, Does.Not.Contain("Tab to insert"), TypeName + " hover has no keyboard hint");
            Assert.That(Hover, Does.Not.Contain("Example"), TypeName + " hover has no example");
            Assert.That(RelationshipLines(Hover), Is.Empty, TypeName + " hover must not list relationships");

            string Typed = "enterprise\n  " + TypeName;
            bool FoundType = false;
            foreach (CompletionItem TypeItem in Engine.Build(Typed, Typed.Length).Items)
            {
                if (!string.Equals(TypeItem.DisplayText, TypeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FoundType = true;
                Assert.That(TypeItem.Documentation, Does.Contain("<id>"), TypeName);
                Assert.That(TypeItem.Documentation, Does.Contain("above"), TypeName);
                Assert.That(TypeItem.Documentation, Does.Contain("push"), TypeName);
            }

            Assert.That(FoundType, Is.True, TypeName + " is missing from type completion");

            string AfterLabel = "enterprise\n  " + TypeName + " item \"Item\" ";
            AssertDisplayNames(Engine.Build(AfterLabel, AfterLabel.Length), ClauseWords, TypeName + " clauses");

            string BeforeLabel = "enterprise\n  " + TypeName + " item ";
            CompletionContext LabelPosition = Engine.Build(BeforeLabel, BeforeLabel.Length);
            foreach (string Word in ClauseWords)
            {
                Assert.That(FirstWords(LabelPosition), Does.Not.Contain(Word), TypeName + " must prompt for the label before " + Word);
            }

            ParameterHelp? LabelHelp = Engine.BuildParameterHelp(BeforeLabel, BeforeLabel.Length);
            Assert.That(LabelHelp, Is.Not.Null, TypeName);
            Assert.That(LabelHelp!.Signature, Does.Contain("[\"label\"]"));
            Assert.That(LabelHelp.Signature, Does.Contain("above"), TypeName + " label help shows the placement clauses");
            Assert.That(LabelHelp.Signature, Does.Contain("push"), TypeName + " label help shows the push clause");
            Assert.That(LabelHelp.Body, Does.Not.Contain("The statement can end"), TypeName + " label help has no filler");

            string Identifier = "enterprise\n  " + TypeName + " ";
            ParameterHelp? IdentifierHelp = Engine.BuildParameterHelp(Identifier, Identifier.Length);
            Assert.That(IdentifierHelp, Is.Not.Null, TypeName);
            Assert.That(IdentifierHelp!.Body, Does.Contain("unique"));

            string Push = "enterprise\n  " + TypeName + " item \"Item\" push ";
            AssertDisplayNames(Engine.Build(Push, Push.Length), PushDirections, TypeName + " push");

            string Peer = "enterprise\n  grouping box \"Box\"\n  " + TypeName + " item \"Item\" above ";
            HashSet<string> Peers = FirstWords(Engine.Build(Peer, Peer.Length));
            Assert.That(Peers, Does.Contain("box"), TypeName + " peer");
            Assert.That(Peers, Does.Not.Contain("item"), TypeName + " peer excludes itself");

            string Container = "enterprise\n  grouping box \"Box\"\n  " + TypeName + " item \"Item\" in ";
            Assert.That(FirstWords(Engine.Build(Container, Container.Length)), Does.Contain("box"), TypeName + " container");

            string Offset = "enterprise\n  grouping box \"Box\"\n  " + TypeName + " item \"Item\" above box +";
            ParameterHelp? OffsetHelp = Engine.BuildParameterHelp(Offset, Offset.Length);
            Assert.That(OffsetHelp, Is.Not.Null, TypeName);
            Assert.That(OffsetHelp!.Signature, Does.Contain("[+n]"));
            Assert.That(OffsetHelp.Heading, Does.Contain("+n"));

            string Relationship = "enterprise\n  " + TypeName + " item \"Item\"\n  item -";
            CompletionContext Kinds = Engine.Build(Relationship, Relationship.Length);
            Assert.That(FirstWords(Kinds), Is.EquivalentTo(ExpectedRelationships), TypeName + " relationship kinds");
            foreach (CompletionItem Item in Kinds.Items)
            {
                Assert.That(Item.Documentation, Does.Contain("target"), Item.DisplayText);
                Assert.That(Item.Documentation, Does.Contain("\"label\""), Item.DisplayText);
                Assert.That(Item.Documentation, Does.Contain("routing"), Item.DisplayText);
                Assert.That(Item.Documentation, Does.Contain("leave"), Item.DisplayText);
                Assert.That(Item.Documentation, Does.Contain("enter"), Item.DisplayText);
                Assert.That(Item.Documentation, Does.Contain("travel"), Item.DisplayText);
                if (string.Equals(Item.DisplayText, "access", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.That(Item.Documentation, Does.Contain("read"));
                    Assert.That(Item.Documentation, Does.Contain("write"));
                    Assert.That(Item.Documentation, Does.Contain("readwrite"));
                    Assert.That(Item.Documentation, Does.Contain("Default when omitted: write."));
                }

                if (string.Equals(Item.DisplayText, "influence", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.That(Item.Documentation, Does.Contain("++"));
                    Assert.That(Item.Documentation, Does.Contain("--"));
                }

                if (string.Equals(Item.DisplayText, "association", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.That(Item.Documentation, Does.Contain("directed"));
                }
            }
        }
    }

    private static IEnumerable<string> TypeNames()
    {
        foreach (EnterpriseElementMetadata Metadata in EnterpriseElementCatalog.Elements.Values)
        {
            yield return Metadata.KebabName;
        }

        yield return "and-junction";
        yield return "or-junction";
        yield return "junction-and";
        yield return "junction-or";
        yield return "group";
    }

    private static string RulesTypeName(string TypeName)
    {
        if (!EnterpriseElementCatalog.TryResolveElementType(TypeName, out EnterpriseElementType ElementType))
        {
            return TypeName;
        }

        return EnterpriseElementCatalog.GetKebabName(ElementType);
    }

    private static string DocumentationFor(string TypeName)
    {
        if (!EnterpriseElementCatalog.TryResolveElementType(TypeName, out EnterpriseElementType ElementType))
        {
            Assert.Fail("Unknown type " + TypeName);
            return string.Empty;
        }

        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[ElementType];
        return EnterpriseHelp.ElementTypeDocumentation(Metadata, TypeName);
    }

    private static HashSet<string> RelationshipNamesFromRules(string RulesType)
    {
        HashSet<char> Letters = new HashSet<char>();
        EnterpriseRulesCatalog Rules = EnterpriseRulesCatalog.Instance;
        foreach (string TargetType in Rules.Types)
        {
            string Allowed = Rules.GetAllowedLetters(EnterpriseParser.DEFAULT_VERSION, RulesType, TargetType);
            foreach (char Letter in Allowed)
            {
                Letters.Add(Letter);
            }
        }

        HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<EnterpriseRelationshipKind, EnterpriseRelationshipMetadata> Pair in EnterpriseElementCatalog.Relationships)
        {
            if (Letters.Contains(Pair.Value.Letter))
            {
                Names.Add(Pair.Value.Name);
            }
        }

        return Names;
    }

    private static HashSet<string> RelationshipLines(string Documentation)
    {
        HashSet<string> Known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (EnterpriseRelationshipMetadata Metadata in EnterpriseElementCatalog.Relationships.Values)
        {
            Known.Add(Metadata.Name);
        }

        HashSet<string> Found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string Line in Documentation.Split('\n'))
        {
            string Trimmed = Line.Trim();
            if (Known.Contains(Trimmed))
            {
                Found.Add(Trimmed);
            }
        }

        return Found;
    }

    private static HashSet<string> FirstWords(CompletionContext Context)
    {
        HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CompletionItem Item in Context.Items)
        {
            string Text = Item.DisplayText;
            int Space = Text.IndexOf(' ');
            Names.Add(Space < 0 ? Text : Text.Substring(0, Space));
        }

        return Names;
    }

    private static void AssertDisplayNames(CompletionContext Context, IEnumerable<string> Expected, string Because)
    {
        Assert.That(FirstWords(Context), Is.EquivalentTo(Expected), Because);
    }
}
