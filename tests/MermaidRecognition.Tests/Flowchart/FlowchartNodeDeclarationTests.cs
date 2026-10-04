using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartNodeDeclarationTests
{
    [Test]
    public void MetadataShape_SetsShapeFromMetadata()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                Doc@{ shape: doc }
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "Doc");
        Assert.That(Node.Shape, Is.EqualTo("doc"));
        Assert.That(Node.ShapeSource, Is.EqualTo("metadata"));
        Assert.That(Node.Metadata["shape"], Is.TypeOf<ShapeMetadata>());
    }

    [Test]
    public void HtmlLabelType_IsPreserved()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                H@{ label: "<b>Hi</b>", labelType: html }
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "H");
        Assert.That(Node.LabelType, Is.EqualTo(MermaidLabelType.Html));
        Assert.That(Node.Label, Is.EqualTo("<b>Hi</b>"));
    }

    [Test]
    public void BracketShape_SetsRectLabel()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                Start[Begin]
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "Start");
        Assert.That(Node.Label, Is.EqualTo("Begin"));
        Assert.That(Node.Shape, Is.EqualTo("rect"));
        Assert.That(Node.ShapeSource, Is.EqualTo("bracket"));
    }

    [Test]
    public void QuotedBracketLabel_StripsDelimiterQuotes()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            Only["Single isolated node"]
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "Only");
        Assert.That(Node.Label, Is.EqualTo("Single isolated node"));
    }

    [Test]
    public void RoundedBracket_SetsRoundedShape()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                Step(Process)
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "Step");
        Assert.That(Node.Label, Is.EqualTo("Process"));
        Assert.That(Node.Shape, Is.EqualTo("rounded"));
    }

    [Test]
    public void DiamondBracket_SetsDiamondShape()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                Q{Yes or no?}
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "Q");
        Assert.That(Node.Shape, Is.EqualTo("diamond"));
    }

    [Test]
    public void HexagonBracket_SetsHexagonShape()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A{{Hexagon}} --> B[Next]
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "A");
        Assert.That(Node.Label, Is.EqualTo("Hexagon"));
        Assert.That(Node.Shape, Is.EqualTo("hexagon"));
        Assert.That(Node.ShapeSource, Is.EqualTo("bracket"));
    }

    [Test]
    public void DoubleCircleBracket_SetsDoubleCircleShape()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            Start(((Start)))
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "Start");
        Assert.That(Node.Shape, Is.EqualTo("doublecircle"));
    }

    [Test]
    public void DoubleCircleLinkStatement_SetsDoubleCircleShapeOnBothNodes()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            Start(((Start))) --> Stop(((Stop)))
            """);

        Assert.That(Contract.Nodes.Single(Node => Node.Id == "Start").Shape, Is.EqualTo("doublecircle"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "Stop").Shape, Is.EqualTo("doublecircle"));
    }

    [Test]
    public void AsymmetricBracket_SetsAsymmetricShape()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A>Asymmetric flag] --> B[Target]
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "A");
        Assert.That(Node.Label, Is.EqualTo("Asymmetric flag"));
        Assert.That(Node.Shape, Is.EqualTo("asymmetric"));
        Assert.That(Node.ShapeSource, Is.EqualTo("bracket"));
    }

    [Test]
    public void CircleBracket_SetsCircleShape()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                End((Done))
            """);

        NodeContract Node = Contract.Nodes.Single(Node => Node.Id == "End");
        Assert.That(Node.Shape, Is.EqualTo("circle"));
    }

    [Test]
    public void QuotedLinkStatementLabels_StripsDelimiterQuotes()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A["Label with spaces"] --> B["Second label"]
            """);

        Assert.That(Contract.Nodes.Single(Node => Node.Id == "A").Label, Is.EqualTo("Label with spaces"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "B").Label, Is.EqualTo("Second label"));
    }

    [Test]
    public void EdgeLabel_IsPreserved()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A -->|Yes| B
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Label, Is.EqualTo("Yes"));
    }

    [Test]
    public void TrapezoidLeanRightBracket_F81Top_ParsesLabelAndShape()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            Top[/Wide top/] --> Bottom[\Wide bottom/]
            """);

        NodeContract Top = Contract.Nodes.Single(Node => Node.Id == "Top");
        NodeContract Bottom = Contract.Nodes.Single(Node => Node.Id == "Bottom");
        Assert.That(Top.Label, Is.EqualTo("Wide top"));
        Assert.That(Top.Shape, Is.EqualTo("lean_right"));
        Assert.That(Top.ShapeSource, Is.EqualTo("bracket"));
        Assert.That(Bottom.Label, Is.EqualTo("Wide bottom"));
        Assert.That(Bottom.Shape, Is.EqualTo("inv_trapezoid"));
    }

    [Test]
    public void TrapezoidBracketVariants_ParseMermaidDelimiters()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
            WideTop[/text\]
            WideBottom[\text\]
            """);

        Assert.That(Contract.Nodes.Single(Node => Node.Id == "WideTop").Shape, Is.EqualTo("trapezoid"));
        Assert.That(Contract.Nodes.Single(Node => Node.Id == "WideBottom").Shape, Is.EqualTo("lean_left"));

        DiagramContract FourShapes = FlowchartRecognizer.Parse(
            """
            flowchart LR
            T[/trapezoid \]
            I[\inv_trapezoid /]
            """);
        Assert.That(FourShapes.Nodes.Single(Node => Node.Id == "T").Shape, Is.EqualTo("trapezoid"));
        Assert.That(FourShapes.Nodes.Single(Node => Node.Id == "I").Shape, Is.EqualTo("inv_trapezoid"));
    }
}
