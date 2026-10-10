using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Render;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Wraps relationship text and staggers competing labels along straight runs.</summary>
internal static class EnterpriseRelationshipLabelLayout
{
    private const double FontSize = 12;
    private const double Clearance = 6;

    internal static void Place(List<EnterpriseRoutedEdge> Edges,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        foreach (var Edge in Edges)
        {
            Edge.LabelLines = Wrap(Edge.Label);
            Edge.LabelWidth = Edge.LabelLines.Count == 0 ? 0 : Edge.LabelLines.Max(Line =>
                Line.Length * FontSize * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM);
            Edge.LabelHeight = string.IsNullOrWhiteSpace(Edge.Label) ? 0 : FontSize + 15 * (Edge.LabelLines.Count - 1);
        }
        List<EnterpriseRoutedEdge> Placed = new();
        // Reserve the most constrained text first. Stable ordering resolves equal sizes.
        foreach (var Edge in Edges.Where(Edge => !string.IsNullOrWhiteSpace(Edge.Label))
            .OrderByDescending(Edge => Edge.LabelWidth * Edge.LabelHeight))
        {
            FlowchartEdgeLabelGapHelper.LabelSegmentBreak? Preferred = null;
            bool Found = false;
            foreach (int WrapWidth in new[] { 32, 24, 16, 8 })
            {
                Edge.LabelLines = Wrap(Edge.Label, WrapWidth);
                Edge.LabelWidth = Edge.LabelLines.Max(Line => Line.Length * FontSize * FlowchartEdgeLabelGapHelper.LABEL_CHAR_WIDTH_EM);
                Edge.LabelHeight = FontSize + 15 * (Edge.LabelLines.Count - 1);
                var Dimensions = new FlowchartLabelDimensions { DisplayText = string.Join("\n", Edge.LabelLines),
                    Width = Edge.LabelWidth, Height = Edge.LabelHeight };
                FlowchartEdgeLabelGapHelper.TryResolveLabelSegmentBreak(Edge.SvgPoints, Dimensions, FontSize, out Preferred);
                var Segments = Enumerable.Range(0, Edge.SvgPoints.Count - 1)
                    .OrderByDescending(Index => Distance(Edge.SvgPoints[Index], Edge.SvgPoints[Index + 1]));
                foreach (int Index in Segments)
                {
                    var A = Edge.SvgPoints[Index];
                    var B = Edge.SvgPoints[Index + 1];
                    bool Horizontal = Math.Abs(A.Y - B.Y) < 0.5;
                    if (!Horizontal && Math.Abs(A.X - B.X) >= 0.5) continue;
                    double Length = Distance(A, B);
                    double HalfGap = (Horizontal ? Edge.LabelWidth : Edge.LabelHeight) / 2 + Clearance;
                    double Margin = HalfGap + DiagramGridRouteBuilder.CORNER_RADIUS_PX;
                    if (Length < 2 * Margin) continue;
                    for (int Step = 0; Step <= (int)(Length / 15) && !Found; Step++)
                    {
                        double Offset = Step == 0 ? 0 : ((Step + 1) / 2) * 15 * (Step % 2 == 1 ? -1 : 1);
                        double Along = Length / 2 + Offset;
                        if (Along < Margin || Along > Length - Margin) continue;
                        double X = A.X + (B.X - A.X) * Along / Length;
                        double Y = A.Y + (B.Y - A.Y) * Along / Length;
                        if (!IsClear(X, Y, Edge.LabelWidth, Edge.LabelHeight, Nodes, Placed)) continue;
                        if (Placed.Any(Other => Other.LabelBreak?.Horizontal == Horizontal && (Horizontal
                            ? Math.Abs(Y - Other.LabelY) < Edge.LabelHeight + Other.LabelHeight + Clearance
                                && Math.Abs(X - Other.LabelX) < (Edge.LabelWidth + Other.LabelWidth) / 2 + Clearance
                            : Math.Abs(X - Other.LabelX) < Edge.LabelWidth + Other.LabelWidth + Clearance
                                && Math.Abs(Y - Other.LabelY) < (Edge.LabelHeight + Other.LabelHeight) / 2 + Clearance))) continue;
                        // No other connector may strike through the text.
                        bool Crosses = Edges.Where(Other => Other != Edge).Any(Other =>
                            Enumerable.Range(0, Other.SvgPoints.Count - 1).Any(Segment =>
                            {
                                var P = Other.SvgPoints[Segment];
                                var Q = Other.SvgPoints[Segment + 1];
                                return Math.Max(P.X, Q.X) > X - Edge.LabelWidth / 2 - Clearance
                                    && Math.Min(P.X, Q.X) < X + Edge.LabelWidth / 2 + Clearance
                                    && Math.Max(P.Y, Q.Y) > Y - Edge.LabelHeight / 2 - Clearance
                                    && Math.Min(P.Y, Q.Y) < Y + Edge.LabelHeight / 2 + Clearance;
                            }));
                        if (Crosses) continue;
                        Edge.LabelX = X;
                        Edge.LabelY = Y;
                        Edge.LabelBreak = new(Index, Index + 1, X, Y, HalfGap, Horizontal);
                        Found = true;
                    }
                    if (Found) break;
                }
                if (Found) break;
            }
            if (!Found && Preferred != null)
            {
                // Preserve visible text and its break when a local corridor is exhausted.
                Edge.LabelX = Preferred.CentreX;
                Edge.LabelY = Preferred.CentreY;
                Edge.LabelBreak = Preferred;
            }
            Placed.Add(Edge);
        }
    }

    private static bool IsClear(double X, double Y, double Width, double Height,
        Dictionary<string, EnterpriseLayoutNode> Nodes, List<EnterpriseRoutedEdge> Placed)
    {
        if (X - Width / 2 < Clearance || Y - Height / 2 < Clearance) return false;
        bool Overlaps(double Left, double Top, double OtherWidth, double OtherHeight) =>
            X - Width / 2 < Left + OtherWidth + Clearance && X + Width / 2 > Left - Clearance
            && Y - Height / 2 < Top + OtherHeight + Clearance && Y + Height / 2 > Top - Clearance;
        foreach (var Node in Nodes.Values)
        {
            if (Overlaps(Node.AbsoluteX, Node.AbsoluteY, Node.Width, Node.IsGroup ? 18 + 15 * (Node.LabelLines.Count - 1) : Node.Height)) return false;
            if (Node.IsGroup && (Overlaps(Node.AbsoluteX, Node.AbsoluteY, 0, Node.Height)
                || Overlaps(Node.AbsoluteX + Node.Width, Node.AbsoluteY, 0, Node.Height)
                || Overlaps(Node.AbsoluteX, Node.AbsoluteY + Node.Height, Node.Width, 0))) return false;
        }
        foreach (var Other in Placed)
            if (Overlaps(Other.LabelX - Other.LabelWidth / 2, Other.LabelY - Other.LabelHeight / 2,
                Other.LabelWidth, Other.LabelHeight)) return false;
        return true;
    }

    private static double Distance((double X, double Y) A, (double X, double Y) B) =>
        Math.Abs(B.X - A.X) + Math.Abs(B.Y - A.Y);

    private static List<string> Wrap(string Text, int Width = 32) => EnterpriseLabelText.Wrap(Text, Width);
}
