using System;
using System.IO;
using System.Linq;
using LiveEditor.Azure;
using MDConverter;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class AzureResourceGraphImportTests
{
    [Test]
    public void LandgateCsv_ImportsAsParseableEnterprise()
    {
        string CsvPath = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "..", "..", "..", "..", "..", "in", "Landgate.csv");
        CsvPath = System.IO.Path.GetFullPath(CsvPath);
        Assert.That(File.Exists(CsvPath), Is.True, "Expected sample CSV at in/Landgate.csv");

        AzureResourceGraphImportOptions Options = AzureResourceGraphImportOptions.CreateEnterpriseDefaults();
        bool Ok = AzureResourceGraphInterop.TryImportFile(CsvPath, Options, out string Source, out string Warnings, out string Error);
        Assert.That(Ok, Is.True, Error);
        Assert.That(Source, Does.StartWith("enterprise"));
        int ResourceLines = 0;
        foreach (string Line in Source.Split('\n'))
        {
            string Trimmed = Line.Trim();
            if (Trimmed.Length == 0 || Trimmed.StartsWith("enterprise", StringComparison.Ordinal) || Trimmed.StartsWith("title ", StringComparison.Ordinal) || Trimmed.StartsWith("version ", StringComparison.Ordinal))
            {
                continue;
            }

            if (Trimmed.Contains(" -", StringComparison.Ordinal) && Trimmed.Contains("->", StringComparison.Ordinal))
            {
                continue;
            }

            ResourceLines++;
        }

        Assert.That(ResourceLines, Is.LessThan(200), "Enterprise defaults should exclude extensions, alerts, NICs, and disks.");
        EnterpriseParseResult ParseResult = EnterpriseParser.ParseDocument(Source);
        bool HasErrors = ParseResult.Diagnostics.Any(Diagnostic => Diagnostic.Severity == MermaidDiagnosticSeverity.Error);
        Assert.That(HasErrors, Is.False);
    }

    [Test]
    public void EnterpriseDefaults_ExcludeDisksAndNics()
    {
        AzureResourceGraphImportOptions Options = AzureResourceGraphImportOptions.CreateEnterpriseDefaults();
        Assert.That(Options.EnabledCategories.Contains(AzureResourceGraphIncludeCatalog.CategoryKeys.Disks), Is.False);
        Assert.That(Options.EnabledCategories.Contains(AzureResourceGraphIncludeCatalog.CategoryKeys.NetworkInterfaces), Is.False);
        Assert.That(Options.EnabledCategories.Contains(AzureResourceGraphIncludeCatalog.CategoryKeys.VmExtensions), Is.False);
        Assert.That(Options.EnabledCategories.Contains(AzureResourceGraphIncludeCatalog.CategoryKeys.MonitoringAlerts), Is.False);
    }

    [Test]
    [Explicit("Full layout and rasterise of Landgate enterprise defaults can take several minutes.")]
    public void LandgateCsv_ImportsAndRendersPreview()
    {
        string CsvPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "..", "..", "..", "..", "..", "in", "Landgate.csv"));
        AzureResourceGraphImportOptions Options = AzureResourceGraphImportOptions.CreateEnterpriseDefaults();
        Assert.That(AzureResourceGraphInterop.TryImportFile(CsvPath, Options, out string Source, out _, out string Error), Is.True, Error);
        bool Rendered = MermaidDiagramExport.TryRenderPreview(Source, out string Svg, out string Failure);
        Assert.That(Rendered, Is.True, Failure);
        Assert.That(Svg, Is.Not.Empty);
    }

    [Test]
    public void SlotRow_GetsParentArmId()
    {
        string Child = "/subscriptions/000/resourceGroups/rg/providers/Microsoft.Web/sites/app/slots/staging";
        string? Parent = AzureResourceGraphEnterpriseMapper.TryGetParentArmId(Child);
        Assert.That(Parent, Is.EqualTo("/subscriptions/000/resourceGroups/rg/providers/Microsoft.Web/sites/app"));
    }
}
