using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Freshly drawn Application and Technology notation. Geometry is ported from
/// <c>docs/enterprise/reference-poc/notation.js</c> (author-drawn; not copied from any
/// specification figure or modelling tool).
/// </summary>
public static class EnterpriseNotationSvg
{
    private const string STROKE = "stroke=\"#000\" stroke-width=\"1\" stroke-linejoin=\"round\"";

    /// <summary>Application layer fill.</summary>
    public const string APPLICATION_FILL = "#AFFFFF";

    /// <summary>Technology layer fill.</summary>
    public const string TECHNOLOGY_FILL = "#AFFFAF";

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
        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[ElementType];
        string Fill = Metadata.Layer == EnterpriseLayer.Application ? APPLICATION_FILL : TECHNOLOGY_FILL;
        string Stroke = "#000000";
        string FillStroke = "fill=\"" + Fill + "\" stroke=\"" + Stroke + "\" stroke-width=\"1\"";
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
                + "<line stroke=\"" + Stroke + "\" stroke-width=\"1\" x1=\"" + N(X) + "\" y1=\"" + N(Y + 12) + "\" x2=\"" + N(X + Width) + "\" y2=\"" + N(Y + 12) + "\"/>";
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
