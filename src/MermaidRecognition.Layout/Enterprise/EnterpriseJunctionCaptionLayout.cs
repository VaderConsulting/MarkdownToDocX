using System;
using System.Collections.Generic;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Places external junction captions clear of completed routes and node bodies.</summary>
internal static class EnterpriseJunctionCaptionLayout
{
    internal const double FontSize = 11;
    private const double Clearance = 6;
    internal sealed record Caption(double X, double Y, double Width, double Height);

    internal static Dictionary<string, Caption> Place(
        Dictionary<string, EnterpriseLayoutNode> Nodes, List<EnterpriseRoutedEdge> Edges)
    {
        Dictionary<string, Caption> Captions = new(StringComparer.Ordinal);
        foreach (var Node in Nodes.Values)
        {
            if (Node.Element?.ElementType != EnterpriseElementType.Junction
                || string.IsNullOrEmpty(Node.Element.Label)
                || string.Equals(Node.Element.Label, Node.Element.Id, StringComparison.Ordinal)) continue;
            double Width = FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Node.Element.Label, FontSize);
            double CentreY = Node.AbsoluteY + Node.Height / 2;
            Caption? Selected = null;
            // Search nearest positions first; captions never alter connector geometry.
            for (int Step = 0; Step < 100 && Selected == null; Step++)
            {
                double Offset = Step * 15;
                Caption[] Candidates =
                {
                    new(Node.AbsoluteX + Node.Width + Clearance, CentreY - FontSize - Clearance - Offset, Width, FontSize),
                    new(Node.AbsoluteX + Node.Width + Clearance, CentreY + Clearance + Offset, Width, FontSize),
                    new(Node.AbsoluteX - Width - Clearance, CentreY - FontSize - Clearance - Offset, Width, FontSize),
                    new(Node.AbsoluteX - Width - Clearance, CentreY + Clearance + Offset, Width, FontSize)
                };
                foreach (var Candidate in Candidates)
                {
                    if (IsClear(Candidate, Nodes, Edges, Captions.Values))
                    {
                        Selected = Candidate;
                        break;
                    }
                }
            }
            // An exhausted search remains visible for diagnosis, rather than hiding text.
            Captions[Node.Id] = Selected ?? new(Node.AbsoluteX + Node.Width + Clearance,
                CentreY - FontSize - Clearance, Width, FontSize);
        }
        return Captions;
    }

    private static bool IsClear(Caption Candidate, Dictionary<string, EnterpriseLayoutNode> Nodes,
        List<EnterpriseRoutedEdge> Edges, IEnumerable<Caption> Captions)
    {
        if (Candidate.X < 0 || Candidate.Y < 0) return false;
        foreach (var Node in Nodes.Values)
        {
            // Frame interiors are available; their title strips are occupied.
            double Height = Node.IsGroup ? 20 : Node.Height;
            if (Overlaps(Candidate, Node.AbsoluteX, Node.AbsoluteY, Node.Width, Height)) return false;
        }
        foreach (var Existing in Captions)
            if (Overlaps(Candidate, Existing.X, Existing.Y, Existing.Width, Existing.Height)) return false;
        foreach (var Edge in Edges)
        {
            if (Edge.Label.Length > 0 && Overlaps(Candidate,
                Edge.LabelX - Edge.Label.Length * 3.6, Edge.LabelY - 6,
                Edge.Label.Length * 7.2, 12)) return false;
            for (int Index = 1; Index < Edge.SvgPoints.Count; Index++)
            {
                var A = Edge.SvgPoints[Index - 1];
                var B = Edge.SvgPoints[Index];
                // The segment envelope also conservatively reserves rounded corners.
                if (Overlaps(Candidate, Math.Min(A.X, B.X), Math.Min(A.Y, B.Y),
                    Math.Abs(B.X - A.X), Math.Abs(B.Y - A.Y))) return false;
            }
        }
        return true;
    }

    private static bool Overlaps(Caption Candidate, double X, double Y, double Width, double Height) =>
        Candidate.X < X + Width + Clearance && Candidate.X + Candidate.Width > X - Clearance
        && Candidate.Y < Y + Height + Clearance && Candidate.Y + Candidate.Height > Y - Clearance;
}
