using System;
using System.Drawing;
using System.Threading;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// A preview is built from a script, or by adding, removing, and editing items. The image is rendered.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class PreviewCanvasModelTests
{
    private const string SCRIPT = "enterprise\n"
        + "application-component shop \"Shop\"\n"
        + "application-component cart \"Cart\"\n"
        + "data-object order \"Order\"\n"
        + "shop -flow-> cart\n"
        + "order -serving-> shop\n";

    [Test]
    public void AScriptBuildsItemsAndTheErrorsThatRelateThem()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        Preview.Load(SCRIPT);

        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        PreviewObject Order = Require(Preview, "node\u001forder");
        PreviewObject Flow = Require(Preview, "line\u001fshop\u001fcart\u001fflow\u001f0");
        PreviewObject Serving = Require(Preview, "line\u001forder\u001fshop\u001fserving\u001f0");

        Assert.That(Preview.Errors.Count, Is.EqualTo(1));
        PreviewError Error = Preview.Errors[0];
        Assert.That(Error.Description, Does.Contain("not allowed"));
        Assert.That(Error.Description, Does.Contain("serving"));
        Assert.That(Error.Nodes, Is.EquivalentTo(new PreviewObject[] { Order, Shop }));
        Assert.That(Error.Relationships, Is.EquivalentTo(new PreviewObject[] { Serving }));
        Assert.That(Error.Nodes, Does.Not.Contain(Cart));
        Assert.That(Error.Relationships, Does.Not.Contain(Flow));
        Assert.That(Shop.Selected, Is.False);
    }

    [Test]
    public void AScriptThatCannotBeDrawnKeepsTheErrorAndNoImage()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        Preview.Load(SCRIPT);

        Assert.That(Preview.Image, Is.Null);
        Assert.That(Preview.HasPicture, Is.False);
        Assert.That(Preview.Errors.Count, Is.EqualTo(1));
        PreviewError Error = Preview.Errors[0];
        Assert.That(Error.Nodes.Count, Is.EqualTo(2));
        Assert.That(Error.Relationships.Count, Is.EqualTo(1));
        Assert.That(Error.Relationships[0].RelationshipKind, Is.EqualTo("serving"));
        Assert.That(Error.Description, Does.Contain("not allowed"));
    }

    [Test]
    public void AMissingEndpointNamesTheRelationshipAndTheDeclaredNode()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        Preview.Load("enterprise\napplication-component shop \"Shop\"\nshop -flow-> gone\n");

        Assert.That(Preview.Errors.Count, Is.EqualTo(1));
        PreviewError Error = Preview.Errors[0];
        Assert.That(Error.Description, Does.Contain("not a declared element"));
        Assert.That(Error.Description, Does.Contain("gone"));
        Assert.That(Error.Nodes.Count, Is.EqualTo(1));
        Assert.That(Error.Nodes[0].Id, Is.EqualTo("shop"));
        Assert.That(Error.Relationships.Count, Is.EqualTo(1));
        Assert.That(Error.Relationships[0].TargetId, Is.EqualTo("gone"));
    }

    [Test]
    public void ItemsCanBeAddedEditedAndRemovedWithoutAScript()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        Assert.That(Preview.Image, Is.Null);
        Assert.That(Preview.Items, Is.Empty);
        Assert.That(Preview.Errors, Is.Empty);

        PreviewObject Shop = Preview.AddItem();
        Shop.Id = "shop";
        Shop.Kind = PreviewObjectKind.Node;
        Shop.LineNumber = 2;
        PreviewObject Flow = Preview.AddItem();
        Flow.Kind = PreviewObjectKind.Relationship;
        Flow.SourceId = "shop";
        Flow.TargetId = "cart";
        Flow.RelationshipKind = "flow";

        Assert.That(Preview.Items, Is.EquivalentTo(new PreviewObject[] { Shop, Flow }));
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Preview.FindItem("node\u001fshop"), Is.SameAs(Shop));

        Shop.Id = "store";
        Assert.That(Preview.FindItem("node\u001fstore"), Is.SameAs(Shop));
        Assert.That(Preview.FindItem("node\u001fshop"), Is.Null);

        Assert.That(Preview.RemoveItem(Shop), Is.True);
        Assert.That(Preview.Items, Is.EquivalentTo(new PreviewObject[] { Flow }));
        Assert.That(Preview.RemoveItem(Shop), Is.False);
        Assert.Throws<ArgumentNullException>(() => Preview.AddItem(null!));
        Assert.Throws<ArgumentNullException>(() => Preview.RemoveItem(null!));
    }

    [Test]
    public void RemovingTheSelectedItemClearsTheSelection()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        PreviewObject Shop = Preview.AddItem();
        Shop.Id = "shop";
        Preview.SelectedItems = new PreviewObject[] { Shop };
        int Events = 0;
        Preview.SelectedItemsChanged += (Sender, E) => Events++;

        Assert.That(Preview.RemoveItem(Shop), Is.True);

        Assert.That(Events, Is.EqualTo(1));
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Shop.Selected, Is.False);
    }

    [Test]
    public void LoadingAScriptReplacesItemsAndErrorsBuiltByHand()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        PreviewObject Handmade = Preview.AddItem();
        Handmade.Id = "handmade";

        Preview.Load(SCRIPT);

        Assert.That(Preview.FindItem("node\u001fhandmade"), Is.Null);
        Assert.That(Preview.FindItem("node\u001fshop"), Is.Not.Null);
        Assert.That(Preview.Errors.Count, Is.EqualTo(1));
        Assert.That(Preview.Errors[0].Description, Does.Contain("not allowed"));
    }

    [Test]
    public void RemovingAnItemDropsItFromTheErrors()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        Preview.Load(SCRIPT);
        PreviewObject Order = Require(Preview, "node\u001forder");
        PreviewError Error = Preview.Errors[0];

        Assert.That(Preview.RemoveItem(Order), Is.True);

        Assert.That(Error.Nodes, Does.Not.Contain(Order));
        Assert.That(Error.Description, Does.Contain("not allowed"));
        Assert.That(Error.Relationships, Is.Not.Empty);
    }

    [Test]
    public void AnAddedItemSurvivesUntilTheNextScript()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        Preview.Load("enterprise\napplication-component shop \"Shop\"\n");
        PreviewObject Extra = Preview.AddItem();
        Extra.Kind = PreviewObjectKind.Node;
        Extra.Id = "extra";

        Assert.That(Preview.FindItem("node\u001fshop"), Is.Not.Null);
        Assert.That(Preview.FindItem("node\u001fextra"), Is.SameAs(Extra));
        Assert.That(Preview.Errors, Is.Empty);
        Assert.That(Preview.Image, Is.Not.Null);
    }

    [Test]
    public void ClearDropsItemsErrorsAndTheSelection()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        Preview.Load(SCRIPT);
        Preview.SelectedItems = new PreviewObject[] { Require(Preview, "node\u001fshop") };

        Preview.Clear();

        Assert.That(Preview.Items, Is.Empty);
        Assert.That(Preview.Errors, Is.Empty);
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Preview.Image, Is.Null);
        Assert.That(Preview.HasPicture, Is.False);
    }

    [Test]
    public void TheSelectionStyleStartsFromTheDiagramSelectionValues()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        int Original = Preview.SelectionStyle.LineColour.ToArgb();

        Assert.That(Original, Is.EqualTo(Preview.SelectionColour.ToArgb()));
        Assert.That(Preview.SelectionStyle.Bold, Is.True);
        Assert.That(Preview.SelectionStyle.Width, Is.EqualTo(PreviewSelection.LINE_WIDTH));
        Assert.That(Preview.SelectionStyle.FillColour.A, Is.EqualTo(0));

        Preview.SelectionColour = Color.FromArgb(0x1E, 0x1E, 0x1E);

        Assert.That(Preview.SelectionColour.ToArgb(), Is.EqualTo(Color.FromArgb(0x1E, 0x1E, 0x1E).ToArgb()));
        Assert.That(Preview.SelectionStyle.LineColour.ToArgb(), Is.EqualTo(Original));

        Preview.SelectionStyle.LineColour = Color.Red;

        Assert.That(Preview.SelectionColour.ToArgb(), Is.EqualTo(Color.FromArgb(0x1E, 0x1E, 0x1E).ToArgb()));
    }

    [Test]
    public void ASelectedItemKeepsItsOwnVisualProperties()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        PreviewObject Shop = Preview.AddItem();
        Shop.Id = "shop";
        Shop.Style.LineColour = Color.Red;
        Shop.Style.FillColour = Color.Blue;
        Shop.Style.Bold = true;
        Shop.Style.Width = 4F;
        Preview.SelectedItems = new PreviewObject[] { Shop };

        Assert.That(Shop.Style.LineColour.ToArgb(), Is.EqualTo(Color.Red.ToArgb()));
        Assert.That(Shop.Style.FillColour.ToArgb(), Is.EqualTo(Color.Blue.ToArgb()));
        Assert.That(Shop.Style.Width, Is.EqualTo(4F));
        Assert.That(Preview.SelectionStyle.LineColour.ToArgb(), Is.Not.EqualTo(Color.Red.ToArgb()));

        Shop.Style.LineColour = Color.Green;

        Assert.That(Shop.Selected, Is.True);
        Assert.That(Shop.Style.LineColour.ToArgb(), Is.EqualTo(Color.Green.ToArgb()));
        Assert.That(Preview.SelectionColour.ToArgb(), Is.Not.EqualTo(Color.Green.ToArgb()));
        Assert.That(Preview.SelectionStyle.LineColour.ToArgb(), Is.Not.EqualTo(Color.Green.ToArgb()));
    }

    private static PreviewObject Require(PreviewCanvas Preview, string Key)
    {
        PreviewObject? Item = Preview.FindItem(Key);
        Assert.That(Item, Is.Not.Null, Key);
        return Item!;
    }

}
