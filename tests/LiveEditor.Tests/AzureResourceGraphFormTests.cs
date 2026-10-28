using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class AzureResourceGraphFormTests
{
    [TestCase(1F, false)]
    [TestCase(1F, true)]
    [TestCase(1.5F, false)]
    [TestCase(2F, false)]
    public void ConnectionDialogKeepsInputsActionsAndSubscriptionListVisible(float Scale, bool Minimum)
    {
        using AzureResourceGraphConnectionForm Dialog = new AzureResourceGraphConnectionForm();
        if (Scale != 1F)
        {
            Dialog.Font = new Font(Dialog.Font.FontFamily, Dialog.Font.Size * Scale);
        }

        Dialog.StartPosition = FormStartPosition.Manual;
        Dialog.Location = new Point(-20000, -20000);
        Dialog.Show();
        if (Minimum)
        {
            Dialog.Size = Dialog.MinimumSize;
        }

        Dialog.PerformLayout();
        Application.DoEvents();

        List<string> Problems = [];
        CheckBounds(Dialog, Problems);
        CheckedListBox Subscriptions = (CheckedListBox)Dialog.Controls.Find("SubscriptionsList", true)[0];
        Assert.That(Subscriptions.Height, Is.GreaterThanOrEqualTo(Subscriptions.ItemHeight * 3), "At least three subscriptions should fit.");
        Assert.That(Problems, Is.Empty, string.Join(Environment.NewLine, Problems));

        if (Scale == 1F && !Minimum)
        {
            using Bitmap Image = new Bitmap(Dialog.Width, Dialog.Height);
            Dialog.DrawToBitmap(Image, new Rectangle(Point.Empty, Image.Size));
            Image.Save(Path.Combine(TestContext.CurrentContext.WorkDirectory, "azure-resource-graph-dialog.png"));
        }
    }

    private static void CheckBounds(Control Parent, List<string> Problems)
    {
        List<Control> Visible = [];
        foreach (Control Child in Parent.Controls)
        {
            if (!Child.Visible)
            {
                continue;
            }

            Visible.Add(Child);
            if (!Parent.ClientRectangle.Contains(Child.Bounds))
            {
                Problems.Add($"{Child.Name} runs outside {Parent.Name}");
            }

            if (Child is Label || Child is ButtonBase)
            {
                Size Wanted = Child.GetPreferredSize(new Size(Child.Width, 0));
                if (Child.Height < Wanted.Height)
                {
                    Problems.Add($"{Child.Name} text is clipped");
                }
            }

            CheckBounds(Child, Problems);
        }

        for (int First = 0; First < Visible.Count; First++)
        {
            for (int Second = First + 1; Second < Visible.Count; Second++)
            {
                if (Visible[First].Bounds.IntersectsWith(Visible[Second].Bounds))
                {
                    Problems.Add($"{Visible[First].Name} overlaps {Visible[Second].Name}");
                }
            }
        }
    }
}
