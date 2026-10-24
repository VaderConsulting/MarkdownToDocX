using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using MDConverter;
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
        Assert.That(Index.Hit(20, 10), Is.EqualTo("Shop is an application component"));
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
        Assert.That(Index.Hit(10, 10), Is.EqualTo("Shop is an application component"));
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
        Assert.That(Find(Index, "Shop is an application component"), Is.True);
        Assert.That(Find(Index, "Cart is an application component"), Is.True);
        Assert.That(Find(Index, "Api is an application service"), Is.True);
        Assert.That(Find(Index, "Till is an application component"), Is.True);
        Assert.That(Find(Index, "Shop is composed of Cart"), Is.True);
        Assert.That(Find(Index, "Shop realises Api"), Is.True);
        Assert.That(Find(Index, "Shop flows to Till"), Is.True);
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
}
