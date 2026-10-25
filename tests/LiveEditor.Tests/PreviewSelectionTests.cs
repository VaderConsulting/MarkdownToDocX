using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using MDConverter;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Clicking the preview selects the node, container or relationship under the pointer and links it to its source line.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class PreviewSelectionTests
{
    private const string SOURCE = "enterprise\n"
        + "group box \"Box\"\n"
        + "application-component shop \"Shop\" in box\n"
        + "application-component cart \"Cart\" in box\n"
        + "application-service orders \"Orders\"\n"
        + "shop -flow-> cart\n"
        + "orders -serving-> shop\n";

    private const double SHIFT_X = 10;

    private const double SHIFT_Y = 20;

    [Test]
    public void ANodeIsPickedOverItsContainer()
    {
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg(SHIFT_X, true), SOURCE);
        PreviewObject? Hit = Index.HitObject(SHIFT_X + 50, SHIFT_Y + 60, 4);

        Assert.That(Hit, Is.Not.Null);
        Assert.That(Hit!.Kind, Is.EqualTo(PreviewObjectKind.Node));
        Assert.That(Hit.Id, Is.EqualTo("shop"));
        Assert.That(Hit.Outlines.Count, Is.EqualTo(1), "the icon inside the box is not part of the outline");
    }

    [Test]
    public void AContainerIsPickedInItsEmptyArea()
    {
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg(SHIFT_X, true), SOURCE);
        PreviewObject? Hit = Index.HitObject(SHIFT_X + 180, SHIFT_Y + 25, 4);

        Assert.That(Hit, Is.Not.Null);
        Assert.That(Hit!.Kind, Is.EqualTo(PreviewObjectKind.Container));
        Assert.That(Hit.Id, Is.EqualTo("box"));
        Assert.That(Hit.Outlines.Count, Is.EqualTo(2), "the tab and the frame");
        Assert.That(Hit.Outlines[1].Dash, Is.EqualTo(new float[] { 5, 3 }));
    }

    [Test]
    public void ARelationshipIsPickedNearItsStroke()
    {
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg(SHIFT_X, true), SOURCE);
        PreviewObject? Flow = Index.HitObject(SHIFT_X + 100, SHIFT_Y + 62, 4);
        PreviewObject? Serving = Index.HitObject(SHIFT_X + 150, SHIFT_Y + 111, 4);

        Assert.That(Flow, Is.Not.Null);
        Assert.That(Flow!.Kind, Is.EqualTo(PreviewObjectKind.Relationship));
        Assert.That(Flow.SourceId, Is.EqualTo("shop"));
        Assert.That(Flow.TargetId, Is.EqualTo("cart"));
        Assert.That(Flow.RelationshipKind, Is.EqualTo("flow"));
        Assert.That(Flow.Outlines[0].Dash, Is.EqualTo(new float[] { 6, 4 }));
        Assert.That(Serving, Is.Not.Null);
        Assert.That(Serving!.SourceId, Is.EqualTo("orders"));
        Assert.That(Index.HitObject(SHIFT_X + 100, SHIFT_Y + 70, 4)!.Kind, Is.EqualTo(PreviewObjectKind.Container), "too far from the line");
    }

    [Test]
    public void EmptySpaceSelectsNothing()
    {
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg(SHIFT_X, true), SOURCE);

        Assert.That(Index.HitObject(295, 195, 4), Is.Null);
        Assert.That(Index.HitObject(2, 2, 4), Is.Null);
    }

    [Test]
    public void TheSelectionSurvivesARerender()
    {
        PreviewHoverIndex First = PreviewHoverIndex.Build(Svg(SHIFT_X, true), SOURCE);
        PreviewObject Flow = First.HitObject(SHIFT_X + 100, SHIFT_Y + 60, 4)!;
        PreviewObject Shop = First.HitObject(SHIFT_X + 50, SHIFT_Y + 60, 4)!;

        PreviewHoverIndex Moved = PreviewHoverIndex.Build(Svg(SHIFT_X + 15, true), SOURCE);
        PreviewObject? FlowAgain = Moved.FindObject(Flow.Key);
        PreviewObject? ShopAgain = Moved.FindObject(Shop.Key);

        Assert.That(FlowAgain, Is.Not.Null);
        Assert.That(FlowAgain!.LineNumber, Is.EqualTo(6));
        Assert.That(FlowAgain.Left, Is.EqualTo(Flow.Left + 15).Within(0.01));
        Assert.That(ShopAgain, Is.Not.Null);
        Assert.That(ShopAgain!.Id, Is.EqualTo("shop"));

        string WithoutFlow = SOURCE.Replace("shop -flow-> cart\n", "\n", StringComparison.Ordinal);
        PreviewHoverIndex Removed = PreviewHoverIndex.Build(Svg(SHIFT_X, false), WithoutFlow);
        Assert.That(Removed.FindObject(Flow.Key), Is.Null);
        Assert.That(Removed.FindObject(Shop.Key), Is.Not.Null);
    }

    [Test]
    public void TheCaretLineMapsToItsObject()
    {
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg(SHIFT_X, true), SOURCE);

        Assert.That(Index.ObjectAtLine(1), Is.Null);
        Assert.That(Index.ObjectAtLine(2)!.Id, Is.EqualTo("box"));
        Assert.That(Index.ObjectAtLine(3)!.Id, Is.EqualTo("shop"));
        Assert.That(Index.ObjectAtLine(5)!.Id, Is.EqualTo("orders"));
        Assert.That(Index.ObjectAtLine(6)!.RelationshipKind, Is.EqualTo("flow"));
        Assert.That(Index.ObjectAtLine(7)!.RelationshipKind, Is.EqualTo("serving"));
        int Caret = SOURCE.IndexOf("orders -serving", StringComparison.Ordinal) + 3;
        Assert.That(PreviewSelection.LineNumberAt(SOURCE, Caret), Is.EqualTo(7));
    }

    [Test]
    public void AnObjectSelectsItsDeclarationLine()
    {
        string Text = "enterprise\r\n  group box \"Box\"\r\n  shop -flow-> cart\r\n";
        bool Found = PreviewSelection.TryGetLineSelection(Text, 3, out int Start, out int Length);

        Assert.That(Found, Is.True);
        Assert.That(Text.Substring(Start, Length), Is.EqualTo("shop -flow-> cart"));
        Assert.That(PreviewSelection.TryGetLineSelection(Text, 9, out _, out _), Is.False);
    }

    [Test]
    public void BoldDashesFollowTheDashesUnderneath()
    {
        Assert.That(PreviewSelection.DashPattern(new float[] { 6, 4 }, 1.5), Is.EqualTo(new float[] { 3, 2 }));
        Assert.That(PreviewSelection.DashPattern(new float[] { 3 }, 1.0)!.Length, Is.EqualTo(2));
        Assert.That(PreviewSelection.DashPattern(null, 1.0), Is.Null);
    }

    [Test]
    public void ARenderedDiagramHasSelectableObjectsOnTheirLines()
    {
        string Svg;
        bool Rendered = MermaidDiagramExport.TryRenderPreviewSvg(SOURCE, out Svg);
        Assert.That(Rendered, Is.True);
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg, SOURCE);

        PreviewObject? Box = Index.FindObject("node\u001fbox");
        PreviewObject? Shop = Index.FindObject("node\u001fshop");
        Assert.That(Box, Is.Not.Null);
        Assert.That(Box!.Kind, Is.EqualTo(PreviewObjectKind.Container));
        Assert.That(Box.LineNumber, Is.EqualTo(2));
        Assert.That(Shop, Is.Not.Null);
        Assert.That(Shop!.LineNumber, Is.EqualTo(3));
        Assert.That(Index.FindObject("node\u001fcart")!.LineNumber, Is.EqualTo(4));
        Assert.That(Index.FindObject("node\u001forders")!.LineNumber, Is.EqualTo(5));
        Assert.That(Index.ObjectAtLine(6)!.Kind, Is.EqualTo(PreviewObjectKind.Relationship));
        Assert.That(Index.ObjectAtLine(7)!.SourceId, Is.EqualTo("orders"));

        PreviewObject? Centre = Index.HitObject((Shop.Left + Shop.Right) / 2, (Shop.Top + Shop.Bottom) / 2, 4);
        Assert.That(Centre, Is.SameAs(Shop));
    }

    [Test]
    public void FlowchartEdgesAreSelectableAndNodesAreNot()
    {
        string Source = "flowchart LR\nA --> B\n";
        string Svg = "<svg width=\"100\" height=\"40\" viewBox=\"0 0 100 40\">"
            + "<rect x=\"0\" y=\"0\" width=\"10\" height=\"20\"/>"
            + "<path d=\"M10,10 L50,10\" data-edge-source=\"A\" data-edge-target=\"B\"/>"
            + "<line x1=\"55\" y1=\"10\" x2=\"90\" y2=\"10\" data-edge-source=\"A\" data-edge-target=\"B\"/>"
            + "</svg>";
        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg, Source);

        Assert.That(Index.Objects.Count, Is.EqualTo(1));
        PreviewObject? Edge = Index.HitObject(70, 11, 3);
        Assert.That(Edge, Is.Not.Null);
        Assert.That(Edge!.LineNumber, Is.EqualTo(2));
        Assert.That(Index.HitObject(5, 5, 3), Is.Null);
        Assert.That(Index.Hit(70, 11), Is.Null, "hover text stays enterprise-only");
    }

    [Test]
    public void OtherDiagramKindsDoNotThrow()
    {
        string Source = "flowchart TD\nA[Start] --> B{Choice}\nB --> C\n";
        string Svg;
        if (!MermaidDiagramExport.TryRenderPreviewSvg(Source, out Svg))
        {
            Svg = string.Empty;
        }

        PreviewHoverIndex Index = PreviewHoverIndex.Build(Svg, Source);
        foreach (PreviewObject Item in Index.Objects)
        {
            Assert.That(Item.Kind, Is.EqualTo(PreviewObjectKind.Relationship));
        }

        Assert.That(PreviewHoverIndex.Build("<svg width=\"10\" height=\"10\"></svg>", "pie\n\"A\" : 1\n").Objects, Is.Empty);
    }

    [Test]
    public void TheEditorAndPreviewSelectionFollowEachOther()
    {
        SessionSettings Settings = new SessionSettings
        {
            HighlightDelayMilliseconds = 40,
            RenderDelayMilliseconds = 40
        };
        using MainForm Form = new MainForm(Settings);
        Form.ShowInTaskbar = false;
        Form.StartPosition = FormStartPosition.Manual;
        Form.Location = new Point(40, 40);
        Form.ClientSize = new Size(1200, 800);
        Form.Show();
        Application.DoEvents();
        RichTextBox Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = SOURCE;
        Editor.SelectionStart = Editor.Text.IndexOf("application-component shop", StringComparison.Ordinal);
        WaitForCurrentIndex(Form);

        PreviewObject Shop = Form.PreviewIndex!.FindObject("node\u001fshop")!;
        Assert.That(Shop.Selected, Is.False);
        Assert.That(Form.PreviewSelectionObject, Is.Null);
        Form.SelectPreviewObject(Shop);
        Assert.That(Form.PreviewSelectionObject, Is.SameAs(Shop));
        Assert.That(Shop.Selected, Is.True);
        foreach (PreviewObject Item in Form.PreviewIndex.Objects)
        {
            if (!ReferenceEquals(Item, Shop))
            {
                Assert.That(Item.Selected, Is.False);
            }
        }

        Assert.That(Editor.SelectedText, Is.EqualTo("application-component shop \"Shop\" in box"));

        Editor.Select(Editor.Text.IndexOf("shop -flow", StringComparison.Ordinal) + 2, 0);
        Application.DoEvents();
        Assert.That(Form.PreviewSelectionObject, Is.Not.Null);
        Assert.That(Form.PreviewSelectionObject!.RelationshipKind, Is.EqualTo("flow"));
        Assert.That(Form.PreviewSelectionObject.Selected, Is.True);
        Assert.That(Shop.Selected, Is.False);
        string Key = Form.PreviewSelectionObject.Key;

        Editor.Text = SOURCE + "application-component till \"Till\"\n";
        WaitForCurrentIndex(Form);
        Assert.That(Form.PreviewSelectionObject, Is.Not.Null);
        Assert.That(Form.PreviewSelectionObject!.Key, Is.EqualTo(Key));
        Assert.That(Form.PreviewSelectionObject, Is.SameAs(Form.PreviewIndex!.FindObject(Key)));

        PreviewObject Shown = Form.PreviewSelectionObject!;
        Form.PressPreviewEscape();
        Assert.That(Form.PreviewSelectionObject, Is.Null);
        Assert.That(Shown.Selected, Is.False);
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

    /// <summary>
    /// Builds an SVG laid out like the enterprise renderer draws it: a group with two nodes, a node outside it,
    /// and two relationships, all inside one translated group.
    /// </summary>
    private static string Svg(double ShiftX, bool WithFlow)
    {
        string Translate = "translate(" + ShiftX.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + SHIFT_Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        string Flow = WithFlow
            ? "<g class=\"relationship flow\" data-source=\"shop\" data-target=\"cart\">"
                + "<path data-edge-source=\"shop\" data-edge-target=\"cart\" d=\"M80,60 L120,60\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\" stroke-dasharray=\"6 4\"/>"
                + "<path d=\"M114,57 L120,60 L114,63\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"/></g>"
            : string.Empty;
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"300\" height=\"200\">"
            + "<rect width=\"300\" height=\"200\" fill=\"#fff\"/>"
            + "<g transform=\"" + Translate + "\">"
            + "<g class=\"group\" data-id=\"box\">"
            + "<path d=\"M0,0 L60,0 L60,15 L0,15 Z\" fill=\"#fff\" stroke=\"#000\" stroke-width=\"1\" stroke-dasharray=\"5 3\"/>"
            + "<rect fill=\"none\" stroke=\"#000\" stroke-width=\"1\" stroke-dasharray=\"5 3\" x=\"0\" y=\"15\" width=\"200\" height=\"120\"/>"
            + "<text x=\"5\" y=\"11\">Box</text></g>"
            + "<g class=\"element application-component\" data-id=\"shop\">"
            + "<rect fill=\"#b5ffff\" stroke=\"#000\" stroke-width=\"1\" x=\"20\" y=\"40\" width=\"60\" height=\"40\"/>"
            + "<path d=\"M57,43 L77,43 L77,53 L57,53 Z\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"/>"
            + "<text x=\"50\" y=\"65\">Shop</text></g>"
            + "<g class=\"element application-component\" data-id=\"cart\">"
            + "<rect fill=\"#b5ffff\" stroke=\"#000\" stroke-width=\"1\" x=\"120\" y=\"40\" width=\"60\" height=\"40\"/></g>"
            + "<g class=\"element application-service\" data-id=\"orders\">"
            + "<rect fill=\"#b5ffff\" stroke=\"#000\" stroke-width=\"1\" x=\"220\" y=\"40\" width=\"60\" height=\"40\" rx=\"10\" ry=\"10\"/></g>"
            + Flow
            + "<g class=\"relationship serving\" data-source=\"orders\" data-target=\"shop\">"
            + "<path data-edge-source=\"orders\" data-edge-target=\"shop\" d=\"M250,80 L250,110 L50,110 L50,80\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"/></g>"
            + "</g></svg>";
    }
}
