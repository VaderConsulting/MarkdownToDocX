using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using MermaidRecognition.Kinds.Enterprise;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Drives the editor for every Enterprise element type, then selects, removes, and retypes nodes.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EnterpriseNodeEditingTests
{
    [Test]
    public void ToolboxInsertsEveryElementType()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        TabControl Tabs = Require<TabControl>(Form, "LayerTabs");
        ReplaceSource(Editor, "enterprise\n");

        List<EnterpriseElementMetadata> Types = new List<EnterpriseElementMetadata>(EnterpriseElementCatalog.Elements.Values);
        Assert.That(Types.Count, Is.GreaterThan(50));
        int Index = 0;
        foreach (EnterpriseElementMetadata Metadata in Types)
        {
            string Identifier = "n" + Index.ToString();
            string Label = ElementShapePainter.DisplayName(Metadata.KebabName);
            SelectLayer(Tabs, Metadata.Layer.ToString());
            Click(RequireGlyph(Form, Metadata.KebabName));
            Application.DoEvents();

            TextBox LabelBox = Require<TextBox>(Form, "PromptLabelBox");
            TextBox IdentifierBox = Require<TextBox>(Form, "PromptIdBox");
            Assert.That(LabelBox.Visible, Is.True, Metadata.KebabName);
            LabelBox.Text = Label;
            IdentifierBox.Text = Identifier;
            Require<Button>(Form, "InsertLineButton").PerformClick();
            Application.DoEvents();

            string Expected = Metadata.KebabName + " " + Identifier + " \"" + Label + "\"";
            Assert.That(Editor.Text, Does.Contain(Expected), Metadata.KebabName);
            Assert.That(Editor.Focused, Is.True, Metadata.KebabName);
            Index++;
        }

        Settle();
        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("Error:"));
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Editor.Text);
        Assert.That(Parsed.Document.Elements.Count, Is.EqualTo(Types.Count));
        Index = 0;
        foreach (EnterpriseElementMetadata Metadata in Types)
        {
            string Identifier = "n" + Index.ToString();
            Assert.That(Parsed.Document.Elements.ContainsKey(Identifier), Is.True, Metadata.KebabName);
            EnterpriseElement Element = Parsed.Document.Elements[Identifier];
            Assert.That(Element.ElementType, Is.EqualTo(Metadata.ElementType), Metadata.KebabName);
            Assert.That(Element.Label, Is.EqualTo(ElementShapePainter.DisplayName(Metadata.KebabName)), Metadata.KebabName);
            Index++;
        }
    }

    [Test]
    public void ClickingANodeSelectsItsTypeAndKeepsTheOtherTypes()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        string Source = "enterprise\n  application-component shop \"Web Shop\"\n  business-actor clerk \"Clerk\"\n";
        ReplaceSource(Editor, Source);
        int Shop = Editor.Text.IndexOf("shop", StringComparison.Ordinal);
        int Clerk = Editor.Text.IndexOf("clerk", StringComparison.Ordinal);
        Editor.SelectionStart = 0;
        ClickChar(Editor, Shop);

        Assert.That(Editor.SelectionStart, Is.GreaterThanOrEqualTo(Shop));
        Assert.That(Editor.SelectionStart, Is.LessThan(Shop + "shop".Length));
        Assert.That(LiveKebab(Form), Is.EqualTo("application-component"));
        AssertSelectedLayerShowsEveryGlyph(Form);
        Assert.That(Glyphs(Form).Count, Is.EqualTo(EnterpriseElementCatalog.Elements.Count));
        Assert.That(Editor.Focused, Is.True);

        ClickChar(Editor, Clerk);
        Assert.That(Editor.SelectionStart, Is.GreaterThanOrEqualTo(Clerk));
        Assert.That(Editor.SelectionStart, Is.LessThan(Clerk + "clerk".Length));
        Assert.That(LiveKebab(Form), Is.EqualTo("business-actor"));
        AssertSelectedLayerShowsEveryGlyph(Form);
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void RemovingANodeLeavesTheOtherNodes()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        string Source = "enterprise\n  application-component shop \"Web Shop\"\n  business-actor clerk \"Clerk\"\n  data-object order \"Order\"\n";
        ReplaceSource(Editor, Source);
        int ClerkLine = Editor.Text.IndexOf("business-actor", StringComparison.Ordinal);
        ClickChar(Editor, ClerkLine + 1);
        SelectLine(Editor, ClerkLine);
        PressDelete(Editor);
        Application.DoEvents();
        Settle();

        Assert.That(Editor.Text, Does.Contain("application-component shop \"Web Shop\""));
        Assert.That(Editor.Text, Does.Contain("data-object order \"Order\""));
        Assert.That(Editor.Text, Does.Not.Contain("clerk"));
        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("Error:"));
        Assert.That(Editor.Focused, Is.True);
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Editor.Text);
        Assert.That(Parsed.Document.Elements.ContainsKey("shop"), Is.True);
        Assert.That(Parsed.Document.Elements.ContainsKey("order"), Is.True);
        Assert.That(Parsed.Document.Elements.ContainsKey("clerk"), Is.False);
    }

    [Test]
    public void ChangingTheTypeKeepsTheIdAndLabel()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        string Source = "enterprise\n  application-component shop \"Web Shop\"\n  business-actor clerk \"Clerk\"\n";
        ReplaceSource(Editor, Source);
        int TypeAt = Editor.Text.IndexOf("application-component", StringComparison.Ordinal);
        ClickChar(Editor, TypeAt + 3);
        Assert.That(Editor.SelectionStart, Is.GreaterThanOrEqualTo(TypeAt));
        Assert.That(Editor.SelectionStart, Is.LessThan(TypeAt + "application-component".Length));
        Editor.Select(TypeAt, "application-component".Length);
        Editor.SelectedText = "business-role";
        Application.DoEvents();
        Settle();

        Assert.That(Editor.Text, Does.Contain("business-role shop \"Web Shop\""));
        Assert.That(Editor.Text, Does.Not.Contain("application-component"));
        Assert.That(Editor.Text, Does.Contain("business-actor clerk \"Clerk\""));
        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("Error:"));
        Assert.That(Editor.Focused, Is.True);
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Editor.Text);
        Assert.That(Parsed.Document.Elements["shop"].ElementType, Is.EqualTo(EnterpriseElementType.BusinessRole));
        Assert.That(Parsed.Document.Elements["shop"].Label, Is.EqualTo("Web Shop"));
        Assert.That(Parsed.Document.Elements["clerk"].ElementType, Is.EqualTo(EnterpriseElementType.BusinessActor));
        Assert.That(Parsed.Document.Elements["clerk"].Label, Is.EqualTo("Clerk"));
    }

    [Test]
    public void ChangingTheTypeKeepsAnIdWhenThereIsNoLabel()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        ReplaceSource(Editor, "enterprise\n  application-component shopFloor\n");
        int TypeAt = Editor.Text.IndexOf("application-component", StringComparison.Ordinal);
        ClickChar(Editor, TypeAt);
        Editor.Select(TypeAt, "application-component".Length);
        Editor.SelectedText = "device";
        Application.DoEvents();
        Settle();

        Assert.That(Editor.Text, Does.Contain("device shopFloor"));
        Assert.That(Editor.Text, Does.Not.Contain("\""));
        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("Error:"));
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Editor.Text);
        Assert.That(Parsed.Document.Elements["shopFloor"].ElementType, Is.EqualTo(EnterpriseElementType.Device));
        Assert.That(Parsed.Document.Elements["shopFloor"].Label, Is.EqualTo("shopFloor"));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void AlternateTypeSpellingsStayCompatible()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        StringBuilder Source = new StringBuilder();
        Source.AppendLine("enterprise");
        Source.AppendLine("  ApplicationComponent shop \"Web Shop\"");
        Source.AppendLine("  applicationcomponent cart \"Cart\"");
        Source.AppendLine("  and-junction join \"Join\"");
        Source.AppendLine("  or-junction split \"Split\"");
        Source.AppendLine("  grouping area \"Area\"");
        ReplaceSource(Editor, Source.ToString());
        Settle();

        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("Error:"));
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Editor.Text);
        Assert.That(Parsed.Document.Elements["shop"].ElementType, Is.EqualTo(EnterpriseElementType.ApplicationComponent));
        Assert.That(Parsed.Document.Elements["shop"].Label, Is.EqualTo("Web Shop"));
        Assert.That(Parsed.Document.Elements["cart"].ElementType, Is.EqualTo(EnterpriseElementType.ApplicationComponent));
        Assert.That(Parsed.Document.Elements["join"].ElementType, Is.EqualTo(EnterpriseElementType.Junction));
        Assert.That(Parsed.Document.Elements["join"].Label, Is.EqualTo("Join"));
        Assert.That(Parsed.Document.Elements["split"].ElementType, Is.EqualTo(EnterpriseElementType.Junction));
        Assert.That(Parsed.Document.Elements["area"].ElementType, Is.EqualTo(EnterpriseElementType.Grouping));
        int Join = Editor.Text.IndexOf("and-junction", StringComparison.Ordinal);
        ClickChar(Editor, Join + 2);
        Assert.That(LiveKebab(Form), Is.EqualTo("junction"));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void DuplicateIdIsReportedAndCanBeCorrected()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        ReplaceSource(Editor, "enterprise\n  goal goalA \"Ship\"\n  requirement goalA \"Ship late\"\n");
        Settle();
        Assert.That(DiagnosticText(Diagnostics), Does.Contain("duplicate element id"));
        Assert.That(Editor.Focused, Is.True);

        int Second = Editor.Text.LastIndexOf("goalA", StringComparison.Ordinal);
        ClickChar(Editor, Second);
        Editor.Select(Second, "goalA".Length);
        Editor.SelectedText = "goalB";
        Application.DoEvents();
        Settle();

        Assert.That(DiagnosticText(Diagnostics), Does.Not.Contain("Error:"));
        Assert.That(Editor.Text, Does.Contain("goal goalA \"Ship\""));
        Assert.That(Editor.Text, Does.Contain("requirement goalB \"Ship late\""));
        Assert.That(Editor.Focused, Is.True);
    }

    [Test]
    public void RemovingAReferencedNodeReportsTheRelationship()
    {
        using MainForm Form = OpenEditor();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        ListBox Diagnostics = Require<ListBox>(Form, "DiagnosticsList");
        string Source = "enterprise\n  application-component shop \"Web Shop\"\n  application-service api \"Api\"\n  shop -association-> api\n";
        ReplaceSource(Editor, Source);
        int ApiLine = Editor.Text.IndexOf("application-service", StringComparison.Ordinal);
        ClickChar(Editor, ApiLine);
        SelectLine(Editor, ApiLine);
        PressDelete(Editor);
        Application.DoEvents();
        Settle();

        Assert.That(Editor.Text, Does.Contain("application-component shop \"Web Shop\""));
        Assert.That(Editor.Text, Does.Not.Contain("application-service"));
        Assert.That(Editor.Text, Does.Contain("shop -association-> api"));
        Assert.That(DiagnosticText(Diagnostics), Does.Contain("not a declared element"));
        Assert.That(Editor.Focused, Is.True);
        EnterpriseParseResult Parsed = EnterpriseParser.Parse(Editor.Text);
        Assert.That(Parsed.Document.Elements.ContainsKey("shop"), Is.True);
        Assert.That(Parsed.Document.Elements.ContainsKey("api"), Is.False);
    }

    private static MainForm OpenEditor()
    {
        SessionSettings Settings = new SessionSettings();
        Settings.DiagnosticsVisible = true;
        Settings.HighlightDelayMilliseconds = 40;
        Settings.RenderDelayMilliseconds = 60000;
        Settings.WideToolbar = true;
        Settings.EditorOnLeft = true;
        Settings.VerticalSplit = true;
        MainForm Form = new MainForm(Settings);
        Form.ShowInTaskbar = false;
        Form.StartPosition = FormStartPosition.Manual;
        Form.Location = new Point(40, 40);
        Form.ClientSize = new Size(1200, 800);
        Form.Show();
        Application.DoEvents();
        RichTextBox Editor = Require<RichTextBox>(Form, "SourceEditor");
        Editor.Focus();
        Application.DoEvents();
        return Form;
    }

    private static void ReplaceSource(RichTextBox Editor, string Source)
    {
        Editor.Focus();
        Editor.Text = Source.Replace("\r\n", "\n", StringComparison.Ordinal);
        Editor.SelectionStart = Editor.TextLength;
        Editor.SelectionLength = 0;
        Application.DoEvents();
    }

    private static void SelectLayer(TabControl Tabs, string LayerName)
    {
        foreach (TabPage Page in Tabs.TabPages)
        {
            if (string.Equals(Page.Text, LayerName, StringComparison.Ordinal))
            {
                Tabs.SelectedTab = Page;
                Application.DoEvents();
                return;
            }
        }

        throw new InvalidOperationException("Missing layer " + LayerName);
    }

    private static void ClickChar(RichTextBox Editor, int Index)
    {
        Editor.Focus();
        Editor.SelectionStart = Index;
        Editor.ScrollToCaret();
        Application.DoEvents();
        Editor.SelectionStart = 0;
        Point Point = Editor.GetPositionFromCharIndex(Index);
        IntPtr Packed = (IntPtr)((Point.X & 0xFFFF) | ((Point.Y & 0xFFFF) << 16));
        SendMessage(Editor.Handle, WM_LBUTTONDOWN, (IntPtr)1, Packed);
        SendMessage(Editor.Handle, WM_LBUTTONUP, IntPtr.Zero, Packed);
        Application.DoEvents();
    }

    private static void SelectLine(RichTextBox Editor, int IndexOnLine)
    {
        int Line = Editor.GetLineFromCharIndex(IndexOnLine);
        int Start = Editor.GetFirstCharIndexFromLine(Line);
        int Next = Line + 1 < Editor.Lines.Length ? Editor.GetFirstCharIndexFromLine(Line + 1) : Editor.TextLength;
        if (Next < Start)
        {
            Next = Editor.TextLength;
        }

        Editor.Select(Start, Next - Start);
    }

    private static void PressDelete(RichTextBox Editor)
    {
        Assert.That(Editor.Focused, Is.True);
        SendMessage(Editor.Handle, WM_KEYDOWN, (IntPtr)VK_DELETE, IntPtr.Zero);
        SendMessage(Editor.Handle, WM_KEYUP, (IntPtr)VK_DELETE, IntPtr.Zero);
    }

    private static void Settle()
    {
        DateTime End = DateTime.UtcNow.AddMilliseconds(700);
        while (DateTime.UtcNow < End)
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }
    }

    private static string DiagnosticText(ListBox Diagnostics)
    {
        StringBuilder Text = new StringBuilder();
        foreach (object Item in Diagnostics.Items)
        {
            if (Text.Length > 0)
            {
                Text.Append('\n');
            }

            Text.Append(Item.ToString());
        }

        return Text.ToString();
    }

    private static string LiveKebab(Control Root)
    {
        string Found = string.Empty;
        foreach (ElementGlyphButton Button in Glyphs(Root))
        {
            if (Button.IsLive && Button.Metadata != null)
            {
                Found = Button.Metadata.KebabName;
            }
        }

        return Found;
    }

    private static void AssertSelectedLayerShowsEveryGlyph(Control Root)
    {
        int Shown = 0;
        int OnShownPage = 0;
        foreach (ElementGlyphButton Button in Glyphs(Root))
        {
            if (Button.Parent != null && Button.Parent.Visible)
            {
                OnShownPage++;
                if (Button.Visible)
                {
                    Shown++;
                }
            }
        }

        Assert.That(OnShownPage, Is.GreaterThan(0));
        Assert.That(Shown, Is.EqualTo(OnShownPage));
    }

    private static ElementGlyphButton RequireGlyph(Control Root, string KebabName)
    {
        foreach (ElementGlyphButton Button in Glyphs(Root))
        {
            if (Button.Metadata != null && string.Equals(Button.Metadata.KebabName, KebabName, StringComparison.Ordinal))
            {
                return Button;
            }
        }

        throw new InvalidOperationException("Missing " + KebabName);
    }

    private static List<ElementGlyphButton> Glyphs(Control Root)
    {
        List<ElementGlyphButton> Found = new List<ElementGlyphButton>();
        CollectGlyphs(Root, Found);
        return Found;
    }

    private static void CollectGlyphs(Control Root, List<ElementGlyphButton> Found)
    {
        ElementGlyphButton? Button = Root as ElementGlyphButton;
        if (Button != null)
        {
            Found.Add(Button);
        }

        foreach (Control Child in Root.Controls)
        {
            CollectGlyphs(Child, Found);
        }
    }

    private static void Click(Control Control)
    {
        MethodInfo? OnClick = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
        if (OnClick == null)
        {
            throw new InvalidOperationException("Control.OnClick is missing.");
        }

        OnClick.Invoke(Control, new object[] { EventArgs.Empty });
    }

    private static T Require<T>(Control Root, string Name) where T : Control
    {
        Control[] Matches = Root.Controls.Find(Name, true);
        if (Matches.Length != 1 || Matches[0] is not T Typed)
        {
            throw new InvalidOperationException("Missing " + Name);
        }

        return Typed;
    }

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int VK_DELETE = 0x2E;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr HWnd, int Msg, IntPtr WParam, IntPtr LParam);
}
