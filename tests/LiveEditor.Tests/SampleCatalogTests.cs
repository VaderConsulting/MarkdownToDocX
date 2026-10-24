using System.Collections.Generic;
using LiveEditor.Editor;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Every diagram type has a simple sample and a sample that uses its options.
/// </summary>
[TestFixture]
public sealed class SampleCatalogTests
{
    [Test]
    public void EveryDiagramTypeHasASimpleSampleAndAFullSample()
    {
        string[] Expected = new string[]
        {
            "enterprise",
            "flowchart",
            "sequence",
            "class",
            "state",
            "er",
            "gantt",
            "pie",
            "gitGraph",
            "mindmap",
            "timeline",
            "journey",
            "quadrantChart",
            "xychart",
            "architecture",
            "block",
            "packet",
            "radar",
            "treemap",
            "c4"
        };
        Assert.That(SampleCatalog.Groups.Count, Is.EqualTo(Expected.Length));
        for (int Index = 0; Index < Expected.Length; Index++)
        {
            DiagramSampleGroup Group = SampleCatalog.Groups[Index];
            Assert.That(Group.KindId, Is.EqualTo(Expected[Index]));
            Assert.That(Group.MenuTitle, Is.EqualTo(DiagramKindCatalog.TitleFor(Expected[Index])));
            Assert.That(DiagramKindCatalog.Detect(Group.SimpleSource), Is.EqualTo(Expected[Index]));
            Assert.That(DiagramKindCatalog.Detect(Group.FullSource), Is.EqualTo(Expected[Index]));
            Assert.That(Group.SimpleSource, Is.Not.EqualTo(Group.FullSource));
            Assert.That(Group.SimpleTitle, Does.EndWith(" (Simple)"));
            Assert.That(Group.CompleteTitle, Does.EndWith(" (Complete)"));
            Assert.That(Group.SimpleTitle, Does.Not.StartWith(" (Simple)"));
            Assert.That(Group.CompleteTitle, Does.Not.StartWith(" (Complete)"));
        }
    }

    [Test]
    public void EnterpriseAllOptionsSampleParses()
    {
        DiagramSampleGroup? Enterprise = null;
        for (int Index = 0; Index < SampleCatalog.Groups.Count; Index++)
        {
            if (SampleCatalog.Groups[Index].KindId == "enterprise")
            {
                Enterprise = SampleCatalog.Groups[Index];
            }
        }

        Assert.That(Enterprise, Is.Not.Null);
        EnterpriseParseResult Result = EnterpriseParser.Parse(Enterprise!.FullSource);
        List<string> Errors = new List<string>();
        for (int Index = 0; Index < Result.Diagnostics.Count; Index++)
        {
            EnterpriseDiagnostic Diagnostic = Result.Diagnostics[Index];
            if (Diagnostic.Severity == MermaidDiagnosticSeverity.Error)
            {
                Errors.Add(Diagnostic.LineNumber.ToString() + ": " + Diagnostic.Message);
            }
        }

        Assert.That(Errors, Is.Empty, string.Join("\n", Errors));
    }
}
