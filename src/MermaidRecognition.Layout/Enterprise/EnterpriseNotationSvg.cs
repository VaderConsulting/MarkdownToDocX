using System;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Freshly drawn enterprise notation. Application and Technology geometry is ported from
/// <c>docs/enterprise/reference-poc/notation.js</c>. Additional layer icons are author-drawn
/// from verbal descriptions of the default iconography (not copied from any specification
/// figure or modelling tool).
/// </summary>
public static class EnterpriseNotationSvg
{
    private const string STROKE = "stroke=\"#000\" stroke-width=\"1\" stroke-linejoin=\"round\"";

    /// <summary>Application layer fill.</summary>
    public const string APPLICATION_FILL = "#AFFFFF";

    /// <summary>Technology layer fill.</summary>
    public const string TECHNOLOGY_FILL = "#AFFFAF";

    /// <summary>Business layer fill.</summary>
    public const string BUSINESS_FILL = "#FFFFAF";

    /// <summary>Motivation aspect fill.</summary>
    public const string MOTIVATION_FILL = "#CCCCFF";

    /// <summary>Strategy aspect fill.</summary>
    public const string STRATEGY_FILL = "#F8D0C8";

    /// <summary>Implementation and Migration fill.</summary>
    public const string IMPLEMENTATION_FILL = "#FFE0E0";

    /// <summary>
    /// Draws the type icon in a 20×14 box whose top-left is (<paramref name="X"/>, <paramref name="Y"/>).
    /// </summary>
    /// <param name="Icon">Icon key from the element catalogue.</param>
    /// <param name="X">Left of the icon box.</param>
    /// <param name="Y">Top of the icon box.</param>
    /// <param name="Fill">Fill colour for solid icon parts.</param>
    /// <returns>SVG fragment.</returns>
    public static string IconSvg(string? Icon, double X, double Y, string Fill)
    {
        if (string.IsNullOrEmpty(Icon))
        {
            return string.Empty;
        }

        switch (Icon)
        {
            case "component":
                return "<g " + STROKE + " fill=\"" + Fill + "\"><rect x=\"" + N(X + 6) + "\" y=\"" + N(Y + 1) + "\" width=\"12\" height=\"12\"/>"
                    + "<rect x=\"" + N(X + 3) + "\" y=\"" + N(Y + 3.5) + "\" width=\"6\" height=\"2.5\"/><rect x=\"" + N(X + 3) + "\" y=\"" + N(Y + 8) + "\" width=\"6\" height=\"2.5\"/></g>";
            case "collaboration":
                return "<g " + STROKE + " fill=\"none\"><circle cx=\"" + N(X + 7.5) + "\" cy=\"" + N(Y + 7) + "\" r=\"5\"/><circle cx=\"" + N(X + 12.5) + "\" cy=\"" + N(Y + 7) + "\" r=\"5\"/></g>";
            case "interface":
                return "<g " + STROKE + " fill=\"none\"><line x1=\"" + N(X + 2) + "\" y1=\"" + N(Y + 7) + "\" x2=\"" + N(X + 9) + "\" y2=\"" + N(Y + 7) + "\"/><circle cx=\"" + N(X + 13.5) + "\" cy=\"" + N(Y + 7) + "\" r=\"4.5\"/></g>";
            case "function":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 5) + "," + N(Y + 5) + " L" + N(X + 10) + "," + N(Y + 1) + " L" + N(X + 15) + "," + N(Y + 5) + " L" + N(X + 15) + "," + N(Y + 13) + " L" + N(X + 10) + "," + N(Y + 9) + " L" + N(X + 5) + "," + N(Y + 13) + " Z\"/>";
            case "interaction":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 9) + "," + N(Y + 1.5) + " A5.5,5.5 0 0 0 " + N(X + 9) + "," + N(Y + 12.5) + " Z\"/>"
                    + "<path d=\"M" + N(X + 11) + "," + N(Y + 1.5) + " A5.5,5.5 0 0 1 " + N(X + 11) + "," + N(Y + 12.5) + " Z\"/></g>";
            case "process":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 2) + "," + N(Y + 5) + " H" + N(X + 11) + " V" + N(Y + 2) + " L" + N(X + 18) + "," + N(Y + 7) + " L" + N(X + 11) + "," + N(Y + 12) + " V" + N(Y + 9) + " H" + N(X + 2) + " Z\"/>";
            case "event":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 3) + "," + N(Y + 2) + " H" + N(X + 12) + " A5,5 0 0 1 " + N(X + 12) + "," + N(Y + 12) + " H" + N(X + 3) + " L" + N(X + 6.5) + "," + N(Y + 7) + " Z\"/>";
            case "service":
                return "<rect " + STROKE + " fill=\"none\" x=\"" + N(X + 2) + "\" y=\"" + N(Y + 3) + "\" width=\"16\" height=\"8\" rx=\"4\" ry=\"4\"/>";
            case "node":
                return "<g " + STROKE + " fill=\"none\"><rect x=\"" + N(X + 4) + "\" y=\"" + N(Y + 4) + "\" width=\"10\" height=\"9\"/>"
                    + "<path d=\"M" + N(X + 4) + "," + N(Y + 4) + " L" + N(X + 7) + "," + N(Y + 1) + " H" + N(X + 17) + " V" + N(Y + 10) + " L" + N(X + 14) + "," + N(Y + 13) + " M" + N(X + 14) + "," + N(Y + 4) + " L" + N(X + 17) + "," + N(Y + 1) + "\"/></g>";
            case "device":
                return "<g " + STROKE + " fill=\"none\"><rect x=\"" + N(X + 4) + "\" y=\"" + N(Y + 1) + "\" width=\"12\" height=\"8\" rx=\"1.5\"/>"
                    + "<path d=\"M" + N(X + 6) + "," + N(Y + 9) + " L" + N(X + 3) + "," + N(Y + 13) + " H" + N(X + 17) + " L" + N(X + 14) + "," + N(Y + 9) + "\"/></g>";
            case "systemSoftware":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 9) + "," + N(Y + 3.2) + " A5,5 0 1 1 " + N(X + 16.6) + "," + N(Y + 9.3) + "\"/>"
                    + "<circle cx=\"" + N(X + 9.5) + "\" cy=\"" + N(Y + 8) + "\" r=\"5\"/></g>";
            case "path":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 6) + "," + N(Y + 3) + " L" + N(X + 2) + "," + N(Y + 7) + " L" + N(X + 6) + "," + N(Y + 11) + " M" + N(X + 14) + "," + N(Y + 3) + " L" + N(X + 18) + "," + N(Y + 7) + " L" + N(X + 14) + "," + N(Y + 11) + "\"/>"
                    + "<line x1=\"" + N(X + 5) + "\" y1=\"" + N(Y + 7) + "\" x2=\"" + N(X + 15) + "\" y2=\"" + N(Y + 7) + "\" stroke-dasharray=\"1.2 1.8\"/></g>";
            case "network":
                double[][] Points =
                [
                    [X + 6, Y + 2],
                    [X + 17, Y + 2],
                    [X + 14, Y + 12],
                    [X + 3, Y + 12]
                ];
                StringBuilder Network = new StringBuilder();
                Network.Append("<g ").Append(STROKE).Append("><path fill=\"none\" d=\"M");
                for (int Index = 0; Index < Points.Length; Index++)
                {
                    if (Index > 0)
                    {
                        Network.Append(" L");
                    }

                    Network.Append(N(Points[Index][0])).Append(',').Append(N(Points[Index][1]));
                }

                Network.Append(" Z\"/>");
                foreach (double[] Point in Points)
                {
                    Network.Append("<circle cx=\"").Append(N(Point[0])).Append("\" cy=\"").Append(N(Point[1])).Append("\" r=\"1.8\" fill=\"#000\"/>");
                }

                Network.Append("</g>");
                return Network.ToString();
            case "artifact":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 5) + "," + N(Y + 1) + " H" + N(X + 11) + " L" + N(X + 15) + "," + N(Y + 5) + " V" + N(Y + 13) + " H" + N(X + 5) + " Z M" + N(X + 11) + "," + N(Y + 1) + " V" + N(Y + 5) + " H" + N(X + 15) + "\"/>";
            case "stakeholder":
                return "<g " + STROKE + " fill=\"none\"><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 4) + "\" r=\"2.4\"/><path d=\"M" + N(X + 5) + "," + N(Y + 13) + " Q" + N(X + 10) + "," + N(Y + 7) + " " + N(X + 15) + "," + N(Y + 13) + "\"/></g>";
            case "driver":
                return "<g " + STROKE + " fill=\"none\"><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 7) + "\" r=\"5\"/><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 7) + "\" r=\"1.4\" fill=\"#000\"/><line x1=\"" + N(X + 10) + "\" y1=\"" + N(Y + 2) + "\" x2=\"" + N(X + 10) + "\" y2=\"" + N(Y + 12) + "\"/><line x1=\"" + N(X + 5) + "\" y1=\"" + N(Y + 7) + "\" x2=\"" + N(X + 15) + "\" y2=\"" + N(Y + 7) + "\"/></g>";
            case "assessment":
                return "<g " + STROKE + " fill=\"none\"><line x1=\"" + N(X + 4) + "\" y1=\"" + N(Y + 12) + "\" x2=\"" + N(X + 16) + "\" y2=\"" + N(Y + 12) + "\"/><rect x=\"" + N(X + 5) + "\" y=\"" + N(Y + 7) + "\" width=\"3\" height=\"5\"/><rect x=\"" + N(X + 9) + "\" y=\"" + N(Y + 4) + "\" width=\"3\" height=\"8\"/><rect x=\"" + N(X + 13) + "\" y=\"" + N(Y + 6) + "\" width=\"3\" height=\"6\"/></g>";
            case "goal":
                return "<g " + STROKE + " fill=\"none\"><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 7) + "\" r=\"5.2\"/><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 7) + "\" r=\"3.2\"/><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 7) + "\" r=\"1.2\" fill=\"#000\"/></g>";
            case "outcome":
                return "<g " + STROKE + " fill=\"none\"><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 7) + "\" r=\"5\"/><path d=\"M" + N(X + 7) + "," + N(Y + 7.5) + " L" + N(X + 9.2) + "," + N(Y + 9.5) + " L" + N(X + 13.4) + "," + N(Y + 4.8) + "\"/></g>";
            case "principle":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 10) + "," + N(Y + 1.5) + " L" + N(X + 16) + "," + N(Y + 7) + " L" + N(X + 10) + "," + N(Y + 12.5) + " L" + N(X + 4) + "," + N(Y + 7) + " Z\"/><line x1=\"" + N(X + 10) + "\" y1=\"" + N(Y + 4) + "\" x2=\"" + N(X + 10) + "\" y2=\"" + N(Y + 10) + "\"/><line x1=\"" + N(X + 7) + "\" y1=\"" + N(Y + 7) + "\" x2=\"" + N(X + 13) + "\" y2=\"" + N(Y + 7) + "\"/></g>";
            case "requirement":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 4) + "," + N(Y + 3) + " H" + N(X + 16) + " L" + N(X + 14) + "," + N(Y + 11) + " H" + N(X + 6) + " Z\"/>";
            case "constraint":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 4) + "," + N(Y + 3) + " H" + N(X + 16) + " L" + N(X + 14) + "," + N(Y + 11) + " H" + N(X + 6) + " Z\"/><line x1=\"" + N(X + 6) + "\" y1=\"" + N(Y + 11) + "\" x2=\"" + N(X + 16) + "\" y2=\"" + N(Y + 3) + "\"/></g>";
            case "meaning":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 6) + "," + N(Y + 4) + " Q" + N(X + 6) + "," + N(Y + 2) + " " + N(X + 8) + "," + N(Y + 2) + "\"/><path d=\"M" + N(X + 12) + "," + N(Y + 4) + " Q" + N(X + 12) + "," + N(Y + 2) + " " + N(X + 14) + "," + N(Y + 2) + "\"/><line x1=\"" + N(X + 6) + "\" y1=\"" + N(Y + 4) + "\" x2=\"" + N(X + 6) + "\" y2=\"" + N(Y + 9) + "\"/><line x1=\"" + N(X + 12) + "\" y1=\"" + N(Y + 4) + "\" x2=\"" + N(X + 12) + "\" y2=\"" + N(Y + 9) + "\"/></g>";
            case "resource":
                return "<g " + STROKE + " fill=\"none\"><rect x=\"" + N(X + 5) + "\" y=\"" + N(Y + 3) + "\" width=\"10\" height=\"8\"/><line x1=\"" + N(X + 5) + "\" y1=\"" + N(Y + 3) + "\" x2=\"" + N(X + 15) + "\" y2=\"" + N(Y + 11) + "\"/><line x1=\"" + N(X + 15) + "\" y1=\"" + N(Y + 3) + "\" x2=\"" + N(X + 5) + "\" y2=\"" + N(Y + 11) + "\"/></g>";
            case "capability":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 3) + "," + N(Y + 3) + " H" + N(X + 11) + " L" + N(X + 17) + "," + N(Y + 7) + " L" + N(X + 11) + "," + N(Y + 11) + " H" + N(X + 3) + " Z\"/>";
            case "valueStream":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 2) + "," + N(Y + 4) + " H" + N(X + 12) + " L" + N(X + 18) + "," + N(Y + 7) + " L" + N(X + 12) + "," + N(Y + 10) + " H" + N(X + 2) + " L" + N(X + 6) + "," + N(Y + 7) + " Z\"/>";
            case "courseOfAction":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 3) + "," + N(Y + 10) + " Q" + N(X + 6) + "," + N(Y + 3) + " " + N(X + 13) + "," + N(Y + 5) + "\"/><path d=\"M" + N(X + 11) + "," + N(Y + 2.5) + " L" + N(X + 16) + "," + N(Y + 5.5) + " L" + N(X + 11) + "," + N(Y + 8) + "\"/></g>";
            case "actor":
                return "<g " + STROKE + " fill=\"none\"><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 3.2) + "\" r=\"2.1\"/><line x1=\"" + N(X + 10) + "\" y1=\"" + N(Y + 5.3) + "\" x2=\"" + N(X + 10) + "\" y2=\"" + N(Y + 9.5) + "\"/><line x1=\"" + N(X + 6.5) + "\" y1=\"" + N(Y + 7) + "\" x2=\"" + N(X + 13.5) + "\" y2=\"" + N(Y + 7) + "\"/><line x1=\"" + N(X + 10) + "\" y1=\"" + N(Y + 9.5) + "\" x2=\"" + N(X + 7) + "\" y2=\"" + N(Y + 13) + "\"/><line x1=\"" + N(X + 10) + "\" y1=\"" + N(Y + 9.5) + "\" x2=\"" + N(X + 13) + "\" y2=\"" + N(Y + 13) + "\"/></g>";
            case "role":
                return "<g " + STROKE + " fill=\"none\"><ellipse cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 4.5) + "\" rx=\"5\" ry=\"2\"/><path d=\"M" + N(X + 5) + "," + N(Y + 4.5) + " V" + N(Y + 10) + " Q" + N(X + 10) + "," + N(Y + 13) + " " + N(X + 15) + "," + N(Y + 10) + " V" + N(Y + 4.5) + "\"/></g>";
            case "representation":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 4) + "," + N(Y + 3) + " H" + N(X + 16) + " V" + N(Y + 9) + " Q" + N(X + 13) + "," + N(Y + 12) + " " + N(X + 10) + "," + N(Y + 9) + " Q" + N(X + 7) + "," + N(Y + 6) + " " + N(X + 4) + "," + N(Y + 9) + " Z\"/>";
            case "product":
                return "<g " + STROKE + " fill=\"none\"><rect x=\"" + N(X + 4) + "\" y=\"" + N(Y + 3) + "\" width=\"12\" height=\"8\"/><line x1=\"" + N(X + 4) + "\" y1=\"" + N(Y + 6) + "\" x2=\"" + N(X + 16) + "\" y2=\"" + N(Y + 6) + "\"/></g>";
            case "equipment":
                return "<g " + STROKE + " fill=\"none\"><rect x=\"" + N(X + 5) + "\" y=\"" + N(Y + 6) + "\" width=\"10\" height=\"6\"/><path d=\"M" + N(X + 7) + "," + N(Y + 6) + " L" + N(X + 8.5) + "," + N(Y + 3) + " H" + N(X + 11.5) + " L" + N(X + 13) + "," + N(Y + 6) + "\"/></g>";
            case "facility":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 3) + "," + N(Y + 12) + " V" + N(Y + 6) + " L" + N(X + 8) + "," + N(Y + 2) + " L" + N(X + 13) + "," + N(Y + 6) + " V" + N(Y + 12) + " Z\"/><rect x=\"" + N(X + 13) + "\" y=\"" + N(Y + 7) + "\" width=\"4\" height=\"5\"/></g>";
            case "distributionNetwork":
                return "<g " + STROKE + " fill=\"none\"><circle cx=\"" + N(X + 5) + "\" cy=\"" + N(Y + 7) + "\" r=\"1.6\" fill=\"#000\"/><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 3.5) + "\" r=\"1.6\" fill=\"#000\"/><circle cx=\"" + N(X + 15) + "\" cy=\"" + N(Y + 7) + "\" r=\"1.6\" fill=\"#000\"/><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 11) + "\" r=\"1.6\" fill=\"#000\"/><path d=\"M" + N(X + 5) + "," + N(Y + 7) + " L" + N(X + 10) + "," + N(Y + 3.5) + " L" + N(X + 15) + "," + N(Y + 7) + " L" + N(X + 10) + "," + N(Y + 11) + " Z\"/></g>";
            case "material":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 4) + "," + N(Y + 10) + " L" + N(X + 10) + "," + N(Y + 12.5) + " L" + N(X + 16) + "," + N(Y + 10) + " L" + N(X + 10) + "," + N(Y + 7.5) + " Z\"/><path d=\"M" + N(X + 4) + "," + N(Y + 7) + " L" + N(X + 10) + "," + N(Y + 9.5) + " L" + N(X + 16) + "," + N(Y + 7) + " L" + N(X + 10) + "," + N(Y + 4.5) + " Z\"/></g>";
            case "workPackage":
                return "<path " + STROKE + " fill=\"none\" d=\"M" + N(X + 3) + "," + N(Y + 3) + " H" + N(X + 12) + " L" + N(X + 17) + "," + N(Y + 7) + " L" + N(X + 12) + "," + N(Y + 11) + " H" + N(X + 3) + " Z\"/>";
            case "deliverable":
                return "<rect " + STROKE + " fill=\"none\" x=\"" + N(X + 4) + "\" y=\"" + N(Y + 3) + "\" width=\"12\" height=\"8\" rx=\"2\"/>";
            case "plateau":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 3) + "," + N(Y + 11) + " H" + N(X + 17) + " M" + N(X + 5) + "," + N(Y + 8) + " H" + N(X + 15) + " M" + N(X + 7) + "," + N(Y + 5) + " H" + N(X + 13) + "\"/></g>";
            case "gap":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 4) + "," + N(Y + 3) + " V" + N(Y + 11) + " M" + N(X + 16) + "," + N(Y + 3) + " V" + N(Y + 11) + "\"/><path d=\"M" + N(X + 6) + "," + N(Y + 7) + " H" + N(X + 9) + " M" + N(X + 11) + "," + N(Y + 7) + " H" + N(X + 14) + "\"/></g>";
            case "location":
                return "<g " + STROKE + " fill=\"none\"><path d=\"M" + N(X + 10) + "," + N(Y + 13) + " L" + N(X + 5.5) + "," + N(Y + 6.5) + " A4.5,4.5 0 1 1 " + N(X + 14.5) + "," + N(Y + 6.5) + " Z\"/><circle cx=\"" + N(X + 10) + "\" cy=\"" + N(Y + 6) + "\" r=\"1.6\"/></g>";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Draws the element box and corner icon.
    /// </summary>
    /// <param name="ElementType">Element type.</param>
    /// <param name="X">Left.</param>
    /// <param name="Y">Top.</param>
    /// <param name="Width">Box width.</param>
    /// <param name="Height">Box height.</param>
    /// <returns>SVG fragment.</returns>
    public static string ElementBoxSvg(EnterpriseElementType ElementType, double X, double Y, double Width, double Height)
    {
        return ElementBoxSvg(ElementType, X, Y, Width, Height, null);
    }

    /// <summary>
    /// Draws the element box and corner icon, including junction polarity.
    /// </summary>
    /// <param name="ElementType">Element type.</param>
    /// <param name="X">Left.</param>
    /// <param name="Y">Top.</param>
    /// <param name="Width">Box width.</param>
    /// <param name="Height">Box height.</param>
    /// <param name="JunctionKind">And/or when the type is a junction.</param>
    /// <returns>SVG fragment.</returns>
    public static string ElementBoxSvg(
        EnterpriseElementType ElementType,
        double X,
        double Y,
        double Width,
        double Height,
        EnterpriseJunctionKind? JunctionKind)
    {
        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[ElementType];
        string Fill = EnterpriseElementCatalog.GetLayerFill(Metadata.Layer);
        string Stroke = "#000000";
        string FillStroke = "fill=\"" + Fill + "\" stroke=\"" + Stroke + "\" stroke-width=\"1\"";
        if (Metadata.BoxKind == EnterpriseBoxKind.Junction)
        {
            double Radius = Math.Min(Width, Height) / 2.0;
            double Cx = X + (Width / 2.0);
            double Cy = Y + (Height / 2.0);
            bool Hollow = JunctionKind == EnterpriseJunctionKind.Or;
            return "<circle cx=\"" + N(Cx) + "\" cy=\"" + N(Cy) + "\" r=\"" + N(Radius) + "\" fill=\""
                + (Hollow ? "#fff" : "#000") + "\" stroke=\"" + Stroke + "\" stroke-width=\"1\"/>";
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Grouping)
        {
            return "<rect fill=\"none\" stroke=\"" + Stroke + "\" stroke-width=\"1\" stroke-dasharray=\"5 3\" x=\""
                + N(X) + "\" y=\"" + N(Y) + "\" width=\"" + N(Width) + "\" height=\"" + N(Height) + "\"/>";
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Ellipse)
        {
            return "<ellipse " + FillStroke + " cx=\"" + N(X + (Width / 2.0)) + "\" cy=\"" + N(Y + (Height / 2.0))
                + "\" rx=\"" + N(Width / 2.0) + "\" ry=\"" + N(Height / 2.0) + "\"/>";
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Contract)
        {
            double Notch = 6;
            return "<path " + FillStroke + " d=\"M" + N(X) + "," + N(Y)
                + " H" + N(X + Width)
                + " L" + N(X + Width - Notch) + "," + N(Y + Notch)
                + " V" + N(Y + Height)
                + " H" + N(X + Notch)
                + " V" + N(Y + Notch)
                + " Z\"/>"
                + "<line stroke=\"" + Stroke + "\" stroke-width=\"1\" x1=\"" + N(X + Notch) + "\" y1=\"" + N(Y + Notch)
                + "\" x2=\"" + N(X + Width - Notch) + "\" y2=\"" + N(Y + Notch) + "\"/>";
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Representation)
        {
            double Wave = Height - 8;
            return "<path " + FillStroke + " d=\"M" + N(X) + "," + N(Y)
                + " H" + N(X + Width)
                + " V" + N(Y + Wave)
                + " Q" + N(X + (Width * 0.75)) + "," + N(Y + Height + 2)
                + " " + N(X + (Width * 0.5)) + "," + N(Y + Wave)
                + " Q" + N(X + (Width * 0.25)) + "," + N(Y + Wave - 10)
                + " " + N(X) + "," + N(Y + Wave)
                + " Z\"/>"
                + IconSvg(Metadata.Icon, X + Width - 23, Y + 3, Fill);
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Box3d)
        {
            const double DEPTH = 8;
            StringBuilder Box3d = new StringBuilder();
            Box3d.Append("<path ").Append(FillStroke).Append(" d=\"M").Append(N(X)).Append(',').Append(N(Y + DEPTH))
                .Append(" L").Append(N(X + DEPTH)).Append(',').Append(N(Y))
                .Append(" H").Append(N(X + Width))
                .Append(" V").Append(N(Y + Height - DEPTH))
                .Append(" L").Append(N(X + Width - DEPTH)).Append(',').Append(N(Y + Height))
                .Append(" H").Append(N(X)).Append(" Z\"/>");
            Box3d.Append("<path fill=\"none\" stroke=\"").Append(Stroke).Append("\" stroke-width=\"1\" d=\"M")
                .Append(N(X)).Append(',').Append(N(Y + DEPTH))
                .Append(" H").Append(N(X + Width - DEPTH))
                .Append(" V").Append(N(Y + Height))
                .Append(" M").Append(N(X + Width - DEPTH)).Append(',').Append(N(Y + DEPTH))
                .Append(" L").Append(N(X + Width)).Append(',').Append(N(Y)).Append("\"/>");
            Box3d.Append(IconSvg(Metadata.Icon, X + Width - DEPTH - 23, Y + DEPTH + 3, Fill));
            return Box3d.ToString();
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Object)
        {
            return "<rect " + FillStroke + " x=\"" + N(X) + "\" y=\"" + N(Y) + "\" width=\"" + N(Width) + "\" height=\"" + N(Height) + "\"/>"
                + "<line stroke=\"" + Stroke + "\" stroke-width=\"1\" x1=\"" + N(X) + "\" y1=\"" + N(Y + 12) + "\" x2=\"" + N(X + Width) + "\" y2=\"" + N(Y + 12) + "\"/>"
                + IconSvg(Metadata.Icon, X + Width - 23, Y + 15, Fill);
        }

        int Radius = Metadata.Aspect == EnterpriseAspect.Behaviour ? 10 : 0;
        return "<rect " + FillStroke + " x=\"" + N(X) + "\" y=\"" + N(Y) + "\" width=\"" + N(Width) + "\" height=\"" + N(Height)
            + "\" rx=\"" + Radius.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\" ry=\"" + Radius.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"/>"
            + IconSvg(Metadata.Icon, X + Width - 23, Y + 3, Fill);
    }

    /// <summary>
    /// Draws a relationship end at the element boundary. <paramref name="Ux"/>,<paramref name="Uy"/> is the
    /// unit vector from the line toward the tip.
    /// </summary>
    /// <param name="Kind">Marker key.</param>
    /// <param name="Px">Tip X on the boundary.</param>
    /// <param name="Py">Tip Y on the boundary.</param>
    /// <param name="Ux">Unit X toward the tip.</param>
    /// <param name="Uy">Unit Y toward the tip.</param>
    /// <returns>SVG and stroke inset.</returns>
    internal static EnterpriseEndMark EndSvg(string? Kind, double Px, double Py, double Ux, double Uy)
    {
        if (string.IsNullOrEmpty(Kind))
        {
            return new EnterpriseEndMark(string.Empty, 0);
        }

        double Vx = -Uy;
        double Vy = Ux;
        string Point(double Along, double Side)
        {
            return N(Px - (Ux * Along) + (Vx * Side)) + "," + N(Py - (Uy * Along) + (Vy * Side));
        }

        switch (Kind)
        {
            case "diamondFilled":
            case "diamondHollow":
                return new EnterpriseEndMark(
                    "<path d=\"M" + Point(0, 0) + " L" + Point(8, 5.5) + " L" + Point(16, 0) + " L" + Point(8, -5.5) + " Z\" fill=\""
                    + (Kind == "diamondFilled" ? "#000" : "#fff") + "\" stroke=\"#000\" stroke-width=\"1\"/>",
                    16);
            case "dot":
                return new EnterpriseEndMark(
                    "<circle cx=\"" + N(Px - (Ux * 3.5)) + "\" cy=\"" + N(Py - (Uy * 3.5)) + "\" r=\"3.5\" fill=\"#000\"/>",
                    3.5);
            case "arrowFilled":
                return new EnterpriseEndMark(
                    "<path d=\"M" + Point(0, 0) + " L" + Point(10, 4.5) + " L" + Point(10, -4.5) + " Z\" fill=\"#000\" stroke=\"#000\" stroke-width=\"1\"/>",
                    10);
            case "triangleHollow":
                return new EnterpriseEndMark(
                    "<path d=\"M" + Point(0, 0) + " L" + Point(14, 7) + " L" + Point(14, -7) + " Z\" fill=\"#fff\" stroke=\"#000\" stroke-width=\"1\"/>",
                    14);
            case "arrowOpen":
                return new EnterpriseEndMark(
                    "<path d=\"M" + Point(11, 5.5) + " L" + Point(0, 0) + " L" + Point(11, -5.5) + "\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"/>",
                    0);
            case "arrowOpenSmall":
                return new EnterpriseEndMark(
                    "<path d=\"M" + Point(7, 3.5) + " L" + Point(0, 0) + " L" + Point(7, -3.5) + "\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"/>",
                    0);
            case "halfArrow":
                return new EnterpriseEndMark(
                    "<path d=\"M" + Point(10, 5) + " L" + Point(0, 0) + "\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"/>",
                    0);
            default:
                return new EnterpriseEndMark(string.Empty, 0);
        }
    }

    private static string N(double Value)
    {
        return EnterpriseSvgFormat.Number(Value);
    }
}
