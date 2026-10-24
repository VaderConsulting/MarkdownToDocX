using System;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// New-diagram choices and the toolbar that follows the open diagram.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class NewDiagramTests
{
    [Test]
    public void DetectReadsTheDiagramHeader()
    {
        Assert.That(DiagramKindCatalog.Detect("enterprise\r\n  title Shop"), Is.EqualTo("enterprise"));
        Assert.That(DiagramKindCatalog.Detect("%% note\r\nflowchart LR\r\n  A-->B"), Is.EqualTo("flowchart"));
        Assert.That(DiagramKindCatalog.Detect("sequenceDiagram\r\n  Alice->>Bob: Hi"), Is.EqualTo("sequence"));
        Assert.That(DiagramKindCatalog.Detect("gantt\r\n  title Plan"), Is.EqualTo("gantt"));
        Assert.That(DiagramKindCatalog.Detect(string.Empty), Is.EqualTo(string.Empty));
    }

    [Test]
    public void NewDialogOffersTheSupportedTypesOnly()
    {
        using NewDiagramForm Dialog = new NewDiagramForm();
        Dialog.Show();
        Application.DoEvents();
        Assert.That(Dialog.Controls.Find("Kind_enterprise", true).Length, Is.EqualTo(1));
        Assert.That(Dialog.Controls.Find("Kind_flowchart", true).Length, Is.EqualTo(1));
        Assert.That(Dialog.Controls.Find("Kind_sequence", true).Length, Is.EqualTo(1));
        Assert.That(Dialog.Controls.Find("Kind_gantt", true).Length, Is.EqualTo(0));
        Assert.That(Dialog.Controls.Find("Kind_class", true).Length, Is.EqualTo(0));
        Assert.That(Dialog.SelectedKind, Is.Not.Null);
        Assert.That(Dialog.SelectedKind!.Id, Is.EqualTo("enterprise"));

        TabControl Tabs = (TabControl)Dialog.Controls.Find("KindTabs", true)[0];
        Tabs.SelectedIndex = 1;
        Application.DoEvents();
        Button Flowchart = (Button)Dialog.Controls.Find("Kind_flowchart", true)[0];
        Flowchart.PerformClick();
        Assert.That(Dialog.SelectedKind.Id, Is.EqualTo("flowchart"));
        Assert.That(Dialog.SelectedKind.Starter, Is.EqualTo("flowchart TD\r\n"));
    }

    [Test]
    public void ToolbarFollowsTheOpenDiagram()
    {
        using Form Host = new Form();
        ElementPalette Palette = new ElementPalette();
        Host.ClientSize = new System.Drawing.Size(900, 240);
        Palette.Dock = DockStyle.Fill;
        Host.Controls.Add(Palette);
        Host.Show();
        Application.DoEvents();

        Palette.UpdateFromEditor("flowchart TD\r\n  Start[Go]\r\n", 0);
        Application.DoEvents();
        TabControl Tabs = (TabControl)Palette.Controls.Find("LayerTabs", true)[0];
        Assert.That(TabTexts(Tabs), Does.Contain("Shapes"));
        Assert.That(TabTexts(Tabs), Does.Not.Contain("Application"));
        Assert.That(Palette.Controls.Find("Tool_Diamond", true).Length, Is.EqualTo(1));

        Palette.UpdateFromEditor("gantt\r\n  title Plan\r\n", 0);
        Application.DoEvents();
        Assert.That(TabTexts(Tabs), Does.Contain("Gantt"));
        Assert.That(Palette.Controls.Find("Tool_Diamond", true).Length, Is.EqualTo(0));

        Palette.UpdateFromEditor("enterprise\r\n  application-component webShop \"Web Shop\"\r\n", 20);
        Application.DoEvents();
        Assert.That(TabTexts(Tabs), Does.Contain("Application"));
        Assert.That(Palette.Controls.Find("Tool_Diamond", true).Length, Is.EqualTo(0));
    }

    private static string TabTexts(TabControl Tabs)
    {
        string[] Names = new string[Tabs.TabPages.Count];
        for (int Index = 0; Index < Tabs.TabPages.Count; Index++)
        {
            Names[Index] = Tabs.TabPages[Index].Text;
        }

        return string.Join(",", Names);
    }
}
