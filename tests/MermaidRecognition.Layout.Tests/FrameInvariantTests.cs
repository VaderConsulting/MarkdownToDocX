using System.Reflection;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Checks frame orientation, enclosure and exact restoration at layout and repair boundaries.</summary>
[TestFixture]
[NonParallelizable]
public sealed class FrameInvariantTests
{
    private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;

    /// <summary>Exercises the actual stage implementations and observes their intermediate repair and candidate boundaries.</summary>
    /// <param name="Direction">One of the four declared page flow directions.</param>
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void FramesRemainOrderedAndEncloseMembersAtRepairBoundaries(string Direction)
    {
        var Contract = CreateContract(Direction);
        var State = CreateState(Contract);
        var Model = Property<FlowchartLayoutResult>(State, "Copy");
        bool YUp = false;
        bool Fitted = false;
        int Boundaries = 0;
        DiagramGridLayout.LayoutStageObserver = (Name, Layout) =>
        {
            if (!ReferenceEquals(Layout, Model)) return;
            CheckState(State, Contract, YUp, Name);
            Boundaries++;
        };
        DiagramGridLayout.RouteRepairCandidateObserver = (Kind, Subject, Cost, Accepted) =>
        {
            CheckState(State, Contract, YUp, $"{Kind}:accepted={Accepted}:{Subject}");
            Boundaries++;
        };
        try
        {
            foreach (string Stage in new[] { "GridPreparationStage", "PlacementHintStage", "NodePlacementStage",
                "GroupFrameStage", "RouteCoordinateStage", "JointPortAllocationStage", "RoutingHintStage",
                "RouteConstructionStage", "RouteRepairStage", "ModelRouteCompletionStage", "LegalityValidationStage" })
            {
                TestContext.Progress.WriteLine($"FRAME_STAGE|{Direction}|{Stage}");
                InvokeStage(Stage, State);
                if (Stage == "GroupFrameStage") Fitted = true;
                if (Stage == "RouteCoordinateStage") YUp = true;
                CheckState(State, Contract, YUp, Stage, Fitted);
                Boundaries++;
            }
            Assert.That(Boundaries, Is.GreaterThan(11), "The test must visit repair boundaries as well as top-level stages.");
            CheckModel(Model);
            Assert.That(FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Model, out var Svg), Is.True);
            var Frame = Model.ClusterBounds.First();
            var North = Svg.LayoutToSvg(Frame.CenterX, Frame.CenterY + Frame.Height / 2);
            var South = Svg.LayoutToSvg(Frame.CenterX, Frame.CenterY - Frame.Height / 2);
            Assert.That(North.Y, Is.LessThan(South.Y), "North must paint above south.");
        }
        finally
        {
            DiagramGridLayout.LayoutStageObserver = null;
            DiagramGridLayout.RouteRepairCandidateObserver = null;
        }
    }

    /// <summary>A rejected final track repair must restore all frame boundaries and public bounds bit for bit.</summary>
    /// <param name="Direction">The declared flow direction, independent of the final model's axis convention.</param>
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void RejectedTrackRepairRestoresExactFrameGeometry(string Direction)
    {
        var Contract = CreateContract(Direction);
        var State = CreateState(Contract);
        foreach (string Stage in new[] { "GridPreparationStage", "PlacementHintStage", "NodePlacementStage",
            "GroupFrameStage", "RouteCoordinateStage" }) InvokeStage(Stage, State);
        var Model = Property<FlowchartLayoutResult>(State, "Copy");
        var Placed = Property<List<DiagramGridLayout.GridNode>>(State, "Placed");
        var Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames");
        Invoke("WriteBack", Model, Placed, Frames);
        // Two complete, deliberately shared paths force the rollback branch.
        // Neither has an internal run or a gap-growth candidate. The frame
        // fitter must not be invoked just because separation was attempted.
        Model.EdgeRoutesMutable.Clear();
        foreach (var Edge in Contract.Edges.Take(2))
        {
            var Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId,
                TargetId = Edge.TargetId, ExitSide = "right", EntrySide = "left" };
            Route.Points.Add(new() { X = 100.125, Y = 200.375 });
            Route.Points.Add(new() { X = 400.125, Y = 200.375 });
            Model.EdgeRoutesMutable.Add(Route);
        }
        var Bounds = Model.NodeBounds.Concat(Model.ClusterBounds).ToDictionary(Box => Box.NodeId);
        string Before = Snapshot(State);
        int Rejections = 0;
        DiagramGridLayout.RouteRepairCandidateObserver = (Kind, Subject, Cost, Accepted) =>
        {
            if (Kind == "track-separation" && !Accepted) Rejections++;
        };
        try
        {
            typeof(DiagramGridLayout).GetNestedType("ModelRouteCompletionStage", BindingFlags.NonPublic)!
                .GetMethod("SealDistinctTracks", HiddenStatic)!.Invoke(null, [State, Bounds]);
        }
        finally { DiagramGridLayout.RouteRepairCandidateObserver = null; }
        Assert.That(Rejections, Is.EqualTo(1), "The rejected-candidate rollback must actually execute.");
        Assert.That(Snapshot(State), Is.EqualTo(Before), "Rollback must preserve exact frames, nodes and fractional route coordinates.");
        CheckState(State, Contract, true, "rejected rollback");
        CheckModel(Model);
    }

    /// <summary>Refitting the same final nodes must preserve positive dimensions and nested enclosure even after grid growth.</summary>
    /// <param name="Direction">One of the four declared flow directions.</param>
    [TestCase("TB")]
    [TestCase("BT")]
    [TestCase("LR")]
    [TestCase("RL")]
    public void RepairTimeFrameRefitAndGrowthKeepPositiveHeights(string Direction)
    {
        var Contract = CreateContract(Direction);
        var State = CreateState(Contract);
        foreach (string Stage in new[] { "GridPreparationStage", "PlacementHintStage", "NodePlacementStage",
            "GroupFrameStage", "RouteCoordinateStage" }) InvokeStage(Stage, State);
        var Placed = Property<List<DiagramGridLayout.GridNode>>(State, "Placed");
        var Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames");
        int OldHeight = Frames.Single(Frame => Frame.Id == "Outer").Top - Frames.Single(Frame => Frame.Id == "Outer").Bottom;
        var Member = Placed.Single(Node => Node.Id == "A");
        Member.SetPositionFromFrameFit(Member.X, Member.Y + OldHeight + DiagramGridMetrics.GRID_UNIT_PX);
        Contract.GroupsMutable.Add(new GroupContract { Id = "Empty", Title = "Empty title" });
        Frames.Clear();
        Invoke("BuildFramesYUp", Contract, Placed, Frames);
        CheckState(State, Contract, true, "repair frame growth");
        Assert.That(Frames.Single(Frame => Frame.Id == "Outer").Top - Frames.Single(Frame => Frame.Id == "Outer").Bottom,
            Is.GreaterThan(OldHeight));
        var Model = Property<FlowchartLayoutResult>(State, "Copy");
        Invoke("WriteBack", Model, Placed, Frames);
        CheckModel(Model);
    }

    /// <summary>Builds a nested, empty-group and hinted fixture with two visible links.</summary>
    /// <param name="Direction">The page flow direction to exercise.</param>
    /// <returns>The parsed contract shared by the stage and rollback checks.</returns>
    private static DiagramContract CreateContract(string Direction) => MermaidRecognizer.Parse($"""
        flowchart {Direction}
        subgraph Outer[Outer title]
          subgraph Inner[Inner title]
            A[A]
            B[B]
          end
          C[C]
        end
        D[D]
        A --> C
        B --> D
        """);

    /// <summary>Constructs the real private stage state without adding production-only testing APIs.</summary>
    /// <param name="Contract">The fixture to place.</param>
    /// <returns>The state passed to every real stage implementation.</returns>
    private static object CreateState(DiagramContract Contract)
    {
        Contract.Nodes.Single(Node => Node.Id == "B").PlacementHint = new()
            { Kind = PlacementHintKind.RightOf, PeerId = "A" };
        // Seed measured placement directly: this test exercises the shared
        // stages, including empty frames, rather than Dagre's graph builder.
        var Raw = new FlowchartLayoutResult();
        int Index = 0;
        foreach (var Node in Contract.Nodes)
            Raw.NodeBoundsMutable.Add(new() { NodeId = Node.Id, CenterX = 240 * Index++, CenterY = 120,
                Width = 150, Height = 60 });
        var State = Activator.CreateInstance(typeof(DiagramGridLayout).GetNestedType("GridLayoutState", BindingFlags.NonPublic)!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [Contract, Raw, new FlowchartRenderContext()], null)!;
        State.GetType().GetProperty("SkipRootGroupRepacking")!.SetValue(State, true);
        return State;
    }

    /// <summary>Reads an existing stage-state property through the private state boundary.</summary>
    /// <typeparam name="T">The actual property type.</typeparam>
    /// <param name="State">The real layout state.</param>
    /// <param name="Name">The property to inspect.</param>
    /// <returns>The original property value.</returns>
    private static T Property<T>(object State, string Name) => (T)State.GetType().GetProperty(Name)!.GetValue(State)!;

    /// <summary>Invokes a real stage while preserving the original exception for useful test diagnostics.</summary>
    /// <param name="Name">The nested stage type.</param>
    /// <param name="State">The stage's real state.</param>
    private static void InvokeStage(string Name, object State) => typeof(DiagramGridLayout)
        .GetNestedType(Name, BindingFlags.NonPublic)!.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [State]);

    /// <summary>Invokes a frame helper directly to cover conversions and otherwise forced rollback paths.</summary>
    /// <param name="Name">The private helper name.</param>
    /// <param name="Arguments">Its original typed arguments.</param>
    private static void Invoke(string Name, params object[] Arguments) => typeof(DiagramGridLayout).GetMethod(Name, HiddenStatic)!.Invoke(null, Arguments);

    /// <summary>Checks signed boundaries and enclosure in the coordinate system active at this boundary.</summary>
    /// <param name="State">Mutable frame and member geometry.</param>
    /// <param name="Contract">Authoritative nesting membership.</param>
    /// <param name="YUp">Whether larger Y means north at this stage.</param>
    /// <param name="Boundary">The boundary reported in assertion failures.</param>
    /// <param name="CheckEnclosure">False only before group fitting has established authoritative member enclosure.</param>
    private static void CheckState(object State, DiagramContract Contract, bool YUp, string Boundary, bool CheckEnclosure = true)
    {
        var Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames");
        var Nodes = Property<List<DiagramGridLayout.GridNode>>(State, "Placed");
        foreach (var Frame in Frames)
        {
            Assert.That(Frame.Right, Is.GreaterThan(Frame.Left), $"{Boundary}: {Frame.Id} width");
            Assert.That(YUp ? Frame.Top - Frame.Bottom : Frame.Bottom - Frame.Top, Is.GreaterThan(0), $"{Boundary}: {Frame.Id} height/order");
            if (!CheckEnclosure) continue;
            double Low = Math.Min(Frame.Top, Frame.Bottom), High = Math.Max(Frame.Top, Frame.Bottom);
            var Group = Contract.Groups.Single(Group => Group.Id == Frame.Id);
            foreach (var Node in Nodes.Where(Node => Node.GroupId == Frame.Id || Group.ChildNodeIds.Contains(Node.Id)))
                Assert.That(Node.X - Node.Width / 2.0 >= Frame.Left && Node.X + Node.Width / 2.0 <= Frame.Right
                    && Node.Y - Node.Height / 2.0 >= Low && Node.Y + Node.Height / 2.0 <= High,
                    Is.True, $"{Boundary}: {Frame.Id} contains {Node.Id}");
            foreach (var Child in Frames.Where(Child => Group.ChildGroupIds.Contains(Child.Id)))
                Assert.That(Child.Left >= Frame.Left && Child.Right <= Frame.Right
                    && Math.Min(Child.Top, Child.Bottom) >= Low && Math.Max(Child.Top, Child.Bottom) <= High,
                    Is.True, $"{Boundary}: {Frame.Id} contains nested {Child.Id}");
        }
    }

    /// <summary>Checks that public model dimensions are finite and strictly positive.</summary>
    /// <param name="Model">The authoritative final or restored geometry.</param>
    private static void CheckModel(FlowchartLayoutResult Model)
    {
        foreach (var Box in Model.NodeBounds.Concat(Model.ClusterBounds))
            Assert.That(double.IsFinite(Box.CenterX) && double.IsFinite(Box.CenterY)
                && double.IsFinite(Box.Width) && double.IsFinite(Box.Height) && Box.Width > 0 && Box.Height > 0,
                Is.True, $"{Box.NodeId} must have finite positive geometry.");
    }

    /// <summary>Captures exact immutable geometry for rollback comparison, including fractional port slots.</summary>
    /// <param name="State">The mutable state whose restore is under test.</param>
    /// <returns>A deterministic bit-preserving serialization of the relevant state.</returns>
    private static string Snapshot(object State)
    {
        var Model = Property<FlowchartLayoutResult>(State, "Copy");
        return System.Text.Json.JsonSerializer.Serialize(new {
            Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames").Select(Frame => new { Frame.Id, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom }),
            Bounds = Model.NodeBounds.Concat(Model.ClusterBounds),
            Routes = Model.EdgeRoutes.Select(Route => new { Route.EdgeId, Route.ExitSide, Route.EntrySide,
                Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY, Route.Points }) });
    }
}
