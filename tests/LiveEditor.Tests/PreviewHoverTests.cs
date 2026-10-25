using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MDConverter;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Checks preview hover text for nodes and relationships.
/// </summary>
[TestFixture]
public sealed class PreviewHoverTests
{
    [Test]
    public void NodeHoverNamesTheElementType()
    {
        PreviewHoverIndex Index = Build("application-component shop \"Shop\"\napplication-component cart \"Cart\"\nshop -composition-> cart", "composition");
        Assert.That(Index.Hit(20, 10), Is.EqualTo("Shop is an application component composed of Cart."));
    }

    [Test]
    public void RelationshipHoverUsesAPlainSentence()
    {
        Assert.That(Line("shop -composition-> cart", "composition", "shop", "cart"), Is.EqualTo("Shop is composed of Cart"));
        Assert.That(Line("a -aggregation-> b", "aggregation", "a", "b"), Is.EqualTo("A aggregates B"));
        Assert.That(Line("a -assignment-> b", "assignment", "a", "b"), Is.EqualTo("A is assigned to B"));
        Assert.That(Line("a -realization-> b", "realization", "a", "b"), Is.EqualTo("A realises B"));
        Assert.That(Line("a -serving-> b", "serving", "a", "b"), Is.EqualTo("A serves B"));
        Assert.That(Line("a -access(read)-> b", "access", "a", "b"), Is.EqualTo("A reads B"));
        Assert.That(Line("a -access(write)-> b", "access", "a", "b"), Is.EqualTo("A writes B"));
        Assert.That(Line("a -access(readwrite)-> b", "access", "a", "b"), Is.EqualTo("A reads and writes B"));
        Assert.That(Line("a -access(access)-> b", "access", "a", "b"), Is.EqualTo("A accesses B"));
        Assert.That(Line("a -influence-> b", "influence", "a", "b"), Is.EqualTo("A influences B"));
        Assert.That(Line("a -influence(+)-> b", "influence", "a", "b"), Is.EqualTo("A positively influences B"));
        Assert.That(Line("a -influence(++)-> b", "influence", "a", "b"), Is.EqualTo("A strongly influences B"));
        Assert.That(Line("a -influence(-)-> b", "influence", "a", "b"), Is.EqualTo("A negatively influences B"));
        Assert.That(Line("a -influence(--)-> b", "influence", "a", "b"), Is.EqualTo("A strongly opposes B"));
        Assert.That(Line("a -triggering-> b", "triggering", "a", "b"), Is.EqualTo("A triggers B"));
        Assert.That(Line("a -flow-> b", "flow", "a", "b"), Is.EqualTo("A flows to B"));
        Assert.That(Line("a -specialization-> b", "specialization", "a", "b"), Is.EqualTo("A specialises B"));
        Assert.That(Line("a -association-> b", "association", "a", "b"), Is.EqualTo("A is associated with B"));
        Assert.That(Line("a -association(directed)-> b", "association", "a", "b"), Is.EqualTo("A is directed toward B"));
        Assert.That(Line("a -realization-> b \"implements\"", "realization", "a", "b"), Is.EqualTo("A realises B: implements"));
    }

    [Test]
    public void ANodeWinsWhenThePointerIsInsideIt()
    {
        string Source = "enterprise\ndirection TB\napplication-component shop \"Shop\"\napplication-component cart \"Cart\"\nshop -flow-> cart\n";
        string Svg = "<svg width=\"100\" height=\"100\"><g transform=\"translate(0,0)\">"
            + "<g class=\"element application-component\" data-id=\"shop\"><rect x=\"0\" y=\"0\" width=\"40\" height=\"20\"/></g>"
            + "<g class=\"relationship flow\" data-source=\"shop\" data-target=\"cart\"><path d=\"M10,10 L10,80\"/></g>"
            + "</g></svg>";
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg, Source);
        Assert.That(Index.Hit(10, 10), Is.EqualTo("Shop is an application component. It flows to Cart."));
        Assert.That(Index.Hit(10, 50), Is.EqualTo("Shop flows to Cart"));
    }

    [Test]
    public void PointerOutsideThePictureIsNotAHit()
    {
        double SvgX;
        double SvgY;
        bool Mapped = PreviewHoverIndex.TryMapClientToSvg(5, 5, 200, 200, 100, 50, 100, 50, out SvgX, out SvgY);
        Assert.That(Mapped, Is.False);
        Mapped = PreviewHoverIndex.TryMapClientToSvg(100, 100, 200, 200, 100, 50, 100, 50, out SvgX, out SvgY);
        Assert.That(Mapped, Is.True);
        Assert.That(SvgX, Is.EqualTo(50).Within(0.01));
        Assert.That(SvgY, Is.EqualTo(25).Within(0.01));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void HoverNoteClosesWhenDismissed()
    {
        using Form Host = new Form();
        using PreviewHoverWindow Window = new PreviewHoverWindow();
        Host.Show();
        Window.ShowNear(Host, "Shop is an application component", new Point(200, 200));
        Assert.That(Window.Visible, Is.True);
        Window.HideTip();
        Assert.That(Window.Visible, Is.False);
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void HoverNoteWrapsALongSentence()
    {
        using Form Host = new Form();
        using PreviewHoverWindow Window = new PreviewHoverWindow();
        Host.Show();
        string Text = "Web Shop is an application component realised by Order API, Billing and Portal with an application service provided by Integration. It relies on 3 data objects - Bbb, Ccc and Ddd.";
        Window.ShowNear(Host, Text, new Point(40, 40));
        Assert.That(Window.ClientSize.Width, Is.LessThanOrEqualTo(476));
        Assert.That(Window.ClientSize.Height, Is.GreaterThan(30));
        Window.HideTip();
    }

    [Test]
    public void DestinationNodeDescribesIncomingAndOutgoingRelationships()
    {
        string Source = "enterprise\n"
            + "application-component webShop \"Web Shop\"\n"
            + "application-service orderApi \"Order API\"\n"
            + "application-service billing \"Billing\"\n"
            + "application-interface portal \"Portal\"\n"
            + "application-service integration \"Integration\"\n"
            + "data-object bbb \"Bbb\"\n"
            + "data-object ccc \"Ccc\"\n"
            + "data-object ddd \"Ddd\"\n"
            + "orderApi -realization-> webShop\n"
            + "billing -realization-> webShop\n"
            + "portal -realization-> webShop\n"
            + "integration -serving-> webShop\n"
            + "webShop -access-> bbb\n"
            + "webShop -access-> ccc\n"
            + "webShop -access-> ddd\n";
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Source);
        string Story = ElementStory.DescribeElement(Parsed.Document, Parsed.Document.Elements["webShop"]);
        Assert.That(Story, Is.EqualTo("Web Shop is an application component realised by Billing, Order API and Portal with an application service provided by Integration. It relies on 3 data objects - Bbb, Ccc and Ddd."));
    }

    [Test]
    public void NodeStoryOmitsLabelsThatOnlyRepeatTheVerb()
    {
        string Source = "enterprise\n"
            + "application-component webShop \"Web Shop\"\n"
            + "application-service orderApi \"Order API\"\n"
            + "application-component partner \"Partner\"\n"
            + "business-process checkout \"Checkout\"\n"
            + "application-component neighbour \"Neighbour\"\n"
            + "webShop -serving-> orderApi \"Serves\"\n"
            + "webShop -association(directed)-> partner \"Directed\"\n"
            + "webShop -triggering-> checkout \"Starts\"\n"
            + "webShop -association-> neighbour\n"
            + "orderApi -flow-> checkout \"Moves\"\n";
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Source);
        string Story = ElementStory.DescribeElement(Parsed.Document, Parsed.Document.Elements["webShop"]);
        Assert.That(Story, Does.Not.Contain("(Serves)"));
        Assert.That(Story, Does.Not.Contain("(Directed)"));
        Assert.That(Story, Does.Not.Contain("(Starts)"));
        Assert.That(Story, Does.Contain("serving Order API"));
        Assert.That(Story, Does.Contain("directed toward Partner"));
        Assert.That(Story, Does.Contain("triggers Checkout"));
        string Checkout = ElementStory.DescribeElement(Parsed.Document, Parsed.Document.Elements["checkout"]);
        Assert.That(Checkout, Does.Contain("(Moves)"));
        Assert.That(Checkout, Does.Not.Contain("(Starts)"));
    }

    [Test]
    public void ChangeTypeCanKeepOrDropARelationship()
    {
        string Source = "enterprise\napplication-service api \"Api\"\napplication-interface portal \"Portal\"\napi -realization-> portal\n";
        string Replacement = string.Empty;
        foreach (EnterpriseElementMetadata Metadata in EnterpriseElementCatalog.Elements.Values)
        {
            if (string.Equals(Metadata.KebabName, "application-service", StringComparison.Ordinal))
            {
                continue;
            }

            string Letters = EnterpriseRulesCatalog.Instance.GetAllowedLetters(EnterpriseParser.DEFAULT_VERSION, Metadata.KebabName, "application-interface");
            if (Letters.IndexOf('r') < 0)
            {
                Replacement = Metadata.KebabName;
                break;
            }
        }

        Assert.That(Replacement, Is.Not.Empty);
        string Kept = ElementTypeChange.Apply(Source, "api", Replacement, false);
        Assert.That(Kept, Does.Contain(Replacement + " api"));
        Assert.That(Kept, Does.Contain("realization"));
        string Dropped = ElementTypeChange.Apply(Source, "api", Replacement, true);
        Assert.That(Dropped, Does.Contain(Replacement + " api"));
        Assert.That(Dropped, Does.Not.Contain("realization"));
    }

    [Test]
    public void RenderedPreviewNamesNodesAndRelationships()
    {
        string Source = "enterprise\n"
            + "direction TB\n"
            + "application-component shop \"Shop\"\n"
            + "application-component cart \"Cart\"\n"
            + "application-service api \"Api\"\n"
            + "application-component till \"Till\"\n"
            + "shop -composition-> cart\n"
            + "shop -realization-> api\n"
            + "shop -flow-> till\n";
        string Svg;
        bool Rendered = MermaidDiagramExport.TryRenderPreviewSvg(Source, out Svg);
        Assert.That(Rendered, Is.True);
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg, Source);
        Assert.That(Find(Index, "Shop is an application component composed of Cart, realising Api. It flows to Till."), Is.True);
        Assert.That(Find(Index, "Cart is an application component and is part of Shop."), Is.True);
        Assert.That(Find(Index, "Api is an application service realised by Shop."), Is.True);
        Assert.That(Find(Index, "Till is an application component. It has a flow from Shop."), Is.True);
        Assert.That(Find(Index, "Shop is composed of Cart"), Is.True);
        Assert.That(Find(Index, "Shop realises Api"), Is.True);
        Assert.That(Find(Index, "Shop flows to Till"), Is.True);
    }

    [Test]
    public void EveryElementTypeHasHoverText()
    {
        StringBuilder Source = new StringBuilder();
        Source.Append("enterprise\r\n");
        Source.Append("direction TB\r\n");
        Source.Append("group box \"Box\" {\r\n}\r\n");
        Source.Append("note margin \"Note\" below box\r\n");
        Dictionary<string, string> ExpectedById = new Dictionary<string, string>(StringComparer.Ordinal);
        ExpectedById["box"] = "Box is a grouping";
        ExpectedById["margin"] = "Note is a note";
        string PreviousId = "margin";
        int Number = 0;
        foreach (EnterpriseElementMetadata Metadata in EnterpriseElementCatalog.Elements.Values)
        {
            string Id = "n" + Number.ToString();
            Number++;
            if (Metadata.ElementType == EnterpriseElementType.Junction)
            {
                Source.Append("and-junction ").Append(Id).Append(" \"And\" below ").Append(PreviousId).Append("\r\n");
                ExpectedById[Id] = "And is an and junction";
                PreviousId = Id;
                string OrId = "n" + Number.ToString();
                Number++;
                Source.Append("or-junction ").Append(OrId).Append(" \"Or\" below ").Append(PreviousId).Append("\r\n");
                ExpectedById[OrId] = "Or is an or junction";
                PreviousId = OrId;
                continue;
            }

            string Label = Metadata.KebabName;
            Source.Append(Metadata.KebabName).Append(' ').Append(Id).Append(" \"").Append(Label).Append("\" below ").Append(PreviousId).Append("\r\n");
            PreviousId = Id;
            string TypeName = Metadata.KebabName.Replace('-', ' ');
            string Article = "a";
            char First = TypeName[0];
            if (First == 'a' || First == 'e' || First == 'i' || First == 'o' || First == 'u')
            {
                Article = "an";
            }

            ExpectedById[Id] = Label + " is " + Article + " " + TypeName;
        }

        string Svg;
        bool Rendered = MermaidDiagramExport.TryRenderPreviewSvg(Source.ToString(), out Svg);
        Assert.That(Rendered, Is.True, Svg);
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg, Source.ToString());
        StringBuilder Missing = new StringBuilder();
        int Search = 0;
        while (Search < Svg.Length)
        {
            int Marker = Svg.IndexOf("data-id=\"", Search, StringComparison.Ordinal);
            if (Marker < 0)
            {
                break;
            }

            int IdStart = Marker + "data-id=\"".Length;
            int IdEnd = Svg.IndexOf('"', IdStart);
            string Id = Svg.Substring(IdStart, IdEnd - IdStart);
            int TagEnd = Svg.IndexOf('>', Marker);
            int BodyEnd = MatchingClose(Svg, TagEnd);
            string Body = Svg.Substring(TagEnd + 1, BodyEnd - TagEnd - 1);
            Search = BodyEnd + 4;
            if (!TryCentre(Body, out double CentreX, out double CentreY))
            {
                Missing.AppendLine(Id + " has no measured shape");
                continue;
            }

            string? Text = Index.Hit(CentreX, CentreY);
            string Wanted = ExpectedById.ContainsKey(Id) ? ExpectedById[Id] : Id;
            if (!string.Equals(Text, Wanted, StringComparison.Ordinal))
            {
                Missing.AppendLine(Id + " -> " + (Text ?? "(none)") + " expected " + Wanted);
            }
        }

        Assert.That(Missing.ToString(), Is.EqualTo(string.Empty));
    }

    private static string Line(string Relationship, string Kind, string SourceId, string TargetId)
    {
        string Source = "enterprise\ndirection TB\napplication-component a \"A\"\napplication-component b \"B\"\napplication-component shop \"Shop\"\napplication-component cart \"Cart\"\n" + Relationship + "\n";
        string Svg = "<svg width=\"120\" height=\"80\"><g transform=\"translate(0,0)\">"
            + "<g class=\"relationship " + Kind + "\" data-source=\"" + SourceId + "\" data-target=\"" + TargetId + "\"><path d=\"M0,40 L80,40\"/></g>"
            + "</g></svg>";
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg, Source);
        string? Text = Index.Hit(40, 40);
        return Text ?? string.Empty;
    }

    private static PreviewHoverIndex Build(string Body, string Kind)
    {
        string Source = "enterprise\ndirection TB\n" + Body + "\n";
        string Svg = "<svg width=\"100\" height=\"100\"><g transform=\"translate(10,5)\">"
            + "<g class=\"element application-component\" data-id=\"shop\"><rect x=\"0\" y=\"0\" width=\"40\" height=\"20\"/></g>"
            + "<g class=\"relationship " + Kind + "\" data-source=\"shop\" data-target=\"cart\"><path d=\"M20,20 L20,70\"/></g>"
            + "</g></svg>";
        return PreviewHoverIndex.Build(Svg, Source);
    }

    private static bool Find(PreviewHoverIndex Index, string Expected)
    {
        for (double Y = 0; Y <= Index.Height; Y += 3)
        {
            for (double X = 0; X <= Index.Width; X += 3)
            {
                if (Index.Hit(X, Y) == Expected)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int MatchingClose(string Svg, int TagEnd)
    {
        int Depth = 1;
        int Index = TagEnd + 1;
        while (Index < Svg.Length && Depth > 0)
        {
            int NextOpen = Svg.IndexOf("<g", Index, StringComparison.Ordinal);
            int NextClose = Svg.IndexOf("</g>", Index, StringComparison.Ordinal);
            if (NextClose < 0)
            {
                return Svg.Length;
            }

            if (NextOpen >= 0 && NextOpen < NextClose)
            {
                Depth++;
                Index = NextOpen + 2;
            }
            else
            {
                Depth--;
                if (Depth == 0)
                {
                    return NextClose;
                }

                Index = NextClose + 4;
            }
        }

        return Svg.Length;
    }

    private static bool TryCentre(string Body, out double CentreX, out double CentreY)
    {
        CentreX = 0;
        CentreY = 0;
        int Rect = Body.IndexOf("<rect ", StringComparison.Ordinal);
        if (Rect >= 0)
        {
            int End = Body.IndexOf('>', Rect);
            string Tag = Body.Substring(Rect, End - Rect);
            CentreX = Read(Tag, "x") + (Read(Tag, "width") / 2.0);
            CentreY = Read(Tag, "y") + (Read(Tag, "height") / 2.0);
            return true;
        }

        int Ellipse = Body.IndexOf("<ellipse ", StringComparison.Ordinal);
        if (Ellipse >= 0)
        {
            int End = Body.IndexOf('>', Ellipse);
            string Tag = Body.Substring(Ellipse, End - Ellipse);
            CentreX = Read(Tag, "cx");
            CentreY = Read(Tag, "cy");
            return true;
        }

        int Circle = Body.IndexOf("<circle ", StringComparison.Ordinal);
        if (Circle >= 0)
        {
            int End = Body.IndexOf('>', Circle);
            string Tag = Body.Substring(Circle, End - Circle);
            CentreX = Read(Tag, "cx");
            CentreY = Read(Tag, "cy");
            return true;
        }

        int Path = Body.IndexOf(" d=\"M", StringComparison.Ordinal);
        if (Path >= 0)
        {
            int Start = Path + " d=\"M".Length;
            int End = Body.IndexOf('"', Start);
            string Data = Body.Substring(Start, End - Start);
            string[] Parts = Data.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (Parts.Length >= 2)
            {
                double.TryParse(Parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out CentreX);
                double.TryParse(Parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out CentreY);
                CentreX += 12;
                CentreY += 12;
                return true;
            }
        }

        return false;
    }

    private static double Read(string Tag, string Name)
    {
        string Marker = Name + "=\"";
        int Start = Tag.IndexOf(Marker, StringComparison.Ordinal);
        if (Start < 0)
        {
            return 0;
        }

        Start += Marker.Length;
        int End = Tag.IndexOf('"', Start);
        double Value;
        double.TryParse(Tag.Substring(Start, End - Start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Value);
        return Value;
    }
}
