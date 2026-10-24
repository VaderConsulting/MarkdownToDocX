using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor;

/// <summary>
/// Picks a Mermaid diagram type. The buttons follow the same tab layout as the element toolbar.
/// </summary>
internal sealed partial class NewDiagramForm : Form
{
    private readonly List<Button> _Choices = new List<Button>();

    private DiagramKindDefinition? _SelectedKind;

    /// <summary>
    /// Creates the dialog and fills one tab for each supported group.
    /// </summary>
    public NewDiagramForm()
    {
        InitializeComponent();
        BuildChoices();
        CreateDiagramButton.Click += CreateDiagramButton_Click;
        CancelDiagramButton.Click += CancelDiagramButton_Click;
    }

    /// <summary>Gets the kind chosen by the user.</summary>
    public DiagramKindDefinition? SelectedKind
    {
        get
        {
            return _SelectedKind;
        }
    }

    private void BuildChoices()
    {
        Dictionary<string, FlowLayoutPanel> Groups = new Dictionary<string, FlowLayoutPanel>(StringComparer.Ordinal);
        for (int Index = 0; Index < DiagramKindCatalog.Supported.Count; Index++)
        {
            DiagramKindDefinition Kind = DiagramKindCatalog.Supported[Index];
            if (!Groups.TryGetValue(Kind.Group, out FlowLayoutPanel? Flow))
            {
                TabPage Page = new TabPage(Kind.Group);
                Page.Padding = new Padding(0);
                Flow = new FlowLayoutPanel();
                Flow.Dock = DockStyle.Fill;
                Flow.Padding = new Padding(8);
                Flow.WrapContents = true;
                Page.Controls.Add(Flow);
                KindTabs.TabPages.Add(Page);
                Groups[Kind.Group] = Flow;
            }

            Button Choice = new Button();
            Choice.Name = "Kind_" + Kind.Id;
            Choice.AutoSize = true;
            Choice.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Choice.Margin = new Padding(4);
            Choice.Text = Kind.Title;
            Choice.Tag = Kind;
            Choice.UseVisualStyleBackColor = false;
            Choice.Click += Choice_Click;
            Choice.DoubleClick += Choice_DoubleClick;
            Flow.Controls.Add(Choice);
            _Choices.Add(Choice);
        }

        if (_Choices.Count > 0)
        {
            SelectChoice(_Choices[0]);
        }
    }

    private void Choice_Click(object? Sender, EventArgs E)
    {
        Button? Choice = Sender as Button;
        if (Choice != null)
        {
            SelectChoice(Choice);
        }
    }

    private void Choice_DoubleClick(object? Sender, EventArgs E)
    {
        Button? Choice = Sender as Button;
        if (Choice == null)
        {
            return;
        }

        SelectChoice(Choice);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SelectChoice(Button Choice)
    {
        _SelectedKind = Choice.Tag as DiagramKindDefinition;
        for (int Index = 0; Index < _Choices.Count; Index++)
        {
            bool Selected = ReferenceEquals(_Choices[Index], Choice);
            _Choices[Index].BackColor = Selected ? Color.FromArgb(255, 230, 150) : SystemColors.Control;
        }

        if (_SelectedKind == null)
        {
            KindDescriptionLabel.Text = string.Empty;
            return;
        }

        KindDescriptionLabel.Text = _SelectedKind.Description;
    }

    private void CreateDiagramButton_Click(object? Sender, EventArgs E)
    {
        if (_SelectedKind == null)
        {
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelDiagramButton_Click(object? Sender, EventArgs E)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
