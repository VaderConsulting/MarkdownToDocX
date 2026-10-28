using System;
using System.Collections.Generic;
using System.Text;
using LiveEditor.Editor;
using MDConverter;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Renders the simple and complete sample for every diagram type.
/// </summary>
[TestFixture]
public sealed class DiagramSupportTests
{
    [Test]
    public void EachSampleShowsWhetherTheDiagramTypeRenders()
    {
        HashSet<string> Expected = new HashSet<string>(StringComparer.Ordinal)
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
        string[] Hidden = ["requirement", "kanban", "sankey"];

        StringBuilder Mismatches = new StringBuilder();
        Assert.That(SampleCatalog.Groups.Count, Is.EqualTo(Expected.Count));
        for (int HiddenIndex = 0; HiddenIndex < Hidden.Length; HiddenIndex++)
        {
            string HiddenId = Hidden[HiddenIndex];
            for (int GroupIndex = 0; GroupIndex < SampleCatalog.Groups.Count; GroupIndex++)
            {
                if (SampleCatalog.Groups[GroupIndex].KindId == HiddenId)
                {
                    Mismatches.AppendLine(HiddenId + " is still on the Samples menu");
                }
            }

            if (DiagramKindCatalog.Find(HiddenId) != null)
            {
                Mismatches.AppendLine(HiddenId + " still has a toolbar");
            }
        }
        for (int Index = 0; Index < SampleCatalog.Groups.Count; Index++)
        {
            DiagramSampleGroup Group = SampleCatalog.Groups[Index];
            string SimpleFailure = Failure(Group.SimpleSource);
            string CompleteFailure = Failure(Group.FullSource);
            DiagramKindDefinition? Kind = DiagramKindCatalog.Find(Group.KindId);
            if (!Expected.Contains(Group.KindId))
            {
                Mismatches.AppendLine(Group.KindId + " is not an expected sample");
            }

            if (SimpleFailure.Length > 0)
            {
                Mismatches.AppendLine(Group.KindId + " simple: " + SimpleFailure);
            }

            if (CompleteFailure.Length > 0)
            {
                Mismatches.AppendLine(Group.KindId + " complete: " + CompleteFailure);
            }

            if (Kind == null)
            {
                Mismatches.AppendLine(Group.KindId + " renders but has no toolbar");
            }
            else if (Group.KindId == "enterprise")
            {
                if (Kind.Toolbar.Length != 0)
                {
                    Mismatches.AppendLine("enterprise toolbar should stay on the element catalogue");
                }
            }
            else if (Kind.Toolbar.Length == 0)
            {
                Mismatches.AppendLine(Group.KindId + " toolbar is empty");
            }
        }

        Assert.That(Mismatches.ToString(), Is.EqualTo(string.Empty));
    }

    private static string Failure(string Source)
    {
        try
        {
            string Svg;
            bool Rendered = MermaidDiagramExport.TryRenderPreviewSvg(Source, out Svg);
            if (Rendered && !string.IsNullOrWhiteSpace(Svg) && Svg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return string.Empty;
            }

            return "returned " + Rendered + " length " + (Svg == null ? "null" : Svg.Length.ToString());
        }
        catch (Exception Error)
        {
            return Error.GetType().Name + ": " + Error.Message;
        }
    }
}
