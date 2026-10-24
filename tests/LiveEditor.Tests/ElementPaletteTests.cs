using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Checks that the element toolbox keeps every type while the editor caret moves.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ElementPaletteTests
{
    [Test]
    public void SelectingANodeTypeKeepsTheOtherTypes()
    {
        using Form Host = new Form();
        ElementPalette Palette = new ElementPalette();
        Host.ClientSize = new Size(900, 240);
        Palette.Dock = DockStyle.Fill;
        Host.Controls.Add(Palette);
        Host.Show();
        Application.DoEvents();
        string Source = "enterprise\r\n  application-component webShop \"Web Shop\"\r\n";
        int Caret = Source.IndexOf("application-component", StringComparison.Ordinal) + 4;
        Palette.UpdateFromEditor(Source, Caret);
        Application.DoEvents();

        TabControl? Tabs = FindTabs(Palette);
        Assert.That(Tabs, Is.Not.Null);
        Assert.That(Tabs!.SelectedTab, Is.Not.Null);
        Control Flow = Tabs.SelectedTab!.Controls[0];
        int VisibleCount = 0;
        int LiveCount = 0;
        string LiveName = string.Empty;
        foreach (Control Child in Flow.Controls)
        {
            if (Child.Visible)
            {
                VisibleCount++;
            }

            ElementGlyphButton? Button = Child as ElementGlyphButton;
            if (Button != null && Button.IsLive && Button.Metadata != null)
            {
                LiveCount++;
                LiveName = Button.Metadata.KebabName;
            }
        }

        Assert.That(Flow.Controls.Count, Is.GreaterThan(1));
        Assert.That(VisibleCount, Is.EqualTo(Flow.Controls.Count));
        Assert.That(LiveCount, Is.EqualTo(1));
        Assert.That(LiveName, Is.EqualTo("application-component"));
    }

    [Test]
    public void ToolbarEndsAtTheLastIconRow()
    {
        AssertSlackUnderLastIcon(1100);
        AssertSlackUnderLastIcon(420);
    }

    private static void AssertSlackUnderLastIcon(int Width)
    {
        using Form Host = new Form();
        ElementPalette Palette = new ElementPalette();
        Host.ClientSize = new Size(Width, 420);
        Palette.Dock = DockStyle.Top;
        Host.Controls.Add(Palette);
        Host.Show();
        Application.DoEvents();
        string Source = "enterprise\r\n  application-component webShop \"Web Shop\"\r\n";
        Palette.UpdateFromEditor(Source, Source.IndexOf("application-component", StringComparison.Ordinal));
        Application.DoEvents();

        ElementGlyphButton? Lowest = null;
        int LowestBottom = -1;
        foreach (Control Child in Host.Controls)
        {
            CollectLowest(Child, Palette, ref Lowest, ref LowestBottom);
        }

        Assert.That(Lowest, Is.Not.Null);
        int Slack = Palette.ClientSize.Height - LowestBottom;
        Assert.That(Slack, Is.GreaterThanOrEqualTo(0), "The last icon is clipped.");
        Assert.That(Slack, Is.LessThanOrEqualTo(12), "Empty space remains under the icons.");
    }

    private static void CollectLowest(Control Root, Control Palette, ref ElementGlyphButton? Lowest, ref int LowestBottom)
    {
        ElementGlyphButton? Button = Root as ElementGlyphButton;
        if (Button != null && Button.Visible)
        {
            Point Bottom = Palette.PointToClient(Button.PointToScreen(new Point(0, Button.Height)));
            if (Bottom.Y > LowestBottom)
            {
                LowestBottom = Bottom.Y;
                Lowest = Button;
            }
        }

        foreach (Control Child in Root.Controls)
        {
            CollectLowest(Child, Palette, ref Lowest, ref LowestBottom);
        }
    }

    private static TabControl? FindTabs(Control Root)
    {
        foreach (Control Child in Root.Controls)
        {
            TabControl? Tabs = Child as TabControl;
            if (Tabs != null)
            {
                return Tabs;
            }

            TabControl? Nested = FindTabs(Child);
            if (Nested != null)
            {
                return Nested;
            }
        }

        return null;
    }
}
