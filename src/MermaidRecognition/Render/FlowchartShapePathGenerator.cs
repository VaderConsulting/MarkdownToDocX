using System;
using System.Globalization;

namespace MermaidRecognition.Render;

/// <summary>
/// SVG path data for Mermaid flowchart node shapes (aligned with Naiad <c>ShapePathGenerator</c> / dagre-wrapper).
/// </summary>
public static class FlowchartShapePathGenerator
{
    private const double FLOWCHART_DOUBLE_CIRCLE_GAP_PX = 5.0;

    /// <summary>
    /// Corner radius used by the rounded-rectangle shape.
    /// </summary>
    public const double FLOWCHART_ROUNDED_RECT_RADIUS_PX = 5.0;

    /// <summary>
    /// Returns the primary fill path for a node shape, or empty when the shape needs custom rendering (cylinder).
    /// </summary>
    public static string GetFillPath(string? Shape, double X, double Y, double Width, double Height)
    {
        string Normalised = NormaliseShape(Shape);
        double CentreX = X + (Width / 2.0);
        double CentreY = Y + (Height / 2.0);
        double Radius = Math.Min(Width, Height) / 2.0;

        if (string.Equals(Normalised, "cylinder", StringComparison.Ordinal))
        {
            return BuildCylinderFillPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "diamond", StringComparison.Ordinal)
            || string.Equals(Normalised, "rhombus", StringComparison.Ordinal))
        {
            return BuildDiamondPath(CentreX, CentreY, Width, Height);
        }

        if (string.Equals(Normalised, "circle", StringComparison.Ordinal))
        {
            return BuildCirclePath(CentreX, CentreY, Radius);
        }

        if (string.Equals(Normalised, "doublecircle", StringComparison.Ordinal))
        {
            double InnerRadius = Math.Max(0.0, Radius - FLOWCHART_DOUBLE_CIRCLE_GAP_PX);
            return BuildCirclePath(CentreX, CentreY, Radius) + " " + BuildCirclePath(CentreX, CentreY, InnerRadius);
        }

        if (string.Equals(Normalised, "hexagon", StringComparison.Ordinal))
        {
            return BuildHexagonPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "stadium", StringComparison.Ordinal))
        {
            return BuildStadiumPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "rounded", StringComparison.Ordinal))
        {
            return BuildRoundedRectanglePath(X, Y, Width, Height, FLOWCHART_ROUNDED_RECT_RADIUS_PX);
        }

        if (string.Equals(Normalised, "subroutine", StringComparison.Ordinal))
        {
            return BuildSubroutineFillPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "asymmetric", StringComparison.Ordinal))
        {
            return BuildAsymmetricPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "lean_right", StringComparison.Ordinal))
        {
            return BuildParallelogramPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "lean_left", StringComparison.Ordinal))
        {
            return BuildParallelogramAltPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "trapezoid", StringComparison.Ordinal))
        {
            return BuildTrapezoidAltPath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "inv_trapezoid", StringComparison.Ordinal))
        {
            return BuildTrapezoidPath(X, Y, Width, Height);
        }

        return BuildRectanglePath(X, Y, Width, Height);
    }

    /// <summary>
    /// Optional stroke-only overlay path (cylinder outline, subroutine side bars).
    /// </summary>
    public static string? GetStrokeOverlayPath(string? Shape, double X, double Y, double Width, double Height)
    {
        string Normalised = NormaliseShape(Shape);

        if (string.Equals(Normalised, "cylinder", StringComparison.Ordinal))
        {
            return BuildCylinderOutlinePath(X, Y, Width, Height);
        }

        if (string.Equals(Normalised, "subroutine", StringComparison.Ordinal))
        {
            return BuildSubroutineSideBarsPath(X, Y, Width, Height);
        }

        return null;
    }

    /// <summary>
    /// Vertical ellipse radius for cylinder top/bottom caps.
    /// </summary>
    public static double GetCylinderEllipseRadiusY(double Width)
    {
        double Rx = Width / 2.0;
        return Rx / (2.5 + (Width / 50.0));
    }

    private static string NormaliseShape(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return "rect";
        }

        return Shape.Trim().ToLowerInvariant();
    }

    private static string BuildRectanglePath(double X, double Y, double Width, double Height) => string.Create(
            CultureInfo.InvariantCulture,
            $"M{Format(X)},{Format(Y)} H{Format(X + Width)} V{Format(Y + Height)} H{Format(X)} Z");

    private static string BuildRoundedRectanglePath(double X, double Y, double Width, double Height, double Radius)
    {
        double Rx = Math.Min(Radius, Math.Min(Width, Height) / 2.0);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X + Rx)},{Format(Y)}
            H{Format(X + Width - Rx)}
            Q{Format(X + Width)},{Format(Y)} {Format(X + Width)},{Format(Y + Rx)}
            V{Format(Y + Height - Rx)}
            Q{Format(X + Width)},{Format(Y + Height)} {Format(X + Width - Rx)},{Format(Y + Height)}
            H{Format(X + Rx)}
            Q{Format(X)},{Format(Y + Height)} {Format(X)},{Format(Y + Height - Rx)}
            V{Format(Y + Rx)}
            Q{Format(X)},{Format(Y)} {Format(X + Rx)},{Format(Y)} Z
            """);
    }

    private static string BuildCirclePath(double CentreX, double CentreY, double Radius) => string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(CentreX)},{Format(CentreY - Radius)}
            A{Format(Radius)},{Format(Radius)} 0 1 1 {Format(CentreX)},{Format(CentreY + Radius)}
            A{Format(Radius)},{Format(Radius)} 0 1 1 {Format(CentreX)},{Format(CentreY - Radius)} Z
            """);

    private static string BuildDiamondPath(double CentreX, double CentreY, double Width, double Height)
    {
        double HalfWidth = Width / 2.0;
        double HalfHeight = Height / 2.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(CentreX)},{Format(CentreY - HalfHeight)}
            L{Format(CentreX + HalfWidth)},{Format(CentreY)}
            L{Format(CentreX)},{Format(CentreY + HalfHeight)}
            L{Format(CentreX - HalfWidth)},{Format(CentreY)} Z
            """);
    }

    private static string BuildHexagonPath(double X, double Y, double Width, double Height)
    {
        double Bevel = Height / 4.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X + Bevel)},{Format(Y)}
            L{Format(X + Width - Bevel)},{Format(Y)}
            L{Format(X + Width)},{Format(Y + (Height / 2.0))}
            L{Format(X + Width - Bevel)},{Format(Y + Height)}
            L{Format(X + Bevel)},{Format(Y + Height)}
            L{Format(X)},{Format(Y + (Height / 2.0))} Z
            """);
    }

    private static string BuildStadiumPath(double X, double Y, double Width, double Height)
    {
        double Radius = Height / 2.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X + Radius)},{Format(Y)}
            H{Format(X + Width - Radius)}
            A{Format(Radius)},{Format(Radius)} 0 0 1 {Format(X + Width - Radius)},{Format(Y + Height)}
            H{Format(X + Radius)}
            A{Format(Radius)},{Format(Radius)} 0 0 1 {Format(X + Radius)},{Format(Y)} Z
            """);
    }

    private static void BuildCylinderPoints(
        double X,
        double Y,
        double Width,
        double Height,
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
        Rx = Width / 2.0;
        Ry = GetCylinderEllipseRadiusY(Width);
        double BodySpan = Height - (2.0 * Ry);
        if (BodySpan < 0.0)
        {
            BodySpan = 0.0;
        }

        X1 = X;
        Y1 = Y + Ry;
        X2 = X + Width;
        Y2 = Y1;
        X3 = X;
        Y3 = Y1 + BodySpan;
        X4 = X + Width;
        Y4 = Y3;
    }

    private static string BuildCylinderFillPath(double X, double Y, double Width, double Height)
    {
        BuildCylinderPoints(X, Y, Width, Height, out double X1, out double Y1, out double X2, out double Y2, out double X3, out double Y3, out double X4, out double Y4, out double Rx, out double Ry);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X1)},{Format(Y1)}
            A{Format(Rx)},{Format(Ry)} 0 0 0 {Format(X2)},{Format(Y2)}
            A{Format(Rx)},{Format(Ry)} 0 0 0 {Format(X1)},{Format(Y1)}
            L{Format(X3)},{Format(Y3)}
            A{Format(Rx)},{Format(Ry)} 0 0 0 {Format(X4)},{Format(Y4)}
            L{Format(X2)},{Format(Y2)} Z
            """);
    }

    private static string BuildCylinderOutlinePath(double X, double Y, double Width, double Height)
    {
        BuildCylinderPoints(X, Y, Width, Height, out double X1, out double Y1, out double X2, out double Y2, out double X3, out double Y3, out double X4, out double Y4, out double Rx, out double Ry);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X1)},{Format(Y1)}
            A{Format(Rx)},{Format(Ry)} 0 0 0 {Format(X2)},{Format(Y2)}
            A{Format(Rx)},{Format(Ry)} 0 0 0 {Format(X1)},{Format(Y1)}
            L{Format(X3)},{Format(Y3)}
            A{Format(Rx)},{Format(Ry)} 0 0 0 {Format(X4)},{Format(Y4)}
            L{Format(X2)},{Format(Y2)}
            """);
    }

    private static string BuildParallelogramPath(double X, double Y, double Width, double Height)
    {
        double H6 = Height / 6.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X + (2.0 * H6))},{Format(Y)}
            L{Format(X + Width + H6)},{Format(Y)}
            L{Format(X + Width - (2.0 * H6))},{Format(Y + Height)}
            L{Format(X - H6)},{Format(Y + Height)} Z
            """);
    }

    private static string BuildParallelogramAltPath(double X, double Y, double Width, double Height)
    {
        double H6 = Height / 6.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X - (2.0 * H6))},{Format(Y)}
            L{Format(X + Width - H6)},{Format(Y)}
            L{Format(X + Width + (2.0 * H6))},{Format(Y + Height)}
            L{Format(X + H6)},{Format(Y + Height)} Z
            """);
    }

    private static string BuildTrapezoidPath(double X, double Y, double Width, double Height)
    {
        double H6 = Height / 6.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X + H6)},{Format(Y)}
            L{Format(X + Width - H6)},{Format(Y)}
            L{Format(X + Width + (2.0 * H6))},{Format(Y + Height)}
            L{Format(X - (2.0 * H6))},{Format(Y + Height)} Z
            """);
    }

    private static string BuildTrapezoidAltPath(double X, double Y, double Width, double Height)
    {
        double H6 = Height / 6.0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X - (2.0 * H6))},{Format(Y)}
            L{Format(X + Width + (2.0 * H6))},{Format(Y)}
            L{Format(X + Width - H6)},{Format(Y + Height)}
            L{Format(X + H6)},{Format(Y + Height)} Z
            """);
    }

    private static string BuildAsymmetricPath(double X, double Y, double Width, double Height)
    {
        double HalfHeight = Height / 2.0;
        double LeftX = X - HalfHeight;
        double RightX = X + Width;
        double TopY = Y;
        double BottomY = Y + Height;
        double NotchX = X;
        double NotchY = Y + HalfHeight;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M{Format(NotchX)},{Format(NotchY)}L{Format(LeftX)},{Format(TopY)}L{Format(RightX)},{Format(TopY)}L{Format(RightX)},{Format(BottomY)}L{Format(LeftX)},{Format(BottomY)}Z");
    }

    private static string BuildSubroutineFillPath(double X, double Y, double Width, double Height) => BuildRectanglePath(X, Y, Width, Height);

    private static string BuildSubroutineSideBarsPath(double X, double Y, double Width, double Height)
    {
        double Inset = Width * 0.1;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            M{Format(X + Inset)},{Format(Y)} V{Format(Y + Height)}
            M{Format(X + Width - Inset)},{Format(Y)} V{Format(Y + Height)}
            """);
    }

    private static string Format(double Value) => Value.ToString("0.##", CultureInfo.InvariantCulture);
}
