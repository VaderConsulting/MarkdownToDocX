using MermaidRecognition.Config;
using MermaidRecognition.Core;

namespace MermaidRecognition.Tests;

[TestFixture]
public sealed class DiagramLayoutSettingsTests
{
    [TestCase("enterprise")]
    [TestCase("flowchart TB")]
    public void MissingSetting_DefaultsToGrid(string Header)
    {
        Assert.That(MermaidRecognizer.Parse(Header).LayoutMode, Is.EqualTo(DiagramLayoutMode.Grid));
    }

    [TestCase("enterprise")]
    [TestCase("flowchart TB")]
    public void UnknownSetting_WarnsOnceAndFallsBackToGrid(string Header)
    {
        DiagramContract Result = MermaidRecognizer.Parse("---\nconfig:\n  layout: freeform\n---\n" + Header);
        Assert.That(Result.LayoutMode, Is.EqualTo(DiagramLayoutMode.Grid));
        Assert.That(Result.Diagnostics.Count(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.EqualTo(1));
    }

    [TestCase("enterprise")]
    [TestCase("flowchart TB")]
    public void Frontmatter_SelectsOpenAcrossDiagramKinds(string Header)
    {
        DiagramContract Result = MermaidRecognizer.Parse("---\nconfig:\n  layout: open\n---\n" + Header);
        Assert.That(Result.LayoutMode, Is.EqualTo(DiagramLayoutMode.Open));
    }
}
