using System.IO;
using MermaidRecognition.RuleValidation.Models;
using MermaidRecognition.RuleValidation.Pipeline;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>
/// Pipeline regression tests for catalogue scenarios F13, F21, F25, F29, and F30.
/// </summary>
[TestFixture]
public sealed class FlowCatalogueRuleValidationTests : ProductionCatalogueTestFixture
{
    private string _SpecDirectory = string.Empty;

    private string _FlowMarkdownPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        string BaseDirectory = AppContext.BaseDirectory;
        _SpecDirectory = Path.Combine(BaseDirectory, "Rule Specification");
        _FlowMarkdownPath = Path.Combine(BaseDirectory, "flow.md");
    }

    [Test]
    public void Pipeline_F13_TerminatesAtVisibleCylinderBoundary()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F13 — Cylinder database node

            ```mermaid
            flowchart TB
            Store[(Database / cylinder)]
            App[Application] --> Store
            ```
            """);

        EdgeActualResult? Edge = Result.ActualResults.Diagrams
            .Single()
            .Edges
            .FirstOrDefault(Candidate => Candidate.SourceNodeId == "App");
        Assert.That(Edge, Is.Not.Null);
        Assert.That(Edge!.Facts["Ports.TerminateAtVisibleBoundary"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F13");
        Assert.That(
            FailureRules,
            Does.Not.Contain("Render.Ports.TerminateAtVisibleBoundary"),
            "F13 arrow must meet the visible cylinder boundary.");
    }

    [Test]
    public void Pipeline_F21_MarkersMatchTokens()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F21 — Circle and cross markers

            ```mermaid
            flowchart LR
            A[A] --o B[B]
            C[C] --x D[D]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        EdgeActualResult CircleEdge = Diagram.Edges.First(Edge => Edge.SourceNodeId == "A");
        EdgeActualResult CrossEdge = Diagram.Edges.First(Edge => Edge.SourceNodeId == "C");

        Assert.That(CircleEdge.MarkerFacts["StartMatchesToken"], Is.EqualTo(true));
        Assert.That(CrossEdge.MarkerFacts["EndMatchesToken"], Is.EqualTo(true));
        Assert.That(CrossEdge.Facts["Ports.CrossEndpointStandoffValid"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F21");
        Assert.That(FailureRules, Does.Not.Contain("Render.Marker.StartMatchesToken"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Marker.EndMatchesToken"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Marker.CrossEndpointStandoff"));
    }

    [Test]
    public void Pipeline_F111_SiblingSubgraphsHaveMinimumGap()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F111 — Dense bipartite graph

            ```mermaid
            flowchart TB
            subgraph L["Left"]
              L1[L1]
              L2[L2]
              L3[L3]
            end
            subgraph R["Right"]
              R1[R1]
              R2[R2]
              R3[R3]
            end
            L1 --> R1
            L1 --> R2
            L1 --> R3
            L2 --> R1
            L2 --> R2
            L2 --> R3
            L3 --> R1
            L3 --> R2
            L3 --> R3
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        GroupActualResult LeftGroup = Diagram.Groups.Single(Group => Group.GroupId == "L");
        GroupActualResult RightGroup = Diagram.Groups.Single(Group => Group.GroupId == "R");

        Assert.That(LeftGroup.Facts["Layout.SiblingSubgraphGapValid"], Is.EqualTo(true));
        Assert.That(RightGroup.Facts["Layout.SiblingSubgraphGapValid"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["SiblingSubgraphsRespectMinimumGap"], Is.EqualTo(true));
        Assert.That(LeftGroup.Facts["Layout.MemberClusterInsetValid"], Is.EqualTo(true));
        Assert.That(RightGroup.Facts["Layout.MemberClusterInsetValid"], Is.EqualTo(true));
        Assert.That(LeftGroup.Facts["Layout.MembersVerticallyCenteredInCluster"], Is.EqualTo(true));
        Assert.That(RightGroup.Facts["Layout.MembersVerticallyCenteredInCluster"], Is.EqualTo(true));
        Assert.That(LeftGroup.Facts["Layout.MembersHorizontallyCenteredInCluster"], Is.EqualTo(true));
        Assert.That(RightGroup.Facts["Layout.MembersHorizontallyCenteredInCluster"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F111");
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.SiblingMinimumGap"));
        Assert.That(FailureRules, Does.Not.Contain("Subgraph.MemberMinimumInset"));
        Assert.That(FailureRules, Does.Not.Contain("Subgraph.MembersVerticallyCentered"));
        Assert.That(FailureRules, Does.Not.Contain("Subgraph.MembersHorizontallyCentered"));
    }

    [Test]
    public void Pipeline_F36_ExternalNodesRespectSubgraphMinimumGap()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F36 — Single subgraph, default direction

            ```mermaid
            flowchart TB
            subgraph SG["Processing"]
              A[Parse] --> B[Transform] --> C[Emit]
            end
            IN[In] --> A
            C --> OUT[Out]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        GroupActualResult ProcessingGroup = Diagram.Groups.Single(Group => Group.GroupId == "SG");

        Assert.That(ProcessingGroup.Facts["HasExternalBoundaryNode"], Is.EqualTo(true));
        Assert.That(ProcessingGroup.Facts["Layout.ExternalSubgraphGapValid"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["ExternalNodesRespectSubgraphMinimumGap"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F36");
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.ExternalMinimumGap"));
    }

    [Test]
    public void Pipeline_F58_PassesRenderedPathAndLongBypassRouting()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F58 — Full pipeline (shapes + decisions + labels + cylinder)

            ```mermaid
            flowchart TD
            Start([Start]) --> Ingest[Ingest file]
            Ingest --> Validate{Valid markdown?}
            Validate -->|No| Reject[Reject with error]
            Validate -->|Yes| Parse[Parse blocks]
            Parse --> HasMermaid{Mermaid fence?}
            HasMermaid -->|Yes| Render[Render SVG]
            HasMermaid -->|No| Word[Build DOCX]
            Render --> Word
            DoArchive{Archive?}
            Word --> DoArchive
            DoArchive -->|Yes| Persist
            DoArchive -->|No| Done
            Reject --> Done
            Persist[(Optional archive)] --> Done
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();

        Assert.That(Diagram.Config["RequiresCurveBasisPaths"], Is.EqualTo(true));

        EdgeActualResult ValidateReject = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-Validate-Reject-2");
        EdgeActualResult RenderWord = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-Render-Word-7");
        EdgeActualResult ArchiveDone = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-DoArchive-Done-10");

        Assert.That(ValidateReject.Facts["Ports.PerpendicularAttach"], Is.EqualTo(true));
        Assert.That(RenderWord.Facts["Ports.PerpendicularAttach"], Is.EqualTo(true));
        Assert.That(ArchiveDone.Facts["Path.NoVisibleKinkInSmoothBody"], Is.EqualTo(true));
        Assert.That(ArchiveDone.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"], Is.EqualTo(true));

        DiagramValidationResult? DiagramResult = Result.ValidationReport.DiagramResults
            .FirstOrDefault(Candidate => Candidate.DiagramId == "F58");
        Assert.That(DiagramResult, Is.Not.Null);
        Assert.That(DiagramResult!.Result, Is.EqualTo("Pass"));
        Assert.That(DiagramResult.ErrorCount, Is.EqualTo(0));

        List<string> FailureRules = FailureRuleIds(Result, "F58");
        Assert.That(FailureRules, Is.Empty);
    }

    [Test]
    public void Pipeline_F59_PrimarySubgraphColumnCenteredInWorkerCluster()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F59 — Subgraph + decision + retry loop

            ```mermaid
            flowchart TB
            subgraph Worker["Worker pool"]
              direction TB
              Q[Queue] --> W{Worker free?}
              W -->|Yes| P[Process job]
              W -->|No| Wait[Wait]
              Wait --> Q
            end
            Client[Client] --> Q
            P --> Out[Output]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        GroupActualResult WorkerGroup = Diagram.Groups.Single(Group => Group.GroupId == "Worker");

        Assert.That(WorkerGroup.Facts["Layout.MembersHorizontallyCenteredInCluster"], Is.EqualTo(true));
        Assert.That(WorkerGroup.Facts["Layout.MembersVerticallyCenteredInCluster"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["SubgraphMembersHorizontallyCentered"], Is.EqualTo(true));

        NodeActualResult QueueNode = Diagram.Nodes.Single(Node => Node.NodeId == "Q");
        NodeActualResult DecisionNode = Diagram.Nodes.Single(Node => Node.NodeId == "W");

        Assert.That(Math.Abs(QueueNode.Bounds.CentreX - WorkerGroup.Bounds.CentreX), Is.LessThan(2.0));
        Assert.That(Math.Abs(DecisionNode.Bounds.CentreX - WorkerGroup.Bounds.CentreX), Is.LessThan(2.0));

        EdgeActualResult YesBranch = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-W-P-1");
        EdgeActualResult NoBranch = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-W-Wait-2");
        NodeActualResult HubNode = Diagram.Nodes.Single(Node => Node.NodeId == "W");
        NodeActualResult WaitNode = Diagram.Nodes.Single(Node => Node.NodeId == "Wait");
        NodeActualResult ProcessNode = Diagram.Nodes.Single(Node => Node.NodeId == "P");

        // Two-way decisions leave from the shoulder that faces each target, never the bottom vertex.
        AssertBranchLeavesFacingShoulder(YesBranch, HubNode, ProcessNode, "Yes");
        AssertBranchLeavesFacingShoulder(NoBranch, HubNode, WaitNode, "No");
        Assert.That(NoBranch.Facts["Path.NoVisibleKinkInSmoothBody"], Is.EqualTo(true));
        Assert.That(NoBranch.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"], Is.EqualTo(true));
        Assert.That(YesBranch.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F59");
        Assert.That(FailureRules, Does.Not.Contain("Subgraph.MembersHorizontallyCentered"));
        Assert.That(FailureRules, Does.Not.Contain("Subgraph.MembersVerticallyCentered"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Path.NoVisibleKinkInSmoothBody"));
    }

    [Test]
    public void Pipeline_F73_ParallelColumnsShareAlignedRows()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F73 — Subgraph bare title (`subgraph one`)

            ```mermaid
            flowchart TB
            subgraph one
              a1[Alpha] --> a2[Beta]
            end
            subgraph two
              b1[Gamma] --> b2[Delta]
            end
            Ingress[Ingress] --> a1
            b2 --> Egress[Egress]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();

        Assert.That(Diagram.Layout["HasParallelColumnLayout"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["ParallelColumnRowsAligned"], Is.EqualTo(true));

        NodeActualResult Ingress = Diagram.Nodes.Single(Node => Node.NodeId == "Ingress");
        NodeActualResult Gamma = Diagram.Nodes.Single(Node => Node.NodeId == "b1");
        NodeActualResult Alpha = Diagram.Nodes.Single(Node => Node.NodeId == "a1");
        NodeActualResult Delta = Diagram.Nodes.Single(Node => Node.NodeId == "b2");
        NodeActualResult Beta = Diagram.Nodes.Single(Node => Node.NodeId == "a2");
        NodeActualResult Egress = Diagram.Nodes.Single(Node => Node.NodeId == "Egress");

        Assert.That(Math.Abs(Ingress.Bounds.CentreY - Gamma.Bounds.CentreY), Is.LessThan(2.0));
        Assert.That(Math.Abs(Alpha.Bounds.CentreY - Delta.Bounds.CentreY), Is.LessThan(2.0));
        Assert.That(Math.Abs(Beta.Bounds.CentreY - Egress.Bounds.CentreY), Is.LessThan(2.0));

        List<string> FailureRules = FailureRuleIds(Result, "F73");
        Assert.That(FailureRules, Does.Not.Contain("Layout.ParallelColumnRowsAligned"));
    }

    [Test]
    public void Pipeline_F60_SiblingSubgraphsAlignedForHorizontalCrossStageRouting()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F60 — LR diagram with nested TB and cross-links (integration)

            ```mermaid
            flowchart LR
            subgraph Ingest["Ingest"]
              direction TB
              I1[Receive drop] --> I2[Verify manifest]
            end
            subgraph Transform["Transform"]
              direction TB
              T1[Normalise] --> T2[Translate]
            end
            subgraph Publish["Publish"]
              direction TB
              P1[Package] --> P2[Release]
            end
            Source[Source] --> I1
            I2 --> T1
            T2 --> P1
            P2 --> Sink[Sink]
            I2 -.->|audit copy| P2
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();

        Assert.That(Diagram.Layout["SiblingSubgraphCrossBoundarySpineAligned"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["SubgraphMembersVerticallyCentered"], Is.EqualTo(true));

        foreach (string GroupId in new[] { "Ingest", "Transform", "Publish" })
        {
            GroupActualResult Group = Diagram.Groups.Single(Candidate => Candidate.GroupId == GroupId);
            Assert.That(Group.Facts["Layout.SiblingSubgraphSpineAligned"], Is.EqualTo(true));
            Assert.That(Group.Facts["Layout.MembersVerticallyCenteredInCluster"], Is.EqualTo(true));
        }

        NodeActualResult I2 = Diagram.Nodes.Single(Node => Node.NodeId == "I2");
        NodeActualResult T1 = Diagram.Nodes.Single(Node => Node.NodeId == "T1");
        NodeActualResult T2 = Diagram.Nodes.Single(Node => Node.NodeId == "T2");
        NodeActualResult P1 = Diagram.Nodes.Single(Node => Node.NodeId == "P1");
        NodeActualResult P2 = Diagram.Nodes.Single(Node => Node.NodeId == "P2");

        Assert.That(Math.Abs(I2.Bounds.CentreY - T1.Bounds.CentreY), Is.LessThan(2.0));
        Assert.That(Math.Abs(T2.Bounds.CentreY - P1.Bounds.CentreY), Is.LessThan(2.0));
        Assert.That(Math.Abs(I2.Bounds.CentreY - P2.Bounds.CentreY), Is.LessThan(2.0));

        EdgeActualResult AuditCopy = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-I2-P2-7");
        Assert.That(AuditCopy.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"], Is.EqualTo(true));
        Assert.That(AuditCopy.Path.Points.Count, Is.GreaterThanOrEqualTo(4));

        EdgeActualResult CrossStage = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-I2-T1-4");
        Assert.That(CrossStage.Facts["Path.StraightWithinTolerance"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F60");
        Assert.That(FailureRules, Does.Not.Contain("Subgraph.SiblingCrossBoundarySpineAligned"));
        Assert.That(FailureRules, Does.Not.Contain("Subgraph.MembersVerticallyCentered"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Obstacle.NoNodeBodyIntersection"));
    }

    [Test]
    public void Pipeline_F112_DeeplyNestedSubgraphsRespectMinimumExteriorGap()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F112 — Deeply nested subgraphs (5 levels)

            ```mermaid
            flowchart TB
            subgraph L0
              subgraph L1
                subgraph L2
                  subgraph L3
                    subgraph L4
                      A[A] --> B[B]
                    end
                  end
                end
              end
            end
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();

        foreach (string ParentGroupId in new[] { "L0", "L1", "L2", "L3" })
        {
            GroupActualResult ParentGroup = Diagram.Groups.Single(Group => Group.GroupId == ParentGroupId);
            Assert.That(ParentGroup.Facts["HasNestedChildSubgraph"], Is.EqualTo(true));
            Assert.That(ParentGroup.Facts["Layout.NestedSubgraphContainmentValid"], Is.EqualTo(true));
        }

        Assert.That(Diagram.Layout["NestedSubgraphsContainedInParentClusters"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F112");
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.NestedClusterContainment"));
    }

    [Test]
    public void Pipeline_F46_NestedChildSubgraphContainedInParentWithMinimumGap()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F46 — Nested subgraph, outer LR, inner LR

            ```mermaid
            flowchart LR
            subgraph OuterLR["Outer LR"]
              direction LR
              O1[O1] --> O2[O2]
              subgraph InnerLR["Inner LR"]
                direction LR
                I1[I1] --> I2[I2]
              end
              O2 --> I1
              I2 --> O3[O3]
            end
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        GroupActualResult OuterGroup = Diagram.Groups.Single(Group => Group.GroupId == "OuterLR");

        Assert.That(OuterGroup.Facts["HasNestedChildSubgraph"], Is.EqualTo(true));
        Assert.That(OuterGroup.Facts["Layout.NestedSubgraphContainmentValid"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["NestedSubgraphsContainedInParentClusters"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F46");
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.NestedClusterContainment"));
    }

    [Test]
    public void Pipeline_F44_NestedChildSubgraphContainedInParentWithMinimumGap()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F44 — Nested subgraph with inner `direction TB`

            ```mermaid
            flowchart TB
            subgraph Outer["Outer"]
              direction TB
              Start[Start] --> InnerTop
              subgraph Inner["Inner TB"]
                direction TB
                InnerTop[Inner top] --> InnerBottom[Inner bottom]
              end
              InnerBottom --> End[End]
            end
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        GroupActualResult OuterGroup = Diagram.Groups.Single(Group => Group.GroupId == "Outer");

        Assert.That(OuterGroup.Facts["HasNestedChildSubgraph"], Is.EqualTo(true));
        Assert.That(OuterGroup.Facts["Layout.NestedSubgraphContainmentValid"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["NestedSubgraphsContainedInParentClusters"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F44");
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.NestedClusterContainment"));
    }

    [Test]
    public void Pipeline_F45_NestedSubgraphsSeparatedOnFlowAxisAndDirectMembersClearNestedClusters()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F45 — Two levels of nesting

            ```mermaid
            flowchart TB
            subgraph L0["Level 0"]
              direction TB
              A[A] --> B[B]
              subgraph L1["Level 1"]
                direction TB
                subgraph L2["Level 2"]
                  direction TB
                  C[C] --> D[D]
                end
              end
              B --> C
              D --> E[E]
            end
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        GroupActualResult L0Group = Diagram.Groups.Single(Group => Group.GroupId == "L0");
        GroupActualResult L1Group = Diagram.Groups.Single(Group => Group.GroupId == "L1");

        Assert.That(L1Group.Facts["HasNestedChildSubgraph"], Is.EqualTo(true));
        Assert.That(L1Group.Facts["Layout.NestedSubgraphContainmentValid"], Is.EqualTo(true));
        Assert.That(L0Group.Facts["HasDirectMemberWithNestedChildSubgraph"], Is.EqualTo(true));
        Assert.That(L0Group.Facts["Layout.DirectMemberNestedGapValid"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["NestedSubgraphsContainedInParentClusters"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["DirectMembersRespectNestedSubgraphSeparation"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F45");
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.NestedClusterContainment"));
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.DirectMemberNestedSeparation"));
    }

    [Test]
    public void Pipeline_F37_ExternalNodesRespectSubgraphMinimumGap()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F37 — Subgraph with explicit `direction TB` (overridden by LR diagram via cross-boundary links)

            ```mermaid
            flowchart LR
            subgraph Box["Vertical stack inside horizontal diagram"]
              direction TB
              S1[Step 1] --> S2[Step 2] --> S3[Step 3]
            end
            Start[Start] --> S1
            S3 --> End[End]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        GroupActualResult BoxGroup = Diagram.Groups.Single(Group => Group.GroupId == "Box");

        Assert.That(BoxGroup.Facts["HasExternalBoundaryNode"], Is.EqualTo(true));
        Assert.That(BoxGroup.Facts["Layout.ExternalSubgraphGapValid"], Is.EqualTo(true));
        Assert.That(Diagram.Layout["ExternalNodesRespectSubgraphMinimumGap"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F37");
        Assert.That(FailureRules, Does.Not.Contain("Layout.Subgraph.ExternalMinimumGap"));
    }

    [Test]
    public void Pipeline_F113_ParallelBranchFanInAvoidsNodeBodyIntersection()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F113 — 20 parallel branches

            ```mermaid
            flowchart LR
            Start[Start] --> B1 --> End
            Start --> B2 --> End
            Start --> B3 --> End
            Start --> B4 --> End
            Start --> B5 --> End
            Start --> B6 --> End
            Start --> B7 --> End
            Start --> B8 --> End
            Start --> B9 --> End
            Start --> B10 --> End
            Start --> B11 --> End
            Start --> B12 --> End
            Start --> B13 --> End
            Start --> B14 --> End
            Start --> B15 --> End
            Start --> B16 --> End
            Start --> B17 --> End
            Start --> B18 --> End
            Start --> B19 --> End
            Start --> B20 --> End
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => Candidate.TargetNodeId == "End"))
        {
            Assert.That(Edge.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F113");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Obstacle.NoNodeBodyIntersection"));
    }

    [Test]
    public void Pipeline_F81_TrapezoidBracketEdgesTerminateAtVisibleBoundary()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F81 — Legacy trapezoid bracket shapes

            ```mermaid
            flowchart TD
            Top[/Wide top/] --> Bottom[\Wide bottom/]
            ```
            """);

        EdgeActualResult? Edge = Result.ActualResults.Diagrams
            .Single()
            .Edges
            .FirstOrDefault();
        Assert.That(Edge, Is.Not.Null);
        Assert.That(Edge!.Facts["Ports.TerminateAtVisibleBoundary"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F81");
        Assert.That(FailureRules, Does.Not.Contain("Render.Ports.TerminateAtVisibleBoundary"));
    }

    [Test]
    public void Pipeline_F81_TrapezoidBracketShapesRecognisedAndRendered()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F81 — Legacy trapezoid bracket shapes

            ```mermaid
            flowchart TD
            Top[/Wide top/] --> Bottom[\Wide bottom/]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        NodeActualResult Top = Diagram.Nodes.Single(Node => Node.NodeId == "Top");
        NodeActualResult Bottom = Diagram.Nodes.Single(Node => Node.NodeId == "Bottom");

        Assert.That(Top.Shape, Is.EqualTo("lean_right"));
        Assert.That(Bottom.Shape, Is.EqualTo("inv_trapezoid"));
        Assert.That(Top.Facts["ShapeSource"], Is.EqualTo("bracket"));
        Assert.That(Bottom.Facts["ShapeSource"], Is.EqualTo("bracket"));
        Assert.That(Top.Facts["TrapezoidBracketShapeRecognised"], Is.EqualTo(true));
        Assert.That(Bottom.Facts["TrapezoidBracketShapeRecognised"], Is.EqualTo(true));
        Assert.That(Top.Facts["RenderedAsTrapezoidBracket"], Is.EqualTo(true));
        Assert.That(Bottom.Facts["RenderedAsTrapezoidBracket"], Is.EqualTo(true));
        Assert.That(Top.Facts["LabelDelimiterStripped"], Is.EqualTo(true));
        Assert.That(Bottom.Facts["LabelDelimiterStripped"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F81");
        Assert.That(FailureRules, Does.Not.Contain("Recognition.Node.Shape.Trapezoid"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Shape.TrapezoidBracketGeometry"));
    }

    [Test]
    public void Pipeline_F82_TrapezoidBracketVariantsUseCorrectGeometry()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F82 — Trapezoid bracket shape variants

            ```mermaid
            flowchart TD
            LeanRight[/lean_right/]
            Trapezoid[/trapezoid \]
            InvTrap[\inv_trapezoid /]
            LeanLeft[\lean_left \]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();

        foreach (NodeActualResult Node in Diagram.Nodes)
        {
            Assert.That(Node.Facts["RenderedAsTrapezoidBracket"], Is.EqualTo(true));
        }

        Assert.That(Diagram.Layout["HasDisconnectedSingleNodeComponents"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F82");
        Assert.That(FailureRules, Does.Not.Contain("Render.Shape.TrapezoidBracketGeometry"));
    }

    [Test]
    public void Pipeline_F24_FanOutEdgesTerminateAtVisibleBoundary()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F24 — Edge labels (short)

            ```mermaid
            flowchart LR
            A[Request] -->|approve| B[Approved]
            A -->|reject| C[Rejected]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => Candidate.IsVisible))
        {
            Assert.That(Edge.Facts["Ports.TerminateAtVisibleBoundary"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F24");
        Assert.That(FailureRules, Does.Not.Contain("Render.Ports.TerminateAtVisibleBoundary"));
    }

    [Test]
    public void Pipeline_LrFanOutEdgeLabels_CentredOnHorizontalConnector()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F24 — Edge labels (short)

            ```mermaid
            flowchart LR
            A[Request] -->|approve| B[Approved]
            A -->|reject| C[Rejected]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(Edge.LabelFacts["CentredOnConnectorSpine"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["CentredOnConnectorHorizontally"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["CentredOnConnectorVertically"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F24");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.CentredOnConnectorSpine"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.CentredOnConnectorHorizontally"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.CentredOnConnectorVertically"));
    }

    [Test]
    public void Pipeline_ShapeCatalogueRow_NodeLabelsCentredInNodeBodies()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### Shape catalogue row

            ```mermaid
            flowchart LR
            R[Rect] --> RN(Rounded) --> D{Diamond} --> C((Circle))
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (NodeActualResult Node in Diagram.Nodes.Where(Candidate => Candidate.IsRendered))
        {
            Assert.That(Node.Facts["Label.CentredHorizontally"], Is.EqualTo(true));
            Assert.That(Node.Facts["Label.CentredVertically"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F17");
        Assert.That(FailureRules, Does.Not.Contain("Render.Label.NodeCentredHorizontally"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Label.NodeCentredVertically"));
    }

    [Test]
    public void FlowMarkdown_AllActiveDiagrams_PassTerminalStubRules()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-terminal-stub-" + Guid.NewGuid().ToString("N"));
        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(_FlowMarkdownPath, OutputDirectory);

        string[] TerminalStubRules =
        [
            "Render.Ports.PerpendicularAttach"
        ];

        List<ValidationFailureRecord> StubFailures = [.. Result.ValidationReport.Failures.Where(Failure => TerminalStubRules.Contains(Failure.RuleId, StringComparer.Ordinal))];

        Assert.That(
            StubFailures,
            Is.Empty,
            () => string.Join(
                Environment.NewLine,
                StubFailures.Select(
                    Failure => Failure.DiagramId + " " + Failure.RuleId + ": " + Failure.Message)));
    }

    [Test]
    public void FlowMarkdown_AllActiveDiagrams_PassMarkerTangentRules()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-marker-tangent-" + Guid.NewGuid().ToString("N"));
        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(_FlowMarkdownPath, OutputDirectory);

        string[] MarkerTangentRules =
        [
            "Render.Marker.EndAlignedWithPathTangent",
            "Render.Marker.StartAlignedWithPathTangent"
        ];

        List<ValidationFailureRecord> TangentFailures = [.. Result.ValidationReport.Failures.Where(Failure => MarkerTangentRules.Contains(Failure.RuleId, StringComparer.Ordinal))];

        Assert.That(
            TangentFailures,
            Is.Empty,
            () => string.Join(
                Environment.NewLine,
                TangentFailures.Select(
                    Failure => Failure.DiagramId + " " + Failure.RuleId + ": " + Failure.Message)));
    }

    [Test]
    public void Pipeline_F31_HubFanOut_ArrowheadsAlignWithConnectorPath()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F31 — Fan-out (one to many)

            ```mermaid
            flowchart TB
            Hub[Hub] --> W1[Worker 1]
            Hub --> W2[Worker 2]
            Hub --> W3[Worker 3]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => Candidate.IsVisible))
        {
            if (string.Equals(Edge.ArrowEnd, "Point", StringComparison.Ordinal))
            {
                Assert.That(Edge.MarkerFacts["EndAlignedWithPathTangent"], Is.EqualTo(true), Edge.EdgeId);
            }
        }

        List<string> FailureRules = FailureRuleIds(Result, "F31");
        Assert.That(FailureRules, Does.Not.Contain("Render.Marker.EndAlignedWithPathTangent"));
    }

    [Test]
    public void Pipeline_F31_HubFanOut_PerpendicularTerminalStubs()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F31 — Fan-out (one to many)

            ```mermaid
            flowchart TB
            Hub[Hub] --> W1[Worker 1]
            Hub --> W2[Worker 2]
            Hub --> W3[Worker 3]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => Candidate.IsVisible))
        {
            Assert.That(Edge.Facts["Ports.PerpendicularAttach"], Is.EqualTo(true), () => $"{Edge.EdgeId}: {DescribeAttachFailure(Edge)}");
        }

        List<string> FailureRules = FailureRuleIds(Result, "F31");
        Assert.That(FailureRules, Does.Not.Contain("Render.Ports.PerpendicularAttach"));
    }

    [Test]
    public void FlowMarkdown_AllActiveDiagrams_PassLabelAlignmentRules()
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-label-align-" + Guid.NewGuid().ToString("N"));
        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(_FlowMarkdownPath, OutputDirectory);

        string[] LabelAlignmentRules =
        [
            "Render.Label.NodeCentredHorizontally",
            "Render.Label.NodeCentredVertically",
            "Routing.Label.CentredOnConnectorSpine",
            "Routing.Label.CentredOnConnectorHorizontally",
            "Routing.Label.CentredOnConnectorVertically"
        ];

        List<ValidationFailureRecord> LabelFailures = [.. Result.ValidationReport.Failures.Where(Failure => LabelAlignmentRules.Contains(Failure.RuleId, StringComparer.Ordinal))];

        Assert.That(
            LabelFailures,
            Is.Empty,
            () => string.Join(
                Environment.NewLine,
                LabelFailures.Select(
                    Failure => Failure.DiagramId + " " + Failure.RuleId + ": " + Failure.Message)));
    }

    [Test]
    public void Pipeline_F25_SpineLabelsCentredOnConnector()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F25 — Quoted edge labels on spine

            ```mermaid
            flowchart TB
            A[Source] -->|"Step 1: validate input"| B[Validator]
            B -->|"Step 2: persist (async)"| C[Store]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(Edge.LabelFacts["CentredOnConnectorSpine"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F25");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.CentredOnConnectorSpine"));
    }

    [Test]
    public void Pipeline_F26_LongEdgeLabelCentredOnConnectorSpine()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F26 — Long edge label (wrapping stress)

            ```mermaid
            flowchart LR
            A[Producer] -->|"This edge label is intentionally long so wrapping, clearance from arrowheads, and node borders can be verified in Word."| B[Consumer]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        EdgeActualResult LabelledEdge = Diagram.Edges.Single(Edge => !string.IsNullOrWhiteSpace(Edge.Label));

        Assert.That(LabelledEdge.LabelFacts["CentredOnConnectorSpine"], Is.EqualTo(true));
        Assert.That(LabelledEdge.Facts["Label.IsLong"], Is.EqualTo(true));
        Assert.That(LabelledEdge.LabelFacts["LongLabelWrapped"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F26");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.CentredOnConnectorSpine"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.LongLabelWrapped"));
    }

    [Test]
    public void Pipeline_F29_DecisionBranchesClassifiedAndLabelled()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F29 — Binary decision (Yes / No)

            ```mermaid
            flowchart TD
            Start([Start]) --> Check{Valid?}
            Check -->|Yes| OK[Accept]
            Check -->|No| Fail[Reject]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        List<EdgeActualResult> BranchEdges = [.. Diagram.Edges.Where(Edge => Edge.SourceNodeId == "Check")];

        Assert.That(BranchEdges.Count, Is.EqualTo(2));
        Assert.That(BranchEdges.All(Edge => Edge.RouteClass == "LocalDecisionBranch"), Is.True);

        foreach (EdgeActualResult Edge in BranchEdges.Where(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(Edge.LabelFacts["CentredOnConnectorSpine"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["HalfPolylineMidspan"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["NearBranchSegment"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F29");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Class.LocalDecisionBranch"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.CentredOnConnectorSpine"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.HalfPolylineMidspan"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.BranchLabelNearBranch"));
    }

    [Test]
    public void Pipeline_F30_MultiWayDecisionBranchesClassified()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F30 — Multi-way decision

            ```mermaid
            flowchart TB
            Hub[Hub] --> Gate{Pick one}
            Gate -->|A| PathA[Path A]
            Gate -->|B| PathB[Path B]
            Gate -->|C| PathC[Path C]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        List<EdgeActualResult> BranchEdges = [.. Diagram.Edges.Where(Edge => Edge.SourceNodeId == "Gate")];

        Assert.That(BranchEdges.Count, Is.EqualTo(3));
        Assert.That(BranchEdges.All(Edge => Edge.RouteClass == "LocalDecisionBranch"), Is.True);
        Assert.That(Diagram.Routing["Path.SiblingBranchesDoNotCross"], Is.EqualTo(true));

        foreach (EdgeActualResult Edge in BranchEdges.Where(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(Edge.LabelFacts["HalfPolylineMidspan"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["NearBranchSegment"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F30");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Obstacle.NoSiblingBranchCrossing"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Class.LocalDecisionBranch"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.HalfPolylineMidspan"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.BranchLabelNearBranch"));
    }

    [Test]
    public void Pipeline_F34_RetryLoopDecisionLabelsClearOfConnector()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F34 — Simple cycle (retry loop)

            ```mermaid
            flowchart LR
            A[Receive] --> B{OK?}
            B -->|Yes| C[Done]
            B -->|No| D[Retry]
            D --> A
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        List<EdgeActualResult> BranchEdges = [.. Diagram.Edges.Where(Edge => Edge.SourceNodeId == "B" && !string.IsNullOrWhiteSpace(Edge.Label))];

        Assert.That(BranchEdges.Count, Is.EqualTo(2));
        foreach (EdgeActualResult Edge in BranchEdges)
        {
            Assert.That(Edge.LabelFacts["NotOnConnectorLine"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["HalfPolylineMidspan"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["NearBranchSegment"], Is.EqualTo(true));
        }

        NodeActualResult HubNode = Diagram.Nodes.Single(Node => Node.NodeId == "B");
        EdgeActualResult YesBranch = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-B-C-1");
        EdgeActualResult NoBranch = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-B-D-2");
        double HubCentreY = HubNode.Bounds.CentreY;
        double YesGuideY = YesBranch.Path.Points[2].Y;
        double NoGuideY = NoBranch.Path.Points[2].Y;

        Assert.That(
            Math.Abs((YesGuideY - HubCentreY) + (NoGuideY - HubCentreY)),
            Is.LessThan(2.0),
            "F34 Yes/No branches should mirror about the decision hub.");
        Assert.That(NoBranch.Facts["Path.NoVisibleKinkInSmoothBody"], Is.EqualTo(true));

        List<string> FailureRules = FailureRuleIds(Result, "F34");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.NotOnConnectorLine"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.HalfPolylineMidspan"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.BranchLabelNearBranch"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Path.NoVisibleKinkInSmoothBody"));
    }

    [Test]
    public void Pipeline_F30_CatalogueDiagram_DecisionBranchLabelsAtHalfPolylineMidspan()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F30 — Multi-way decision (three branches)

            ```mermaid
            flowchart TD
            A[Christmas] -->|Get money| B(Go shopping)
            B --> C{Let me think}
            C -->|One| D[Laptop]
            C -->|Two| E[iPhone]
            C -->|Three| F[Other gift]
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        List<EdgeActualResult> BranchEdges = [.. Diagram.Edges.Where(Edge => Edge.SourceNodeId == "C" && !string.IsNullOrWhiteSpace(Edge.Label))];

        Assert.That(BranchEdges.Count, Is.EqualTo(3));
        foreach (EdgeActualResult Edge in BranchEdges)
        {
            Assert.That(Edge.LabelFacts["HalfPolylineMidspan"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["NearBranchSegment"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F30");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.HalfPolylineMidspan"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.BranchLabelNearBranch"));
    }

    [Test]
    public void Pipeline_F53A_StadiumMerge_UsesCentreSourceSlotsAndShoulderTargets()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F53A — `style` directive (fill / stroke)

            ```mermaid
            flowchart TD
            Success --> End([End])
            Error --> End

            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        NodeActualResult EndNode = Diagram.Nodes.Single(Node => Node.NodeId == "End");

        foreach (string SourceId in new[] { "Success", "Error" })
        {
            EdgeActualResult Edge = Diagram.Edges.Single(
                Candidate => Candidate.SourceNodeId == SourceId && Candidate.TargetNodeId == "End");
            NodeActualResult SourceNode = Diagram.Nodes.Single(Node => Node.NodeId == SourceId);

            double SourceBottom = SourceNode.Bounds.CentreY - (SourceNode.Bounds.Height / 2.0);
            double SourceTop = SourceNode.Bounds.CentreY + (SourceNode.Bounds.Height / 2.0);
            double StartY = Edge.Path.Points[0].Y;
            Assert.That(
                Math.Min(Math.Abs(StartY - SourceBottom), Math.Abs(StartY - SourceTop)),
                Is.LessThan(2.0),
                $"{SourceId} should leave the source outline.");
            (double EndX, double EndY) = (Edge.Path.Points[Edge.Path.Points.Count - 1].X, Edge.Path.Points[Edge.Path.Points.Count - 1].Y);
            double EndLeft = EndNode.Bounds.CentreX - (EndNode.Bounds.Width / 2.0);
            double EndRight = EndNode.Bounds.CentreX + (EndNode.Bounds.Width / 2.0);
            double EndBottom = EndNode.Bounds.CentreY - (EndNode.Bounds.Height / 2.0);
            double EndTop = EndNode.Bounds.CentreY + (EndNode.Bounds.Height / 2.0);
            double EndDistance = Math.Min(
                Math.Min(Math.Abs(EndX - EndLeft), Math.Abs(EndX - EndRight)),
                Math.Min(Math.Abs(EndY - EndBottom), Math.Abs(EndY - EndTop)));
            Assert.That(EndDistance, Is.LessThan(2.0), $"{SourceId} should end on the stadium outline.");
        }
    }

    [Test]
    public void Pipeline_F53_StadiumMerge_EndEdgesUseShoulderAttachAndVisibleArrows()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F53 — `style` directive (fill / stroke)

            ```mermaid
            flowchart TD
            Start([Start]) --> Process[Process]
            Process --> Decision{Valid?}
            Decision -->|Yes| Success[Success]
            Decision -->|No| Error[Error]
            Success --> End([End])
            Error --> End

            style Start fill:#e1f5e1
            style End fill:#e1f5e1
            style Error fill:#f8d7da
            style Success fill:#d1ecf1
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        NodeActualResult EndNode = Diagram.Nodes.Single(Node => Node.NodeId == "End");

        foreach (string SourceId in new[] { "Success", "Error" })
        {
            EdgeActualResult Edge = Diagram.Edges.Single(
                Candidate => Candidate.SourceNodeId == SourceId && Candidate.TargetNodeId == "End");
            NodeActualResult SourceNode = Diagram.Nodes.Single(Node => Node.NodeId == SourceId);

            double ExpectedSourceAttachY = SourceNode.Bounds.CentreY - (SourceNode.Bounds.Height / 2.0);
            Assert.That(
                Math.Abs(Edge.Path.Points[0].X - SourceNode.Bounds.CentreX),
                Is.LessThan(1.0),
                $"{SourceId} should use the centre outbound slot.");
            Assert.That(
                Math.Abs(Edge.Path.Points[0].Y - ExpectedSourceAttachY),
                Is.LessThan(2.0),
                $"{SourceId} should leave from the source bottom face.");
            Assert.That(
                Math.Abs(Edge.Path.Points[Edge.Path.Points.Count - 1].X - EndNode.Bounds.CentreX),
                Is.GreaterThan(4.0),
                $"{SourceId} should attach on the stadium shoulder, not the top-centre slot.");
            Assert.That(Edge.MarkerFacts["NotLost"], Is.EqualTo(true), $"{SourceId} arrowhead should be visible.");
            Assert.That(Edge.MarkerFacts["EndAlignedWithPathTangent"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F53");
        Assert.That(FailureRules, Does.Not.Contain("Render.Marker.NotLost"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Marker.EndAlignedWithPathTangent"));
        Assert.That(FailureRules, Does.Not.Contain("Render.Ports.PerpendicularAttach"));
    }

    [Test]
    public void Pipeline_F125_NodesFirstDiamondFanIn_PassesRoutingRules()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F125 — Nodes first: diamond with multiple incoming edges

            ```mermaid
            flowchart TB
            PA[Path A]
            PB[Path B]
            PC[Path C]
            D{Merge?}
            E[Continue]
            F[Stop]
            PA --> D
            PB --> D
            PC --> D
            D -->|yes| E
            D -->|no| F
            ```
            """);

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        EdgeActualResult PathBEdge = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-PB-D-1");
        List<EdgeActualResult> BranchEdges = [.. Diagram.Edges.Where(Edge => Edge.SourceNodeId == "D")];

        Assert.That(PathBEdge.Facts["Path.NoUnnecessaryDetour"], Is.EqualTo(true));
        Assert.That(PathBEdge.Facts["Path.StraightWithinTolerance"], Is.EqualTo(true));
        Assert.That(PathBEdge.RouteClass, Is.EqualTo("FanIn"));
        Assert.That(BranchEdges.Count, Is.EqualTo(2));
        Assert.That(BranchEdges.All(Edge => Edge.RouteClass == "LocalDecisionBranch"), Is.True);

        foreach (EdgeActualResult Edge in BranchEdges.Where(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(Edge.LabelFacts["HalfPolylineMidspan"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["NearBranchSegment"], Is.EqualTo(true));
            Assert.That(Edge.LabelFacts["CentredOnConnectorSpine"], Is.EqualTo(true));
        }

        List<string> FailureRules = FailureRuleIds(Result, "F125");
        Assert.That(FailureRules, Does.Not.Contain("Routing.Path.NoUnnecessaryDetour"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Class.LocalDecisionBranch"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.HalfPolylineMidspan"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.BranchLabelNearBranch"));
        Assert.That(FailureRules, Does.Not.Contain("Routing.Label.CentredOnConnectorSpine"));
    }

    [Test]
    public void Pipeline_F95_HtmlEdgeLabel_CentredOnConnector()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F95 — HTML in edge label

            ```mermaid
            flowchart LR
            A -->|"<b>Approve</b> or <i>Reject</i>"| B
            ```
            """);

        EdgeActualResult Edge = Result.ActualResults.Diagrams.Single().Edges
            .Single(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label));

        Assert.That(Edge.LabelFacts["CentredOnConnectorHorizontally"], Is.EqualTo(true));
        Assert.That(Edge.LabelFacts["CentredOnConnectorVertically"], Is.EqualTo(true));
        Assert.That(Edge.LabelFacts["CentredOnConnectorSpine"], Is.EqualTo(true));
    }

    [Test]
    public void Pipeline_F96_MarkdownEdgeLabel_CentredOnConnector()
    {
        MermaidRuleValidationPipelineResult Result = RunDiagram("""
            ### F96 — Markdown in edge label

            ```mermaid
            flowchart LR
            A -->|"**Strong** / *emphasis* / `code`"| B
            ```
            """);

        EdgeActualResult Edge = Result.ActualResults.Diagrams.Single().Edges
            .Single(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label));

        Assert.That(Edge.LabelFacts["CentredOnConnectorHorizontally"], Is.EqualTo(true));
        Assert.That(Edge.LabelFacts["CentredOnConnectorVertically"], Is.EqualTo(true));
        Assert.That(Edge.LabelFacts["CentredOnConnectorSpine"], Is.EqualTo(true));
    }

    private MermaidRuleValidationPipelineResult RunDiagram(string Markdown)
    {
        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-catalogue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(OutputDirectory);
        string TempFile = Path.Combine(OutputDirectory, "diagram.md");
        File.WriteAllText(TempFile, Markdown);
        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);
        return Pipeline.ProcessMarkdownFile(TempFile, OutputDirectory);
    }

    private static string DescribeAttachFailure(EdgeActualResult Edge)
    {
        List<string> Details = [];
        foreach (string Key in new[] { "Path.LayoutAttachFailure", "Path.RenderedAttachFailure" })
        {
            if (Edge.Facts.TryGetValue(Key, out object? Value) && Value is not null)
            {
                Details.Add($"{Key}={Value}");
            }
        }

        return string.Join("; ", Details);
    }

    /// <summary>
    /// Checks a decision branch leaves the diamond shoulder on the side of its target and lands on the target's top centre.
    /// </summary>
    private static void AssertBranchLeavesFacingShoulder(EdgeActualResult Branch, NodeActualResult Hub, NodeActualResult Target, string BranchName)
    {
        double TargetSide = Math.Sign(Target.Bounds.CentreX - Hub.Bounds.CentreX);
        Assert.That(TargetSide, Is.Not.EqualTo(0.0), $"{BranchName} target should sit beside the decision");
        Assert.That((Branch.Path.Points[0].X - Hub.Bounds.CentreX) * TargetSide, Is.GreaterThan(8.0), $"{BranchName} branch should leave the shoulder that faces its target, not the bottom vertex");
        Assert.That(Branch.Path.Points[Branch.Path.Points.Count - 1].X, Is.EqualTo(Target.Bounds.CentreX).Within(1.0), $"{BranchName} branch should land on the target's top centre");
    }

    private static List<string> FailureRuleIds(MermaidRuleValidationPipelineResult Result, string DiagramId) => [.. Result.ValidationReport.Failures
            .Where(Failure => Failure.DiagramId == DiagramId)
            .Select(Failure => Failure.RuleId)];
}
