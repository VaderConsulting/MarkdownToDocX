using System.Reflection;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Flowchart;

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
        DiagramContract Contract = CreateContract(Direction);
        var State = CreateState(Contract);
        FlowchartLayoutResult Model = Property<FlowchartLayoutResult>(State, "Copy");
        bool YUp = false;
        bool Fitted = false;
        int Boundaries = 0;
        DiagramGridLayout.LayoutStageObserver = (Name, Layout) =>
        {
            if (!ReferenceEquals(Layout, Model))
            {
                return;
            }

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
                if (Stage == "GroupFrameStage")
                {
                    Fitted = true;
                    // Fit an empty frame after node packing so every repair
                    // boundary sees it without exercising the unrelated empty-
                    // group packing convergence fault.
                    Contract.GroupsMutable.Add(CreateEmptyGroup());
                    List<DiagramGridLayout.GridFrame> Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames");
                    Frames.Clear();
                    Invoke("BuildFrames", Contract, Property<List<DiagramGridLayout.GridNode>>(State, "Placed"), Frames);
                }
                if (Stage == "RouteCoordinateStage")
                {
                    YUp = true;
                }

                CheckState(State, Contract, YUp, Stage, Fitted);
                Boundaries++;
            }
            Assert.That(Boundaries, Is.GreaterThan(11), "The test must visit repair boundaries as well as top-level stages.");
            CheckModel(Model);
            Assert.That(FlowchartSvgCoordinateFrameResolver.TryResolve(Contract, Model, out FlowchartSvgCoordinateFrame? Svg), Is.True);
            FlowchartLayoutNodeBounds Frame = Model.ClusterBounds.First();
            (double X, double Y) North = Svg.LayoutToSvg(Frame.CenterX, Frame.CenterY + (Frame.Height / 2));
            (double X, double Y) South = Svg.LayoutToSvg(Frame.CenterX, Frame.CenterY - (Frame.Height / 2));
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
        DiagramContract Contract = CreateContract(Direction);
        var State = CreateState(Contract);
        foreach (string Stage in new[] { "GridPreparationStage", "PlacementHintStage", "NodePlacementStage",
            "GroupFrameStage", "RouteCoordinateStage" })
        {
            InvokeStage(Stage, State);
        }

        FlowchartLayoutResult Model = Property<FlowchartLayoutResult>(State, "Copy");
        List<DiagramGridLayout.GridNode> Placed = Property<List<DiagramGridLayout.GridNode>>(State, "Placed");
        List<DiagramGridLayout.GridFrame> Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames");
        bool CellOwned = Placed.Any(Node => Node.CellNode != null);
        Contract.GroupsMutable.Add(CreateEmptyGroup());
        Frames.Clear();
        Invoke("BuildFramesYUp", Contract, Placed, Frames);
        Invoke("WriteBack", Model, Placed, Frames);
        // Two complete, deliberately shared paths force the rollback branch.
        // Neither has an internal run or a gap-growth candidate. The frame
        // fitter must not be invoked just because separation was attempted.
        Model.EdgeRoutesMutable.Clear();
        foreach (EdgeContract? Edge in Contract.Edges.Take(2))
        {
            FlowchartLayoutEdgeRoute Route = new FlowchartLayoutEdgeRoute { EdgeId = Edge.Id!, SourceId = Edge.SourceId,
                TargetId = Edge.TargetId, ExitSide = "right", EntrySide = "left" };
            Route.Points.Add(new() { X = 100.125, Y = 200.375 });
            Route.Points.Add(new() { X = 400.125, Y = 200.375 });
            Model.EdgeRoutesMutable.Add(Route);
        }
        if (CellOwned)
        {
            // The cell transaction scores completed allocated routes. Finish
            // the fixture exactly as production does before forcing rejection.
            Dictionary<string, NodeContract> Definitions = Property<Dictionary<string, NodeContract>>(State, "Nodes");
            FlowchartDirection Flow = Property<MermaidRecognition.Kinds.Flowchart.FlowchartDirection>(State, "Direction");
            Invoke("AssignPortSides", Contract, Model, Placed, Frames, Definitions, Flow, new HashSet<string>());
            DiagramGridRouteBuilder.RebuildAllRoutes(Model, Placed, Definitions, Contract, Frames, Flow);
            State.GetType().GetProperty("CellFinalRoutingReady")!.SetValue(State, true);
            FlowchartLayoutEdgeRoute Route = Model.EdgeRoutes.First(Route => Route.Points.Count >= 2);
            FlowchartLayoutPoint A = Route.Points[0]; FlowchartLayoutPoint B = Route.Points[1];
            double Length = Math.Sqrt(Math.Pow(B.X - A.X, 2) + Math.Pow(B.Y - A.Y, 2));
            Route.Points.Insert(1, new() { X = A.X + ((B.X - A.X) / Length * 0.125),
                Y = A.Y + ((B.Y - A.Y) / Length * 0.125) });
        }
        Dictionary<string, FlowchartLayoutNodeBounds> Bounds = Model.NodeBounds.Concat(Model.ClusterBounds).ToDictionary(Box => Box.NodeId);
        string Before = Snapshot(State);
        int Rejections = 0;
        DiagramGridLayout.RouteRepairCandidateObserver = (Kind, Subject, Cost, Accepted) =>
        {
            if (CellOwned && Kind == "cell-move")
            {
                Rejections++;
                throw new InvalidOperationException("reject-cell-frame-trial");
            }
            if (Kind == "track-separation" && !Accepted)
            {
                Rejections++;
            }
        };
        try
        {
            if (CellOwned)
            {
                // Cell track repair uses the assignment transaction rather than
                // the retired pixel rollback. Supply one real free-cell frame
                // growth option so the rejection branch is guaranteed to run.
                // This is a fresh isolated search, not extra production budget
                // after the earlier frame-fitting fixture setup.
                FlowchartRenderContext.RepairSearchBudget Budget = Model.RenderContext.MoveBeforeDetourBudget;
                Budget.CandidateMoves = 0; Budget.AcceptedMoves = 0; Budget.AttemptedMoves.Clear();
                IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Placed[0].CellNode!.Grid.Capture().Regions;
                int Delta = Regions.Values.Min(Region => Region.StartRow) - Regions["C"].EndRow - 1;
                FlowchartCellRepair.Assignment? Plan = FlowchartCellRepair.PlanAssignment(Placed, new Dictionary<string, FlowchartCellRegion>
                    { ["C"] = FlowchartCellRepair.Translate(Regions["C"], Delta, 0) });
                Assert.That(Plan, Is.Not.Null);
                Type Repair = typeof(DiagramGridLayout).GetNestedType("RouteRepairStage", BindingFlags.NonPublic)!;
                Type OptionType = Repair.GetNestedType("CellRepairOption", BindingFlags.NonPublic)!;
                var Option = Activator.CreateInstance(OptionType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, ["forced-frame-track-rejection", "C", Plan!], null)!;
                Array Options = Array.CreateInstance(OptionType, 1); Options.SetValue(Option, 0);
                TargetInvocationException? Failure = Assert.Throws<TargetInvocationException>(() => Repair.GetMethod("SearchCellAssignments", HiddenStatic)!
                    .Invoke(null, [State, Options, FlowchartLayoutRules.RepairCandidateMoves, 1, new HashSet<string>(), null, false]));
                Assert.That(Failure!.InnerException!.Message, Is.EqualTo("reject-cell-frame-trial"));
            }
            else
            {
                typeof(DiagramGridLayout).GetNestedType("ModelRouteCompletionStage", BindingFlags.NonPublic)!
                    .GetMethod("SealDistinctTracks", HiddenStatic)!.Invoke(null, [State, Bounds]);
            }
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
        DiagramContract Contract = CreateContract(Direction);
        var State = CreateState(Contract);
        foreach (string Stage in new[] { "GridPreparationStage", "PlacementHintStage", "NodePlacementStage",
            "GroupFrameStage", "RouteCoordinateStage" })
        {
            InvokeStage(Stage, State);
        }

        List<DiagramGridLayout.GridNode> Placed = Property<List<DiagramGridLayout.GridNode>>(State, "Placed");
        List<DiagramGridLayout.GridFrame> Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames");
        int OldHeight = Frames.Single(Frame => Frame.Id == "Outer").Top - Frames.Single(Frame => Frame.Id == "Outer").Bottom;
        DiagramGridLayout.GridNode Member = Placed.Single(Node => Node.Id == "A");
        if (Member.CellNode is { } Owner)
        {
            // A and its right-of dependent B move together above the old
            // envelope. Fit from whole-cell state, never a pixel mutation.
            IReadOnlyDictionary<string, FlowchartCellRegion> Regions = Owner.Grid.Capture().Regions;
            int ExtraRows = (int)Math.Ceiling((OldHeight + DiagramGridMetrics.GRID_UNIT_PX)
                / (Owner.Grid.Configuration.CellHeight + Owner.Grid.Configuration.RowGap));
            int Delta = Regions.Values.Min(Region => Region.StartRow) - Regions["A"].EndRow - ExtraRows;
            Dictionary<string, FlowchartCellRegion> Targets = new[] { "A", "B" }.ToDictionary(Id => Id,
                Id => FlowchartCellRepair.Translate(Regions[Id], Delta, 0));
            Assert.That(FlowchartCellRepair.PlanAssignment(Placed, Targets, AllowHintedRegionRepair: true)!.TryApply(), Is.True);
        }
        else
        {
            Member.SetPositionFromFrameFit(Member.X, Member.Y + OldHeight + DiagramGridMetrics.GRID_UNIT_PX);
        }

        Contract.GroupsMutable.Add(CreateEmptyGroup());
        Frames.Clear();
        Invoke("BuildFramesYUp", Contract, Placed, Frames);
        CheckState(State, Contract, true, "repair frame growth");
        Assert.That(Frames.Single(Frame => Frame.Id == "Outer").Top - Frames.Single(Frame => Frame.Id == "Outer").Bottom,
            Is.GreaterThan(OldHeight));
        FlowchartLayoutResult Model = Property<FlowchartLayoutResult>(State, "Copy");
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

    /// <summary>Places an empty frame beyond the measured fixture, preserving a real group hint through repair and rollback.</summary>
    /// <returns>An empty root frame whose right-of hint prevents incidental overlap with the fixture's nodes.</returns>
    private static GroupContract CreateEmptyGroup() => new() { Id = "Empty", Title = "Empty title",
        PlacementHint = new() { Kind = PlacementHintKind.RightOf, PeerId = "D" } };

    /// <summary>Constructs the real private stage state without adding production-only testing APIs.</summary>
    /// <param name="Contract">The fixture to place.</param>
    /// <returns>The state passed to every real stage implementation.</returns>
    private static object CreateState(DiagramContract Contract)
    {
        Contract.Nodes.Single(Node => Node.Id == "B").PlacementHint = new()
            { Kind = PlacementHintKind.RightOf, PeerId = "A" };
        // Seed measured placement directly: this test exercises the shared
        // stages, including empty frames, rather than Dagre's graph builder.
        FlowchartLayoutResult Raw = new FlowchartLayoutResult();
        int Index = 0;
        foreach (NodeContract Node in Contract.Nodes)
        {
            Raw.NodeBoundsMutable.Add(new() { NodeId = Node.Id, CenterX = 240 * Index++, CenterY = 120,
                Width = 150, Height = 60 });
        }

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
        List<DiagramGridLayout.GridFrame> Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames");
        List<DiagramGridLayout.GridNode> Nodes = Property<List<DiagramGridLayout.GridNode>>(State, "Placed");
        foreach (DiagramGridLayout.GridFrame Frame in Frames)
        {
            Assert.That(Frame.Right, Is.GreaterThan(Frame.Left), $"{Boundary}: {Frame.Id} width");
            Assert.That(YUp ? Frame.Top - Frame.Bottom : Frame.Bottom - Frame.Top, Is.GreaterThan(0), $"{Boundary}: {Frame.Id} height/order");
            if (!CheckEnclosure)
            {
                continue;
            }

            double Low = Math.Min(Frame.Top, Frame.Bottom), High = Math.Max(Frame.Top, Frame.Bottom);
            GroupContract Group = Contract.Groups.Single(Group => Group.Id == Frame.Id);
            foreach (DiagramGridLayout.GridNode? Node in Nodes.Where(Node => Node.GroupId == Frame.Id || Group.ChildNodeIds.Contains(Node.Id)))
            {
                Assert.That(Node.X - (Node.Width / 2.0) >= Frame.Left && Node.X + (Node.Width / 2.0) <= Frame.Right
                    && Node.Y - (Node.Height / 2.0) >= Low && Node.Y + (Node.Height / 2.0) <= High,
                    Is.True, $"{Boundary}: {Frame.Id} contains {Node.Id}");
            }

            foreach (DiagramGridLayout.GridFrame? Child in Frames.Where(Child => Group.ChildGroupIds.Contains(Child.Id)))
            {
                Assert.That(Child.Left >= Frame.Left && Child.Right <= Frame.Right
                    && Math.Min(Child.Top, Child.Bottom) >= Low && Math.Max(Child.Top, Child.Bottom) <= High,
                    Is.True, $"{Boundary}: {Frame.Id} contains nested {Child.Id}");
            }
        }
    }

    /// <summary>Checks that public model dimensions are finite and strictly positive.</summary>
    /// <param name="Model">The authoritative final or restored geometry.</param>
    private static void CheckModel(FlowchartLayoutResult Model)
    {
        foreach (FlowchartLayoutNodeBounds? Box in Model.NodeBounds.Concat(Model.ClusterBounds))
        {
            Assert.That(double.IsFinite(Box.CenterX) && double.IsFinite(Box.CenterY)
                && double.IsFinite(Box.Width) && double.IsFinite(Box.Height) && Box.Width > 0 && Box.Height > 0,
                Is.True, $"{Box.NodeId} must have finite positive geometry.");
        }
    }

    /// <summary>Captures exact immutable geometry for rollback comparison, including fractional port slots.</summary>
    /// <param name="State">The mutable state whose restore is under test.</param>
    /// <returns>A deterministic bit-preserving serialization of the relevant state.</returns>
    private static string Snapshot(object State)
    {
        FlowchartLayoutResult Model = Property<FlowchartLayoutResult>(State, "Copy");
        List<DiagramGridLayout.GridNode> Nodes = Property<List<DiagramGridLayout.GridNode>>(State, "Placed");
        FlowchartLogicalGrid.Snapshot? Cells = Nodes.Any(Node => Node.CellNode != null) ? FlowchartCellRepair.Capture(Nodes) : null;
        return System.Text.Json.JsonSerializer.Serialize(new {
            Cells = Cells == null ? null : new {
                Regions = Cells.Regions.OrderBy(Pair => Pair.Key, StringComparer.Ordinal).ToArray(),
                Columns = Cells.ColumnGaps.OrderBy(Pair => Pair.Key).ToArray(),
                Rows = Cells.RowGaps.OrderBy(Pair => Pair.Key).ToArray() },
            Frames = Property<List<DiagramGridLayout.GridFrame>>(State, "Frames").Select(Frame => new { Frame.Id, Frame.Left, Frame.Right, Frame.Top, Frame.Bottom }),
            Bounds = Model.NodeBounds.Concat(Model.ClusterBounds),
            Routes = Model.EdgeRoutes.Select(Route => new { Route.EdgeId, Route.ExitSide, Route.EntrySide,
                Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY, Route.Points }) });
    }
}
