using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed class PreviewRelationshipMoveTests
{
    [TestCase(LayoutHintDirection.North, LayoutHintDirection.North)]
    [TestCase(LayoutHintDirection.East, LayoutHintDirection.West)]
    [TestCase(LayoutHintDirection.South, LayoutHintDirection.North)]
    public void LoopFeedbackStaysOrthogonalAndOutsideItsNode(LayoutHintDirection Leave, LayoutHintDirection Enter)
    {
        PointF Port(LayoutHintDirection Side, int Offset) => Side switch { LayoutHintDirection.North => new(50 + Offset, 0),
            LayoutHintDirection.South => new(50 + Offset, 60), LayoutHintDirection.East => new(100, 30 + Offset), _ => new(0, 30 + Offset) };
        var Box = new RectangleF(0, 0, 100, 60);
        var Path = PreviewEndpointGhost.Loop(Box, Port(Leave, 0), Leave, Port(Enter, 15), Enter);
        for (int Index = 1; Index < Path.Length; Index++)
        {
            Assert.That(Path[Index].X == Path[Index - 1].X || Path[Index].Y == Path[Index - 1].Y, Is.True);
            Assert.That(Box.Contains(new PointF((Path[Index].X + Path[Index - 1].X) / 2, (Path[Index].Y + Path[Index - 1].Y) / 2)), Is.False);
        }
    }
    internal const string Source = "enterprise\napplication-component a\napplication-component b below a +3\n"
        + "application-component c right-of b +3\na -flow-> b \"keep [this] label\"\n";

    internal static (RetainedEnterprisePreview Session, string Svg, PreviewRelationshipMove Move) Fixture(string Script = Source)
    {
        var Session = new RetainedEnterprisePreview(); Session.Observe(1);
        var Initial = Session.Prepare(Script, 1, EnterpriseValidator.Analyze(Script), new() { EnableIncrementalRouting = true });
        Assert.That(Session.TryCommit(Initial), Is.True);
        var Index = PreviewHoverIndex.Build(Initial.Svg, Script);
        var Item = Index.Objects.Single(Item => Item.Kind == PreviewObjectKind.Relationship);
        var Move = PreviewRelationshipMove.Create(Script, Item, Index.Objects, Session.CopyInteractionSnapshot()!);
        Assert.That(Move, Is.Not.Null);
        return (Session, Initial.Svg, Move!);
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public void EndpointCommandsPreserveTheDeclarationAndUseLocalRouting(bool SourceEnd, bool Reconnect)
    {
        string Script = SourceEnd ? Source.Replace("right-of b +3", "left-of a +3") : Source;
        var (Session, Svg, Move) = Fixture(Script);
        var Original = Move.Original(SourceEnd);
        var Target = Move.Targets(SourceEnd).First(Point => Point.NodeId == (Reconnect ? "c" : Original.NodeId)
            && Point.Direction == Original.Direction && Point.Step == (Reconnect ? 0 : 1));
        Assert.That(Move.TryCreateEdit(SourceEnd, Target, out var Edit, out var Reason), Is.True, Reason);
        string Updated = Script[..Edit!.Start] + Edit.Replacement + Script[(Edit.Start + Edit.Length)..];
        Assert.That(Updated, Does.Contain("\"keep [this] label\""));
        var Parsed = EnterpriseValidator.Analyze(Updated);
        Assert.That(Parsed.HasErrors, Is.False);
        Assert.That(Parsed.Document.Relationships.Single().SourceId, Is.EqualTo(SourceEnd ? Target.NodeId : "a"));
        Assert.That(Parsed.Document.Relationships.Single().TargetId, Is.EqualTo(SourceEnd ? "b" : Target.NodeId));
        var Before = Session.CopyInteractionSnapshot()!;
        Session.Observe(2);
        var Candidate = Session.Prepare(Updated, 2, Parsed, new() { EnableIncrementalRouting = true });
        Assert.That(Candidate.Kind, Is.EqualTo("LocalRouting"), Candidate.RebuildReason);
        Assert.That(Candidate.RebuiltRouteIds, Is.EqualTo(new[] { Move.Identity }));
        Assert.That(Session.TryCommit(Candidate), Is.True);
        var After = Session.CopyInteractionSnapshot()!;
        Assert.That(JsonSerializer.Serialize(After.Layout.NodeBounds), Is.EqualTo(JsonSerializer.Serialize(Before.Layout.NodeBounds)));
        Assert.That(FlowchartConnectionPoints.Validate(After.Model, After.Layout, CheckRoutes: true), Is.Null);
        Assert.That(After.Identities.Bindings.Single().Id, Is.EqualTo(Move.Identity));
        Session.Observe(3);
        var Undo = Session.Prepare(Script, 3, EnterpriseValidator.Analyze(Script), new() { EnableIncrementalRouting = true });
        Assert.That(Undo.Kind, Is.EqualTo("LocalRouting"));
        Assert.That(Undo.Svg, Is.EqualTo(Svg));
    }

    [Test]
    public void NoOpAndForeignTargetsProduceNoSourceEdit()
    {
        var (_, _, Move) = Fixture();
        Assert.That(Move.TryCreateEdit(false, Move.Original(false), out _, out _), Is.False);
        Assert.That(Move.TryCreateEdit(false, Move.Original(false) with { NodeId = "missing" }, out _, out _), Is.False);
        Assert.That(Move.Targets(false).Any(Point => Point.NodeId == Move.Original(true).NodeId
            && Point.Direction == Move.Original(true).Direction && Point.Step == Move.Original(true).Step), Is.False,
            "the opposite endpoint's exact point cannot be reused");
    }

    [Test]
    public void ReconnectingIntoALoopRetainsIdentityAndCanReconnectOutAgain()
    {
        var (Session, _, Move) = Fixture();
        var Target = Move.Targets(false).First(Point => Point.NodeId == "a" && Point.Direction == LayoutHintDirection.East && Point.Step == 0);
        Assert.That(Move.TryCreateEdit(false, Target, out var Edit, out var Reason), Is.True, Reason);
        string Updated = Source[..Edit!.Start] + Edit.Replacement + Source[(Edit.Start + Edit.Length)..];
        Session.Observe(2);
        var Candidate = Session.Prepare(Updated, 2, EnterpriseValidator.Analyze(Updated), new() { EnableIncrementalRouting = true });
        Assert.That(Candidate.Kind, Is.EqualTo("LocalRouting"));
        Assert.That(Session.TryCommit(Candidate), Is.True);
        var Index = PreviewHoverIndex.Build(Candidate.Svg, Updated);
        var Loop = PreviewRelationshipMove.Create(Updated, Index.Objects.Single(Item => Item.Kind == PreviewObjectKind.Relationship),
            Index.Objects, Session.CopyInteractionSnapshot()!)!;
        Assert.That(Loop.Identity, Is.EqualTo(Move.Identity));
        Assert.That(Loop.Targets(false).Any(Point => Point.NodeId == "b"), Is.True);
        Assert.That(Loop.TryCreateEdit(false, Loop.Targets(false).First(Point => Point.NodeId == "b" && Point.Direction == LayoutHintDirection.North && Point.Step == 0),
            out var Reconnect, out Reason), Is.True, Reason);
        Updated = Updated[..Reconnect!.Start] + Reconnect.Replacement + Updated[(Reconnect.Start + Reconnect.Length)..];
        Session.Observe(3);
        Candidate = Session.Prepare(Updated, 3, EnterpriseValidator.Analyze(Updated), new() { EnableIncrementalRouting = true });
        Assert.That(Session.TryCommit(Candidate), Is.True);
        Assert.That(Session.CopyInteractionSnapshot()!.Model.Edges.Single().TargetId, Is.EqualTo("b"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PointCommandsKeepPlainConnectionSyntaxAndAccessQualifiers(bool Plain)
    {
        string Script = Plain ? Source.Replace("a -flow-> b \"keep [this] label\"", "a --- b").Replace("application-component b", "note b \"Note\"")
            : Source.Replace("application-component a", "application-service a").Replace("application-component b", "data-object b")
                .Replace("a -flow-> b", "a -access(read)-> b");
        var (_, _, Move) = Fixture(Script);
        var Target = Move.Targets(false).First(Point => Point.NodeId == "b" && Point.Direction == Move.Original(false).Direction && Point.Step == 1);
        Assert.That(Move.TryCreateEdit(false, Target, out var Edit, out _), Is.True);
        Assert.That(Edit!.Replacement, Does.Contain(Plain ? "---" : "-access(read)->"));
        Assert.That(EnterpriseValidator.Analyze(Script[..Edit.Start] + Edit.Replacement + Script[(Edit.Start + Edit.Length)..]).HasErrors, Is.False);
    }

    [TestCase("commit")]
    [TestCase("escape")]
    [TestCase("capture")]
    [TestCase("selection")]
    [TestCase("view")]
    [TestCase("outside")]
    [TestCase("invalid")]
    [TestCase("click")]
    [TestCase("viewport")]
    [TestCase("source")]
    public void EndpointGesturePublishesOnceOrCancelsWithoutChangingTheAcceptedImage(string Action)
    {
        var (_, Svg, Move) = Fixture();
        using var Host = new Form { ClientSize = new(1000, 750), ShowInTaskbar = false };
        using var Preview = new PreviewCanvas { Dock = DockStyle.Fill };
        Host.Controls.Add(Preview); Host.Show(); Application.DoEvents();
        var Index = PreviewHoverIndex.Build(Svg, Source);
        using var Image = new Bitmap((int)Math.Ceiling(Index.Width), (int)Math.Ceiling(Index.Height));
        using var Stream = new MemoryStream(); Image.Save(Stream, System.Drawing.Imaging.ImageFormat.Png);
        byte[] Png = Stream.ToArray();
        Preview.LoadAccepted(Source, Svg, Png, null, false);
        var Line = Preview.Items.Single(Item => Item.Kind == PreviewObjectKind.Relationship);
        Preview.SelectedItems = [Line];
        Preview.CreateRelationshipMove = _ => Move;
        Preview.CanMoveNode = _ => true; // The selected endpoint handle must win over its node body.
        Preview.CreateNodeMoveSnap = _ => Point => Point;
        var Picture = (PictureBox)Preview.Controls.Find("DiagramPicture", true)[0];
        PreviewSelection.TryGetSvgToClient(Picture.Width, Picture.Height, Image.Width, Image.Height, Index.Width, Index.Height,
            out double Sx, out double Sy, out double Ox, out double Oy);
        Point Project(PointF P) => new((int)Math.Round(P.X * Sx + Ox), (int)Math.Round(P.Y * Sy + Oy));
        Point Start = Project(Move.Original(false).Point);
        var Target = Move.Targets(false).First(Point => Point.NodeId == "c" && Point.Direction == LayoutHintDirection.North && Point.Step == 0);
        Point End = Project(Target.Point);
        static void Mouse(PreviewCanvas Canvas, string Method, Point Point) => typeof(PreviewCanvas)
            .GetMethod(Method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Canvas, [null, new MouseEventArgs(MouseButtons.Left, 1, Point.X, Point.Y, 0)]);
        int Requests = 0;
        Preview.EndpointMoveRequested += (_, SourceEnd, Point) =>
        { Requests++; Assert.That(SourceEnd, Is.False); Assert.That(Point, Is.EqualTo(Target)); };
        if (Action == "viewport")
        {
            var Viewport = (Panel)Preview.Controls.Find("Viewport", true)[0];
            Mouse(Preview, "Viewport_MouseDown", Viewport.PointToClient(Picture.PointToScreen(Start)));
        }
        else Mouse(Preview, "Picture_MouseDown", Start);
        Assert.That(Preview.IsMovingEndpoint, Is.True);
        Assert.That(Preview.IsMovingNode, Is.False);
        using var BeforeFeedback = new Bitmap(Preview.Width, Preview.Height);
        if (Action == "commit") Preview.DrawToBitmap(BeforeFeedback, Preview.ClientRectangle);
        if (Action != "click") Mouse(Preview, "Picture_MouseMove", End);
        if (Action == "commit")
        {
            using var Feedback = new Bitmap(Preview.Width, Preview.Height);
            Preview.DrawToBitmap(Feedback, Preview.ClientRectangle);
            int Changed = 0;
            for (int Y = 0; Y < Feedback.Height; Y++)
                for (int X = 0; X < Feedback.Width; X++)
                    if (Feedback.GetPixel(X, Y) != BeforeFeedback.GetPixel(X, Y)) Changed++;
            Assert.That(Changed, Is.GreaterThan(50), "snapped endpoint feedback must be visible");
        }
        Assert.That(Requests, Is.Zero);
        Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
        Assert.That(Preview.LastPreviewPngBytes, Is.EqualTo(Png));
        switch (Action)
        {
            case "escape": Preview.PressEscape(); break;
            case "capture": Picture.Capture = false; break;
            case "selection": Preview.SelectedItems = null; break;
            case "view": Preview.ApplyView(); break;
            case "outside": End = new(-1000, -1000); break;
            case "invalid": End = new(1, 1); break;
            case "click": End = Start; break;
            case "source": Preview.LoadAccepted(Source + "%% updated\n", Svg, Png, null, false); break;
        }
        Mouse(Preview, "Picture_MouseUp", End); Mouse(Preview, "Picture_MouseUp", End);
        Assert.That(Requests, Is.EqualTo(Action is "commit" or "viewport" ? 1 : 0));
        Assert.That(Preview.IsMovingEndpoint, Is.False);
        Assert.That(Preview.RenderedSvg, Is.EqualTo(Svg));
        Assert.That(Preview.LastPreviewPngBytes, Is.EqualTo(Png));
        if (Action == "escape") Assert.That(Preview.SelectedItems!.Single(), Is.SameAs(Line));
    }
}
