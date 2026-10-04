using System.IO;

using MermaidRecognition.RuleValidation.Models;

using MermaidRecognition.RuleValidation.Pipeline;

using NUnit.Framework;



namespace MermaidRecognition.RuleValidation.Tests;



/// <summary>

/// Regression tests for F34 retry-loop routing and layout validation.

/// </summary>

[TestFixture]

public sealed class FlowF34RuleValidationTests

{

    private string _SpecDirectory = string.Empty;



    [SetUp]

    public void SetUp() => _SpecDirectory = Path.Combine(AppContext.BaseDirectory, "Rule Specification");



    [Test]

    public void FlowMarkdown_F34_PassesLayoutOverlapAndRoutingValidation()

    {

        string OutputDirectory = Path.Combine(Path.GetTempPath(), "flow-f34-" + Guid.NewGuid().ToString("N"));

        string TempMarkdown = """

            ### F34 — Simple cycle (retry loop)



            ```mermaid

            flowchart LR

            A[Receive] --> B{OK?}

            B -->|Yes| C[Done]

            B -->|No| D[Retry]

            D --> A

            ```

            """;

        string TempFile = Path.Combine(OutputDirectory, "f34.md");

        Directory.CreateDirectory(OutputDirectory);

        File.WriteAllText(TempFile, TempMarkdown);



        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(_SpecDirectory);

        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(TempFile, OutputDirectory);



        List<string> F34FailureRules = Result.ValidationReport.Failures

            .Where(Failure => Failure.DiagramId == "F34")

            .Select(Failure => Failure.RuleId)

            .ToList();



        Assert.That(F34FailureRules, Is.Empty, "F34 retry loop should pass layout and routing validation.");

        DiagramActualResult Diagram = Result.ActualResults.Diagrams.Single();
        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => !string.IsNullOrWhiteSpace(Candidate.Label)))
        {
            Assert.That(Edge.LabelFacts["NotOnConnectorLine"], Is.EqualTo(true));
        }

        foreach (EdgeActualResult Edge in Diagram.Edges.Where(Candidate => Candidate.IsVisible))
        {
            Assert.That(Edge.Facts["Ports.PerpendicularAttach"], Is.EqualTo(true), Edge.EdgeId);
            Assert.That(Edge.Facts["Path.RenderedPathClearsUnrelatedNodeBodies"], Is.EqualTo(true), Edge.EdgeId);
            Assert.That(Edge.Facts["Path.NoVisibleKinkInSmoothBody"], Is.EqualTo(true), Edge.EdgeId);
        }

        EdgeActualResult RetryLoopEdge = Diagram.Edges.Single(
            Edge => Edge.SourceNodeId == "D" && Edge.TargetNodeId == "A");
        Assert.That(RetryLoopEdge.Facts["Path.DoesNotIntersectUnrelatedNodeBodies"], Is.EqualTo(true));

        NodeActualResult HubNode = Diagram.Nodes.Single(Node => Node.NodeId == "B");
        EdgeActualResult YesBranch = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-B-C-1");
        EdgeActualResult NoBranch = Diagram.Edges.Single(Edge => Edge.EdgeId == "L-B-D-2");

        Assert.That(YesBranch.Path.Points.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(NoBranch.Path.Points.Count, Is.GreaterThanOrEqualTo(3));

        double YesGuideY = YesBranch.Path.Points[2].Y;
        double NoGuideY = NoBranch.Path.Points[2].Y;
        double HubCentreY = HubNode.Bounds.CentreY;
        double DoneCentreY = Diagram.Nodes.Single(Node => Node.NodeId == "C").Bounds.CentreY;
        double RetryCentreY = Diagram.Nodes.Single(Node => Node.NodeId == "D").Bounds.CentreY;

        Assert.That(
            Math.Abs(YesGuideY - DoneCentreY),
            Is.LessThan(2.0),
            "Yes branch guide should bow to the Done target cross-axis, not stop at the source stub.");
        Assert.That(
            Math.Abs(NoGuideY - RetryCentreY),
            Is.LessThan(2.0),
            "No branch guide should bow to the Retry target cross-axis, not stop at the source stub.");
        Assert.That(
            Math.Abs((YesGuideY - HubCentreY) + (NoGuideY - HubCentreY)),
            Is.LessThan(2.0),
            "LR decision branches should mirror about the hub cross-axis.");
        Assert.That(NoBranch.Facts["Path.NoVisibleKinkInSmoothBody"], Is.EqualTo(true), "L-B-D-2");
    }

}



