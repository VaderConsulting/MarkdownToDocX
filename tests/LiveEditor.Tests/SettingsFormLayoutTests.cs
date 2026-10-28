using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Every control on the settings dialog is fully visible, with nothing clipped or overlapping, at normal and high DPI.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class SettingsFormLayoutTests
{
    [TestCase(1.0F)]
    [TestCase(1.5F)]
    [TestCase(2.0F)]
    public void EveryControlIsFullyVisibleWithoutOverlap(float Scale)
    {
        using SettingsForm Dialog = Open(Scale);
        List<string> Problems = [];
        TabControl Tabs = Require<TabControl>(Dialog, "SettingsTabs");
        foreach (TabPage Page in Tabs.TabPages)
        {
            Tabs.SelectedTab = Page;
            Dialog.PerformLayout();
            Application.DoEvents();
            Check(Dialog, Problems);
            using Bitmap Image = new Bitmap(Dialog.Width, Dialog.Height);
            Dialog.DrawToBitmap(Image, new Rectangle(Point.Empty, Image.Size));
            Image.Save(Path.Combine(TestContext.CurrentContext.TestDirectory, "settings-" + Page.Name + "-" + Scale.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
        }

        Assert.That(Problems, Is.Empty, string.Join(Environment.NewLine, Problems));
    }

    [Test]
    public void TheSelectionColourRowShowsASwatchAndItsHexValue()
    {
        SessionSettings Settings = new SessionSettings
        {
            SelectionColour = "#FF8000"
        };
        using SettingsForm Dialog = Open(Settings, 1.0F);
        Panel Swatch = Require<Panel>(Dialog, "SelectionColourSwatch");
        Label Value = Require<Label>(Dialog, "SelectionColourValueLabel");

        Assert.That(Swatch.BackColor.ToArgb(), Is.EqualTo(Color.FromArgb(0xFF, 0x80, 0x00).ToArgb()));
        Assert.That(Value.Text, Is.EqualTo("#FF8000"));
        Dialog.SelectionColour = Color.FromArgb(0x12, 0x34, 0x56);
        Assert.That(Swatch.BackColor.ToArgb(), Is.EqualTo(Color.FromArgb(0x12, 0x34, 0x56).ToArgb()));
        Assert.That(Value.Text, Is.EqualTo("#123456"));
    }

    internal static SettingsForm Open(float Scale)
    {
        return Open(new SessionSettings(), Scale);
    }

    private static SettingsForm Open(SessionSettings Settings, float Scale)
    {
        SettingsForm Dialog = new SettingsForm(Settings);
        if (Scale != 1.0F)
        {
            Dialog.Font = new Font(Dialog.Font.FontFamily, Dialog.Font.Size * Scale);
        }

        Dialog.StartPosition = FormStartPosition.Manual;
        Dialog.Location = new Point(-20000, -20000);
        Dialog.Show();
        Dialog.PerformLayout();
        Application.DoEvents();
        return Dialog;
    }

    private static void Check(Control Parent, List<string> Problems)
    {
        Rectangle Room = Parent is GroupBox ? Parent.DisplayRectangle : Parent.ClientRectangle;
        List<Control> Shown = [];
        foreach (Control Child in Parent.Controls)
        {
            if (!Child.Visible)
            {
                continue;
            }

            Shown.Add(Child);
            if (!Room.Contains(Child.Bounds))
            {
                Problems.Add(Child.Name + " " + Child.Bounds + " runs outside " + Parent.Name + " " + Room);
            }

            if (Child is ButtonBase || Child is Label)
            {
                Size Wanted = Child.GetPreferredSize(Size.Empty);
                if (Child.Width < Wanted.Width || Child.Height < Wanted.Height)
                {
                    Problems.Add(Child.Name + " is " + Child.Size + " but needs " + Wanted);
                }
            }

            Check(Child, Problems);
        }

        for (int First = 0; First < Shown.Count; First++)
        {
            for (int Second = First + 1; Second < Shown.Count; Second++)
            {
                if (Shown[First].Bounds.IntersectsWith(Shown[Second].Bounds))
                {
                    Problems.Add(Shown[First].Name + " overlaps " + Shown[Second].Name);
                }
            }
        }
    }

    private static T Require<T>(Control Root, string Name) where T : Control
    {
        Control[] Found = Root.Controls.Find(Name, true);
        Assert.That(Found, Has.Length.EqualTo(1), Name);
        return (T)Found[0];
    }
}
