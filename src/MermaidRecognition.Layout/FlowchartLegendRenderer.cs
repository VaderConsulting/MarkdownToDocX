using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

internal static class FlowchartLegendRenderer
{
    private const double Width = 360;

    internal static string Apply(DiagramContract Contract, string Svg)
    {
        if (!Contract.Configuration.RawDirectiveValues.TryGetValue("legend", out string? Raw)) return Svg;
        string Setting = Normalize(Raw);
        if (Setting is "off" or "table") return Svg;
        IReadOnlyList<string> Entries = EntryNames(Contract);
        double Height = 52 + Entries.Count * 36;
        XDocument Xml = XDocument.Parse(Svg, LoadOptions.PreserveWhitespace);
        XElement Root = Xml.Root!;
        double MainWidth = Number(Root.Attribute("width")?.Value), MainHeight = Number(Root.Attribute("height")?.Value);
        double Gap = DiagramGridMetrics.GRID_UNIT_PX;
        double X, Y, MinX = 0, MinY = 0, CanvasWidth = MainWidth, CanvasHeight = MainHeight;
        if (Setting is "right" || Setting.StartsWith("right-of", StringComparison.Ordinal))
        {
            X = MainWidth + Gap; Y = (MainHeight - Height) / 2;
            CanvasWidth = MainWidth + Gap + Width; MinY = Math.Min(0, Y); CanvasHeight = Math.Max(MainHeight, Y + Height) - MinY;
        }
        else if (Setting == "below")
        {
            X = (MainWidth - Width) / 2; Y = MainHeight + Gap;
            MinX = Math.Min(0, X); CanvasWidth = Math.Max(MainWidth, X + Width) - MinX; CanvasHeight = MainHeight + Gap + Height;
        }
        else
        {
            bool Left = Setting.EndsWith("left", StringComparison.Ordinal), Top = Setting.StartsWith("top", StringComparison.Ordinal);
            X = Left ? 0 : MainWidth - Width; Y = Top ? -Height - Gap : MainHeight + Gap;
            MinX = Math.Min(0, X); MinY = Math.Min(0, Y);
            CanvasWidth = Math.Max(MainWidth, X + Width) - MinX; CanvasHeight = Math.Max(MainHeight, Y + Height) - MinY;
        }
        StringBuilder Body = new();
        Body.Append("<g xmlns=\"http://www.w3.org/2000/svg\" class=\"diagram-legend\" transform=\"translate(")
            .Append(X.ToString("0.###", CultureInfo.InvariantCulture)).Append(',').Append(Y.ToString("0.###", CultureInfo.InvariantCulture)).Append(")\">")
            .Append("<rect width=\"").Append(Width).Append("\" height=\"").Append(Height).Append("\" fill=\"white\" stroke=\"#333\"/>")
            .Append("<text x=\"180\" y=\"24\" text-anchor=\"middle\" font-size=\"16\" font-weight=\"bold\">Legend</text>");
        for (int Index = 0; Index < Entries.Count; Index++)
        {
            double RowY = 38 + Index * 36;
            Body.Append("<rect x=\"18\" y=\"").Append(RowY).Append("\" width=\"74\" height=\"24\" rx=\"4\" fill=\"#eee\" stroke=\"#333\"/>")
                .Append("<line x1=\"108\" y1=\"").Append(RowY + 12).Append("\" x2=\"158\" y2=\"").Append(RowY + 12).Append("\" stroke=\"#333\"/>")
                .Append("<text x=\"174\" y=\"").Append(RowY + 16).Append("\" font-size=\"11\">")
                .Append(System.Security.SecurityElement.Escape(Entries[Index])).Append("</text>");
        }
        Body.Append("</g>");
        Root.Add(XElement.Parse(Body.ToString()));
        Root.SetAttributeValue("width", N(CanvasWidth)); Root.SetAttributeValue("height", N(CanvasHeight));
        Root.SetAttributeValue("viewBox", N(MinX) + " " + N(MinY) + " " + N(CanvasWidth) + " " + N(CanvasHeight));
        return Xml.ToString(SaveOptions.DisableFormatting);
    }

    internal static IReadOnlyList<string> EntryNames(DiagramContract Contract)
    {
        var Result = Contract.Groups.Select(Group => "Group")
            .Concat(Contract.Nodes.Select(Node => Readable(Node.Shape ?? Node.ShapeSource ?? "rectangle")))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Result.AddRange(Contract.Edges.Where(Edge => !string.Equals(Edge.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
            .Select(Edge => Edge.MarkerStart == EdgeEndpointMarker.None && Edge.MarkerEnd == EdgeEndpointMarker.None
                ? "Plain connection" : (string.Equals(Edge.Stroke, "dotted", StringComparison.OrdinalIgnoreCase) ? "Dotted " : "") + "Relationship")
            .Distinct(StringComparer.OrdinalIgnoreCase));
        return Result;
    }

    private static string Normalize(string? Value)
    {
        string Setting = (Value ?? "off").Trim().ToLowerInvariant();
        if (Setting is "on" or "true") return "bottom-right";
        string First = Setting.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "off";
        return First is "off" or "table" or "bottom-right" or "bottom-left" or "top-right" or "top-left" or "below" or "right"
            || First.EndsWith("-of", StringComparison.Ordinal) ? First : "off";
    }

    private static string Readable(string Value)
    {
        if (Value is "rect" or "rectangle") return "Rectangle";
        if (Value is "round" or "rounded") return "Rounded rectangle";
        string Text = Value.Replace('-', ' ').Replace('_', ' ');
        return Text.Length == 0 ? "Rectangle" : char.ToUpperInvariant(Text[0]) + Text[1..];
    }
    private static double Number(string? Value) => double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double Result) ? Result : 0;
    private static string N(double Value) => Value.ToString("0.###", CultureInfo.InvariantCulture);
}
