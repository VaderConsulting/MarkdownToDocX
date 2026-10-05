using System;
using System.Collections.Generic;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Builds a native notation legend for every in-scope ArchiMate 3.2 element type
/// and relationship line style. Geometry comes from <see cref="EnterpriseNotationSvg"/>.
/// </summary>
public static class EnterpriseNotationLegendRenderer
{
    private const double BOX_WIDTH = 150;

    private const double BOX_HEIGHT = 56;

    private const double COLUMN_GAP = 16;

    private const double ROW_GAP = 28;

    private const double SECTION_GAP = 22;

    private const int COLUMNS = 5;

    private const double MARGIN = 24;

    /// <summary>
    /// Renders the full 3.2 notation legend as SVG (no markers or foreignObject).
    /// </summary>
    /// <returns>SVG XML.</returns>
    public static string RenderSvg()
    {
        List<LegendSection> Sections = BuildSections();
        double ContentWidth = (COLUMNS * BOX_WIDTH) + ((COLUMNS - 1) * COLUMN_GAP);
        double Width = ContentWidth + (MARGIN * 2);
        double Y = 48;
        StringBuilder Body = new StringBuilder();
        Body.Append("<text x=\"")
            .Append(EnterpriseSvgFormat.Number(Width / 2.0))
            .Append("\" y=\"30\" text-anchor=\"middle\" font-size=\"18\" font-weight=\"bold\">")
            .Append("Enterprise notation legend (ArchiMate 3.2, native renderer)")
            .Append("</text>");

        foreach (LegendSection Section in Sections)
        {
            Body.Append("<text x=\"")
                .Append(EnterpriseSvgFormat.Number(MARGIN))
                .Append("\" y=\"")
                .Append(EnterpriseSvgFormat.Number(Y + 14))
                .Append("\" font-size=\"14\" font-weight=\"bold\">")
                .Append(EnterpriseSvgFormat.Escape(Section.Title))
                .Append("</text>");
            Y += 24;
            int Column = 0;
            foreach (LegendItem Item in Section.Items)
            {
                double X = MARGIN + (Column * (BOX_WIDTH + COLUMN_GAP));
                Body.Append(EnterpriseNotationSvg.ElementBoxSvg(
                    Item.ElementType,
                    X,
                    Y,
                    BOX_WIDTH,
                    BOX_HEIGHT,
                    Item.JunctionKind));
                Body.Append("<text x=\"")
                    .Append(EnterpriseSvgFormat.Number(X + (BOX_WIDTH / 2.0)))
                    .Append("\" y=\"")
                    .Append(EnterpriseSvgFormat.Number(Y + BOX_HEIGHT + 14))
                    .Append("\" text-anchor=\"middle\" font-size=\"10\">")
                    .Append(EnterpriseSvgFormat.Escape(Item.Caption))
                    .Append("</text>");
                Column++;
                if (Column >= COLUMNS)
                {
                    Column = 0;
                    Y += BOX_HEIGHT + ROW_GAP;
                }
            }

            if (Column != 0)
            {
                Y += BOX_HEIGHT + ROW_GAP;
            }

            Y += SECTION_GAP;
        }

        Body.Append("<text x=\"")
            .Append(EnterpriseSvgFormat.Number(MARGIN))
            .Append("\" y=\"")
            .Append(EnterpriseSvgFormat.Number(Y + 14))
            .Append("\" font-size=\"14\" font-weight=\"bold\">Relationship line styles</text>");
        Y += 36;
        List<LegendRelationship> Relationships = BuildRelationships();
        const int REL_COLUMNS = 3;
        const double REL_WIDTH = 250;
        const double REL_HEIGHT = 42;
        int RelColumn = 0;
        foreach (LegendRelationship Relationship in Relationships)
        {
            double X = MARGIN + (RelColumn * (REL_WIDTH + 20));
            Body.Append(RelationshipSampleSvg(Relationship, X, Y, REL_WIDTH));
            RelColumn++;
            if (RelColumn >= REL_COLUMNS)
            {
                RelColumn = 0;
                Y += REL_HEIGHT;
            }
        }

        if (RelColumn != 0)
        {
            Y += REL_HEIGHT;
        }

        double Height = Y + MARGIN;
        StringBuilder Svg = new StringBuilder();
        Svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append("\" height=\"")
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" viewBox=\"0 0 ")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append(' ')
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"12\">")
            .Append("<rect x=\"0\" y=\"0\" width=\"")
            .Append(EnterpriseSvgFormat.Number(Width))
            .Append("\" height=\"")
            .Append(EnterpriseSvgFormat.Number(Height))
            .Append("\" fill=\"#ffffff\"/>")
            .Append(Body)
            .Append("</svg>");
        return Svg.ToString();
    }

    private static List<LegendSection> BuildSections()
    {
        return new List<LegendSection>
        {
            Section(
                "Motivation",
                EnterpriseElementType.Stakeholder,
                EnterpriseElementType.Driver,
                EnterpriseElementType.Assessment,
                EnterpriseElementType.Goal,
                EnterpriseElementType.Outcome,
                EnterpriseElementType.Principle,
                EnterpriseElementType.Requirement,
                EnterpriseElementType.Constraint,
                EnterpriseElementType.Meaning,
                EnterpriseElementType.Value),
            Section(
                "Strategy",
                EnterpriseElementType.Resource,
                EnterpriseElementType.Capability,
                EnterpriseElementType.ValueStream,
                EnterpriseElementType.CourseOfAction),
            Section(
                "Business",
                EnterpriseElementType.BusinessActor,
                EnterpriseElementType.BusinessRole,
                EnterpriseElementType.BusinessCollaboration,
                EnterpriseElementType.BusinessInterface,
                EnterpriseElementType.BusinessFunction,
                EnterpriseElementType.BusinessInteraction,
                EnterpriseElementType.BusinessProcess,
                EnterpriseElementType.BusinessEvent,
                EnterpriseElementType.BusinessService,
                EnterpriseElementType.BusinessObject,
                EnterpriseElementType.Contract,
                EnterpriseElementType.Representation,
                EnterpriseElementType.Product),
            Section(
                "Application",
                EnterpriseElementType.ApplicationComponent,
                EnterpriseElementType.ApplicationCollaboration,
                EnterpriseElementType.ApplicationInterface,
                EnterpriseElementType.ApplicationFunction,
                EnterpriseElementType.ApplicationInteraction,
                EnterpriseElementType.ApplicationProcess,
                EnterpriseElementType.ApplicationEvent,
                EnterpriseElementType.ApplicationService,
                EnterpriseElementType.DataObject),
            Section(
                "Technology (including Physical)",
                EnterpriseElementType.Node,
                EnterpriseElementType.Device,
                EnterpriseElementType.SystemSoftware,
                EnterpriseElementType.TechnologyCollaboration,
                EnterpriseElementType.TechnologyInterface,
                EnterpriseElementType.Path,
                EnterpriseElementType.CommunicationNetwork,
                EnterpriseElementType.TechnologyFunction,
                EnterpriseElementType.TechnologyProcess,
                EnterpriseElementType.TechnologyInteraction,
                EnterpriseElementType.TechnologyEvent,
                EnterpriseElementType.TechnologyService,
                EnterpriseElementType.Artifact,
                EnterpriseElementType.Equipment,
                EnterpriseElementType.Facility,
                EnterpriseElementType.DistributionNetwork,
                EnterpriseElementType.Material),
            Section(
                "Implementation and Migration",
                EnterpriseElementType.WorkPackage,
                EnterpriseElementType.Deliverable,
                EnterpriseElementType.ImplementationEvent,
                EnterpriseElementType.Plateau,
                EnterpriseElementType.Gap),
            Section(
                "Composite",
                EnterpriseElementType.Grouping,
                EnterpriseElementType.Location),
            new LegendSection(
                "Junction",
                new List<LegendItem>
                {
                    new LegendItem(EnterpriseElementType.Junction, EnterpriseJunctionKind.And, "Junction (and)"),
                    new LegendItem(EnterpriseElementType.Junction, EnterpriseJunctionKind.Or, "Junction (or)")
                })
        };
    }

    private static LegendSection Section(string Title, params EnterpriseElementType[] Types)
    {
        List<LegendItem> Items = new List<LegendItem>();
        foreach (EnterpriseElementType ElementType in Types)
        {
            Items.Add(new LegendItem(ElementType, null, EnterpriseElementCatalog.GetSpecName(ElementType)));
        }

        return new LegendSection(Title, Items);
    }

    private static List<LegendRelationship> BuildRelationships()
    {
        return new List<LegendRelationship>
        {
            new LegendRelationship(EnterpriseRelationshipKind.Composition, null, false, "composition"),
            new LegendRelationship(EnterpriseRelationshipKind.Aggregation, null, false, "aggregation"),
            new LegendRelationship(EnterpriseRelationshipKind.Assignment, null, false, "assignment"),
            new LegendRelationship(EnterpriseRelationshipKind.Realization, null, false, "realization"),
            new LegendRelationship(EnterpriseRelationshipKind.Serving, null, false, "serving"),
            new LegendRelationship(EnterpriseRelationshipKind.Access, EnterpriseAccessKind.Write, false, "access (write)"),
            new LegendRelationship(EnterpriseRelationshipKind.Access, EnterpriseAccessKind.Read, false, "access (read)"),
            new LegendRelationship(EnterpriseRelationshipKind.Access, EnterpriseAccessKind.ReadWrite, false, "access (readwrite)"),
            new LegendRelationship(EnterpriseRelationshipKind.Access, EnterpriseAccessKind.Access, false, "access"),
            new LegendRelationship(EnterpriseRelationshipKind.Influence, null, false, "influence"),
            new LegendRelationship(EnterpriseRelationshipKind.Triggering, null, false, "triggering"),
            new LegendRelationship(EnterpriseRelationshipKind.Flow, null, false, "flow"),
            new LegendRelationship(EnterpriseRelationshipKind.Specialization, null, false, "specialization"),
            new LegendRelationship(EnterpriseRelationshipKind.Association, null, false, "association"),
            new LegendRelationship(EnterpriseRelationshipKind.Association, null, true, "association (directed)")
        };
    }

    private static string RelationshipSampleSvg(LegendRelationship Relationship, double X, double Y, double Width)
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

        if (Relationship.Kind == EnterpriseRelationshipKind.Association && Relationship.Directed)
        {
            End = "halfArrow";
        }

        double X1 = X + 8;
        double X2 = X + Width - 70;
        double MidY = Y + 8;
        EnterpriseEndMark EndMark = EnterpriseNotationSvg.EndSvg(End, X2, MidY, 1, 0);
        EnterpriseEndMark StartMark = EnterpriseNotationSvg.EndSvg(Start, X1, MidY, -1, 0);
        double LineX1 = X1 + StartMark.Inset;
        double LineX2 = X2 - EndMark.Inset;
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
        Builder.Append("<g><path d=\"M")
            .Append(EnterpriseSvgFormat.Number(LineX1))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(MidY))
            .Append(" L")
            .Append(EnterpriseSvgFormat.Number(LineX2))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(MidY))
            .Append("\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"");
        if (Dash != null)
        {
            Builder.Append(" stroke-dasharray=\"").Append(Dash).Append('"');
        }

        Builder.Append("/>")
            .Append(StartMark.Svg)
            .Append(EndMark.Svg)
            .Append("<text x=\"")
            .Append(EnterpriseSvgFormat.Number(X + (Width / 2.0)))
            .Append("\" y=\"")
            .Append(EnterpriseSvgFormat.Number(Y + 28))
            .Append("\" text-anchor=\"middle\" font-size=\"10\">")
            .Append(EnterpriseSvgFormat.Escape(Relationship.Caption))
            .Append("</text></g>");
        return Builder.ToString();
    }

    private sealed class LegendSection
    {
        internal LegendSection(string Title, List<LegendItem> Items)
        {
            this.Title = Title;
            this.Items = Items;
        }

        internal string Title { get; }

        internal List<LegendItem> Items { get; }
    }

    private sealed class LegendItem
    {
        internal LegendItem(EnterpriseElementType ElementType, EnterpriseJunctionKind? JunctionKind, string Caption)
        {
            this.ElementType = ElementType;
            this.JunctionKind = JunctionKind;
            this.Caption = Caption;
        }

        internal EnterpriseElementType ElementType { get; }

        internal EnterpriseJunctionKind? JunctionKind { get; }

        internal string Caption { get; }
    }

    private sealed class LegendRelationship
    {
        internal LegendRelationship(
            EnterpriseRelationshipKind Kind,
            EnterpriseAccessKind? AccessKind,
            bool Directed,
            string Caption)
        {
            this.Kind = Kind;
            this.AccessKind = AccessKind;
            this.Directed = Directed;
            this.Caption = Caption;
        }

        internal EnterpriseRelationshipKind Kind { get; }

        internal EnterpriseAccessKind? AccessKind { get; }

        internal bool Directed { get; }

        internal string Caption { get; }
    }
}
