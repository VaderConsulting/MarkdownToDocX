using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using LiveEditor.Editor;

namespace LiveEditor;

/// <summary>
/// Edits persisted LiveEditor behaviour and editor preferences.
/// </summary>
internal partial class SettingsForm : Form
{
    private Color _SelectionColour;

    public SettingsForm(SessionSettings Settings)
    {
        ArgumentNullException.ThrowIfNull(Settings);
        InitializeComponent();
        AutoIndentCheckBox.Checked = Settings.AutoIndent;
        InsertSpacesCheckBox.Checked = Settings.InsertSpaces;
        WordWrapCheckBox.Checked = Settings.EditorWordWrap;
        IndentSizeNumeric.Value = Clamp(Settings.IndentSize, (int)IndentSizeNumeric.Minimum, (int)IndentSizeNumeric.Maximum);
        FontSizeNumeric.Value = (decimal)Clamp(Settings.EditorFontSize, (float)FontSizeNumeric.Minimum, (float)FontSizeNumeric.Maximum);
        RenderDelayNumeric.Value = Clamp(Settings.RenderDelayMilliseconds, (int)RenderDelayNumeric.Minimum, (int)RenderDelayNumeric.Maximum);
        HighlightDelayNumeric.Value = Clamp(Settings.HighlightDelayMilliseconds, (int)HighlightDelayNumeric.Minimum, (int)HighlightDelayNumeric.Maximum);
        WideToolbarRadio.Checked = Settings.WideToolbar;
        NarrowToolbarRadio.Checked = !Settings.WideToolbar;
        EditorLeftRadio.Checked = Settings.EditorOnLeft;
        EditorRightRadio.Checked = !Settings.EditorOnLeft;
        ShowSelectionColour(SessionSettings.ParseColour(Settings.SelectionColour));
    }

    /// <summary>Gets or sets the selection colour shown on the swatch.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Color SelectionColour
    {
        get
        {
            return _SelectionColour;
        }

        set
        {
            ShowSelectionColour(value);
        }
    }

    public void ApplyTo(SessionSettings Settings)
    {
        ArgumentNullException.ThrowIfNull(Settings);
        Settings.AutoIndent = AutoIndentCheckBox.Checked;
        Settings.InsertSpaces = InsertSpacesCheckBox.Checked;
        Settings.EditorWordWrap = WordWrapCheckBox.Checked;
        Settings.IndentSize = Decimal.ToInt32(IndentSizeNumeric.Value);
        Settings.EditorFontSize = Decimal.ToSingle(FontSizeNumeric.Value);
        Settings.RenderDelayMilliseconds = Decimal.ToInt32(RenderDelayNumeric.Value);
        Settings.HighlightDelayMilliseconds = Decimal.ToInt32(HighlightDelayNumeric.Value);
        Settings.WideToolbar = WideToolbarRadio.Checked;
        Settings.EditorOnLeft = EditorLeftRadio.Checked;
        Settings.SelectionColour = SessionSettings.FormatColour(_SelectionColour);
    }

    private void SelectionColourButton_Click(object? Sender, EventArgs E)
    {
        using ColorDialog Dialog = new ColorDialog();
        Dialog.Color = _SelectionColour;
        Dialog.FullOpen = true;
        if (Dialog.ShowDialog(this) == DialogResult.OK)
        {
            ShowSelectionColour(Dialog.Color);
        }
    }

    private void ShowSelectionColour(Color Value)
    {
        _SelectionColour = Color.FromArgb(Value.R, Value.G, Value.B);
        SelectionColourSwatch.BackColor = _SelectionColour;
        SelectionColourValueLabel.Text = SessionSettings.FormatColour(_SelectionColour);
    }

    private static int Clamp(int Value, int Minimum, int Maximum)
    {
        return Math.Max(Minimum, Math.Min(Maximum, Value));
    }

    private static float Clamp(float Value, float Minimum, float Maximum)
    {
        return Math.Max(Minimum, Math.Min(Maximum, Value));
    }
}
