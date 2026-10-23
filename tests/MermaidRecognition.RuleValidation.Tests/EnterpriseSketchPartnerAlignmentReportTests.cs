using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

[TestFixture, NonParallelizable]
public sealed class EnterpriseSketchPartnerAlignmentReportTests
{
    [TestCase("A23")]
    [TestCase("A26")]
    [TestCase("A29")]
    [TestCase("A33")]
    [TestCase("A38")]
    [TestCase("A40")]
    public void ReportPartnerAlignmentLog(string id)
    {
        var scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == id);
        var context = new FlowchartRenderContext { UseCellRegions = true };
        var contract = EnterpriseFlowchartContractMapper.Build(
            EnterpriseValidator.Analyze(scenario.Source).Document!);
        Assert.That(FlowchartSugiyamaLayoutEngine.TryLayout(contract, context, out _), Is.True);
        foreach (string Line in context.BandSpaceDiagnostics.Where(Item => Item.StartsWith("PARTNER_ALIGN|", StringComparison.Ordinal)))
        {
            TestContext.WriteLine(Line);
        }

        Assert.Pass("Partner alignment log written for " + id);
    }
}
