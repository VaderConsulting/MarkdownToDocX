using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Selection flags, selection events, and edits that do not require the item to be selected.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class PreviewItemTests
{
    private const string SOURCE = "enterprise\n"
        + "group box \"Box\"\n"
        + "application-component shop \"Shop\" in box\n"
        + "application-component cart \"Cart\" in box\n"
        + "application-service orders \"Orders\"\n"
        + "shop -flow-> cart\n"
        + "orders -serving-> shop\n";

    private const string DIAGRAM = "enterprise\n"
        + "application-component shop \"Shop\"\n"
        + "application-component cart \"Cart\"\n"
        + "application-service orders \"Orders\"\n"
        + "application-component spare \"Spare\"\n"
        + "shop -flow-> cart\n"
        + "orders -serving-> shop\n";

    [Test]
    public void NewItemsAreNotSelected()
    {
        using PreviewCanvas Preview = LoadDiagram();
        Assert.That(Preview.Items, Is.Not.Empty);
        Assert.That(Preview.SelectedItems, Is.Null);
        foreach (PreviewObject Item in Preview.Items)
        {
            Assert.That(Item.Selected, Is.False, Item.Key);
        }
    }

    [Test]
    public void SelectingAnItemRaisesOnceAndClearsThePreviousItem()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        List<PreviewObject[]> Seen = new List<PreviewObject[]>();
        Preview.SelectedItemsChanged += (Sender, E) => Seen.Add(Selection(Preview));

        Preview.SelectedItems = new PreviewObject[] { Shop };
        Preview.SelectedItems = new PreviewObject[] { Shop };
        Preview.SelectedItems = new PreviewObject[] { Cart };
        Preview.SelectedItems = null;
        Preview.SelectedItems = null;

        Assert.That(Seen.Count, Is.EqualTo(3));
        Assert.That(Seen[0], Is.EqualTo(new PreviewObject[] { Shop }));
        Assert.That(Seen[1], Is.EqualTo(new PreviewObject[] { Cart }));
        Assert.That(Seen[2], Is.Empty);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);
        Assert.That(Preview.SelectedItems, Is.Null);
    }

    [Test]
    public void OnlyTheSelectedItemIsMarked()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        Preview.SelectedItems = new PreviewObject[] { Shop };

        foreach (PreviewObject Item in Preview.Items)
        {
            Assert.That(Item.Selected, Is.EqualTo(ReferenceEquals(Item, Shop)), Item.Key);
        }

        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Shop }));
    }

    [Test]
    public void AnItemOutsideTheDiagramCanStillBeSelected()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        Preview.SelectedItems = new PreviewObject[] { Shop };
        PreviewObject Stranger = new PreviewObject
        {
            Kind = PreviewObjectKind.Node,
            Id = "stranger"
        };
        int Events = 0;
        Preview.SelectedItemsChanged += (Sender, E) => Events++;

        Preview.SelectedItems = new PreviewObject[] { Stranger };

        Assert.That(Events, Is.EqualTo(1));
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Stranger }));
        Assert.That(Stranger.Selected, Is.True);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Preview.FindItem(Stranger.Key), Is.Null);
    }

    [Test]
    public void ReloadingKeepsTheSameItemSelectedOnANewInstance()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        Preview.SelectedItems = new PreviewObject[] { Shop };
        List<string?> Seen = new List<string?>();
        Preview.SelectedItemsChanged += (Sender, E) => Seen.Add(SelectionKey(Preview));

        Preview.Load(SOURCE);

        PreviewObject Reloaded = Require(Preview, "node\u001fshop");
        Assert.That(Seen, Is.EqualTo(new string?[] { "node\u001fshop" }));
        Assert.That(Reloaded, Is.Not.SameAs(Shop));
        Assert.That(Reloaded.Selected, Is.True);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Reloaded }));
    }

    [Test]
    public void ReloadingDropsASelectionThatIsNoLongerDrawn()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Flow = Require(Preview, "line\u001fshop\u001fcart\u001fflow\u001f0");
        Preview.SelectedItems = new PreviewObject[] { Flow };
        int Events = 0;
        Preview.SelectedItemsChanged += (Sender, E) => Events++;

        Preview.Load(SourceWithoutFlow());

        Assert.That(Events, Is.EqualTo(1));
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Flow.Selected, Is.False);
        Assert.That(Preview.FindItem(Flow.Key), Is.Null);
        foreach (PreviewObject Item in Preview.Items)
        {
            Assert.That(Item.Selected, Is.False, Item.Key);
        }
    }

    [Test]
    public void ClearRemovesEveryItemAndClearsTheSelectionOnce()
    {
        using PreviewCanvas Preview = LoadDiagram();
        Preview.SelectedItems = new PreviewObject[] { Require(Preview, "node\u001fbox") };
        PreviewObject Box = Preview.SelectedItems[0];
        int Events = 0;
        Preview.SelectedItemsChanged += (Sender, E) => Events++;

        Preview.Clear();
        Preview.Clear();

        Assert.That(Events, Is.EqualTo(1));
        Assert.That(Preview.Items, Is.Empty);
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Preview.HasPicture, Is.False);
        Assert.That(Box.Selected, Is.False);
    }

    [Test]
    public void AnEmptyScriptSelectsNothingAndHasNoImage()
    {
        using PreviewCanvas Preview = new PreviewCanvas();
        int Events = 0;
        Preview.SelectedItemsChanged += (Sender, E) => Events++;

        Preview.Load(null);
        Preview.Load(string.Empty);

        Assert.That(Events, Is.EqualTo(0));
        Assert.That(Preview.Items, Is.Empty);
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Preview.Image, Is.Null);
    }

    [Test]
    public void EscapeIsReportedWithoutClearingTheSelectionItself()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Orders = Require(Preview, "node\u001forders");
        Preview.SelectedItems = new PreviewObject[] { Orders };
        int Escapes = 0;
        int SelectionEvents = 0;
        Preview.EscapePressed += (Sender, E) => Escapes++;
        Preview.SelectedItemsChanged += (Sender, E) => SelectionEvents++;

        Preview.PressEscape();

        Assert.That(Escapes, Is.EqualTo(1));
        Assert.That(SelectionEvents, Is.EqualTo(0));
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Orders }));
        Assert.That(Orders.Selected, Is.True);
    }

    [Test]
    public void ZoomEventsFollowTheViewCommandsAndNotAStoredValue()
    {
        using PreviewCanvas Bare = new PreviewCanvas();
        int BareEvents = 0;
        Bare.ZoomChanged += (Sender, E) => BareEvents++;
        Bare.Fit();
        Bare.NavigationEnabled = false;
        Bare.ZoomIn();
        Bare.ZoomOut();
        Assert.That(BareEvents, Is.EqualTo(0));

        using PreviewCanvas Preview = LoadDiagram();
        int Events = 0;
        Preview.ZoomChanged += (Sender, E) => Events++;
        Preview.NavigationEnabled = false;
        Preview.ZoomIn();
        Preview.Fit();
        Assert.That(Events, Is.EqualTo(0));

        Preview.NavigationEnabled = true;
        Preview.FitToWindow = false;
        Preview.Zoom = 3;
        Assert.That(Events, Is.EqualTo(0));
        Assert.That(Preview.Zoom, Is.EqualTo(3));
        Preview.ZoomIn();
        Assert.That(Events, Is.EqualTo(1));
        Assert.That(Preview.FitToWindow, Is.False);
        Assert.That(Preview.Zoom, Is.GreaterThan(3));
        Preview.ZoomOut();
        Assert.That(Events, Is.EqualTo(2));
        Preview.Fit();
        Assert.That(Events, Is.EqualTo(3));
        Assert.That(Preview.FitToWindow, Is.True);
    }

    [Test]
    public void CtrlClickTogglesItemsWithoutClearingTheOthers()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        PreviewObject Orders = Require(Preview, "node\u001forders");
        List<PreviewObject[]> Seen = new List<PreviewObject[]>();
        Preview.SelectedItemsChanged += (Sender, E) => Seen.Add(Selection(Preview));

        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        Preview.ToggleItem(Shop);

        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.True);
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Cart }));
        Assert.That(Seen.Count, Is.EqualTo(3));
        Assert.That(Seen[0], Is.EqualTo(new PreviewObject[] { Shop }));
        Assert.That(Seen[1], Is.EqualTo(new PreviewObject[] { Shop, Cart }));
        Assert.That(Seen[2], Is.EqualTo(new PreviewObject[] { Cart }));

        Preview.ToggleItem(Cart);
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Cart.Selected, Is.False);
        Assert.That(Seen[3], Is.Empty);

        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        Preview.SelectedItems = new PreviewObject[] { Orders };
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);
        Assert.That(Orders.Selected, Is.True);
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Orders }));
        foreach (PreviewObject Item in Preview.Items)
        {
            Assert.That(Item.Selected, Is.EqualTo(ReferenceEquals(Item, Orders)), Item.Key);
        }
    }

    [Test]
    public void ChoosingTheCurrentItemAgainDropsTheOtherSelections()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        int Events = 0;
        Preview.SelectedItemsChanged += (Sender, E) => Events++;

        Preview.SelectedItems = new PreviewObject[] { Cart };

        Assert.That(Events, Is.EqualTo(1));
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Cart }));
        Assert.That(Cart.Selected, Is.True);
        Assert.That(Shop.Selected, Is.False);
    }

    [Test]
    public void RemovingTheCurrentItemKeepsTheOtherSelections()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        List<PreviewObject[]> Seen = new List<PreviewObject[]>();
        Preview.SelectedItemsChanged += (Sender, E) => Seen.Add(Selection(Preview));

        Assert.That(Preview.RemoveItem(Cart), Is.True);

        Assert.That(Shop.Selected, Is.True);
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Shop }));
        Assert.That(Cart.Selected, Is.False);
        Assert.That(Seen.Count, Is.EqualTo(1));
        Assert.That(Seen[0], Is.EqualTo(new PreviewObject[] { Shop }));

        PreviewObject Orders = Require(Preview, "node\u001forders");
        Preview.ToggleItem(Orders);
        Seen.Clear();
        Assert.That(Preview.RemoveItem(Shop), Is.True);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Orders.Selected, Is.True);
        Assert.That(Seen.Count, Is.EqualTo(1));
        Assert.That(Seen[0], Is.EqualTo(new PreviewObject[] { Orders }));
    }

    [Test]
    public void ReloadingKeepsEverySelectedItem()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);

        Preview.Load(SOURCE);

        PreviewObject ReloadedShop = Require(Preview, "node\u001fshop");
        PreviewObject ReloadedCart = Require(Preview, "node\u001fcart");
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);
        Assert.That(ReloadedShop.Selected, Is.True);
        Assert.That(ReloadedCart.Selected, Is.True);
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { ReloadedShop, ReloadedCart }));
    }

    [Test]
    public void ClickingTheMarginAroundTheDiagramClearsTheSelection()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        Preview.FitToWindow = false;
        Preview.Zoom = 0.10;
        Preview.Size = new Size(700, 700);
        Preview.Refresh();

        Panel Viewport = (Panel)Preview.Controls.Find("Viewport", true)[0];
        PictureBox Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
        Assert.That(Picture.Width, Is.LessThan(Viewport.ClientSize.Width));
        Assert.That(Picture.Height, Is.LessThan(Viewport.ClientSize.Height));

        MethodInfo? OnMouseDown = typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(OnMouseDown, Is.Not.Null);
        OnMouseDown!.Invoke(Viewport, new object[] { new MouseEventArgs(MouseButtons.Left, 1, Viewport.ClientSize.Width - 2, Viewport.ClientSize.Height - 2, 0) });

        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);

        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        Preview.FitToWindow = true;
        Preview.Refresh();
        Assert.That(Preview.IsOnDiagram(new Point(1, 1)), Is.False);
        OnMouseDown.Invoke(Picture, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0) });

        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);
    }

    [Test]
    public void TheDiagramMenuClearsRefreshesAndCopiesAPng()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        PictureBox Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
        Panel Viewport = (Panel)Preview.Controls.Find("Viewport", true)[0];
        Assert.That(Picture.ContextMenuStrip, Is.Not.Null);
        Assert.That(Viewport.ContextMenuStrip, Is.SameAs(Picture.ContextMenuStrip));
        ContextMenuStrip Menu = Picture.ContextMenuStrip!;
        ToolStripMenuItem Clear = (ToolStripMenuItem)Menu.Items[0];
        ToolStripMenuItem Refresh = (ToolStripMenuItem)Menu.Items[1];
        ToolStripMenuItem Copy = (ToolStripMenuItem)Menu.Items[2];
        Assert.That(Clear.Text, Is.EqualTo("Clear"));
        Assert.That(Refresh.Text, Is.EqualTo("Refresh"));
        Assert.That(Copy.Text, Is.EqualTo("Save to Clipboard (.png)"));

        int Refreshes = 0;
        Preview.RefreshRequested += (Sender, E) => Refreshes++;
        Refresh.PerformClick();
        Assert.That(Refreshes, Is.EqualTo(1));
        Assert.That(Preview.SelectedItems, Is.EqualTo(new PreviewObject[] { Shop, Cart }));

        Clear.PerformClick();
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);

        Copy.PerformClick();
        Assert.That(Clipboard.ContainsImage(), Is.True);
        bool Found = Clipboard.TryGetData("PNG", out MemoryStream? Saved);
        Assert.That(Found, Is.True);
        Assert.That(Saved, Is.Not.Null);
        Assert.That(Saved!.Length, Is.GreaterThan(8));
        Saved.Dispose();
    }

    [Test]
    public void SelectingOffScreenCentresTheItemAndAVisibleItemStaysPut()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        Preview.FitToWindow = false;
        Preview.Zoom = 8;
        Preview.Size = new Size(180, 180);
        Preview.CreateControl();
        Preview.Refresh();
        Panel Viewport = (Panel)Preview.Controls.Find("Viewport", true)[0];
        PictureBox Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
        Assert.That(Picture.Width, Is.GreaterThan(Viewport.ClientSize.Width));
        Assert.That(Picture.Height, Is.GreaterThan(Viewport.ClientSize.Height));

        Viewport.AutoScrollPosition = new Point(Picture.Width, Picture.Height);
        Point Parked = ScrollOf(Viewport);
        Preview.ScrollItemIntoView(Shop, Centre: false);
        Point Centred = ScrollOf(Viewport);
        Assert.That(Centred, Is.Not.EqualTo(Parked));

        Preview.ScrollItemIntoView(Shop, Centre: false);
        Assert.That(ScrollOf(Viewport), Is.EqualTo(Centred));

        Viewport.AutoScrollPosition = new Point(0, 0);
        Preview.ScrollItemIntoView(Shop, Centre: true);
        Assert.That(ScrollOf(Viewport), Is.EqualTo(Centred));
    }

    [Test]
    public void ClickingEmptySpaceClearsAMultipleSelection()
    {
        using PreviewCanvas Preview = LoadDiagram();
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);

        Preview.ApplyClick(null, Extend: true);

        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);

        Preview.ToggleItem(Shop);
        Preview.ToggleItem(Cart);
        Preview.ApplyClick(null, Extend: false);

        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Cart.Selected, Is.False);
    }

    [Test]
    public void ToggleRefusesAMissingItem()
    {
        using PreviewCanvas Preview = LoadDiagram();
        Assert.Throws<ArgumentNullException>(() => Preview.ToggleItem(null!));
    }

    [Test]
    public void ReplacingALineDoesNotRequireTheItemToBeSelected()
    {
        PreviewObject Cart = new PreviewObject
        {
            Kind = PreviewObjectKind.Node,
            Id = "cart",
            LineNumber = 4
        };
        string Updated = PreviewItemEdit.ReplaceDeclaration(SOURCE, Cart, "application-component cart \"Basket\" in box");

        Assert.That(Cart.Selected, Is.False);
        Assert.That(Updated, Does.Contain("application-component cart \"Basket\" in box"));
        Assert.That(Updated, Does.Not.Contain("application-component cart \"Cart\" in box"));
        Assert.That(Updated, Does.Contain("application-component shop \"Shop\" in box"));
        Assert.That(PreviewItemEdit.ReplaceDeclaration(SOURCE, Cart, "application-component cart \"Cart\" in box"), Is.SameAs(SOURCE));
    }

    [Test]
    public void ReplacingALineRefusesAMissingOrBrokenTarget()
    {
        PreviewObject Named = new PreviewObject { Kind = PreviewObjectKind.Node, Id = "shop" };
        PreviewObject Missing = new PreviewObject { Kind = PreviewObjectKind.Node, Id = "gone", LineNumber = 40 };
        PreviewObject Relationship = new PreviewObject
        {
            Kind = PreviewObjectKind.Relationship,
            SourceId = "shop",
            TargetId = "cart",
            RelationshipKind = "flow"
        };

        Assert.Throws<ArgumentNullException>(() => PreviewItemEdit.ReplaceDeclaration(SOURCE, null!, "x"));
        Assert.That(PreviewItemEdit.ReplaceDeclaration(null, Named, "application-component shop \"Shop\""), Is.EqualTo(string.Empty));
        Assert.That(PreviewItemEdit.ReplaceDeclaration(SOURCE, Named, null), Is.SameAs(SOURCE));
        Assert.That(PreviewItemEdit.ReplaceDeclaration(SOURCE, Named, "one\ntwo"), Is.SameAs(SOURCE));
        Assert.That(PreviewItemEdit.ReplaceDeclaration(SOURCE, Missing, "nope"), Is.SameAs(SOURCE));
        Assert.That(PreviewItemEdit.ReplaceDeclaration(SOURCE, Relationship, "shop -flow-> cart"), Is.SameAs(SOURCE));
        Assert.That(PreviewItemEdit.ReplaceDeclaration(SOURCE, Named, "application-component shop \"Store\" in box"), Does.Contain("shop \"Store\""));
    }

    [Test]
    public void RemovingANodeByIdLeavesTheOtherDeclarations()
    {
        PreviewObject Orders = new PreviewObject { Kind = PreviewObjectKind.Node, Id = "orders" };
        string Updated = PreviewItemEdit.Remove(SOURCE, Orders);

        Assert.That(Orders.Selected, Is.False);
        Assert.That(Updated, Does.Not.Contain("application-service orders"));
        Assert.That(Updated, Does.Contain("orders -serving-> shop"));
        Assert.That(Updated, Does.Contain("shop \"Shop\""));
        Assert.That(Updated, Does.Contain("cart \"Cart\""));
        Assert.That(Updated, Does.Contain("shop -flow-> cart"));
    }

    [Test]
    public void RemovingOneOfTwoMatchingRelationshipsRemovesOnlyThatLine()
    {
        string Source = "enterprise\r\napplication-component a \"A\"\r\napplication-component b \"B\"\r\na -flow-> b\r\na -flow-> b\r\n";
        PreviewObject Second = new PreviewObject
        {
            Kind = PreviewObjectKind.Relationship,
            SourceId = "a",
            TargetId = "b",
            RelationshipKind = "flow",
            LineNumber = 5,
            Occurrence = 1
        };
        PreviewObject Unnumbered = new PreviewObject
        {
            Kind = PreviewObjectKind.Relationship,
            SourceId = "a",
            TargetId = "b",
            RelationshipKind = "flow",
            Occurrence = 1
        };

        string Updated = PreviewItemEdit.Remove(Source, Second);

        Assert.That(Updated, Does.Contain("\r\n"));
        Assert.That(CountOf(Updated, "a -flow-> b"), Is.EqualTo(1));
        Assert.That(PreviewItemEdit.Remove(Source, Unnumbered), Is.SameAs(Source));
        Assert.That(PreviewItemEdit.Remove(Source, new PreviewObject { Kind = PreviewObjectKind.Node, Id = "missing" }), Is.SameAs(Source));
        Assert.Throws<ArgumentNullException>(() => PreviewItemEdit.Remove(Source, null!));
    }

    [Test]
    public void ChangingAnElementTypeDoesNotRequireASelectionAndCanDropARelationship()
    {
        string Source = "enterprise\napplication-service api \"Api\"\napplication-interface portal \"Portal\"\napi -realization-> portal\n";
        string Replacement = TypeThatCannotRealizeAPortal();
        PreviewObject Api = new PreviewObject
        {
            Kind = PreviewObjectKind.Node,
            Id = "api"
        };

        string Kept = PreviewItemEdit.ChangeType(Source, Api, Replacement, false);
        string Dropped = PreviewItemEdit.ChangeType(Source, Api, Replacement, true);

        Assert.That(Api.Selected, Is.False);
        Assert.That(Kept, Does.Contain(Replacement + " api"));
        Assert.That(Kept, Does.Contain("realization"));
        Assert.That(Dropped, Does.Contain(Replacement + " api"));
        Assert.That(Dropped, Does.Not.Contain("realization"));
        Assert.That(PreviewItemEdit.ChangeType(Source, Api, "application-service", false), Is.SameAs(Source));
        Assert.That(PreviewItemEdit.ChangeType(Source, Api, "  ", false), Is.SameAs(Source));
    }

    [Test]
    public void ChangingARelationshipKindKeepsItsQualifierAndLength()
    {
        string Source = "enterprise\napplication-component a \"A\"\napplication-component b \"B\"\na -access(write)---> b\n";
        PreviewObject Access = new PreviewObject
        {
            Kind = PreviewObjectKind.Relationship,
            SourceId = "a",
            TargetId = "b",
            RelationshipKind = "access",
            LineNumber = 4
        };
        PreviewObject Plain = new PreviewObject
        {
            Kind = PreviewObjectKind.Relationship,
            RelationshipKind = "connection",
            LineNumber = 4
        };
        PreviewObject Group = new PreviewObject
        {
            Kind = PreviewObjectKind.Container,
            Id = "box"
        };

        string Updated = PreviewItemEdit.ChangeType(Source, Access, "serving", false);

        Assert.That(Updated, Does.Contain("a -serving(write)---> b"));
        Assert.That(Updated, Does.Not.Contain("access"));
        Assert.That(PreviewItemEdit.ChangeType(Source, Access, "access", false), Is.SameAs(Source));
        Assert.That(PreviewItemEdit.ChangeType(Source, Plain, "flow", false), Is.SameAs(Source));
        Assert.That(PreviewItemEdit.ChangeType(SOURCE, Group, "application-component", false), Is.SameAs(SOURCE));
        Assert.Throws<ArgumentNullException>(() => PreviewItemEdit.ChangeType(Source, null!, "flow", false));
    }

    [Test]
    public void TheEditorCanRetypeRemoveAndReplaceAnItemThatIsNotSelected()
    {
        using MainForm Form = OpenForm();
        RichTextBox Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = DIAGRAM;
        WaitForCurrentIndex(Form);
        PreviewCanvas Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Form.SelectPreviewObject(Shop);
        List<string?> Seen = new List<string?>();
        Preview.SelectedItemsChanged += (Sender, E) => Seen.Add(SelectionKey(Preview));

        Assert.That(Form.ChangePreviewItemType(null, "business-actor", false), Is.False);
        Assert.That(Form.ChangePreviewItemType(Cart, "application-component", false), Is.False);
        Assert.That(Seen, Is.Empty);

        Assert.That(Form.ChangePreviewItemType(Cart, "application-service", false), Is.True);
        WaitForCurrentIndex(Form);
        Assert.That(Editor.Text, Does.Contain("application-service cart \"Cart\""));
        IReadOnlyList<PreviewObject> Selected = Preview.SelectedItems!;
        Assert.That(Selected.Count, Is.EqualTo(1));
        Assert.That(Selected[0].Key, Is.EqualTo(Shop.Key));
        Assert.That(Selected[0].Selected, Is.True);
        Assert.That(Require(Preview, "node\u001fcart").Selected, Is.False);
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Seen[Seen.Count - 1], Is.EqualTo(Shop.Key));

        Seen.Clear();
        PreviewObject Spare = Require(Preview, "node\u001fspare");
        Assert.That(Spare.Selected, Is.False);
        Assert.That(Form.RemovePreviewItem(Spare), Is.True);
        WaitForCurrentIndex(Form);
        Assert.That(Editor.Text, Does.Not.Contain("spare"));
        Assert.That(Preview.FindItem("node\u001fspare"), Is.Null);
        Assert.That(Preview.FindItem("node\u001forders"), Is.Not.Null);
        Selected = Preview.SelectedItems!;
        Assert.That(Selected[0].Key, Is.EqualTo("node\u001fshop"));
        Assert.That(Selected[0].Selected, Is.True);

        Seen.Clear();
        Cart = Require(Preview, "node\u001fcart");
        Assert.That(Cart.Selected, Is.False);
        Assert.That(Form.ReplacePreviewItem(Cart, "application-service cart \"Basket\""), Is.True);
        WaitForCurrentIndex(Form);
        Assert.That(Editor.Text, Does.Contain("application-service cart \"Basket\""));
        Selected = Preview.SelectedItems!;
        Assert.That(Selected[0].Key, Is.EqualTo("node\u001fshop"));
        Assert.That(Require(Preview, "node\u001fcart").Selected, Is.False);
        Assert.That(Seen[Seen.Count - 1], Is.EqualTo("node\u001fshop"));
    }

    [Test]
    public void RemovingTheSelectedItemClearsTheSelection()
    {
        using MainForm Form = OpenForm();
        RichTextBox Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = DIAGRAM;
        WaitForCurrentIndex(Form);
        PreviewCanvas Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
        PreviewObject Spare = Require(Preview, "node\u001fspare");
        Form.SelectPreviewObject(Spare);
        List<string?> Seen = new List<string?>();
        Preview.SelectedItemsChanged += (Sender, E) => Seen.Add(SelectionKey(Preview));

        Assert.That(Form.RemovePreviewItem(Spare), Is.True);
        WaitForCurrentIndex(Form);

        Assert.That(Editor.Text, Does.Not.Contain("spare"));
        Assert.That(Editor.Text, Does.Contain("cart \"Cart\""));
        Assert.That(Editor.Text, Does.Contain("shop \"Shop\""));
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Preview.FindItem(Spare.Key), Is.Null);
        Assert.That(Spare.Selected, Is.False);
        Assert.That(Seen, Does.Contain(null));
        foreach (PreviewObject Item in Preview.Items)
        {
            Assert.That(Item.Selected, Is.False, Item.Key);
        }
    }

    [Test]
    public void ReplacingTheSelectedDeclarationKeepsThatItemSelected()
    {
        using MainForm Form = OpenForm();
        RichTextBox Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = DIAGRAM;
        WaitForCurrentIndex(Form);
        PreviewCanvas Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
        PreviewObject Cart = Require(Preview, "node\u001fcart");
        Form.SelectPreviewObject(Cart);
        int Events = 0;
        Preview.SelectedItemsChanged += (Sender, E) => Events++;

        Assert.That(Form.ReplacePreviewItem(Cart, "application-component cart \"Basket\""), Is.True);
        WaitForCurrentIndex(Form);

        Assert.That(Editor.Text, Does.Contain("application-component cart \"Basket\""));
        Assert.That(Events, Is.GreaterThan(0));
        IReadOnlyList<PreviewObject> Selected = Preview.SelectedItems!;
        Assert.That(Selected.Count, Is.EqualTo(1));
        Assert.That(Selected[0].Key, Is.EqualTo("node\u001fcart"));
        Assert.That(Selected[0].Selected, Is.True);
        Assert.That(Cart.Selected, Is.False);
        Assert.That(Require(Preview, "node\u001fshop").Selected, Is.False);
    }

    [Test]
    public void ChangingARelationshipKindLeavesTheSelectedNodeAlone()
    {
        using MainForm Form = OpenForm();
        RichTextBox Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = DIAGRAM;
        WaitForCurrentIndex(Form);
        PreviewCanvas Preview = (PreviewCanvas)Form.Controls.Find("Preview", true)[0];
        PreviewObject Shop = Require(Preview, "node\u001fshop");
        PreviewObject Flow = Require(Preview, "line\u001fshop\u001fcart\u001fflow\u001f0");
        Form.SelectPreviewObject(Shop);

        Assert.That(Form.ChangePreviewItemType(Flow, "serving", false), Is.True);
        WaitForCurrentIndex(Form);

        Assert.That(Editor.Text, Does.Contain("shop -serving-> cart"));
        Assert.That(Editor.Text, Does.Not.Contain("shop -flow-> cart"));
        Assert.That(Preview.FindItem(Flow.Key), Is.Null);
        Assert.That(Preview.FindItem("line\u001fshop\u001fcart\u001fserving\u001f0"), Is.Not.Null);
        IReadOnlyList<PreviewObject> Selected = Preview.SelectedItems!;
        Assert.That(Selected.Count, Is.EqualTo(1));
        Assert.That(Selected[0].Key, Is.EqualTo(Shop.Key));
        Assert.That(Selected[0].Selected, Is.True);
        Assert.That(Flow.Selected, Is.False);
        PreviewObject Shown = Selected[0];
        Form.PressPreviewEscape();
        Assert.That(Preview.SelectedItems, Is.Null);
        Assert.That(Shown.Selected, Is.False);
    }

    private static PreviewCanvas LoadDiagram()
    {
        PreviewCanvas Preview = new PreviewCanvas();
        Preview.Load(SOURCE);
        return Preview;
    }

    private static Point ScrollOf(Panel Viewport)
    {
        return new Point(-Viewport.AutoScrollPosition.X, -Viewport.AutoScrollPosition.Y);
    }

    private static PreviewObject[] Selection(PreviewCanvas Preview)
    {
        if (Preview.SelectedItems == null)
        {
            return Array.Empty<PreviewObject>();
        }

        PreviewObject[] Copy = new PreviewObject[Preview.SelectedItems.Count];
        for (int Index = 0; Index < Copy.Length; Index++)
        {
            Copy[Index] = Preview.SelectedItems[Index];
        }

        return Copy;
    }

    private static string? SelectionKey(PreviewCanvas Preview)
    {
        if (Preview.SelectedItems == null || Preview.SelectedItems.Count == 0)
        {
            return null;
        }

        return Preview.SelectedItems[Preview.SelectedItems.Count - 1].Key;
    }

    private static PreviewObject Require(PreviewCanvas Preview, string Key)
    {
        PreviewObject? Item = Preview.FindItem(Key);
        Assert.That(Item, Is.Not.Null, Key);
        return Item!;
    }

    private static MainForm OpenForm()
    {
        SessionSettings Settings = new SessionSettings
        {
            HighlightDelayMilliseconds = 40,
            RenderDelayMilliseconds = 40
        };
        MainForm Form = new MainForm(Settings);
        Form.ShowInTaskbar = false;
        Form.StartPosition = FormStartPosition.Manual;
        Form.Location = new Point(40, 40);
        Form.ClientSize = new Size(1200, 800);
        Form.Show();
        Application.DoEvents();
        return Form;
    }

    private static void WaitForCurrentIndex(MainForm Form)
    {
        DateTime End = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < End && !Form.PreviewIndexIsCurrent)
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }

        Assert.That(Form.PreviewIndexIsCurrent, Is.True);
    }

    private static int CountOf(string Text, string Value)
    {
        int Count = 0;
        int Search = 0;
        while (Search < Text.Length)
        {
            int Found = Text.IndexOf(Value, Search, StringComparison.Ordinal);
            if (Found < 0)
            {
                break;
            }

            Count++;
            Search = Found + Value.Length;
        }

        return Count;
    }

    private static string TypeThatCannotRealizeAPortal()
    {
        foreach (EnterpriseElementMetadata Metadata in EnterpriseElementCatalog.Elements.Values)
        {
            if (string.Equals(Metadata.KebabName, "application-service", StringComparison.Ordinal))
            {
                continue;
            }

            string Letters = EnterpriseRulesCatalog.Instance.GetAllowedLetters(EnterpriseParser.DEFAULT_VERSION, Metadata.KebabName, "application-interface");
            if (Letters.IndexOf('r') < 0)
            {
                return Metadata.KebabName;
            }
        }

        Assert.Fail("No element type is forbidden from realizing an application interface.");
        return string.Empty;
    }

    private static string SourceWithoutFlow()
    {
        return "enterprise\n"
            + "group box \"Box\"\n"
            + "application-component shop \"Shop\" in box\n"
            + "application-component cart \"Cart\" in box\n"
            + "application-service orders \"Orders\"\n"
            + "orders -serving-> shop\n";
    }
}
