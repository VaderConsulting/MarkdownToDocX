using MDConverter;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Exposes non-flowchart sources that the shared-grid catalogue currently measures through the wrong renderer.</summary>
[TestFixture, NonParallelizable]
public sealed class CatalogueKindPathTests : ProductionCatalogueTestFixture
{
    /// <summary>Keeps the missing kind-specific production adapters visible as a failing check, rather than dropping sources or inventing zero findings.</summary>
    [Test]
    public void SharedGridCatalogueMustUseTheActualProductionDiagramKind()
    {
        var Wrong = new List<string>();
        var Rows = new List<string> { "Scenario\tSourceKind\tProductionRenderer\tSharedGridApplicable" };
        foreach (var Scenario in RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!))
        {
            var Contract = Scenario.Enterprise ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
                : MermaidRecognizer.Parse(Scenario.Source);
            bool Shared = Contract.Kind.Equals("flowchart", StringComparison.OrdinalIgnoreCase) || Contract.Kind.Equals("graph", StringComparison.OrdinalIgnoreCase);
            string Renderer = Scenario.Enterprise ? "Native Enterprise" : Shared ? "Native flowchart" : "MermaidSharp kind renderer";
            Rows.Add($"{Scenario.Id}\t{(Scenario.Enterprise ? "enterprise" : Contract.Kind)}\t{Renderer}\t{Shared}");
            if (!Shared && Contract.Nodes.Count != 0) Wrong.Add(Scenario.Id + ":" + Contract.Kind);
        }
        File.WriteAllLines(Path.Combine(FindRoot(), "docs", "catalogue-production-kind-paths.tsv"), Rows);
        TestContext.Out.WriteLine("CATALOGUE_KIND_PATH_MISMATCH|" + string.Join(",", Wrong));
        Assert.That(Wrong, Is.Empty, "These catalogue inputs need their actual DOCX renderer's geometry; synthetic flowchart metrics cannot stand in for it.");
    }

    /// <summary>Captures representative sequence sources through their actual DOCX PNG dispatcher without pretending they have shared-grid routes.</summary>
    /// <param name="Id">Unchanged source that exposed a renderer-kind mismatch.</param>
    [TestCase("EIP7"), TestCase("EIP42"), TestCase("EIP78"), TestCase("EIP211"), TestCase("EIP271"), TestCase("EIP296")]
    [Explicit("Writes actual native-kind PNGs for the path investigation only.")]
    public void CaptureActualNonFlowchartProductionPng(string Id)
    {
        var Scenario = RouteRuleCatalogueAuditTests.Catalogue().Select(Case =>
            (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Single(Case => Case.Id == Id);
        byte[]? Png = MermaidDiagramExport.TryRenderPng(Scenario.Source);
        Assert.That(Png, Is.Not.Null.And.Not.Empty);
        string DirectoryPath = Path.Combine(FindRoot(), "out", "markup-review", "native-kind-paths");
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllBytes(Path.Combine(DirectoryPath, Id + "-actual-docx.png"), Png!);
        TestContext.Out.WriteLine($"NATIVE_KIND_RENDER|{Id}|{DirectoryPath}");
    }

    /// <summary>Finds the repository that owns the catalogues and the review ledgers.</summary>
    /// <returns>The existing source root.</returns>
    private static string FindRoot()
    {
        for (DirectoryInfo? Current = new(AppContext.BaseDirectory); Current != null; Current = Current.Parent)
            if (File.Exists(Path.Combine(Current.FullName, "enterprise.md"))) return Current.FullName;
        throw new DirectoryNotFoundException("Cannot locate the source catalogues.");
    }
}
