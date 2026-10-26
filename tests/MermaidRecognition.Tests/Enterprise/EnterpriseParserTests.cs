using MermaidRecognition.Kinds;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseParserTests
{
    [Test]
    public void NotesAndPlainConnections_AreDrawingItemsWithHints()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument("""
            enterprise
            group area
            application-component shop in area
            note explanation "Read this" in area right-of shop
            explanation --- shop [leave W, enter E]
            """);

        Assert.That(Result.HasErrors, Is.False,
            string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Elements.ContainsKey("explanation"), Is.False);
        Assert.That(Result.Document.Relationships, Is.Empty);
        Assert.That(Result.Document.Notes["explanation"].ParentId, Is.EqualTo("area"));
        Assert.That(Result.Document.Notes["explanation"].PlacementHint!.Kind, Is.EqualTo(PlacementHintKind.RightOf));
        Assert.That(Result.Document.PlainConnections, Has.Count.EqualTo(1));
        Assert.That(Result.Document.PlainConnections[0].RoutingHint!.Leave, Is.EqualTo(LayoutHintDirection.West));
        Assert.That(Result.Document.PlainConnections[0].RoutingHint!.Enter, Is.EqualTo(LayoutHintDirection.East));
    }

    [Test]
    public void ClosingDashes_SetRelationshipMinLength()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument("""
            enterprise
            application-component shop
            application-service api
            data-object order
            shop -serving-> api
            shop -serving--> order
            api -access(read)---> order
            api -flow------------> shop
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Relationships[0].MinLength, Is.EqualTo(1));
        Assert.That(Result.Document.Relationships[1].MinLength, Is.EqualTo(1));
        Assert.That(Result.Document.Relationships[2].Kind, Is.EqualTo(EnterpriseRelationshipKind.Access));
        Assert.That(Result.Document.Relationships[2].MinLength, Is.EqualTo(2));
        Assert.That(Result.Document.Relationships[3].MinLength, Is.EqualTo(MermaidLinkLength.MAXIMUM));
    }

    [Test]
    public void PlainConnectionDashes_SetMinLength()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument("""
            enterprise
            application-component shop
            note explanation "Read this"
            explanation --- shop
            explanation ---- shop
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.PlainConnections[0].MinLength, Is.EqualTo(1));
        Assert.That(Result.Document.PlainConnections[1].MinLength, Is.EqualTo(2));
    }

    [Test]
    public void PlainConnection_RequiresANoteEnd()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument("""
            enterprise
            application-component a
            application-component b
            a --- b
            """);

        Assert.That(Result.HasErrors, Is.True);
        Assert.That(Result.Diagnostics.Any(Item => Item.Message.Contains("must have a note", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void Parse_PlacementAndRoutingHints_RecordsTypedValues()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component anchor "Anchor"
              group box "Box" right-of anchor +2
              application-component first "First" in box push E
              application-component second "Second" in box below first +1
              anchor -flow-> box [leave S, enter W, travel E]
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.Multiple(() =>
        {
            Assert.That(Result.Document.Groups["box"].PlacementHint!.Kind, Is.EqualTo(PlacementHintKind.RightOf));
            Assert.That(Result.Document.Groups["box"].PlacementHint!.PeerId, Is.EqualTo("anchor"));
            Assert.That(Result.Document.Groups["box"].PlacementHint!.Offset, Is.EqualTo(2));
            Assert.That(Result.Document.Elements["first"].PlacementHint!.Kind, Is.EqualTo(PlacementHintKind.Push));
            Assert.That(Result.Document.Elements["first"].PlacementHint!.Direction, Is.EqualTo(LayoutHintDirection.East));
            Assert.That(Result.Document.Elements["second"].PlacementHint!.Kind, Is.EqualTo(PlacementHintKind.Below));
            Assert.That(Result.Document.Relationships[0].RoutingHint!.Leave, Is.EqualTo(LayoutHintDirection.South));
            Assert.That(Result.Document.Relationships[0].RoutingHint!.Enter, Is.EqualTo(LayoutHintDirection.West));
            Assert.That(Result.Document.Relationships[0].RoutingHint!.Travel, Is.EqualTo(LayoutHintDirection.East));
        });
    }

    [Test]
    public void Parse_InvalidPlacementPeersAndCycle_ReportWarningsWithoutFailing()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              group one "One"
              group two "Two"
              application-component a "A" in one right-of missing
              application-component b "B" in one left-of c
              application-component c "C" in two
              application-component x "X" below y
              application-component y "Y" below x
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.Multiple(() =>
        {
            Assert.That(Result.Document.Elements["a"].PlacementHint!.IsActive, Is.False);
            Assert.That(Result.Document.Elements["b"].PlacementHint!.IsActive, Is.False);
            Assert.That(Result.Document.Elements["x"].PlacementHint!.IsActive, Is.True);
            Assert.That(Result.Document.Elements["y"].PlacementHint!.IsActive, Is.False);
            Assert.That(Result.Diagnostics.Count(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.EqualTo(3));
        });
    }

    [Test]
    public void Parse_GroupIds_AreValidRelationshipEndpoints()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component outside "Outside"
              group box "Box"
              outside -flow-> box
              box -serving-> outside
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Relationships, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_AllStatementForms_ReadsTitleVersionDirectionGroupElementAndRelationship()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              title Web shop
              version 3.1
              direction LR
              group dc "Primary data centre"
              application-component webShop "Web Shop" in dc
              ApplicationFunction checkout "Checkout" in webShop
              webShop -assignment-> checkout "owns"
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Title, Is.EqualTo("Web shop"));
        Assert.That(Result.Document.Version, Is.EqualTo("3.1"));
        Assert.That(Result.Document.Direction, Is.EqualTo(EnterpriseDiagramDirection.Lr));
        Assert.That(Result.Document.Groups.ContainsKey("dc"), Is.True);
        Assert.That(Result.Document.Groups["dc"].Label, Is.EqualTo("Primary data centre"));
        Assert.That(Result.Document.Elements["webShop"].ElementType, Is.EqualTo(EnterpriseElementType.ApplicationComponent));
        Assert.That(Result.Document.Elements["webShop"].ParentId, Is.EqualTo("dc"));
        Assert.That(Result.Document.Elements["checkout"].ElementType, Is.EqualTo(EnterpriseElementType.ApplicationFunction));
        Assert.That(Result.Document.Elements["checkout"].ParentId, Is.EqualTo("webShop"));
        Assert.That(Result.Document.Relationships, Has.Count.EqualTo(1));
        Assert.That(Result.Document.Relationships[0].Kind, Is.EqualTo(EnterpriseRelationshipKind.Assignment));
        Assert.That(Result.Document.Relationships[0].Label, Is.EqualTo("owns"));
    }

    [Test]
    public void Parse_OmitsOptionalFields_DefaultsVersionDirectionAndLabels()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop
              data-object order
              webShop -a-> order
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Version, Is.EqualTo("3.2"));
        Assert.That(Result.Document.Direction, Is.EqualTo(EnterpriseDiagramDirection.Tb));
        Assert.That(Result.Document.Elements["webShop"].Label, Is.EqualTo("webShop"));
        Assert.That(Result.Document.Relationships[0].Kind, Is.EqualTo(EnterpriseRelationshipKind.Access));
        Assert.That(Result.Document.Relationships[0].AccessKind, Is.EqualTo(EnterpriseAccessKind.Write));
    }

    [TestCase("composition", EnterpriseRelationshipKind.Composition)]
    [TestCase("c", EnterpriseRelationshipKind.Composition)]
    [TestCase("aggregation", EnterpriseRelationshipKind.Aggregation)]
    [TestCase("g", EnterpriseRelationshipKind.Aggregation)]
    [TestCase("assignment", EnterpriseRelationshipKind.Assignment)]
    [TestCase("i", EnterpriseRelationshipKind.Assignment)]
    [TestCase("realization", EnterpriseRelationshipKind.Realization)]
    [TestCase("r", EnterpriseRelationshipKind.Realization)]
    [TestCase("serving", EnterpriseRelationshipKind.Serving)]
    [TestCase("v", EnterpriseRelationshipKind.Serving)]
    [TestCase("access", EnterpriseRelationshipKind.Access)]
    [TestCase("a", EnterpriseRelationshipKind.Access)]
    [TestCase("influence", EnterpriseRelationshipKind.Influence)]
    [TestCase("n", EnterpriseRelationshipKind.Influence)]
    [TestCase("triggering", EnterpriseRelationshipKind.Triggering)]
    [TestCase("t", EnterpriseRelationshipKind.Triggering)]
    [TestCase("flow", EnterpriseRelationshipKind.Flow)]
    [TestCase("f", EnterpriseRelationshipKind.Flow)]
    [TestCase("specialization", EnterpriseRelationshipKind.Specialization)]
    [TestCase("s", EnterpriseRelationshipKind.Specialization)]
    [TestCase("association", EnterpriseRelationshipKind.Association)]
    [TestCase("o", EnterpriseRelationshipKind.Association)]
    public void Parse_RelationshipNameOrLetter_ResolvesKind(string Token, EnterpriseRelationshipKind Expected)
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            $"""
            enterprise
              application-component a "A"
              application-component b "B"
              a -{Token}-> b
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Relationships[0].Kind, Is.EqualTo(Expected));
    }

    [TestCase("read", EnterpriseAccessKind.Read)]
    [TestCase("write", EnterpriseAccessKind.Write)]
    [TestCase("readwrite", EnterpriseAccessKind.ReadWrite)]
    [TestCase("access", EnterpriseAccessKind.Access)]
    public void Parse_AccessQualifier_SetsAccessKind(string Qualifier, EnterpriseAccessKind Expected)
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            $"""
            enterprise
              application-function checkout "Checkout"
              data-object order "Order"
              checkout -access({Qualifier})-> order
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Relationships[0].AccessKind, Is.EqualTo(Expected));
    }

    [TestCase("+")]
    [TestCase("-")]
    [TestCase("++")]
    [TestCase("--")]
    [TestCase("2")]
    [TestCase("-3")]
    public void Parse_InfluenceQualifier_SetsStrength(string Qualifier)
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            $"""
            enterprise
              application-component a "A"
              application-component b "B"
              a -influence({Qualifier})-> b
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Relationships[0].InfluenceStrength, Is.EqualTo(Qualifier));
    }

    [Test]
    public void Parse_DirectedAssociation_SetsFlag()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              communication-network lan "LAN"
              node appNode "Node"
              lan -association(directed)-> appNode
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Relationships[0].DirectedAssociation, Is.True);
    }

    [Test]
    public void Parse_InvalidQualifier_ReportsLineNumber()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component a "A"
              application-component b "B"
              a -serving(read)-> b
            """);

        Assert.That(Result.HasErrors, Is.True);
        Assert.That(Result.Diagnostics[0].LineNumber, Is.EqualTo(4));
        Assert.That(Result.Diagnostics[0].Message, Does.Contain("line 4: qualifier \"(read)\" is not valid for serving"));
    }

    [Test]
    public void Parse_UnknownElementType_ReportsLineNumber()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              foobar-widget ghost "Ghost"
            """);

        Assert.That(Result.Diagnostics[0].LineNumber, Is.EqualTo(2));
        Assert.That(Result.Diagnostics[0].Message, Does.Contain("line 2: unknown element type \"foobar-widget\""));
    }

    [TestCase("business-actor", EnterpriseElementType.BusinessActor)]
    [TestCase("stakeholder", EnterpriseElementType.Stakeholder)]
    [TestCase("capability", EnterpriseElementType.Capability)]
    [TestCase("equipment", EnterpriseElementType.Equipment)]
    [TestCase("work-package", EnterpriseElementType.WorkPackage)]
    [TestCase("location", EnterpriseElementType.Location)]
    [TestCase("grouping", EnterpriseElementType.Grouping)]
    [TestCase("and-junction", EnterpriseElementType.Junction)]
    [TestCase("or-junction", EnterpriseElementType.Junction)]
    public void Parse_NewLayerType_ResolvesElement(string Token, EnterpriseElementType Expected)
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            "enterprise\n  " + Token + " item \"Item\"");
        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Elements["item"].ElementType, Is.EqualTo(Expected));
        if (Expected == EnterpriseElementType.Junction)
        {
            EnterpriseJunctionKind ExpectedKind = Token.StartsWith("or", StringComparison.Ordinal)
                ? EnterpriseJunctionKind.Or
                : EnterpriseJunctionKind.And;
            Assert.That(Result.Document.Elements["item"].JunctionKind, Is.EqualTo(ExpectedKind));
        }
    }

    [Test]
    public void Parse_GroupStatement_CreatesGroupingElement()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              group store "Store"
              business-actor clerk "Clerk" in store
            """);
        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Groups.ContainsKey("store"), Is.True);
        Assert.That(Result.Document.Elements["store"].ElementType, Is.EqualTo(EnterpriseElementType.Grouping));
        Assert.That(Result.Document.Elements["clerk"].ParentId, Is.EqualTo("store"));
    }

    [Test]
    public void Parse_DuplicateElementId_ReportsLineNumber()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "One"
              data-object webShop "Two"
            """);

        Assert.That(Result.Diagnostics[0].LineNumber, Is.EqualTo(3));
        Assert.That(Result.Diagnostics[0].Message, Does.Contain("line 3: duplicate element id \"webShop\""));
    }

    [Test]
    public void Parse_SameTypeRedeclaration_UpdatesLabel()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "One"
              application-component webShop "Two"
            """);

        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Elements["webShop"].Label, Is.EqualTo("Two"));
    }

    [Test]
    public void Parse_ExactDuplicateRelationship_ReportsAnError()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "Web Shop"
              data-object order "Order"
              webShop -access(read)-> order
              webShop -access(read)-> order
            """);

        Assert.That(Result.Diagnostics[0].Message, Does.Contain("duplicate relationship"));
        Assert.That(Result.Document.Relationships, Has.Count.EqualTo(1));
    }

    [Test]
    public void Parse_DifferentAccessQualifier_IsNotADuplicate()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "Web Shop"
              data-object order "Order"
              webShop -access(read)-> order
              webShop -access(write)-> order
            """);

        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Relationships, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_UnknownRelationshipEndpoint_ReportsLineNumber()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "Web Shop"
              missing -serving-> webShop
            """);

        Assert.That(Result.Diagnostics[0].LineNumber, Is.EqualTo(3));
        Assert.That(Result.Diagnostics[0].Message, Does.Contain("line 3: relationship source \"missing\" is not a declared element"));
    }

    [Test]
    public void Parse_UnknownInTarget_ReportsLineNumber()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-function checkout "Checkout" in missingParent
            """);

        Assert.That(Result.Diagnostics[0].LineNumber, Is.EqualTo(2));
        Assert.That(Result.Diagnostics[0].Message, Does.Contain("line 2: \"in missingParent\": no such group or element"));
    }

    [Test]
    public void Parse_CountsBlankAndCommentLines_ForErrorLineNumbers()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise

              %% a comment
              application-component webShop "Web Shop"
              unknown-type ghost "Ghost"
            """);

        Assert.That(Result.Diagnostics[0].LineNumber, Is.EqualTo(5));
        Assert.That(Result.Diagnostics[0].Message, Does.StartWith("line 5:"));
    }

    [Test]
    public void Parse_QuotedEscape_UnescapesLabel()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "Web \"Shop\""
            """);

        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Elements["webShop"].Label, Is.EqualTo("Web \"Shop\""));
    }

    [Test]
    public void Parse_ApplicationLayerSample_IsValid()
    {
        EnterpriseParseResult Result = EnterpriseParser.ParseDocument(EnterpriseSamplePaths.ReadSample("01-application-layer.mmd"));
        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Elements, Has.Count.EqualTo(10));
        Assert.That(Result.Document.Relationships, Has.Count.EqualTo(13));
    }

    [Test]
    public void IsEnterpriseSource_AcceptsHeaderAfterComments()
    {
        Assert.That(
            EnterpriseParser.IsEnterpriseSource(
                """
                %% comment
                enterprise
                  application-component a
                """),
            Is.True);
        Assert.That(EnterpriseParser.IsEnterpriseSource("flowchart TD\nA-->B"), Is.False);
    }
    /// <summary>Reports an undeclared endpoint once while retaining the dropped connection's identity for the renderer.</summary>
    [TestCase("missing -serving-> declared", "missing->declared")]
    [TestCase("declared -serving-> missing", "declared->missing")]
    public void Parse_UnknownEndpoint_KeepsRelationshipOnOneDiagnosticLine(string Relationship, string Identity)
    {
        var Result = EnterpriseParser.ParseDocument("enterprise\napplication-component declared \"Declared\"\n" + Relationship);
        Assert.That(Result.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(Result.Diagnostics[0].Message, Does.Contain(Identity));
        Assert.That(Result.Diagnostics[0].Message, Does.Not.Contain("\n"));
    }
}
