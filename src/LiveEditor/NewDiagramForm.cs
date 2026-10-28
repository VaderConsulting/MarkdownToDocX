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
    private readonly List<Button> _Choices = [];

    private DiagramKindDefinition? _SelectedKind;

    /// <summary>
    /// Creates the dialog and fills one tab for each supported group.
    /// </summary>
    public NewDiagramForm()
    {
        InitializeComponent();
        WireChoices();
    }

    /// <summary>Gets the kind chosen by the user.</summary>
    public DiagramKindDefinition? SelectedKind
    {
        get
        {
            return _SelectedKind;
        }
    }

    private void WireChoices()
    {
        Button[] Choices =
        [
            Kind_enterprise,
            Kind_flowchart,
            Kind_sequence,
            Kind_class,
            Kind_state,
            Kind_er,
            Kind_gantt,
            Kind_pie,
            Kind_gitGraph,
            Kind_mindmap,
            Kind_timeline,
            Kind_journey,
            Kind_quadrantChart,
            Kind_xychart,
            Kind_architecture,
            Kind_block,
            Kind_packet,
            Kind_radar,
            Kind_treemap,
            Kind_c4
        ];
        for (int Index = 0; Index < Choices.Length; Index++)
        {
            _Choices.Add(Choices[Index]);
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
        string KindId = Choice.Tag as string ?? string.Empty;
        _SelectedKind = DiagramKindCatalog.Find(KindId);
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
