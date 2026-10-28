using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed class PreviewNodeMoveTests
{
    [TestCase(0.5, 600, 0)]
    [TestCase(2.0, 300, -50)]
    public void DragProjectionPreservesGrabOffsetAcrossZoomRasterScaleAndViewBoxOrigin(double Zoom, int RasterWidth, int Origin)
    {
        using var Host = new Form { ClientSize = new(900, 700), ShowInTaskbar = false };
        using var Preview = new PreviewCanvas { Dock = DockStyle.Fill };
        Host.Controls.Add(Preview); Host.Show(); Application.DoEvents();
        const string Source = "enterprise\napplication-component a\n";
        string Svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"400\" viewBox=\"{Origin} 0 600 400\"><g class=\"element application-component\" data-id=\"a\"><rect x=\"100\" y=\"100\" width=\"150\" height=\"60\"/></g></svg>";
        using var Bitmap = new Bitmap(RasterWidth, RasterWidth * 2 / 3); using var Stream = new MemoryStream();
        Bitmap.Save(Stream, System.Drawing.Imaging.ImageFormat.Png);
        Preview.LoadAccepted(Source, Svg, Stream.ToArray(), null, false);
        Preview.FitToWindow = false; Preview.Zoom = Zoom; Preview.ApplyView();
        var Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
        var A = Preview.Items.Single();
        Assert.That(PreviewSelection.TryGetSvgToClient(Picture.Width, Picture.Height, Bitmap.Width, Bitmap.Height,
            Preview.Index!.Width, Preview.Index.Height, out var Sx, out var Sy, out var Ox, out var Oy), Is.True);
        Point Start = new((int)Math.Round((A.Left + 5) * Sx + Ox), (int)Math.Round((A.Top + 7) * Sy + Oy));
        Point End = new(Start.X + 30, Start.Y + 40);
        PointF? Projected = null;
        Preview.CanMoveNode = _ => true;
        Preview.CreateNodeMoveSnap = _ => Point => { Projected = Point; return Point; };
        typeof(PreviewCanvas).GetMethod("Picture_MouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Preview, [null, new MouseEventArgs(MouseButtons.Left, 1, Start.X, Start.Y, 0)]);
        Assert.That(Preview.IsMovingNode, Is.True);
        typeof(PreviewCanvas).GetMethod("Picture_MouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Preview, [null, new MouseEventArgs(MouseButtons.Left, 1, End.X, End.Y, 0)]);
        Assert.That(Projected, Is.Not.Null);
        Assert.That(Projected!.Value.X, Is.EqualTo((A.Left + A.Right) / 2 + 30 / Sx).Within(0.01));
        Assert.That(Projected.Value.Y, Is.EqualTo((A.Top + A.Bottom) / 2 + 40 / Sy).Within(0.01));
        Preview.CancelNodeMove();
    }

    [TestCase("left-of", -4, -2)]
    [TestCase("right-of", -4, 5)]
    [TestCase("above", -8, 1)]
    [TestCase("below", 0, 1)]
    public void SnapUsesWholeRegionsAndExpandedGapsWithoutChangingTheGrid(string Direction, int Row, int Column)
    {
        const string Source = "enterprise\napplication-component a\napplication-component b\n";
        var Grid = new FlowchartLogicalGrid(new(150, 60, 30, 30, 8));
        Grid.TryPlace("a", new(-4, 1, 2, 2));
        Grid.TryPlace("b", new(5, 8, 2, 1));
        Grid.GrowGap(true, 1, 90, 30);
        Grid.GrowGap(false, -3, 60, 30);
        var A = new DiagramGridLayout.GridNode { Id = "a" }; A.BindCells(Grid);
        var B = new DiagramGridLayout.GridNode { Id = "b" }; B.BindCells(Grid);
        var Layout = new FlowchartLayoutResult { CellNodes = [A, B] };
        static PreviewObject Item(DiagramGridLayout.GridNode Node) => new() { Id = Node.Id, Kind = PreviewObjectKind.Node,
            Left = Node.X - Node.Width / 2d, Top = Node.Y - Node.Height / 2d,
            Right = Node.X + Node.Width / 2d, Bottom = Node.Y + Node.Height / 2d };
        var Items = new[] { Item(A), Item(B) };
        var Before = Grid.Capture();
        var Target = B.CellNode!.Bounds(new(Row, Column, 2, 1), PageCoordinates: true);
        var Planner = new PreviewNodePlacement.GridSnap(Source, Items, Layout);
        var Snap = Planner.Snap("b", new((float)Target.CenterX + 6, (float)Target.CenterY - 7));
        Assert.That(Snap, Is.Not.Null);
        Assert.That(Snap!.Direction, Is.EqualTo(Direction));
        Assert.That(Snap.Offset, Is.EqualTo(2));
        Assert.That(Snap.Center.X, Is.EqualTo(Target.CenterX).Within(0.01));
        Assert.That(Snap.Center.Y, Is.EqualTo(Target.CenterY).Within(0.01));
        Assert.That(PreviewNodePlacement.TryCreate(Source, "b", Items, Snap.Center, out var Edit, out var Reason, Planner), Is.True, Reason);
        Assert.That(Edit!.Hint, Is.EqualTo(Direction + " a +2"));
        Assert.That(Grid.Capture(), Is.SameAs(Before), "Feedback must not mutate occupancy, gaps or routes.");
    }

    [TestCase("escape")]
    [TestCase("capture")]
    [TestCase("view")]
    [TestCase("selection")]
    [TestCase("host")]
    [TestCase("disabled")]
    [TestCase("outside")]
    [TestCase("click")]
    [TestCase("commit")]
    public void GestureHasLightweightSnappedFeedbackAndCommitsOnlyOnceOnAValidRelease(string Action)
    {
        using var Host = new Form { ClientSize = new(800, 600), ShowInTaskbar = false };
        using var Preview = new PreviewCanvas { Dock = DockStyle.Fill };
        Host.Controls.Add(Preview); Host.Show(); Application.DoEvents();
        const string Source = "enterprise\napplication-component a\n";
        const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"600\" height=\"400\"><g class=\"element application-component\" data-id=\"a\"><rect x=\"100\" y=\"100\" width=\"150\" height=\"60\"/></g></svg>";
        using var Bitmap = new Bitmap(600, 400); using var Stream = new MemoryStream();
        Bitmap.Save(Stream, System.Drawing.Imaging.ImageFormat.Png);
        Preview.LoadAccepted(Source, Svg, Stream.ToArray(), null, false);
        Preview.FitToWindow = false; Preview.Zoom = 1; Preview.ApplyView();
        var Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
        var A = Preview.Items.Single();
        Preview.CanMoveNode = _ => true;
        PointF Snapped = new(355, 220);
        Preview.CreateNodeMoveSnap = _ => _ => Snapped;
        int Requests = 0;
        Preview.NodeMoveRequested += (Id, Drop) => { Requests++; Assert.That(Id, Is.EqualTo("a")); Assert.That(Drop, Is.EqualTo(Snapped)); };
        static void Mouse(PreviewCanvas Canvas, string Method, Point Point)
            => typeof(PreviewCanvas).GetMethod(Method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Canvas, [null, new MouseEventArgs(MouseButtons.Left, 1, Point.X, Point.Y, 0)]);
        Point Start = new(175, 130), End = new(350, 220);
        // Exercise the actual event adapter without moving the system pointer.
        Mouse(Preview, "Picture_MouseDown", Start);
        Assert.That(Preview.IsMovingNode, Is.True);
        if (Action != "click") Mouse(Preview, "Picture_MouseMove", End);
        Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
        Assert.That(Requests, Is.Zero);
        switch (Action)
        {
            case "escape": Preview.PressEscape(); break;
            case "capture": Picture.Capture = false; break;
            case "view": Preview.ApplyView(); break;
            case "selection": Preview.SelectedItems = null; break;
            case "host": Preview.CancelNodeMove(); break;
            case "disabled": Preview.CanMoveNode = _ => false; break;
            case "outside": End = new(-1000, -1000); break;
            case "click": End = Start; break;
        }
        Mouse(Preview, "Picture_MouseUp", End);
        Mouse(Preview, "Picture_MouseUp", End);
        Assert.That(Requests, Is.EqualTo(Action == "commit" ? 1 : 0));
        Assert.That(Preview.IsMovingNode, Is.False);
        if (Action == "escape") Assert.That(Preview.SelectedItems!.Single(), Is.SameAs(A));
    }
}
