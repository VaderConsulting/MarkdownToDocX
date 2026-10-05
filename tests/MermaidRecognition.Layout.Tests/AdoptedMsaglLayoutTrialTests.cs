using System.Globalization;
using System.Text;

using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Tests;

/// <summary>
/// Trial of the adopted MSAGL routers. The default render path stays on the custom router.
/// </summary>
[TestFixture]
public sealed class AdoptedMsaglLayoutTrialTests
{
    private const string SAMPLE_03_SOURCE = """
        flowchart TB
          subgraph dc["Primary data centre"]
            subgraph appNode["App Server Node"]
              jvm["Java VM"]
              war["shop.war"]
            end
            pg["PostgreSQL"]
            lb["Load Balancer"]
            lan["DC LAN"]
          end
          subgraph webShop["Web Shop"]
            checkout(["Checkout"])
          end
          shopUi["Shop Web UI"]
          ordering(["Online Ordering"])
          order["Order"]
          hosting(["Java Application Hosting"])
          dbSvc(["Database Service"])
          orderTbl["orders table"]
          webShop --> shopUi
          checkout --> ordering
          shopUi --> ordering
          checkout --> order
          war --> webShop
          orderTbl --> order
          jvm --> hosting
          pg --> dbSvc
          hosting --> webShop
          dbSvc --> webShop
          pg --> orderTbl
          lb -->|HTTPS| appNode
          lan --> lb
          lan --> appNode
        """;

    [Test]
    public void DefaultBridge_StillUsesCustomRouter()
    {
        Assert.That(NativeFlowchartRenderBridge.TryRenderFlowchartSvg(SAMPLE_03_SOURCE, out string Svg), Is.True);
        Assert.That(Svg, Does.Contain("data-edge-source"));
        Assert.That(Svg, Does.Not.Contain("AdoptedMsagl"));
    }

    [Test]
    public void GeometryBuilder_AddsClustersForSubgraphs()
    {
        DiagramContract Contract = MermaidRecognizer.Parse(SAMPLE_03_SOURCE);
        Assert.That(
            AdoptedMsaglLayoutEngine.TryLayoutAndRoute(
                Contract,
                AdoptedMsaglRoutingProfile.SharedRectilinear,
                out GeometryGraph Graph,
                out Dictionary<string, Node> NodesById),
            Is.True);
        Assert.That(NodesById.ContainsKey("dc"), Is.True);
        Assert.That(NodesById["dc"], Is.TypeOf<Cluster>());
        Assert.That(NodesById.ContainsKey("appNode"), Is.True);
        Assert.That(NodesById["appNode"], Is.TypeOf<Cluster>());
        Assert.That(Graph.Edges.Count, Is.GreaterThan(0));
        int Routed = 0;
        foreach (Edge LayoutEdge in Graph.Edges)
        {
            if (LayoutEdge.Curve != null)
            {
                Routed++;
            }
        }

        Assert.That(Routed, Is.GreaterThan(0));
    }

    [Test]
    public void RectilinearAndSpline_RenderSample03WithEdgeCurves()
    {
        Assert.That(
            AdoptedMsaglRenderBridge.TryRenderSvg(
                SAMPLE_03_SOURCE,
                AdoptedMsaglRoutingProfile.SharedRectilinear,
                out string RectilinearSvg),
            Is.True);
        Assert.That(RectilinearSvg, Does.StartWith("<svg"));
        Assert.That(RectilinearSvg, Does.Contain("data-edge-source=\"pg\""));
        Assert.That(RectilinearSvg, Does.Contain("data-edge-target=\"orderTbl\""));
        Assert.That(RectilinearSvg, Does.Contain("<path d="));
        Assert.That(RectilinearSvg, Does.Contain("<polygon"));

        Assert.That(
            AdoptedMsaglRenderBridge.TryRenderSvg(
                SAMPLE_03_SOURCE,
                AdoptedMsaglRoutingProfile.SharedSpline,
                out string SplineSvg),
            Is.True);
        Assert.That(SplineSvg, Does.StartWith("<svg"));
        Assert.That(SplineSvg, Does.Contain("data-edge-source=\"pg\""));
        Assert.That(SplineSvg, Does.Contain("<path d="));
        Assert.That(SplineSvg, Is.Not.EqualTo(RectilinearSvg));
    }

    [Test]
    public void SharedProfiles_AreTheSameForFlowchartAndEnterprise()
    {
        Assert.That(AdoptedMsaglRoutingProfile.SharedRectilinear.Style, Is.EqualTo(AdoptedMsaglRoutingStyle.Rectilinear));
        Assert.That(AdoptedMsaglRoutingProfile.SharedSpline.Style, Is.EqualTo(AdoptedMsaglRoutingStyle.Spline));
        Assert.That(AdoptedMsaglRoutingProfile.SharedRectilinear.ObstaclePaddingPx, Is.EqualTo(8.0));
        Assert.That(AdoptedMsaglRoutingProfile.SharedSpline.ObstaclePaddingPx, Is.EqualTo(8.0));
        Assert.That(AdoptedMsaglRoutingProfile.SharedRectilinear.LineStroke, Is.EqualTo("#333333"));
        Assert.That(AdoptedMsaglRoutingProfile.SharedSpline.LineStroke, Is.EqualTo("#333333"));
    }

    [Test]
    public void PictureGate_StillReportsListedDefectsOnStoredCustomPictures()
    {
        FlowchartRenderedSvgQualityReport FlowchartReport = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-03-flowchart-current",
            SAMPLE_03_SOURCE,
            File.ReadAllText(FindFixture("enterprise-03-flowchart.current.svg")));
        AssertDetected(FlowchartReport, "NodeIntersection", "dbSvc", "webShop", "orderTbl");
        AssertDetected(FlowchartReport, "OverlayingEdges", "pg", "orderTbl", "lan", "appNode");
        AssertDetected(FlowchartReport, "LongDiagonal", "jvm", "hosting");
        AssertDetected(FlowchartReport, "MixedStyle");

        FlowchartRenderedSvgQualityReport EnterpriseReport = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-03-enterprise-current",
            SAMPLE_03_SOURCE,
            File.ReadAllText(FindFixture("enterprise-03-enterprise.current.svg")));
        AssertDetected(EnterpriseReport, "DanglingArrowhead", "pg", "orderTbl");
        AssertDetected(EnterpriseReport, "DetachedEndpoint", "pg", "orderTbl");
        AssertDetected(EnterpriseReport, "LabelOnBox", "HTTPS", "pg");
        AssertDetected(EnterpriseReport, "ForeignGroupTraversal", "dbSvc", "webShop", "dc");
        AssertDetected(EnterpriseReport, "OverlayingEdges", "lan", "appNode", "lb", "appNode");

        FlowchartRenderedSvgQualityReport OrderReport = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-01-current",
            File.ReadAllText(FindEnterpriseSample("01-flowchart.mmd")),
            File.ReadAllText(FindFixture("enterprise-01-flowchart.current.svg")));
        AssertDetected(OrderReport, "RankGap");
        AssertDetected(OrderReport, "LongDiagonal");
        AssertDetected(OrderReport, "LabelOwnEdge", "order details");

        FlowchartRenderedSvgQualityReport SalesReport = FlowchartRenderedSvgQualityGate.EvaluatePicture(
            "sample-06-current",
            File.ReadAllText(FindEnterpriseSample("06-flowchart.mmd")),
            File.ReadAllText(FindFixture("enterprise-06-flowchart.current.svg")));
        AssertDetected(SalesReport, "OverlayingEdges", "fulfil", "selling", "selling", "seller");
        AssertDetected(SalesReport, "CrampedArrow", "clerk", "seller");
    }

    [Test]
    public void TrialEngines_ReportPictureGateCountsForKeyDiagrams()
    {
        List<(string Name, string Source)> Diagrams =
        [
            ("sample-03", SAMPLE_03_SOURCE),
            ("sample-01", File.ReadAllText(FindEnterpriseSample("01-flowchart.mmd"))),
            ("sample-06", File.ReadAllText(FindEnterpriseSample("06-flowchart.mmd")))
        ];

        StringBuilder Summary = new StringBuilder();
        foreach ((string Name, string Source) Diagram in Diagrams)
        {
            Assert.That(NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Diagram.Source, out string CustomSvg), Is.True);
            Assert.That(
                AdoptedMsaglRenderBridge.TryRenderSvg(
                    Diagram.Source,
                    AdoptedMsaglRoutingProfile.SharedRectilinear,
                    out string RectilinearSvg),
                Is.True,
                Diagram.Name + " rectilinear render failed");
            Assert.That(
                AdoptedMsaglRenderBridge.TryRenderSvg(
                    Diagram.Source,
                    AdoptedMsaglRoutingProfile.SharedSpline,
                    out string SplineSvg),
                Is.True,
                Diagram.Name + " spline render failed");

            FlowchartRenderedSvgQualityReport Custom = FlowchartRenderedSvgQualityGate.EvaluatePicture(
                Diagram.Name + "-custom",
                Diagram.Source,
                CustomSvg);
            FlowchartRenderedSvgQualityReport Rectilinear = FlowchartRenderedSvgQualityGate.EvaluatePicture(
                Diagram.Name + "-rectilinear",
                Diagram.Source,
                RectilinearSvg);
            FlowchartRenderedSvgQualityReport Spline = FlowchartRenderedSvgQualityGate.EvaluatePicture(
                Diagram.Name + "-spline",
                Diagram.Source,
                SplineSvg);
            Summary.Append(Diagram.Name);
            Summary.Append(" custom=");
            Summary.Append(Custom.Failures.Count.ToString(CultureInfo.InvariantCulture));
            Summary.Append(" rectilinear=");
            Summary.Append(Rectilinear.Failures.Count.ToString(CultureInfo.InvariantCulture));
            Summary.Append(" spline=");
            Summary.AppendLine(Spline.Failures.Count.ToString(CultureInfo.InvariantCulture));
        }

        TestContext.Out.WriteLine(Summary.ToString());
        Assert.That(Summary.ToString(), Does.Contain("custom="));
    }

    private static void AssertDetected(
        FlowchartRenderedSvgQualityReport Report,
        string Kind,
        params string[] InvolvedIds)
    {
        List<FlowchartLayoutQualityFailure> All = [];
        All.AddRange(Report.Failures);
        All.AddRange(Report.Tolerated);
        foreach (FlowchartLayoutQualityFailure Failure in All)
        {
            if (!string.Equals(Failure.Kind, Kind, StringComparison.Ordinal))
            {
                continue;
            }

            if (Kind == "LabelOwnEdge" || Kind == "LabelOnBox")
            {
                bool AllFound = true;
                foreach (string Token in InvolvedIds)
                {
                    if (!Failure.Description.Contains(Token, StringComparison.Ordinal)
                        && !ContainsId(Failure, Token))
                    {
                        AllFound = false;
                        break;
                    }
                }

                if (AllFound)
                {
                    return;
                }

                continue;
            }

            if (ContainsAllIds(Failure, InvolvedIds))
            {
                return;
            }
        }

        Assert.Fail(
            "The gate did not report "
            + Kind
            + " for "
            + string.Join(", ", InvolvedIds)
            + "."
            + Environment.NewLine
            + FormatReport(Report));
    }

    private static bool ContainsAllIds(FlowchartLayoutQualityFailure Failure, IReadOnlyList<string> InvolvedIds)
    {
        foreach (string Id in InvolvedIds)
        {
            bool Found = Failure.Description.Contains(Id, StringComparison.Ordinal);
            if (!Found)
            {
                foreach (string Involved in Failure.InvolvedIds)
                {
                    if (string.Equals(Involved, Id, StringComparison.Ordinal))
                    {
                        Found = true;
                        break;
                    }
                }
            }

            if (!Found)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsId(FlowchartLayoutQualityFailure Failure, string Id)
    {
        foreach (string Involved in Failure.InvolvedIds)
        {
            if (string.Equals(Involved, Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindEnterpriseSample(string FileName)
    {
        string FromOutput = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "EnterpriseFlowchartSamples",
            FileName);
        if (File.Exists(FromOutput))
        {
            return FromOutput;
        }

        return Path.Combine(FindRepoRoot(), "tests", "MermaidRecognition.Layout.Tests", "EnterpriseFlowchartSamples", FileName);
    }

    private static string FindFixture(string FileName)
    {
        string FromOutput = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "QualityGateFixtures",
            FileName);
        if (File.Exists(FromOutput))
        {
            return FromOutput;
        }

        return Path.Combine(FindRepoRoot(), "tests", "MermaidRecognition.Layout.Tests", "QualityGateFixtures", FileName);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? Cursor = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (Cursor is not null)
        {
            if (File.Exists(Path.Combine(Cursor.FullName, "flow.md")))
            {
                return Cursor.FullName;
            }

            Cursor = Cursor.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private static string FormatReport(FlowchartRenderedSvgQualityReport Report)
    {
        StringBuilder Builder = new StringBuilder();
        Builder.Append(Report.DiagramName);
        Builder.Append(": ");
        Builder.Append(Report.Failures.Count.ToString(CultureInfo.InvariantCulture));
        Builder.AppendLine(" failures");
        foreach (FlowchartLayoutQualityFailure Failure in Report.Failures)
        {
            Builder.Append("  FAIL ");
            Builder.Append(Failure.Kind);
            Builder.Append(": ");
            Builder.AppendLine(Failure.Description);
        }

        return Builder.ToString();
    }
}
