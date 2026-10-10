using System.Text;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseRelationshipLabelLayoutTests
{
    [Test]
    public void ShortParallelRuns_StaggerLabelsWithinAvailableTerminalClearance()
    {
        var A = new EnterpriseRoutedEdge(new EnterpriseRelationship { Kind = EnterpriseRelationshipKind.Flow },
            "", new() { (200, 50), (200, 140) }, null, null, "6 4", "alpha", 0, 0);
        var B = new EnterpriseRoutedEdge(new EnterpriseRelationship { Kind = EnterpriseRelationshipKind.Flow },
            "", new() { (230, 140), (230, 50) }, null, null, "6 4", "beta", 0, 0);
        EnterpriseRelationshipLabelLayout.Place(new() { A, B }, new());
        Assert.That(Math.Abs(A.LabelY - B.LabelY), Is.GreaterThanOrEqualTo(18));
        foreach (var Edge in new[] { A, B })
        {
            Assert.That(Edge.LabelBreak, Is.Not.Null);
            Assert.That(Edge.LabelY, Is.InRange(72, 118));
            Assert.That(Edge.LabelBreak!.CentreY, Is.EqualTo(Edge.LabelY));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NearbyParallelConnectors_StaggerTextAndKeepBreaksAtTheirLabels(bool Horizontal)
    {
        var Edges = new List<EnterpriseRoutedEdge>();
        for (int Index = 0; Index < 2; Index++)
        {
            var Points = Horizontal
                ? new List<(double X, double Y)> { (50, 150 + Index * 30), (650, 150 + Index * 30) }
                : new List<(double X, double Y)> { (200 + Index * 80, 50), (200 + Index * 80, 650) };
            string Label = Index == 0
                ? "A long relationship caption that needs several lines to remain readable in a crowded corridor"
                : "Another relationship";
            Edges.Add(new(new EnterpriseRelationship { Kind = EnterpriseRelationshipKind.Flow },
                "", Points, null, null, "6 4", Label, 0, 0));
        }
        EnterpriseRelationshipLabelLayout.Place(Edges, new());
        var A = Edges[0];
        var B = Edges[1];
        bool Separated = Math.Abs(A.LabelX - B.LabelX) >= (A.LabelWidth + B.LabelWidth) / 2 + 6
            || Math.Abs(A.LabelY - B.LabelY) >= (A.LabelHeight + B.LabelHeight) / 2 + 6;
        Assert.That(Separated, Is.True, "Labels must have clearance even when their connector midpoints compete.");
        Assert.That(A.LabelLines.Count, Is.GreaterThan(1));
        foreach (var Edge in Edges)
        {
            Assert.That(Edge.LabelBreak, Is.Not.Null);
            Assert.That(Edge.LabelBreak!.CentreX, Is.EqualTo(Edge.LabelX));
            Assert.That(Edge.LabelBreak.CentreY, Is.EqualTo(Edge.LabelY));
            Assert.That(Edge.LabelBreak.HalfGapWidthPx, Is.GreaterThanOrEqualTo(
                (Horizontal ? Edge.LabelWidth : Edge.LabelHeight) / 2 + 6));
            var Other = Edges.Single(Other => Other != Edge);
            Assert.That(Horizontal
                ? Math.Abs(Edge.LabelY - Other.SvgPoints[0].Y) >= Edge.LabelHeight / 2 + 6
                : Math.Abs(Edge.LabelX - Other.SvgPoints[0].X) >= Edge.LabelWidth / 2 + 6,
                Is.True, "Text must also clear the neighbouring connector.");
            StringBuilder Builder = new();
            EnterpriseFlowchartEdgeRouter.Append(Builder, Edge);
            XElement Svg = XElement.Parse(Builder.ToString());
            var Lines = Svg.Descendants("tspan").Select(Line => Line.Value).ToArray();
            Assert.That(string.Join(" ", Lines), Is.EqualTo(Edge.Label));
            Assert.That(Svg.Element("path")!.Attribute("d")!.Value.Split('M', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(2));
        }
    }
}
