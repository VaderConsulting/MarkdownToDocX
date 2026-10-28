using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

internal sealed record EnterpriseLabelPlacement(string Identity, string Text, double X, double Y, double Width, double Height,
    ImmutableArray<string> Lines, FlowchartEdgeLabelGapHelper.LabelSegmentBreak? Break);

internal sealed record EnterpriseLabelIssue(string Identity, string Kind, string? OtherIdentity, bool OtherIsRoute, double Severity, string Message)
{
    internal string Key => JsonSerializer.Serialize(new { Identity, Kind, OtherIdentity });
}

internal sealed record EnterpriseLabelSnapshot(ImmutableDictionary<string, EnterpriseLabelPlacement> Placements,
    ImmutableArray<EnterpriseLabelIssue> Issues)
{
    internal static EnterpriseLabelSnapshot Empty { get; } = new(ImmutableDictionary<string, EnterpriseLabelPlacement>.Empty, []);
}

internal static class EnterpriseLabelFont
{
    internal static double Resolve(EnterpriseDocument Document, EnterpriseRelationship Relationship, out bool Supported)
    {
        string? Value = Document.Styles.DefaultLink.GetValueOrDefault("font-size");
        int Index = Document.Relationships.IndexOf(Relationship);
        if (Document.Styles.Links.TryGetValue(Index, out var Style) && Style.TryGetValue("font-size", out var Override)) Value = Override;
        if (Value == null) { Supported = !Relationship.IsMarkdownLabel; return 12; }
        var Match = Regex.Match(Value.Trim(), @"^(?<number>(?:[0-9]+\.?[0-9]*|\.[0-9]+))(?<unit>px|pt|em|rem|%)?$", RegexOptions.IgnoreCase);
        Supported = Match.Success && double.TryParse(Match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            && !Relationship.IsMarkdownLabel;
        if (!Supported) return 12;
        double Size = double.Parse(Match.Groups["number"].Value, CultureInfo.InvariantCulture) * (Match.Groups["unit"].Value.ToLowerInvariant() switch
            { "pt" => 96d / 72, "em" or "rem" => 12, "%" => 0.12, _ => 1 });
        Supported = double.IsFinite(Size) && Size > 0;
        return Supported ? Size : 12;
    }
}

internal static partial class EnterpriseRelationshipLabelLayout
{
    internal static EnterpriseLabelSnapshot Capture(List<EnterpriseRoutedEdge> Edges, Dictionary<string, EnterpriseLayoutNode> Nodes,
        IReadOnlyDictionary<string, EnterpriseLabelPlacement>? Previous = null)
    {
        var Placements = (Previous as ImmutableDictionary<string, EnterpriseLabelPlacement>
            ?? Previous?.ToImmutableDictionary(StringComparer.Ordinal) ?? ImmutableDictionary<string, EnterpriseLabelPlacement>.Empty).ToBuilder();
        var Issues = ImmutableArray.CreateBuilder<EnterpriseLabelIssue>();
        string Identity(EnterpriseRoutedEdge Edge) => Edge.Identity.Length > 0 ? Edge.Identity : "unbound:" + Edges.IndexOf(Edge).ToString(CultureInfo.InvariantCulture);
        var Labels = Edges.Where(Edge => !string.IsNullOrWhiteSpace(Edge.Label)).ToArray();
        var Present = Labels.Select(Identity).ToHashSet(StringComparer.Ordinal);
        foreach (string Removed in Placements.Keys.Where(Id => !Present.Contains(Id)).ToArray()) Placements.Remove(Removed);
        foreach (var Label in Labels)
        {
            string Id = Identity(Label);
            if (!Placements.TryGetValue(Id, out var Old) || Old.Text != Label.Label || Old.X != Label.LabelX || Old.Y != Label.LabelY
                || Old.Width != Label.LabelWidth || Old.Height != Label.LabelHeight || Old.Break != Label.LabelBreak || !Old.Lines.SequenceEqual(Label.LabelLines))
                Placements[Id] = new(Id, Label.Label, Label.LabelX, Label.LabelY, Label.LabelWidth, Label.LabelHeight,
                    Label.LabelLines.ToImmutableArray(), Label.LabelBreak);
            void Issue(string Kind, string? Other, bool Route, double Severity, string Message) => Issues.Add(new(Id, Kind, Other, Route, Severity, Message));
            double Overlap(double Left, double Top, double Width, double Height) =>
                Math.Max(0, Math.Min(Label.LabelX + Label.LabelWidth / 2, Left + Width + Clearance)
                    - Math.Max(Label.LabelX - Label.LabelWidth / 2, Left - Clearance))
                * Math.Max(0, Math.Min(Label.LabelY + Label.LabelHeight / 2, Top + Height + Clearance)
                    - Math.Max(Label.LabelY - Label.LabelHeight / 2, Top - Clearance));
            if (!Label.LabelMeasurementSupported) Issue("FontMeasurement", null, false, 1, "The relationship label uses an unsupported font size or text format.");
            if (Label.LabelBreak == null) Issue("NoSegment", null, false, 1, "The relationship label has no straight segment with measured room.");
            foreach (var Node in Nodes.Values)
            {
                double Height = Node.IsGroup ? 18 + 15 * (Node.LabelLines.Count - 1)
                    : Node.IsCompound ? 36 + 15 * (Node.LabelLines.Count - 1) : Node.Height;
                double Area = Overlap(Node.AbsoluteX, Node.AbsoluteY, Node.Width, Height);
                if (Node.IsGroup || Node.IsCompound)
                    Area += Overlap(Node.AbsoluteX, Node.AbsoluteY, 0, Node.Height)
                        + Overlap(Node.AbsoluteX + Node.Width, Node.AbsoluteY, 0, Node.Height)
                        + Overlap(Node.AbsoluteX, Node.AbsoluteY + Node.Height, Node.Width, 0);
                if (Area > 0.01) Issue("NodeOverlap", Node.Id, false, Area, "The relationship label overlaps node/frame '" + Node.Id + "'.");
            }
            foreach (var Other in Labels.Where(Other => Other != Label))
            {
                double Area = Overlap(Other.LabelX - Other.LabelWidth / 2, Other.LabelY - Other.LabelHeight / 2, Other.LabelWidth, Other.LabelHeight);
                if (Area > 0.01) Issue("LabelOverlap", Identity(Other), true, Area, "Relationship labels overlap.");
            }
            foreach (var Other in Edges.Where(Other => Other != Label))
                if (IntersectsLabel(Other.SvgPoints, Label)) Issue("RouteThroughLabel", Identity(Other), true, 1, "Another relationship crosses the label.");
        }
        return new(Placements.ToImmutable(), Issues.ToImmutable());
    }
}
