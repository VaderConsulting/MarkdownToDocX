using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Native rendering for recognised pie data, with explicit fallback for unsupported configuration.</summary>
public static class NativePieRenderBridge
{
    private static readonly string[] Colours = ["#ECECFF", "#FFFFDE", "#FFE6CC", "#DAE8FC", "#D5E8D4", "#F8CECC", "#E1D5E7", "#FFF2CC", "#D0E0E3", "#F4CCCC", "#D9EAD3", "#C9DAF8"];

    /// <summary>Returns false for another kind or a feature requiring the legacy renderer.</summary>
    public static bool TryRenderSvg(string Source, out string Svg)
    {
        Svg = string.Empty;
        if (!Regex.IsMatch(Source, @"^\s*(?:%%[^\r\n]*[\r\n]\s*)*pie\b", RegexOptions.IgnoreCase))
        {
            return false;
        }

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        if (Contract.Kind != "pie" || Contract.Diagnostics.Any(Diagnostic => Diagnostic.Code == "recognition.pie.unclassified-line")
            || Source.Contains("%%{", StringComparison.Ordinal) || Source.TrimStart().StartsWith("---", StringComparison.Ordinal))
        {
            return false;
        }

        if (Contract.SourceStatements.Count(Statement => Statement.LineTypeId == "pie.section") != Contract.Nodes.Count)
        {
            return false;
        }

        List<(string Label, double Value)> Sections = [];
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (NodeContract Node in Contract.Nodes)
        {
            if (!Node.Metadata.TryGetValue("pieValue", out MermaidMetadataValue? Metadata) || Metadata is not RawMetadata Raw
                || !double.TryParse(Raw.RawValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double Value)
                || !double.IsFinite(Value) || Value < 0)
            {
                return false;
            }

            if (Seen.Add(Node.Label ?? Node.Id))
            {
                Sections.Add((Node.Label ?? Node.Id, Value));
            }
        }
        double Total = Sections.Sum(Section => Section.Value);
        if (!double.IsFinite(Total))
        {
            return false;
        }

        Contract.Configuration.RawDirectiveValues.TryGetValue("pie.title", out string? Title);
        bool ShowData = Contract.Configuration.RawDirectiveValues.TryGetValue("pie.showData", out string? Show) && Show == "true";
        string Legend((string Label, double Value) Section) => Section.Label + (ShowData ? " [" + N(Section.Value) + "]" : string.Empty);
        double LegendWidth = Sections.Select(Section => FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Legend(Section), 16)).DefaultIfEmpty(0).Max();
        double Width = Math.Max(500 + LegendWidth, FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Title ?? "", 20) + 40);
        double Height = Math.Max(450, 90 + (Sections.Count * 26));
        XNamespace Ns = "http://www.w3.org/2000/svg";
        XElement Root = new XElement(Ns + "svg", new XAttribute("viewBox", $"0 0 {N(Width)} {N(Height)}"),
            new XAttribute("width", N(Width)), new XAttribute("height", N(Height)), new XAttribute("role", "img"),
            new XAttribute("data-native-renderer", "pie"), new XAttribute("font-family", "Arial, sans-serif"));
        if (!string.IsNullOrWhiteSpace(Contract.Accessibility.Title))
        {
            Root.Add(new XElement(Ns + "title", Contract.Accessibility.Title));
        }

        if (!string.IsNullOrWhiteSpace(Contract.Accessibility.Description))
        {
            Root.Add(new XElement(Ns + "desc", Contract.Accessibility.Description));
        }

        Root.Add(new XElement(Ns + "rect", new XAttribute("width", N(Width)), new XAttribute("height", N(Height)), new XAttribute("fill", "white")));
        Root.Add(Text(225, 30, Title ?? "", 20, "middle"));
        // Match the vendored pie renderer: preserve declaration order, omit arcs below 1%,
        // and compute displayed percentages against all sections, including omitted values.
        List<(string Label, double Value)> Visible = [.. Sections.Where(Section => Total > 0 && Section.Value / Total >= 0.01)];
        double VisibleTotal = Visible.Sum(Section => Section.Value), Angle = -Math.PI / 2;
        foreach ((string Label, double Value) Section in Visible)
        {
            double Sweep = Section.Value / VisibleTotal * Math.PI * 2, End = Angle + Sweep;
            int Index = Sections.IndexOf(Section);
            XElement Shape;
            if (Visible.Count == 1)
            {
                Shape = new XElement(Ns + "circle", new XAttribute("cx", "225"), new XAttribute("cy", "240"), new XAttribute("r", "170"));
            }
            else
            {
                Shape = new XElement(Ns + "path", new XAttribute("d", $"M225,240 L{N(225 + (170 * Math.Cos(Angle)))},{N(240 + (170 * Math.Sin(Angle)))} A170,170 0 {(Sweep > Math.PI ? 1 : 0)},1 {N(225 + (170 * Math.Cos(End)))},{N(240 + (170 * Math.Sin(End)))} Z"));
            }

            Shape.Add(new XAttribute("class", "pie-slice"), new XAttribute("data-label", Section.Label), new XAttribute("fill", Colours[Index % Colours.Length]), new XAttribute("stroke", "#555"));
            Root.Add(Shape);
            double Mid = Angle + (Sweep / 2);
            Root.Add(Text(225 + (110 * Math.Cos(Mid)), 245 + (110 * Math.Sin(Mid)),
                Math.Round(Section.Value / Total * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) + "%", 16, "middle"));
            Angle = End;
        }
        for (int Index = 0; Index < Sections.Count; Index++)
        {
            double Y = 70 + (Index * 26);
            Root.Add(new XElement(Ns + "rect", new XAttribute("x", "455"), new XAttribute("y", N(Y - 14)), new XAttribute("width", "18"), new XAttribute("height", "18"), new XAttribute("fill", Colours[Index % Colours.Length])));
            Root.Add(Text(481, Y, Legend(Sections[Index]), 16, "start"));
        }
        Svg = Root.ToString(SaveOptions.DisableFormatting);
        return true;

        XElement Text(double X, double Y, string Value, int Size, string Anchor) => new(Ns + "text",
            new XAttribute("x", N(X)), new XAttribute("y", N(Y)), new XAttribute("font-size", Size), new XAttribute("text-anchor", Anchor), Value);
    }

    private static string N(double Value) => Value.ToString("0.###", CultureInfo.InvariantCulture);
}
