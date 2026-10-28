using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Xml.Linq;

namespace LiveEditor;

internal sealed class PlacementSuggestionDialog : Form
{
    internal PlacementSuggestionDialog(PlacementSuggestionPreview Proposal, string BeforeSvg, byte[] BeforePng)
    {
        Text = "Preview placement improvement"; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new(1100, 740); MinimumSize = new(800, 540); ShowInTaskbar = false;
        var Layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new(12) };
        Layout.ColumnStyles.Add(new(SizeType.Percent, 50)); Layout.ColumnStyles.Add(new(SizeType.Percent, 50));
        Layout.RowStyles.Add(new(SizeType.Absolute, 92)); Layout.RowStyles.Add(new(SizeType.Absolute, 28));
        Layout.RowStyles.Add(new(SizeType.Percent, 100)); Layout.RowStyles.Add(new(SizeType.Absolute, 48));
        var Before = Proposal.Comparison.Before; var After = Proposal.Comparison.After!;
        string Number(double Value) => Value.ToString("0.#", CultureInfo.InvariantCulture);
        var Totals = new Label { Name = "SuggestionMetrics", Dock = DockStyle.Fill, AutoSize = false, Text =
            $"Bends: {Before.Routes.Bends} → {After.Routes.Bends}     Crossing pairs: {Before.Routes.CrossingPairs} → {After.Routes.CrossingPairs}     Rendered width: {SvgWidth(BeforeSvg)} → {SvgWidth(Proposal.Svg)} px\n"
            + $"Moved nodes: {After.MovedNodes}     Displacement: {After.DisplacementCells} cells ({Number(After.DisplacementPx)} px)     Route findings: {Before.Routes.Findings.Length} → {After.Routes.Findings.Length}\n"
            + "Apply records the chosen cell position in the source. Undo restores the previous arrangement. Zoom or pan either preview to inspect it." };
        Layout.Controls.Add(Totals, 0, 0); Layout.SetColumnSpan(Totals, 2);
        Layout.Controls.Add(new Label { Text = "Before", Dock = DockStyle.Fill }, 0, 1);
        Layout.Controls.Add(new Label { Text = "Proposed placement", Dock = DockStyle.Fill }, 1, 1);
        var BeforePreview = Canvas(Proposal.OriginalSource, BeforeSvg, BeforePng); BeforePreview.Name = "BeforeSuggestionPreview";
        var AfterPreview = Canvas(Proposal.Edit.Apply(Proposal.OriginalSource), Proposal.Svg, Proposal.Png); AfterPreview.Name = "AfterSuggestionPreview";
        Layout.Controls.Add(BeforePreview, 0, 2); Layout.Controls.Add(AfterPreview, 1, 2);
        var Buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new(0, 8, 0, 0) };
        var Apply = new Button { Name = "ApplySuggestionButton", Text = "Apply", DialogResult = DialogResult.OK, AutoSize = true };
        var Cancel = new Button { Name = "CancelSuggestionButton", Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        Buttons.Controls.Add(Apply); Buttons.Controls.Add(Cancel);
        Layout.Controls.Add(Buttons, 0, 3); Layout.SetColumnSpan(Buttons, 2);
        Controls.Add(Layout); AcceptButton = Apply; CancelButton = Cancel;
        Shown += (_, _) => { BeforePreview.ApplyView(); AfterPreview.ApplyView(); };
    }

    private static PreviewCanvas Canvas(string Source, string Svg, byte[] Png)
    {
        var Preview = new PreviewCanvas { Dock = DockStyle.Fill, NavigationEnabled = true };
        Preview.LoadAccepted(Source, Svg, Png, null, false);
        Preview.FitToWindow = true; Preview.ApplyView();
        return Preview;
    }
    private static string SvgWidth(string Svg)
    {
        string? Value = (string?)XDocument.Parse(Svg).Root?.Attribute("width");
        return double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Width)
            ? Width.ToString("0.#", CultureInfo.InvariantCulture) : "unavailable";
    }
}
