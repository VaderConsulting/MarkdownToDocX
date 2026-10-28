using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

public sealed partial class RetainedEnterprisePreviewTests
{
    [TestCase("application-component extra right-of b +3\n")]
    [TestCase("node extra\n")]
    [TestCase("business-actor extra\n")]
    [TestCase("application-component p NE b +2 +3\napplication-component q below p +2\np -flow-> q\n")]
    public void SourceConstructionAddsNodesWithoutInitialPlacement(string AddedSource)
    {
        var Previous = EnterpriseFlowchartEdgeRouter.StageTimingObserver;
        try
        {
            var Session = new RetainedEnterprisePreview();
            var Initial = Accept(Session, Source, 1);
            var Nodes = new[] { "a", "b", "x", "y" }.ToDictionary(Id => Id, Id => Node(Initial.Svg, Id));
            List<string> Stages = [];
            EnterpriseFlowchartEdgeRouter.StageTimingObserver = (Stage, Time) => { if (Time < 0) Stages.Add(Stage); };
            var Added = Accept(Session, Source + AddedSource, 2);
            Assert.That(Added.Rebuild, Is.False, Added.RebuildReason);
            Assert.That(Added.Kind, Is.EqualTo("LocalRouting"));
            Assert.That(Stages, Does.Not.Contain("enterprise-shared-flowchart-layout").And.Not.Contain("enterprise-contract-map"));
            foreach (var Pair in Nodes) Assert.That(Node(Added.Svg, Pair.Key), Is.EqualTo(Pair.Value));
            Assert.That(Session.CopyInteractionSnapshot()!.Layout.CellNodes!.Count, Is.GreaterThan(4));
        }
        finally { EnterpriseFlowchartEdgeRouter.StageTimingObserver = Previous; }
    }
}
