namespace MermaidSharp.Rendering;

public static class ShapePathGenerator
{
    public static string Rectangle(double x, double y, double width, double height, double rx = 0)
    {
        if (rx > 0)
        {
            return $"""
                    M{Fmt(x + rx)},{Fmt(y)}
                    H{Fmt(x + width - rx)}
                    Q{Fmt(x + width)},{Fmt(y)} {Fmt(x + width)},{Fmt(y + rx)}
                    V{Fmt(y + height - rx)}
                    Q{Fmt(x + width)},{Fmt(y + height)} {Fmt(x + width - rx)},{Fmt(y + height)}
                    H{Fmt(x + rx)}
                    Q{Fmt(x)},{Fmt(y + height)} {Fmt(x)},{Fmt(y + height - rx)}
                    V{Fmt(y + rx)}
                    Q{Fmt(x)},{Fmt(y)} {Fmt(x + rx)},{Fmt(y)} Z
                    """;
        }

        return $"M{Fmt(x)},{Fmt(y)} H{Fmt(x + width)} V{Fmt(y + height)} H{Fmt(x)} Z";
    }

    public static string Circle(double cx, double cy, double r) =>
        $"""
         M{Fmt(cx)},{Fmt(cy - r)}
         A{Fmt(r)},{Fmt(r)} 0 1 1 {Fmt(cx)},{Fmt(cy + r)}
         A{Fmt(r)},{Fmt(r)} 0 1 1 {Fmt(cx)},{Fmt(cy - r)} Z
         """;

    public static string Ellipse(double cx, double cy, double rx, double ry) =>
        $"""
         M{Fmt(cx)},{Fmt(cy - ry)}
         A{Fmt(rx)},{Fmt(ry)} 0 1 1 {Fmt(cx)},{Fmt(cy + ry)}
         A{Fmt(rx)},{Fmt(ry)} 0 1 1 {Fmt(cx)},{Fmt(cy - ry)} Z
         """;

    public static string Diamond(double cx, double cy, double width, double height)
    {
        var w2 = width / 2;
        var h2 = height / 2;
        return $"""
                M{Fmt(cx)},{Fmt(cy - h2)}
                L{Fmt(cx + w2)},{Fmt(cy)}
                L{Fmt(cx)},{Fmt(cy + h2)}
                L{Fmt(cx - w2)},{Fmt(cy)} Z
                """;
    }

    /// <summary>
    /// Mermaid dagre-wrapper <c>hexagon()</c>: bevel <c>m = height / 4</c> (not width / 4).
    /// </summary>
    public static string Hexagon(double x, double y, double width, double height)
    {
#pragma warning disable IDE0007
        double M = height / 4.0;
#pragma warning restore IDE0007
        return $"""
                M{Fmt(x + M)},{Fmt(y)}
                L{Fmt(x + width - M)},{Fmt(y)}
                L{Fmt(x + width)},{Fmt(y + height / 2.0)}
                L{Fmt(x + width - M)},{Fmt(y + height)}
                L{Fmt(x + M)},{Fmt(y + height)}
                L{Fmt(x)},{Fmt(y + height / 2.0)} Z
                """;
    }

    public static string Stadium(double x, double y, double width, double height)
    {
        var r = height / 2;
        return $"""
                M{Fmt(x + r)},{Fmt(y)}
                H{Fmt(x + width - r)}
                A{Fmt(r)},{Fmt(r)} 0 0 1 {Fmt(x + width - r)},{Fmt(y + height)}
                H{Fmt(x + r)}
                A{Fmt(r)},{Fmt(r)} 0 0 1 {Fmt(x + r)},{Fmt(y)} Z
                """;
    }

    /// <summary>
    /// Fill path matching dagre-wrapper <c>cylinder()</c> in <c>nodes.js</c>:
    /// M 0,ry → a w,0 → a -w,0 → l 0,h → a w,0 → l 0,-h (both top arcs, sweep 0).
    /// </summary>
    public static string FlowchartCylinderMermaidFillPath(double x, double y, double width, double height)
    {
#pragma warning disable IDE0007
        FlowchartCylinderPoints(
            x,
            y,
            width,
            height,
            out double X1,
            out double Y1,
            out double X2,
            out double Y2,
            out double X3,
            out double Y3,
            out double X4,
            out double Y4,
            out double Rx,
            out double Ry);
#pragma warning restore IDE0007
        return $"""
                M{Fmt(X1)},{Fmt(Y1)}
                A{Fmt(Rx)},{Fmt(Ry)} 0 0 0 {Fmt(X2)},{Fmt(Y2)}
                A{Fmt(Rx)},{Fmt(Ry)} 0 0 0 {Fmt(X1)},{Fmt(Y1)}
                L{Fmt(X3)},{Fmt(Y3)}
                A{Fmt(Rx)},{Fmt(Ry)} 0 0 0 {Fmt(X4)},{Fmt(Y4)}
                L{Fmt(X2)},{Fmt(Y2)} Z
                """;
    }

    /// <summary>
    /// Front rim of the top ellipse (Mermaid second top arc: 2→1, sweep 0) for stroke only.
    /// </summary>
    public static string FlowchartCylinderFrontRimPath(double x, double y, double width, double height)
    {
#pragma warning disable IDE0007
        FlowchartCylinderPoints(
            x,
            y,
            width,
            height,
            out double X1,
            out double Y1,
            out double X2,
            out double Y2,
            out double _,
            out double _,
            out double _,
            out double _,
            out double Rx,
            out double Ry);
#pragma warning restore IDE0007
        return $"M{Fmt(X2)},{Fmt(Y2)} A{Fmt(Rx)},{Fmt(Ry)} 0 0 0 {Fmt(X1)},{Fmt(Y1)}";
    }

    /// <summary>
    /// Stroke outline: top arc 1→2, front rim 2→1, then 1→3, 3→4, 4→2 (Mermaid <c>nodes.js</c> winding).
    /// </summary>
    public static string FlowchartCylinderOutlinePath(double x, double y, double width, double height)
    {
#pragma warning disable IDE0007
        FlowchartCylinderPoints(
            x,
            y,
            width,
            height,
            out double X1,
            out double Y1,
            out double X2,
            out double Y2,
            out double X3,
            out double Y3,
            out double X4,
            out double Y4,
            out double Rx,
            out double Ry);
#pragma warning restore IDE0007
        return $"""
                M{Fmt(X1)},{Fmt(Y1)}
                A{Fmt(Rx)},{Fmt(Ry)} 0 0 0 {Fmt(X2)},{Fmt(Y2)}
                A{Fmt(Rx)},{Fmt(Ry)} 0 0 0 {Fmt(X1)},{Fmt(Y1)}
                L{Fmt(X3)},{Fmt(Y3)}
                A{Fmt(Rx)},{Fmt(Ry)} 0 0 0 {Fmt(X4)},{Fmt(Y4)}
                L{Fmt(X2)},{Fmt(Y2)}
                """;
    }

    /// <summary>Alias used by generic shape routing; same as Mermaid fill path.</summary>
    public static string FlowchartCylinderShapePath(double x, double y, double width, double height)
    {
#pragma warning disable IDE0022
        return FlowchartCylinderMermaidFillPath(x, y, width, height);
#pragma warning restore IDE0022
    }

    static void FlowchartCylinderPoints(
        double x,
        double y,
        double width,
        double height,
        out double X1,
        out double Y1,
        out double X2,
        out double Y2,
        out double X3,
        out double Y3,
        out double X4,
        out double Y4,
        out double Rx,
        out double Ry)
    {
#pragma warning disable IDE0007
        Rx = width / 2.0;
        Ry = FlowchartCylinderEllipseRadiusY(width);
        double BodySpan = height - (2.0 * Ry);
        if (BodySpan < 0.0)
        {
            BodySpan = 0.0;
        }

        X1 = x;
        Y1 = y + Ry;
        X2 = x + width;
        Y2 = Y1;
        X3 = x;
        Y3 = Y1 + BodySpan;
        X4 = x + width;
        Y4 = Y3;
#pragma warning restore IDE0007
    }

    /// <summary>Legacy single-path cylinder (non-flowchart callers).</summary>
    public static string Cylinder(double x, double y, double width, double height)
    {
#pragma warning disable IDE0022
        return FlowchartCylinderOutlinePath(x, y, width, height);
#pragma warning restore IDE0022
    }

    /// <summary>
    /// Vertical ellipse radius for a flowchart cylinder at the given layout width (Mermaid <c>ry</c>).
    /// </summary>
    public static double FlowchartCylinderEllipseRadiusY(double width)
    {
#pragma warning disable IDE0007
        double Rx = width / 2.0;
        return Rx / (2.5 + (width / 50.0));
#pragma warning restore IDE0007
    }

    public static string Parallelogram(double x, double y, double width, double height, double skew = 0.2)
    {
        var offset = width * skew;
        return $"""
                M{Fmt(x + offset)},{Fmt(y)}
                L{Fmt(x + width)},{Fmt(y)}
                L{Fmt(x + width - offset)},{Fmt(y + height)}
                L{Fmt(x)},{Fmt(y + height)} Z
                """;
    }

    public static string ParallelogramAlt(double x, double y, double width, double height, double skew = 0.2)
    {
        var offset = width * skew;
        return $"""
                M{Fmt(x)},{Fmt(y)}
                L{Fmt(x + width - offset)},{Fmt(y)}
                L{Fmt(x + width)},{Fmt(y + height)}
                L{Fmt(x + offset)},{Fmt(y + height)} Z
                """;
    }

    public static string Trapezoid(double x, double y, double width, double height, double skew = 0.2)
    {
        var offset = width * skew;
        return $"""
                M{Fmt(x + offset)},{Fmt(y)}
                L{Fmt(x + width - offset)},{Fmt(y)}
                L{Fmt(x + width)},{Fmt(y + height)}
                L{Fmt(x)},{Fmt(y + height)} Z
                """;
    }

    public static string TrapezoidAlt(double x, double y, double width, double height, double skew = 0.2)
    {
        var offset = width * skew;
        return $"""
                M{Fmt(x)},{Fmt(y)}
                L{Fmt(x + width)},{Fmt(y)}
                L{Fmt(x + width - offset)},{Fmt(y + height)} {Fmt(x + offset)},{Fmt(y + height)} Z
                """;
    }

    /// <summary>
    /// Mermaid <c>odd</c> / <c>rect_left_inv_arrow</c> (<c>A&gt;</c> syntax): left inverse-arrow notch (concave V), not an outward point.
    /// Source: <c>mermaid/packages/mermaid/src/diagrams/flowchart/flowChartShapes.js</c> — <c>rect_left_inv_arrow</c>.
    /// </summary>
    public static string Asymmetric(double x, double y, double width, double height)
    {
#pragma warning disable IDE0007
        double HalfHeight = height / 2.0;
        double LeftX = x - HalfHeight;
        double RightX = x + width;
        double TopY = y;
        double BottomY = y + height;
        double NotchX = x;
        double NotchY = y + HalfHeight;
#pragma warning restore IDE0007
        // Put the path seam on the notch instead of an exposed outer corner.
        // Word can leave tiny gaps when the visible corner is also the start/end join.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M{Fmt(NotchX)},{Fmt(NotchY)}L{Fmt(LeftX)},{Fmt(TopY)}L{Fmt(RightX)},{Fmt(TopY)}L{Fmt(RightX)},{Fmt(BottomY)}L{Fmt(LeftX)},{Fmt(BottomY)}Z");
    }

    public static string Subroutine(double x, double y, double width, double height)
    {
        var inset = width * 0.1;
        return $"""
                M{Fmt(x)},{Fmt(y)}
                H{Fmt(x + width)}
                V{Fmt(y + height)} H{Fmt(x)}
                Z
                M{Fmt(x + inset)},{Fmt(y)}
                V{Fmt(y + height)}
                M{Fmt(x + width - inset)},{Fmt(y)}
                V{Fmt(y + height)}
                """;
    }

    public static string DoubleCircle(double cx, double cy, double r)
    {
        var innerR = r * 0.85;
        return $"{Circle(cx, cy, r)} {Circle(cx, cy, innerR)}";
    }

    public static string Document(double x, double y, double width, double height)
    {
        var waveHeight = height * 0.15;
        var bodyHeight = height - waveHeight;
        var xWidth = x + width;
        return $"""
                M{Fmt(x)},{Fmt(y)}
                H{Fmt(xWidth)}
                V{Fmt(y + bodyHeight)}
                Q{Fmt(x + width * 0.75)},{Fmt(y + height + waveHeight * 0.5)} {Fmt(x + width * 0.5)},{Fmt(y + bodyHeight)}
                Q{Fmt(x + width * 0.25)},{Fmt(y + bodyHeight - waveHeight * 0.5)} {Fmt(x)},{Fmt(y + bodyHeight)} Z
                """;
    }

    public static string GetPath(NodeShape shape, double x, double y, double width, double height)
    {
        var cx = x + width / 2;
        var cy = y + height / 2;
        var r = Math.Min(width, height) / 2;

        return shape switch
        {
            NodeShape.Rectangle => Rectangle(x, y, width, height),
            NodeShape.RoundedRectangle => Rectangle(x, y, width, height, 5),
            NodeShape.Circle => Circle(cx, cy, r),
            NodeShape.DoubleCircle => DoubleCircle(cx, cy, r),
            NodeShape.Diamond => Diamond(cx, cy, width, height),
            NodeShape.Hexagon => Hexagon(x, y, width, height),
            NodeShape.Stadium => Stadium(x, y, width, height),
            NodeShape.Cylinder => Cylinder(x, y, width, height),
            NodeShape.Parallelogram => Parallelogram(x, y, width, height),
            NodeShape.ParallelogramAlt => ParallelogramAlt(x, y, width, height),
            NodeShape.Trapezoid => Trapezoid(x, y, width, height),
            NodeShape.TrapezoidAlt => TrapezoidAlt(x, y, width, height),
            NodeShape.Asymmetric => Asymmetric(x, y, width, height),
            NodeShape.Subroutine => Subroutine(x, y, width, height),
            NodeShape.Document => Document(x, y, width, height),
            _ => Rectangle(x, y, width, height)
        };
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
