using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

[TestFixture]
public sealed class FlowchartLinkVariantTests
{
    [Test]
    public void SolidOpenLine_HasNoArrowMarkers()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A[A] --> B[B]
            C[C] --- D[D]
            """);

        EdgeContract ArrowEdge = Contract.Edges.Single(Edge => Edge.SourceId == "A");
        EdgeContract OpenEdge = Contract.Edges.Single(Edge => Edge.SourceId == "C");

        Assert.That(ArrowEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Point));
        Assert.That(OpenEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(OpenEdge.MarkerStart, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(OpenEdge.Stroke, Is.EqualTo("normal"));
    }

    [Test]
    public void DottedOpenLine_HasNoArrowMarkers()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A[A] -.-> B[B]
            C[C] -.- D[D]
            """);

        EdgeContract ArrowEdge = Contract.Edges.Single(Edge => Edge.SourceId == "A");
        EdgeContract OpenEdge = Contract.Edges.Single(Edge => Edge.SourceId == "C");

        Assert.That(ArrowEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Point));
        Assert.That(ArrowEdge.Stroke, Is.EqualTo("dotted"));
        Assert.That(OpenEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(OpenEdge.MarkerStart, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(OpenEdge.Stroke, Is.EqualTo("dotted"));
    }

    [Test]
    public void ThickOpenLine_HasNoArrowMarkers()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A[A] ==> B[B]
            C[C] === D[D]
            """);

        EdgeContract ArrowEdge = Contract.Edges.Single(Edge => Edge.SourceId == "A");
        EdgeContract OpenEdge = Contract.Edges.Single(Edge => Edge.SourceId == "C");

        Assert.That(ArrowEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Point));
        Assert.That(ArrowEdge.Stroke, Is.EqualTo("thick"));
        Assert.That(OpenEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(OpenEdge.MarkerStart, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(OpenEdge.Stroke, Is.EqualTo("thick"));
    }

    [Test]
    public void DottedArrow_SetsDottedStroke()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A -.-> B
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Stroke, Is.EqualTo("dotted"));
        Assert.That(Edge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Point));
    }

    [Test]
    public void ThickArrow_SetsThickStroke()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A ==> B
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Stroke, Is.EqualTo("thick"));
    }

    [Test]
    public void InvisibleLink_SetsInvisibleStroke()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A ~~~ B
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Stroke, Is.EqualTo("invisible"));
    }

    [Test]
    public void SubgraphDirection_SetsGroupDirection()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                subgraph Inner
                    direction LR
                    X --> Y
                end
            """);

        GroupContract Group = Contract.Groups.Single(Group => Group.Id == "Inner");
        Assert.That(Group.Direction, Is.EqualTo("LeftToRight"));
    }

    [Test]
    public void BidirectionalArrow_SetsBothMarkers()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
                A <--> B
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.MarkerStart, Is.EqualTo(EdgeEndpointMarker.Point));
        Assert.That(Edge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Point));
    }

    [Test]
    public void F23_AllEdgeStyles_ParseExpectedLinks()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A1[A] --> B1[B]
            C1[C] --- D1[D]
            E1[E] -.-> F1[F]
            G1[G] -.- H1[H]
            I1[I] ==> J1[J]
            K1[K] === L1[L]
            M1[M] --o N1[N]
            O1[O] --x P1[P]
            Q1[Q] <--> R1[R]
            """);

        Assert.That(Contract.Edges, Has.Count.EqualTo(9));

        EdgeContract DottedOpen = Contract.Edges.Single(Edge => Edge.SourceId == "G1");
        Assert.That(DottedOpen.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(DottedOpen.Stroke, Is.EqualTo("dotted"));

        EdgeContract ThickArrow = Contract.Edges.Single(Edge => Edge.SourceId == "I1");
        Assert.That(ThickArrow.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Point));
        Assert.That(ThickArrow.Stroke, Is.EqualTo("thick"));

        EdgeContract ThickOpen = Contract.Edges.Single(Edge => Edge.SourceId == "K1");
        Assert.That(ThickOpen.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(ThickOpen.Stroke, Is.EqualTo("thick"));

        EdgeContract CircleEnd = Contract.Edges.Single(Edge => Edge.SourceId == "M1");
        Assert.That(CircleEnd.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Circle));
    }

    [Test]
    public void DottedOpenLink_DefaultMarkerEnd_IsNone()
    {
        FlowchartLinkToken Token = new FlowchartLinkToken();
        Assert.That(Token.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));

        FlowchartLinkArrowParser.ApplyToToken("-.-", Token);
        Assert.That(Token.Stroke, Is.EqualTo("dotted"));
        Assert.That(Token.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));
        Assert.That(Token.MarkerStart, Is.EqualTo(EdgeEndpointMarker.None));
    }

    [Test]
    public void ThickOpenLink_DefaultMarkerEnd_IsNone()
    {
        FlowchartLinkToken Token = new FlowchartLinkToken();
        FlowchartLinkArrowParser.ApplyToToken("===", Token);
        Assert.That(Token.Stroke, Is.EqualTo("thick"));
        Assert.That(Token.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.None));
    }

    [Test]
    public void DoubleCircleLink_SetsCircleMarkersAtBothEnds()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart LR
            A[A] <--> B[B]
            C[C] o--o D[D]
            E[E] x--x F[F]
            """);

        EdgeContract CircleEdge = Contract.Edges.Single(Edge => Edge.SourceId == "C");
        EdgeContract CrossEdge = Contract.Edges.Single(Edge => Edge.SourceId == "E");

        Assert.That(CircleEdge.MarkerStart, Is.EqualTo(EdgeEndpointMarker.Circle));
        Assert.That(CircleEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Circle));
        Assert.That(CrossEdge.MarkerStart, Is.EqualTo(EdgeEndpointMarker.Cross));
        Assert.That(CrossEdge.MarkerEnd, Is.EqualTo(EdgeEndpointMarker.Cross));
    }

    [Test]
    public void InlineEdgeLabel_IsPreserved()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A -- authorize --> B
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Label, Is.EqualTo("authorize"));
    }

    [Test]
    public void LongDashLink_SetsMinLengthFromToken()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A ----> B
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.MinLength, Is.EqualTo(3));
    }

    [Test]
    public void PipeEdgeLabel_IsPreserved()
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
    public void PipeEdgeLabel_OnLongDashAndDottedLinks_IsPreserved()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A ---|Long dash| B
                C -.->|Dotted| D
            """);

        EdgeContract LongDashEdge = Contract.Edges.Single(Edge => Edge.SourceId == "A");
        EdgeContract DottedEdge = Contract.Edges.Single(Edge => Edge.SourceId == "C");

        Assert.That(LongDashEdge.Label, Is.EqualTo("Long dash"));
        Assert.That(DottedEdge.Label, Is.EqualTo("Dotted"));
    }

    [Test]
    public void EdgeMetadataLine_AppliesToEdgeById()
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(
            """
            flowchart TD
                A e1@--> B
                e1@{ curve: basis }
            """);

        EdgeContract Edge = Contract.Edges.Single();
        Assert.That(Edge.Id, Is.EqualTo("e1"));
        Assert.That(Edge.Metadata.ContainsKey("curve"), Is.True);
        CurveMetadata Curve = (CurveMetadata)Edge.Metadata["curve"];
        Assert.That(Curve.Curve, Is.EqualTo("basis"));
        Assert.That(Edge.Curve, Is.EqualTo("basis"));
    }
}
