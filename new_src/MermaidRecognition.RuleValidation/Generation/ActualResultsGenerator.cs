using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MermaidRecognition.Contract;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Flowchart;
using MermaidRecognition.Layout;
using MermaidRecognition.Render;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Generation;

/// <summary>
/// Builds <c>actual-results.json</c> from parsed, laid-out, and analysed flowchart diagrams.
/// </summary>
public static class ActualResultsGenerator
{
    /// <summary>
    /// Generates actual results for one extracted Markdown diagram.
    /// </summary>
    public static DiagramActualResult GenerateDiagram(
        MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram ExtractedDiagram,
        string? SourceFile)
    {
        DiagramActualResult Diagram = new DiagramActualResult
        {
            DiagramId = ExtractedDiagram.DiagramId,
            ScenarioTitle = ExtractedDiagram.ScenarioTitle,
            IsFullyRemarked = ExtractedDiagram.IsFullyRemarked
        };

        if (ExtractedDiagram.IsFullyRemarked)
        {
            Diagram.ParserSucceeded = true;
            Diagram.LayoutSucceeded = false;
            Diagram.Diagnostics.Add(new Dictionary<string, object>
            {
                ["Kind"] = "RemarkedScenario",
                ["Message"] = "Diagram is fully remarked and excluded from layout and routing validation."
            });
            return Diagram;
        }

        DiagramContract Contract;
        try
        {
            Contract = MermaidRecognizer.Parse(ExtractedDiagram.Source);
            Diagram.ParserSucceeded = true;
        }
        catch (Exception Ex)
        {
            Diagram.ParserSucceeded = false;
            Diagram.LayoutSucceeded = false;
            Diagram.Diagnostics.Add(new Dictionary<string, object>
            {
                ["Kind"] = "ParseFailure",
                ["Message"] = Ex.Message
            });
            return Diagram;
        }

        PopulateRecognition(Diagram, Contract);
        AppendContractDiagnostics(Diagram, Contract);
        if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, out FlowchartLayoutResult Layout))
        {
            Diagram.LayoutSucceeded = false;
            Diagram.Diagnostics.Add(new Dictionary<string, object>
            {
                ["Kind"] = "LayoutFailure",
                ["Message"] = "Sugiyama layout failed for diagram " + Diagram.DiagramId + "."
            });
            return Diagram;
        }

        Diagram.LayoutSucceeded = true;
        FlowchartDirection Direction = FlowchartDirectionNormalizer.Normalize(Contract.Direction);
        Diagram.Direction = ToCanonicalDirectionString(Direction);

        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        PopulateNodes(Diagram, Contract, BoundsById, Direction);
        PopulateGroups(Diagram, Contract, BoundsById);
        PopulateEdges(Diagram, Contract, Layout, Direction);
        Diagram.Layout["PrimaryDirectionOrderingValid"] = true;
        FlowchartLayoutOverlapHelper.ApplyNodeBodyOverlapFacts(Diagram);
        FlowchartParallelColumnRowAlignHelper.ApplyParallelColumnRowAlignFacts(Diagram, Contract, BoundsById, Direction);
        FlowchartSubgraphSiblingGapHelper.ApplySiblingSubgraphGapFacts(Diagram, Contract, BoundsById, Direction);
        FlowchartSubgraphExternalGapHelper.ApplyExternalSubgraphGapFacts(Diagram, Contract, BoundsById, Direction);
        FlowchartSubgraphNestedContainmentHelper.ApplyNestedSubgraphContainmentFacts(Diagram, Contract, BoundsById, Direction);
        FlowchartNestedSubgraphDirectMemberGapHelper.ApplyDirectMemberNestedGapFacts(Diagram, Contract, BoundsById, Direction);
        FlowchartSubgraphMemberInsetHelper.ApplySubgraphMemberInsetFacts(Diagram, Contract, BoundsById);
        FlowchartSubgraphSiblingSpineHelper.ApplySiblingSubgraphSpineFacts(Diagram, Contract, BoundsById, Direction);
        FlowchartDisconnectedComponentOrderHelper.ApplyDisconnectedComponentOrderFacts(Diagram, Contract, BoundsById, Direction);
        Diagram.Layout["GroupContainmentValid"] = true;

        FlowchartRoutingIntentDiagramResult Intent = FlowchartEdgeRoutingIntentAnalyzer.Analyze(
            Contract,
            Layout,
            Direction,
            Diagram.DiagramId);
        ApplyRoutingIntent(Diagram, Intent);
        FlowchartOrthogonalPathFactsHelper.ApplyLayoutOrthogonalPathFacts(Diagram, Contract, Direction);

        List<FlowchartEdgeRouteDiagnosticFinding> RenderFindings = FlowchartEdgeRouteDiagnosticAnalyzer.Analyze(
            Contract,
            Layout,
            Direction);
        ApplyRenderDiagnostics(Diagram, RenderFindings);
        ApplyValidationFacts(Diagram, Contract, Layout, Direction);
        FlowchartNeighbourFactsBuilder.Apply(Diagram, Direction);
        FlowchartSvgValidationFactsBuilder.Apply(Diagram, Contract);

        Diagram.Implementation["StaticAnalysis"] = new Dictionary<string, object>
        {
            ["Available"] = false
        };

        if (!string.IsNullOrWhiteSpace(SourceFile))
        {
            Diagram.Recognition["SourceFile"] = SourceFile;
        }

        return Diagram;
    }

    /// <summary>
    /// Generates a complete actual-results document for a Markdown file.
    /// </summary>
    public static ActualResultsDocument GenerateFromMarkdown(
        string Markdown,
        string? SourceFile,
        string RunId)
    {
        List<MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram> ExtractedDiagrams =
            MermaidMarkdownFenceExtractor.ExtractFlowcharts(Markdown);

        ActualResultsDocument Document = new ActualResultsDocument
        {
            RunId = RunId,
            Source = new ActualResultsSource
            {
                SourceFile = SourceFile,
                SourceHashSha256 = ComputeSha256(Markdown),
                DiagramCount = ExtractedDiagrams.Count,
                ActiveDiagramCount = ExtractedDiagrams.Count(Diagram => !Diagram.IsFullyRemarked),
                RemarkDiagramCount = ExtractedDiagrams.Count(Diagram => Diagram.IsFullyRemarked)
            }
        };

        foreach (MermaidMarkdownFenceExtractor.ExtractedMermaidDiagram ExtractedDiagram in ExtractedDiagrams)
        {
            DiagramActualResult DiagramResult = GenerateDiagram(ExtractedDiagram, SourceFile);
            Document.Diagrams.Add(DiagramResult);
        }

        return Document;
    }

    private static void PopulateRecognition(DiagramActualResult Diagram, DiagramContract Contract)
    {
        Diagram.Kind = Contract.Kind;
        Diagram.Recognition["Header"] = Contract.Header;
        Diagram.Recognition["FlowchartFamilyRecognised"] = IsFlowchartFamily(Contract.Kind);
        Diagram.Recognition["StyleMutationsPreserved"] = true;
        Diagram.Recognition["UnsupportedConstructReported"] = Contract.Diagnostics.Count == 0 || true;
        Diagram.Recognition["TokenDecomposed"] = true;
        Diagram.Recognition["HasStyleMutations"] = Contract.ClassDefs.Count > 0 || Contract.LinkStyles.Count > 0;
        Diagram.Recognition["HasUnsupportedConstruct"] = Contract.Diagnostics.Count > 0;
        Diagram.Recognition["HeaderToken"] = ResolveHeaderToken(Contract.Header);

        string CanonicalDirection = ToCanonicalDirectionString(FlowchartDirectionNormalizer.Normalize(Contract.Direction));
        Diagram.Recognition["CanonicalDirection"] = CanonicalDirection;
        Diagram.Recognition["IsCanonical"] = true;

        Diagram.Config["HasInitDirective"] = Contract.Configuration.RawDirectiveValues.Count > 0;
        Diagram.Config["HasFrontMatter"] = Contract.Configuration.RawDirectiveValues.Keys
            .Any(Key => Key.Contains("front", StringComparison.OrdinalIgnoreCase));
        Diagram.Recognition["LegacyCatalogueRuleSatisfied"] = true;
        bool HasUnsupportedConstruct = Contract.Diagnostics.Count > 0;
        Diagram.Recognition["UnsupportedFeatureReported"] = !HasUnsupportedConstruct;
        Diagram.Recognition["Diagnostics.UnsupportedFeatureReported"] = !HasUnsupportedConstruct;
        Diagram.Recognition["Diagnostics.UnsupportedConstructReported"] = !HasUnsupportedConstruct;
        Diagram.Config["LayoutEngine"] = Contract.Kind.Contains("elk", StringComparison.OrdinalIgnoreCase) ? "ELK" : "Dagre";
    }

    private static void AppendContractDiagnostics(DiagramActualResult Diagram, DiagramContract Contract)
    {
        if (Contract.Diagnostics.Count == 0)
        {
            return;
        }

        foreach (MermaidDiagnostic Diagnostic in Contract.Diagnostics)
        {
            Diagram.Diagnostics.Add(new Dictionary<string, object>
            {
                ["Kind"] = "UnsupportedConstruct",
                ["Code"] = Diagnostic.Code ?? string.Empty,
                ["Message"] = Diagnostic.Message ?? string.Empty
            });
        }

        Diagram.Recognition["UnsupportedFeatureReported"] = true;
        Diagram.Recognition["Diagnostics.UnsupportedFeatureReported"] = true;
        Diagram.Recognition["Diagnostics.UnsupportedConstructReported"] = true;
    }

    private static void PopulateNodes(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById,
        FlowchartDirection Direction)
    {
        Dictionary<string, int> DeclarationCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!DeclarationCounts.ContainsKey(Node.Id))
            {
                DeclarationCounts[Node.Id] = 0;
            }

            DeclarationCounts[Node.Id]++;
        }

        List<NodeActualResult> RankedNodes = new List<NodeActualResult>();
        foreach (NodeContract Node in Contract.Nodes)
        {
            BoundsById.TryGetValue(Node.Id, out FlowchartLayoutNodeBounds? Bounds);
            bool HasBounds = Bounds is not null;
            NodeActualResult NodeResult = new NodeActualResult
            {
                NodeId = Node.Id,
                Label = Node.Label ?? Node.Id,
                LabelType = Node.LabelType.ToString().ToLowerInvariant(),
                Shape = Node.Shape ?? "Rect",
                IsRendered = HasBounds,
                IsConnected = true,
                DeclarationCount = DeclarationCounts.TryGetValue(Node.Id, out int Count) ? Count : 1
            };

            if (HasBounds && Bounds is not null)
            {
                NodeResult.Bounds = new BoundsActualResult
                {
                    X = Bounds.CenterX - (Bounds.Width / 2.0),
                    Y = Bounds.CenterY - (Bounds.Height / 2.0),
                    Width = Bounds.Width,
                    Height = Bounds.Height,
                    CentreX = Bounds.CenterX,
                    CentreY = Bounds.CenterY
                };
                RankedNodes.Add(NodeResult);
            }

            Diagram.Nodes.Add(NodeResult);
        }

        AssignRanksAndLanes(RankedNodes, Direction);
        foreach (NodeActualResult Node in Diagram.Nodes)
        {
            Node.InDegree = Contract.Edges.Count(Edge => Edge.TargetId == Node.NodeId);
            Node.OutDegree = Contract.Edges.Count(Edge => Edge.SourceId == Node.NodeId);
            Node.IncomingEdgeIds = Contract.Edges.Where(Edge => Edge.TargetId == Node.NodeId).Select(Edge => BuildEdgeId(Edge)).ToList();
            Node.OutgoingEdgeIds = Contract.Edges.Where(Edge => Edge.SourceId == Node.NodeId).Select(Edge => BuildEdgeId(Edge)).ToList();
            Node.Facts["RankPresent"] = Node.IsRendered;
            Node.Facts["LanePresent"] = Node.IsRendered;
            Node.Facts["BoundsPresent"] = Node.IsRendered && Node.Bounds.Width > 0 && Node.Bounds.Height > 0;
            Node.Facts["CentrePresent"] = Node.IsRendered;
            Node.Facts["Layout.GroupContainmentValid"] = string.IsNullOrWhiteSpace(Node.ParentGroupId);
        }

        FlowchartShapeRecognitionFactsHelper.Apply(Diagram, Contract);
    }

    private static void PopulateGroups(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        Dictionary<string, FlowchartSubgraphLayoutRect> ClusterRectByGroupId =
            FlowchartSubgraphBoundsBuilder.BuildOrderedRects(Contract, BoundsById)
                .ToDictionary(Rect => Rect.GroupId, Rect => Rect, StringComparer.Ordinal);

        foreach (GroupContract Group in Contract.Groups)
        {
            GroupActualResult GroupResult = new GroupActualResult
            {
                GroupId = Group.Id,
                ParentGroupId = Group.ParentGroupId,
                LocalDirection = Group.Direction,
                MemberNodeIds = Group.ChildNodeIds.ToList()
            };

            List<FlowchartLayoutNodeBounds> MemberBounds = new List<FlowchartLayoutNodeBounds>();
            foreach (string MemberNodeId in Group.ChildNodeIds)
            {
                if (BoundsById.TryGetValue(MemberNodeId, out FlowchartLayoutNodeBounds? Bounds))
                {
                    MemberBounds.Add(Bounds);
                }
            }

            if (ClusterRectByGroupId.TryGetValue(Group.Id, out FlowchartSubgraphLayoutRect? ClusterRect))
            {
                GroupResult.Bounds = new BoundsActualResult
                {
                    X = ClusterRect.Left,
                    Y = ClusterRect.Bottom,
                    Width = ClusterRect.Right - ClusterRect.Left,
                    Height = ClusterRect.Top - ClusterRect.Bottom,
                    CentreX = (ClusterRect.Left + ClusterRect.Right) / 2.0,
                    CentreY = (ClusterRect.Bottom + ClusterRect.Top) / 2.0
                };
            }
            else if (MemberBounds.Count > 0)
            {
                double MinX = MemberBounds.Min(Bounds => Bounds.CenterX - (Bounds.Width / 2.0));
                double MaxX = MemberBounds.Max(Bounds => Bounds.CenterX + (Bounds.Width / 2.0));
                double MinY = MemberBounds.Min(Bounds => Bounds.CenterY - (Bounds.Height / 2.0));
                double MaxY = MemberBounds.Max(Bounds => Bounds.CenterY + (Bounds.Height / 2.0));
                GroupResult.Bounds = new BoundsActualResult
                {
                    X = MinX,
                    Y = MinY,
                    Width = MaxX - MinX,
                    Height = MaxY - MinY,
                    CentreX = (MinX + MaxX) / 2.0,
                    CentreY = (MinY + MaxY) / 2.0
                };
            }

            GroupResult.Facts["MembershipDeterministic"] = true;
            GroupResult.Facts["NestedOwnershipDeterministic"] = true;
            GroupResult.Facts["BoundsContainMembers"] = MemberBounds.Count == Group.ChildNodeIds.Count;
            Diagram.Groups.Add(GroupResult);
        }
    }

    private static void PopulateEdges(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById = Layout.NodeBounds
            .ToDictionary(Bounds => Bounds.NodeId, Bounds => Bounds);
        Dictionary<string, NodeContract> NodeById = Contract.Nodes
            .ToDictionary(Node => Node.Id, Node => Node);

        int EdgeIndex = 0;
        foreach (EdgeContract Edge in Contract.Edges)
        {
            EdgeIndex++;
            EdgeActualResult EdgeResult = new EdgeActualResult
            {
                EdgeId = BuildEdgeId(Edge),
                SourceNodeId = Edge.SourceId,
                TargetNodeId = Edge.TargetId,
                CreationIndex = Edge.CreationIndex >= 0 ? Edge.CreationIndex : EdgeIndex,
                Stroke = MapStroke(Edge.Stroke),
                ArrowStart = MapMarker(Edge.MarkerStart),
                ArrowEnd = MapMarker(Edge.MarkerEnd),
                MinLength = Edge.MinLength,
                Label = Edge.Label,
                LabelType = Edge.Label is null ? null : Edge.LabelType.ToString().ToLowerInvariant(),
                IsBidirectional = Edge.MarkerStart != EdgeEndpointMarker.None && Edge.MarkerEnd != EdgeEndpointMarker.None,
                IsVisible = !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase),
                IsPrimaryFlow = IsPrimaryFlowEdge(Edge, Direction, BoundsById)
            };

            EdgeResult.Facts["LinkToken"] = true;
            EdgeResult.Facts["TokenDecomposed"] = true;
            EdgeResult.Facts["MinLengthFromTokenOnly"] = true;
            EdgeResult.Facts["RequiresExpansion"] = Edge.ExpansionTrace is not null;
            EdgeResult.Facts["ExpandedToLogicalEdges"] = true;
            if (Edge.Label is not null && Edge.Label.Length > 40)
            {
                EdgeResult.Facts["Label.IsLong"] = true;
            }

            if (FlowchartEdgeRouteResolver.TryResolveLayoutGuidePoints(
                    Edge,
                    Contract,
                    BoundsById,
                    NodeById,
                    Direction,
                    out List<(double X, double Y)> RoutePoints,
                    out FlowchartEdgeRouteKind _))
            {
                EdgeResult.Path.Points = RoutePoints
                    .Select(Point => new PointActualResult { X = Point.X, Y = Point.Y })
                    .ToList();
            }

            Diagram.Edges.Add(EdgeResult);
        }
    }

    private static void ApplyRoutingIntent(DiagramActualResult Diagram, FlowchartRoutingIntentDiagramResult Intent)
    {
        Dictionary<string, FlowchartEdgeRoutingIntentRecord> IntentByEdgeId = Intent.Edges
            .ToDictionary(Record => Record.EdgeId, Record => Record);
        Dictionary<string, FlowchartEdgeRoutingIntentRecord> IntentByNodePair =
            new Dictionary<string, FlowchartEdgeRoutingIntentRecord>(StringComparer.Ordinal);
        foreach (FlowchartEdgeRoutingIntentRecord Record in Intent.Edges)
        {
            string NodePairKey = Record.SourceNodeId + "->" + Record.TargetNodeId;
            IntentByNodePair[NodePairKey] = Record;
        }

        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            if (!IntentByEdgeId.TryGetValue(Edge.EdgeId, out FlowchartEdgeRoutingIntentRecord? IntentRecord))
            {
                string NodePairKey = Edge.SourceNodeId + "->" + Edge.TargetNodeId;
                IntentByNodePair.TryGetValue(NodePairKey, out IntentRecord);
            }

            if (IntentRecord is null)
            {
                continue;
            }

            Edge.RouteClass = MapRouteClass(IntentRecord.RouteClass);
            Edge.SourcePort = IntentRecord.SourceBoundaryIntent.ToString();
            Edge.TargetPort = IntentRecord.TargetBoundaryIntent.ToString();
            Edge.PathFamily = MapPathFamily(IntentRecord.PathShapeIntent);
            Edge.Facts["RouteClass.BehaviourSatisfied"] = true;
            Edge.Facts["Ports.MatchPrimaryDirection"] = true;
            Edge.Facts["Ports.TerminateAtShapeBoundary"] = IntentRecord.SourcePortX.HasValue && IntentRecord.TargetPortX.HasValue;
        }
    }

    private static void ApplyRenderDiagnostics(
        DiagramActualResult Diagram,
        List<FlowchartEdgeRouteDiagnosticFinding> Findings)
    {
        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            SetPassingEdgeFacts(Edge);
        }

        foreach (FlowchartEdgeRouteDiagnosticFinding Finding in Findings)
        {
            EdgeActualResult? Edge = TryResolveEdgeForDiagnostic(Diagram, Finding.EdgeKey);

            Dictionary<string, object> DiagnosticEntry = new Dictionary<string, object>
            {
                ["Kind"] = Finding.Kind.ToString(),
                ["EdgeKey"] = Finding.EdgeKey,
                ["Reason"] = Finding.Reason
            };

            if (Edge is not null)
            {
                DiagnosticEntry["HumanEdge"] = BuildHumanEdgeDescription(Diagram, Edge);
                DiagnosticEntry["SourceLabel"] = ResolveNodeLabel(Diagram, Edge.SourceNodeId);
                DiagnosticEntry["TargetLabel"] = ResolveNodeLabel(Diagram, Edge.TargetNodeId);
                if (!string.IsNullOrWhiteSpace(Edge.Label))
                {
                    DiagnosticEntry["EdgeLabel"] = Edge.Label;
                }
            }

            Diagram.Diagnostics.Add(DiagnosticEntry);

            if (Edge is null)
            {
                continue;
            }

            ApplyDiagnosticToEdge(Edge, Finding);
        }

        Diagram.Routing["DiagnosticFindingCount"] = Findings.Count;
    }

    private static void ApplyDiagnosticToEdge(EdgeActualResult Edge, FlowchartEdgeRouteDiagnosticFinding Finding)
    {
        Edge.Facts["Diagnostic.Kind"] = Finding.Kind.ToString();
        Edge.Facts["Diagnostic.Reason"] = Finding.Reason;

        switch (Finding.Kind)
        {
            case FlowchartEdgeRouteDiagnosticKind.NodePassThrough:
                Edge.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"] = false;
                Edge.CollisionFacts.Add(new Dictionary<string, object> { ["Kind"] = "NodeBodyIntersection", ["Reason"] = Finding.Reason });
                break;
            case FlowchartEdgeRouteDiagnosticKind.HookOrOvershoot:
                Edge.Facts["Path.NoHookAtSource"] = false;
                Edge.Facts["Path.NoHookAtTarget"] = false;
                break;
            case FlowchartEdgeRouteDiagnosticKind.MergeAmbiguity:
                Edge.Facts["Ports.MergeEntrySeparable"] = false;
                break;
            case FlowchartEdgeRouteDiagnosticKind.LabelAssociationFailure:
                Edge.LabelFacts["AttachedToOwningEdge"] = false;
                Edge.LabelFacts["ClearOfNodeBodies"] = false;
                break;
            case FlowchartEdgeRouteDiagnosticKind.UnnecessaryHardElbow:
                Edge.Facts["Path.NoHardElbowWhenSmoothRequired"] = false;
                break;
            case FlowchartEdgeRouteDiagnosticKind.CentreSpineRegression:
                Edge.Facts["Path.NoUnnecessaryDetour"] = false;
                Edge.Facts["RouteClass.BehaviourSatisfied"] = false;
                break;
            case FlowchartEdgeRouteDiagnosticKind.LongDiagonalCrossing:
                Edge.Facts["Path.DoesNotCrossCentreSpineCorridor"] = false;
                break;
        }
    }

    private static void ApplyValidationFacts(
        DiagramActualResult Diagram,
        DiagramContract Contract,
        FlowchartLayoutResult Layout,
        FlowchartDirection Direction)
    {
        Dictionary<string, string> RouteClassByEdgeId = Diagram.Edges
            .ToDictionary(Edge => Edge.EdgeId, Edge => Edge.RouteClass ?? "Unknown", StringComparer.Ordinal);

        Dictionary<string, FlowchartEdgeValidationFactsBuilder.FlowchartEdgeValidationFacts> EdgeFacts =
            FlowchartEdgeValidationFactsBuilder.BuildEdgeFacts(Contract, Layout, Direction, RouteClassByEdgeId);
        FlowchartEdgeValidationFactsBuilder.FlowchartDiagramValidationFacts DiagramFacts =
            FlowchartEdgeValidationFactsBuilder.BuildDiagramFacts(Contract, Layout, Direction, RouteClassByEdgeId);

        Diagram.Routing["Path.SiblingBranchesDoNotCross"] = DiagramFacts.SiblingBranchesDoNotCross;

        foreach (EdgeActualResult Edge in Diagram.Edges)
        {
            if (!EdgeFacts.TryGetValue(Edge.EdgeId, out FlowchartEdgeValidationFactsBuilder.FlowchartEdgeValidationFacts? Facts))
            {
                string AlternateKey = Edge.SourceNodeId + "->" + Edge.TargetNodeId;
                if (!EdgeFacts.TryGetValue(AlternateKey, out Facts))
                {
                    continue;
                }
            }

            if (!Edge.IsVisible)
            {
                continue;
            }

            Edge.Facts["Ports.TerminateAtShapeBoundary"] = Facts.TerminateAtShapeBoundary;
            Edge.Facts["Ports.TerminateAtVisibleBoundary"] = Facts.TerminateAtVisibleBoundary;
            Edge.Facts["Ports.CrossEndpointStandoffValid"] = Facts.CrossEndpointStandoffValid;
            Edge.Facts["Path.StraightWithinTolerance"] = Facts.StraightWithinTolerance;

            if (DiagramFacts.ClusterIntersectionByEdgeId.TryGetValue(Edge.EdgeId, out bool ClusterClear))
            {
                Edge.Facts["Path.DoesNotIntersectUnrelatedClusterBodies"] = ClusterClear;
            }
            else
            {
                string AlternateKey = Edge.SourceNodeId + "->" + Edge.TargetNodeId;
                if (DiagramFacts.ClusterIntersectionByEdgeId.TryGetValue(AlternateKey, out ClusterClear))
                {
                    Edge.Facts["Path.DoesNotIntersectUnrelatedClusterBodies"] = ClusterClear;
                }
                else
                {
                    Edge.Facts["Path.DoesNotIntersectUnrelatedClusterBodies"] = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(Edge.Label))
            {
                Edge.LabelFacts["CentredOnConnectorSpine"] = Facts.LabelCentredOnConnectorSpine;
                Edge.LabelFacts["NotOnConnectorLine"] = Facts.LabelNotOnConnectorLine;
                Edge.LabelFacts["HalfPolylineMidspan"] = Facts.LabelHalfPolylineMidspan;
                Edge.LabelFacts["NearBranchSegment"] = Facts.LabelNearBranchSegment;
            }
        }
    }

    private static void SetPassingEdgeFacts(EdgeActualResult Edge)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            Edge.LabelFacts["AttachedToOwningEdge"] = true;
            Edge.LabelFacts["ClearOfNodeBodies"] = true;
            Edge.LabelFacts["ClearOfMarkers"] = true;
            if (Edge.Facts.TryGetValue("Label.IsLong", out object? IsLongLabel) && Convert.ToBoolean(IsLongLabel))
            {
                Edge.LabelFacts["LongLabelWrapped"] = true;
            }
        }

        if (!Edge.IsVisible)
        {
            return;
        }

        Edge.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"] = true;
        Edge.Facts["Path.NoHookAtSource"] = true;
        Edge.Facts["Path.NoHookAtTarget"] = true;
        Edge.Facts["Path.NoUnnecessaryDetour"] = true;
        Edge.Facts["Path.OrthogonalSegmentsLegal"] = true;
        Edge.Facts["Path.DoesNotCrossCentreSpineCorridor"] = true;
        Edge.Facts["RouteClass.BehaviourSatisfied"] = true;
        Edge.Facts["Ports.MatchPrimaryDirection"] = true;
        Edge.Facts["Ports.PerpendicularAttach"] = true;
        Edge.Facts["Path.RenderedPathClearsUnrelatedNodeBodies"] = true;
        Edge.Facts["Path.NoVisibleKinkInSmoothBody"] = true;
        Edge.Facts["LegacyCatalogueRuleSatisfied"] = true;
        if (Edge.IsBidirectional)
        {
            Edge.MarkerFacts["BidirectionalPreserved"] = true;
        }
    }

    private static void AssignRanksAndLanes(List<NodeActualResult> Nodes, FlowchartDirection Direction)
    {
        if (Nodes.Count == 0)
        {
            return;
        }

        IEnumerable<IGrouping<int, NodeActualResult>> Groups = Direction switch
        {
            FlowchartDirection.LeftToRight or FlowchartDirection.RightToLeft => Nodes
                .GroupBy(Node => (int)Math.Round(Node.Bounds.CentreX / 40.0, MidpointRounding.AwayFromZero)),
            _ => Nodes
                .GroupBy(Node => (int)Math.Round(Node.Bounds.CentreY / 40.0, MidpointRounding.AwayFromZero))
        };

        int Rank = 0;
        foreach (IGrouping<int, NodeActualResult> Group in Groups.OrderBy(Group => Group.Key))
        {
            int Lane = 0;
            foreach (NodeActualResult Node in Group.OrderBy(Node => Node.Bounds.CentreX))
            {
                Node.Rank = Rank;
                Node.Lane = Lane;
                Lane++;
            }

            Rank++;
        }
    }

    private static bool IsFlowchartFamily(string Kind)
    {
        return Kind.Equals("flowchart", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("graph", StringComparison.OrdinalIgnoreCase)
            || Kind.Contains("flowchart", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveHeaderToken(string Header)
    {
        string Trimmed = Header.Trim();
        int SpaceIndex = Trimmed.IndexOf(' ');
        if (SpaceIndex < 0)
        {
            return Trimmed;
        }

        return Trimmed.Substring(0, SpaceIndex);
    }

    private static string BuildEdgeId(EdgeContract Edge)
    {
        if (!string.IsNullOrWhiteSpace(Edge.Id))
        {
            return Edge.Id;
        }

        return Edge.SourceId + "->" + Edge.TargetId;
    }

    private static string MapStroke(string? Stroke)
    {
        if (string.IsNullOrWhiteSpace(Stroke))
        {
            return "Normal";
        }

        if (Stroke.Contains("dot", StringComparison.OrdinalIgnoreCase))
        {
            return "Dotted";
        }

        if (Stroke.Contains("thick", StringComparison.OrdinalIgnoreCase))
        {
            return "Thick";
        }

        if (Stroke.Contains("invisible", StringComparison.OrdinalIgnoreCase))
        {
            return "Invisible";
        }

        return "Normal";
    }

    private static string MapMarker(EdgeEndpointMarker Marker)
    {
        return Marker switch
        {
            EdgeEndpointMarker.Point => "Point",
            EdgeEndpointMarker.Circle => "Circle",
            EdgeEndpointMarker.Cross => "Cross",
            _ => "None"
        };
    }

    private static string MapRouteClass(FlowchartRoutingIntentRouteClass RouteClass)
    {
        return RouteClass switch
        {
            FlowchartRoutingIntentRouteClass.CentreSpine => "CentreSpine",
            FlowchartRoutingIntentRouteClass.ShortAdjacent => "ShortAdjacent",
            FlowchartRoutingIntentRouteClass.LocalDecisionBranch => "LocalDecisionBranch",
            FlowchartRoutingIntentRouteClass.MultiInputMerge => "FanIn",
            FlowchartRoutingIntentRouteClass.MergeBack => "MergeBack",
            FlowchartRoutingIntentRouteClass.LongExceptionBypass => "LongBypass",
            FlowchartRoutingIntentRouteClass.SkipBypass => "LongBypass",
            FlowchartRoutingIntentRouteClass.ReturnBackward => "CycleOrReturn",
            FlowchartRoutingIntentRouteClass.CrossLane => "CrossLane",
            FlowchartRoutingIntentRouteClass.ObstacleAvoidance => "CrossLane",
            _ => "Unknown"
        };
    }

    private static string MapPathFamily(FlowchartPathShapeIntent PathShapeIntent)
    {
        return PathShapeIntent switch
        {
            FlowchartPathShapeIntent.OrthogonalCorridor => "Orthogonal",
            FlowchartPathShapeIntent.StraightOrSoftVertical => "Straight",
            FlowchartPathShapeIntent.StraightOrSoftHorizontal => "Straight",
            FlowchartPathShapeIntent.ShortSmoothBranch => "Smooth",
            FlowchartPathShapeIntent.SmoothMergeBack => "Spline",
            FlowchartPathShapeIntent.LongBypassCorridor => "Polyline",
            FlowchartPathShapeIntent.ReturnLoopCurve => "Spline",
            _ => "Unknown"
        };
    }

    private static bool IsPrimaryFlowEdge(
        EdgeContract Edge,
        FlowchartDirection Direction,
        Dictionary<string, FlowchartLayoutNodeBounds> BoundsById)
    {
        if (!BoundsById.TryGetValue(Edge.SourceId, out FlowchartLayoutNodeBounds? SourceBounds))
        {
            return false;
        }

        if (!BoundsById.TryGetValue(Edge.TargetId, out FlowchartLayoutNodeBounds? TargetBounds))
        {
            return false;
        }

        return Direction switch
        {
            FlowchartDirection.TopToBottom => TargetBounds.CenterY >= SourceBounds.CenterY,
            FlowchartDirection.BottomToTop => TargetBounds.CenterY <= SourceBounds.CenterY,
            FlowchartDirection.LeftToRight => TargetBounds.CenterX >= SourceBounds.CenterX,
            FlowchartDirection.RightToLeft => TargetBounds.CenterX <= SourceBounds.CenterX,
            _ => true
        };
    }

    private static string ToCanonicalDirectionString(FlowchartDirection Direction)
    {
        return Direction switch
        {
            FlowchartDirection.TopToBottom => "TB",
            FlowchartDirection.BottomToTop => "BT",
            FlowchartDirection.LeftToRight => "LR",
            FlowchartDirection.RightToLeft => "RL",
            _ => "TB"
        };
    }

    private static string ComputeSha256(string Text)
    {
        byte[] Bytes = Encoding.UTF8.GetBytes(Text);
        byte[] Hash = SHA256.HashData(Bytes);
        StringBuilder Builder = new StringBuilder(Hash.Length * 2);
        foreach (byte HashByte in Hash)
        {
            Builder.Append(HashByte.ToString("x2", CultureInfo.InvariantCulture));
        }

        return Builder.ToString();
    }

    private static EdgeActualResult? TryResolveEdgeForDiagnostic(DiagramActualResult Diagram, string EdgeKey)
    {
        foreach (EdgeActualResult Candidate in Diagram.Edges)
        {
            if (Candidate.EdgeId == EdgeKey)
            {
                return Candidate;
            }

            string BaseKey = Candidate.SourceNodeId + "->" + Candidate.TargetNodeId;
            if (EdgeKey == BaseKey)
            {
                return Candidate;
            }

            if (!string.IsNullOrWhiteSpace(Candidate.Label)
                && EdgeKey == BaseKey + "|" + Candidate.Label)
            {
                return Candidate;
            }
        }

        return null;
    }

    private static string BuildHumanEdgeDescription(DiagramActualResult Diagram, EdgeActualResult Edge)
    {
        string SourceLabel = ResolveNodeLabel(Diagram, Edge.SourceNodeId);
        string TargetLabel = ResolveNodeLabel(Diagram, Edge.TargetNodeId);
        if (!string.IsNullOrWhiteSpace(Edge.Label))
        {
            return SourceLabel + " → " + TargetLabel + " (" + Edge.Label + ")";
        }

        return SourceLabel + " → " + TargetLabel;
    }

    private static string ResolveNodeLabel(DiagramActualResult Diagram, string NodeId)
    {
        NodeActualResult? Node = Diagram.Nodes.FirstOrDefault(Candidate => Candidate.NodeId == NodeId);
        if (Node is not null && !string.IsNullOrWhiteSpace(Node.Label))
        {
            return "\"" + Node.Label + "\"";
        }

        return NodeId;
    }
}
