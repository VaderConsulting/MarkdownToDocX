using System;
using System.Collections.Generic;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Renders an analysed enterprise document to SVG using explicit paths (no markers or foreignObject).
/// </summary>
public static class EnterpriseSvgRenderer
{
    private const double FONT_SIZE = 12;

    private const double LINE_HEIGHT = 15;

    /// <summary>
    /// Renders SVG for a document that has already been validated (hidden-by-nesting flags applied).
    /// </summary>
    /// <param name="Document">Parsed and validated document.</param>
    /// <returns>SVG XML.</returns>
    public static string Render(EnterpriseDocument Document)
    {
        if (Document == null)
        {
            throw new ArgumentNullException(nameof(Document));
        }

        EnterpriseLayoutResult Layout = EnterpriseLayoutEngine.Layout(Document);
        double TitleHeight = string.IsNullOrEmpty(Document.Title) ? 0 : 30;
        double Width = Math.Ceiling(Layout.Width + 10);
        double Height = Math.Ceiling(Layout.Height + TitleHeight + 10);
        StringBuilder Builder = new StringBuilder();
        Builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append("\" height=\"")
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" viewBox=\"0 0 ")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append(' ')
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"")
            .Append(EnterpriseSvgFormat.Number(FONT_SIZE))
            .Append("\">");
        Builder.Append("<rect x=\"0\" y=\"0\" width=\"")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append("\" height=\"")
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" fill=\"#ffffff\"/>");
        if (!string.IsNullOrEmpty(Document.Title))
        {
            Builder.Append("<text x=\"")
                .Append(EnterpriseSvgFormat.Number(Width / 2.0))
                .Append("\" y=\"20\" text-anchor=\"middle\" font-size=\"15\" font-weight=\"bold\">")
                .Append(EnterpriseSvgFormat.Escape(Document.Title))
                .Append("</text>");
        }

        Builder.Append("<g transform=\"translate(0,")
            .Append(EnterpriseSvgFormat.Number(TitleHeight))
            .Append(")\">");

        List<EnterpriseLayoutNode> Ordered = new List<EnterpriseLayoutNode>(Layout.Nodes.Values);
        Ordered.Sort((Left, Right) => Depth(Left, Layout.Nodes).CompareTo(Depth(Right, Layout.Nodes)));
        foreach (EnterpriseLayoutNode Node in Ordered)
        {
            if (Node.IsGroup)
            {
                AppendGroup(Builder, Node);
            }
            else
            {
                AppendElement(Builder, Node);
            }
        }

        foreach (EnterpriseRelationship Relationship in Document.Relationships)
        {
            if (Relationship.HiddenByNesting)
            {
                continue;
            }

            if (!Layout.Nodes.TryGetValue(Relationship.SourceId, out EnterpriseLayoutNode? Source)
                || !Layout.Nodes.TryGetValue(Relationship.TargetId, out EnterpriseLayoutNode? Target)
                || Source.Element == null
                || Target.Element == null)
            {
                continue;
            }

            Builder.Append(RelationshipSvg(Relationship, Source, Target));
        }

        Builder.Append("</g></svg>");
        return Builder.ToString();
    }

    private static int Depth(EnterpriseLayoutNode Node, Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        int Count = 0;
        string? ParentId = Node.ParentId;
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(ParentId) && Seen.Add(ParentId) && Nodes.TryGetValue(ParentId, out EnterpriseLayoutNode? Parent))
        {
            Count++;
            ParentId = Parent.ParentId;
        }

        return Count;
    }

    private static void AppendGroup(StringBuilder Builder, EnterpriseLayoutNode Node)
    {
        double X = Node.AbsoluteX;
        double Y = Node.AbsoluteY;
        double TabWidth = Math.Max(60, Node.DisplayLabel.Length * 6.6 + 16);
        Builder.Append("<g class=\"group\" data-id=\"")
            .Append(EnterpriseSvgFormat.Escape(Node.Id))
            .Append("\"><path d=\"M")
            .Append(EnterpriseSvgFormat.Number(X))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(Y + 18))
            .Append(" V")
            .Append(EnterpriseSvgFormat.Number(Y))
            .Append(" H")
            .Append(EnterpriseSvgFormat.Number(X + TabWidth))
            .Append(" V")
            .Append(EnterpriseSvgFormat.Number(Y + 18))
            .Append("\" fill=\"none\" stroke=\"#000\" stroke-dasharray=\"5 3\"/>")
            .Append("<rect x=\"")
            .Append(EnterpriseSvgFormat.Number(X))
            .Append("\" y=\"")
            .Append(EnterpriseSvgFormat.Number(Y + 18))
            .Append("\" width=\"")
            .Append(EnterpriseSvgFormat.Number(Node.Width))
            .Append("\" height=\"")
            .Append(EnterpriseSvgFormat.Number(Math.Max(10, Node.Height - 18)))
            .Append("\" fill=\"none\" stroke=\"#000\" stroke-dasharray=\"5 3\"/>")
            .Append("<text x=\"")
            .Append(EnterpriseSvgFormat.Number(X + 6))
            .Append("\" y=\"")
            .Append(EnterpriseSvgFormat.Number(Y + 13))
            .Append("\">")
            .Append(EnterpriseSvgFormat.Escape(Node.DisplayLabel))
            .Append("</text></g>");
    }

    private static void AppendElement(StringBuilder Builder, EnterpriseLayoutNode Node)
    {
        EnterpriseElement Element = Node.Element!;
        double X = Node.AbsoluteX;
        double Y = Node.AbsoluteY;
        Builder.Append("<g class=\"element ")
            .Append(EnterpriseElementCatalog.GetKebabName(Element.ElementType))
            .Append("\" data-id=\"")
            .Append(EnterpriseSvgFormat.Escape(Element.Id))
            .Append("\">")
            .Append(EnterpriseNotationSvg.ElementBoxSvg(Element.ElementType, X, Y, Node.Width, Node.Height, Element.JunctionKind));

        if (Element.ElementType == EnterpriseElementType.Junction)
        {
            Builder.Append("</g>");
            return;
        }

        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[Element.ElementType];
        bool Is3d = Metadata.BoxKind == EnterpriseBoxKind.Box3d;
        bool IsObject = Metadata.BoxKind == EnterpriseBoxKind.Object;
        double CentreX = X + (Is3d ? (Node.Width - 30) / 2.0 : Node.Width / 2.0);
        double Top;
        if (Node.IsCompound)
        {
            Top = Y + 20;
        }
        else if (IsObject)
        {
            Top = Y + 12 + ((Node.Height - 12) / 2.0) - (((Node.LabelLines.Count - 1) * LINE_HEIGHT) / 2.0) + 4;
        }
        else
        {
            Top = Y + (Node.Height / 2.0) + (Is3d ? 4 : 0) - (((Node.LabelLines.Count - 1) * LINE_HEIGHT) / 2.0) + 4;
        }

        for (int Index = 0; Index < Node.LabelLines.Count; Index++)
        {
            Builder.Append("<text x=\"")
                .Append(EnterpriseSvgFormat.Number(CentreX))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(Top + (Index * LINE_HEIGHT)))
                .Append("\" text-anchor=\"middle\">")
                .Append(EnterpriseSvgFormat.Escape(Node.LabelLines[Index]))
                .Append("</text>");
        }

        Builder.Append("</g>");
    }

    private static string RelationshipSvg(
        EnterpriseRelationship Relationship,
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target)
    {
        EnterpriseRelationshipMetadata Spec = EnterpriseElementCatalog.Relationships[Relationship.Kind];
        string? Start = Spec.StartMarker;
        string? End = Spec.EndMarker;
        if (Relationship.Kind == EnterpriseRelationshipKind.Access)
        {
            EnterpriseAccessKind Access = Relationship.AccessKind ?? EnterpriseAccessKind.Write;
            Start = Access == EnterpriseAccessKind.Read || Access == EnterpriseAccessKind.ReadWrite ? "arrowOpenSmall" : null;
            End = Access == EnterpriseAccessKind.Write || Access == EnterpriseAccessKind.ReadWrite ? "arrowOpenSmall" : null;
        }

        if (Relationship.Kind == EnterpriseRelationshipKind.Association && Relationship.DirectedAssociation)
        {
            End = "halfArrow";
        }

        (double X1, double Y1, double X2, double Y2) = AttachPoints(Source, Target);
        double[] StartPoint = [X1, Y1];
        double[] EndPoint = [X2, Y2];
        (double Uex, double Uey) = Unit(StartPoint, EndPoint);
        (double Usx, double Usy) = Unit(EndPoint, StartPoint);
        EnterpriseEndMark EndMark = EnterpriseNotationSvg.EndSvg(End, EndPoint[0], EndPoint[1], Uex, Uey);
        EnterpriseEndMark StartMark = EnterpriseNotationSvg.EndSvg(Start, StartPoint[0], StartPoint[1], Usx, Usy);
        EndPoint[0] -= Uex * EndMark.Inset;
        EndPoint[1] -= Uey * EndMark.Inset;
        StartPoint[0] -= Usx * StartMark.Inset;
        StartPoint[1] -= Usy * StartMark.Inset;

        string? Dash = null;
        if (Spec.LineStyle == "dotted")
        {
            Dash = "1.5 3";
        }
        else if (Spec.LineStyle == "dashed")
        {
            Dash = "6 4";
        }

        StringBuilder Builder = new StringBuilder();
        Builder.Append("<g class=\"relationship ")
            .Append(Spec.Name)
            .Append("\" data-source=\"")
            .Append(EnterpriseSvgFormat.Escape(Relationship.SourceId))
            .Append("\" data-target=\"")
            .Append(EnterpriseSvgFormat.Escape(Relationship.TargetId))
            .Append("\"><path d=\"M")
            .Append(EnterpriseSvgFormat.Number(StartPoint[0]))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(StartPoint[1]))
            .Append(" L")
            .Append(EnterpriseSvgFormat.Number(EndPoint[0]))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(EndPoint[1]))
            .Append("\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"");
        if (Dash != null)
        {
            Builder.Append(" stroke-dasharray=\"").Append(Dash).Append('"');
        }

        Builder.Append("/>")
            .Append(StartMark.Svg)
            .Append(EndMark.Svg);

        string Label = EdgeLabel(Relationship);
        if (Label.Length > 0)
        {
            double LabelX = (StartPoint[0] + EndPoint[0]) / 2.0;
            double LabelY = (StartPoint[1] + EndPoint[1]) / 2.0;
            double LabelWidth = (Label.Length * 6) + 8;
            Builder.Append("<rect x=\"")
                .Append(EnterpriseSvgFormat.Number(LabelX - (LabelWidth / 2.0)))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(LabelY - 8))
                .Append("\" width=\"")
                .Append(EnterpriseSvgFormat.Number(LabelWidth))
                .Append("\" height=\"15\" fill=\"#fff\" fill-opacity=\"0.85\"/><text x=\"")
                .Append(EnterpriseSvgFormat.Number(LabelX))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(LabelY + 4))
                .Append("\" text-anchor=\"middle\" font-size=\"11\">")
                .Append(EnterpriseSvgFormat.Escape(Label))
                .Append("</text>");
        }

        Builder.Append("</g>");
        return Builder.ToString();
    }

    private static string EdgeLabel(EnterpriseRelationship Relationship)
    {
        string Label = Relationship.Label ?? string.Empty;
        if (Relationship.Kind == EnterpriseRelationshipKind.Influence && !string.IsNullOrEmpty(Relationship.InfluenceStrength))
        {
            return Label.Length == 0
                ? Relationship.InfluenceStrength
                : Label + " (" + Relationship.InfluenceStrength + ")";
        }

        return Label;
    }

    private static (double X1, double Y1, double X2, double Y2) AttachPoints(EnterpriseLayoutNode Source, EnterpriseLayoutNode Target)
    {
        double Sx = Source.AbsoluteX + (Source.Width / 2.0);
        double Sy = Source.AbsoluteY + (Source.Height / 2.0);
        double Tx = Target.AbsoluteX + (Target.Width / 2.0);
        double Ty = Target.AbsoluteY + (Target.Height / 2.0);
        (double X1, double Y1) = IntersectRectangle(Sx, Sy, Tx, Ty, Source.AbsoluteX, Source.AbsoluteY, Source.Width, Source.Height);
        (double X2, double Y2) = IntersectRectangle(Tx, Ty, Sx, Sy, Target.AbsoluteX, Target.AbsoluteY, Target.Width, Target.Height);
        return (X1, Y1, X2, Y2);
    }

    private static (double X, double Y) IntersectRectangle(
        double FromX,
        double FromY,
        double ToX,
        double ToY,
        double Left,
        double Top,
        double Width,
        double Height)
    {
        double Dx = ToX - FromX;
        double Dy = ToY - FromY;
        if (Math.Abs(Dx) < 0.001 && Math.Abs(Dy) < 0.001)
        {
            return (FromX, Top);
        }

        double BestT = 1;
        TryEdge(FromX, FromY, Dx, Dy, Left, Top, 0, Height, ref BestT);
        TryEdge(FromX, FromY, Dx, Dy, Left + Width, Top, 0, Height, ref BestT);
        TryEdge(FromX, FromY, Dx, Dy, Left, Top, Width, 0, ref BestT);
        TryEdge(FromX, FromY, Dx, Dy, Left, Top + Height, Width, 0, ref BestT);
        return (FromX + (Dx * BestT), FromY + (Dy * BestT));
    }

    private static void TryEdge(
        double FromX,
        double FromY,
        double Dx,
        double Dy,
        double EdgeX,
        double EdgeY,
        double EdgeDx,
        double EdgeDy,
        ref double BestT)
    {
        double Denom = (Dx * EdgeDy) - (Dy * EdgeDx);
        if (Math.Abs(Denom) < 0.0001)
        {
            return;
        }

        double T = ((EdgeX - FromX) * EdgeDy - (EdgeY - FromY) * EdgeDx) / Denom;
        double U = ((EdgeX - FromX) * Dy - (EdgeY - FromY) * Dx) / Denom;
        if (T > 0.001 && T < BestT && U >= -0.001 && U <= 1.001)
        {
            BestT = T;
        }
    }

    private static (double X, double Y) Unit(double[] From, double[] To)
    {
        double Dx = To[0] - From[0];
        double Dy = To[1] - From[1];
        double Length = Math.Sqrt((Dx * Dx) + (Dy * Dy));
        if (Length < 0.001)
        {
            return (0, 1);
        }

        return (Dx / Length, Dy / Length);
    }
}
