using System;

namespace MermaidRecognition.Render;

/// <summary>
/// Layout/render adjustments for non-rectangular flowchart node shapes.
/// </summary>
public static class FlowchartShapeLayoutHelper
{
    /// <summary>
    /// Extra width extending left of the layout box (asymmetric flag notch).
    /// </summary>
    public static double GetLeftExtensionPx(string? Shape, double Height)
    {
        if (string.Equals(NormaliseShape(Shape), "asymmetric", StringComparison.Ordinal))
        {
            return Height / 2.0;
        }

        (double LeftExtension, _) = GetHorizontalExtensionsPx(Shape, 0.0, Height);
        return LeftExtension;
    }

    /// <summary>
    /// Extra width extending right of the layout box for slanted bracket shapes (F81).
    /// </summary>
    public static double GetRightExtensionPx(string? Shape, double Height)
    {
        (_, double RightExtension) = GetHorizontalExtensionsPx(Shape, 0.0, Height);
        return RightExtension;
    }

    /// <summary>
    /// Returns extra horizontal extent beyond the nominal width box for slanted shapes.
    /// </summary>
    public static (double LeftExtensionPx, double RightExtensionPx) GetHorizontalExtensionsPx(
        string? Shape,
        double Width,
        double Height)
    {
        string Normalised = NormaliseShape(Shape);

        double H6 = Height / 6.0;
        double SlantExtension = 2.0 * H6;

        if (string.Equals(Normalised, "asymmetric", StringComparison.Ordinal))
        {
            return (Height / 2.0, 0.0);
        }

        if (string.Equals(Normalised, "lean_right", StringComparison.Ordinal))
        {
            return (SlantExtension, SlantExtension);
        }

        if (string.Equals(Normalised, "lean_left", StringComparison.Ordinal))
        {
            return (SlantExtension, SlantExtension);
        }

        if (string.Equals(Normalised, "trapezoid", StringComparison.Ordinal))
        {
            return (SlantExtension, SlantExtension);
        }

        if (string.Equals(Normalised, "inv_trapezoid", StringComparison.Ordinal))
        {
            return (SlantExtension, SlantExtension);
        }

        return (0.0, 0.0);
    }

    /// <summary>
    /// Axis-aligned clip box for edge attachment (centre + half extents).
    /// </summary>
    public static (double ClipCenterX, double ClipCenterY, double HalfWidth, double HalfHeight) GetClipBox(
        double CenterX,
        double CenterY,
        double Width,
        double Height,
        string? Shape)
    {
        double LeftExtension = GetLeftExtensionPx(Shape, Height);
        double ClipCenterX = CenterX - (LeftExtension / 2.0);
        double HalfWidth = (Width / 2.0) + (LeftExtension / 2.0);
        return (ClipCenterX, CenterY, HalfWidth, Height / 2.0);
    }

    /// <summary>
    /// Returns the point where a ray from the node centre exits the visible shape boundary.
    /// </summary>
    public static (double X, double Y) RayExitShape(
        double CenterX,
        double CenterY,
        double Width,
        double Height,
        string? Shape,
        double DirX,
        double DirY)
    {
        string Normalised = NormaliseShape(Shape);

        if (string.Equals(Normalised, "ellipse", StringComparison.Ordinal))
        {
            double Rx = Math.Max(0.001, Width / 2.0), Ry = Math.Max(0.001, Height / 2.0);
            double Scale = 1.0 / Math.Sqrt((DirX * DirX / (Rx * Rx)) + (DirY * DirY / (Ry * Ry)));
            return (CenterX + (DirX * Scale), CenterY + (DirY * Scale));
        }

        if (string.Equals(Normalised, "diamond", StringComparison.Ordinal)
            || string.Equals(Normalised, "rhombus", StringComparison.Ordinal))
        {
            return RayExitDiamond(CenterX, CenterY, Width, Height, DirX, DirY);
        }

        if (string.Equals(Normalised, "circle", StringComparison.Ordinal)
            || string.Equals(Normalised, "doublecircle", StringComparison.Ordinal))
        {
            return RayExitCircle(CenterX, CenterY, Width, Height, DirX, DirY);
        }

        if (string.Equals(Normalised, "cylinder", StringComparison.Ordinal))
        {
            return RayExitCylinder(CenterX, CenterY, Width, Height, DirX, DirY);
        }

        if (string.Equals(Normalised, "stadium", StringComparison.Ordinal))
        {
            return RayExitRoundedRect(
                CenterX,
                CenterY,
                Width,
                Height,
                Height / 2.0,
                DirX,
                DirY);
        }

        if (string.Equals(Normalised, "rounded", StringComparison.Ordinal))
        {
            return RayExitRoundedRect(
                CenterX,
                CenterY,
                Width,
                Height,
                FlowchartShapePathGenerator.FLOWCHART_ROUNDED_RECT_RADIUS_PX,
                DirX,
                DirY);
        }

        (double ClipCenterX, double ClipCenterY, double HalfWidth, double HalfHeight) =
            GetClipBox(CenterX, CenterY, Width, Height, Shape);
        return RayExitBox(ClipCenterX, ClipCenterY, HalfWidth, HalfHeight, DirX, DirY);
    }

    private static (double X, double Y) RayExitBox(
        double CentreX,
        double CentreY,
        double HalfWidth,
        double HalfHeight,
        double DirX,
        double DirY)
    {
        double Tx = double.PositiveInfinity;
        if (DirX > 1e-9)
        {
            Tx = HalfWidth / DirX;
        }
        else if (DirX < -1e-9)
        {
            Tx = -HalfWidth / DirX;
        }

        double Ty = double.PositiveInfinity;
        if (DirY > 1e-9)
        {
            Ty = HalfHeight / DirY;
        }
        else if (DirY < -1e-9)
        {
            Ty = -HalfHeight / DirY;
        }

        double T = Math.Min(Tx, Ty);
        return (CentreX + (DirX * T), CentreY + (DirY * T));
    }

    private static (double X, double Y) RayExitDiamond(
        double CenterX,
        double CenterY,
        double Width,
        double Height,
        double DirX,
        double DirY)
    {
        double HalfWidth = Width / 2.0;
        double HalfHeight = Height / 2.0;
        double BestT = double.PositiveInfinity;

        TryUpdateBestRayExit(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX,
            CenterY + HalfHeight,
            CenterX + HalfWidth,
            CenterY,
            ref BestT);
        TryUpdateBestRayExit(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX + HalfWidth,
            CenterY,
            CenterX,
            CenterY - HalfHeight,
            ref BestT);
        TryUpdateBestRayExit(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX,
            CenterY - HalfHeight,
            CenterX - HalfWidth,
            CenterY,
            ref BestT);
        TryUpdateBestRayExit(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX - HalfWidth,
            CenterY,
            CenterX,
            CenterY + HalfHeight,
            ref BestT);

        if (double.IsPositiveInfinity(BestT))
        {
            return RayExitBox(CenterX, CenterY, HalfWidth, HalfHeight, DirX, DirY);
        }

        return (CenterX + (DirX * BestT), CenterY + (DirY * BestT));
    }

    private static (double X, double Y) RayExitCircle(
        double CenterX,
        double CenterY,
        double Width,
        double Height,
        double DirX,
        double DirY)
    {
        double Radius = Math.Min(Width, Height) / 2.0;
        return (CenterX + (DirX * Radius), CenterY + (DirY * Radius));
    }

    private static (double X, double Y) RayExitCylinder(
        double CenterX,
        double CenterY,
        double Width,
        double Height,
        double DirX,
        double DirY)
    {
        double HalfWidth = Width / 2.0;
        double HalfHeight = Height / 2.0;
        double Ry = FlowchartShapePathGenerator.GetCylinderEllipseRadiusY(Width);

        if (Math.Abs(DirX) <= 1e-6 && Math.Abs(DirY) > 1e-6)
        {
            if (DirY > 0.0)
            {
                return (CenterX, CenterY + HalfHeight);
            }

            return (CenterX, CenterY - HalfHeight);
        }

        (double X, double Y) BoxExit = RayExitBox(CenterX, CenterY, HalfWidth, HalfHeight, DirX, DirY);
        double LocalX = Math.Abs(BoxExit.X - CenterX);
        if (LocalX >= HalfWidth - 1e-6 || HalfWidth <= 1e-6)
        {
            return BoxExit;
        }

        double NormalisedX = LocalX / HalfWidth;
        double EllipseOffsetY = Ry * (1.0 - Math.Sqrt(Math.Max(0.0, 1.0 - (NormalisedX * NormalisedX))));
        if (DirY > 0.0)
        {
            return (BoxExit.X, BoxExit.Y + EllipseOffsetY);
        }

        if (DirY < 0.0)
        {
            return (BoxExit.X, BoxExit.Y - EllipseOffsetY);
        }

        return BoxExit;
    }

    private static (double X, double Y) RayExitRoundedRect(
        double CenterX,
        double CenterY,
        double Width,
        double Height,
        double Radius,
        double DirX,
        double DirY)
    {
        double HalfWidth = Width / 2.0;
        double HalfHeight = Height / 2.0;
        double CornerRadius = Math.Min(Radius, Math.Min(HalfWidth, HalfHeight));
        double FlatHalfWidth = HalfWidth - CornerRadius;
        double FlatHalfHeight = HalfHeight - CornerRadius;
        double BestT = double.PositiveInfinity;

        TryRayHitHorizontalFlat(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterY + HalfHeight,
            CenterX - FlatHalfWidth,
            CenterX + FlatHalfWidth,
            ref BestT);
        TryRayHitHorizontalFlat(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterY - HalfHeight,
            CenterX - FlatHalfWidth,
            CenterX + FlatHalfWidth,
            ref BestT);
        TryRayHitVerticalFlat(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX + HalfWidth,
            CenterY - FlatHalfHeight,
            CenterY + FlatHalfHeight,
            ref BestT);
        TryRayHitVerticalFlat(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX - HalfWidth,
            CenterY - FlatHalfHeight,
            CenterY + FlatHalfHeight,
            ref BestT);

        TryRayHitCircleQuadrant(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX + FlatHalfWidth,
            CenterY + FlatHalfHeight,
            CornerRadius,
            1,
            1,
            ref BestT);
        TryRayHitCircleQuadrant(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX + FlatHalfWidth,
            CenterY - FlatHalfHeight,
            CornerRadius,
            1,
            -1,
            ref BestT);
        TryRayHitCircleQuadrant(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX - FlatHalfWidth,
            CenterY + FlatHalfHeight,
            CornerRadius,
            -1,
            1,
            ref BestT);
        TryRayHitCircleQuadrant(
            CenterX,
            CenterY,
            DirX,
            DirY,
            CenterX - FlatHalfWidth,
            CenterY - FlatHalfHeight,
            CornerRadius,
            -1,
            -1,
            ref BestT);

        if (double.IsPositiveInfinity(BestT))
        {
            return RayExitBox(CenterX, CenterY, HalfWidth, HalfHeight, DirX, DirY);
        }

        return (CenterX + (DirX * BestT), CenterY + (DirY * BestT));
    }

    private static void TryRayHitHorizontalFlat(
        double OriginX,
        double OriginY,
        double DirX,
        double DirY,
        double LineY,
        double MinX,
        double MaxX,
        ref double BestT)
    {
        if (Math.Abs(DirY) <= 1e-9)
        {
            return;
        }

        double T = (LineY - OriginY) / DirY;
        if (T <= 1e-9)
        {
            return;
        }

        double HitX = OriginX + (DirX * T);
        if (HitX < MinX - 1e-6 || HitX > MaxX + 1e-6)
        {
            return;
        }

        if (T < BestT)
        {
            BestT = T;
        }
    }

    private static void TryRayHitVerticalFlat(
        double OriginX,
        double OriginY,
        double DirX,
        double DirY,
        double LineX,
        double MinY,
        double MaxY,
        ref double BestT)
    {
        if (Math.Abs(DirX) <= 1e-9)
        {
            return;
        }

        double T = (LineX - OriginX) / DirX;
        if (T <= 1e-9)
        {
            return;
        }

        double HitY = OriginY + (DirY * T);
        if (HitY < MinY - 1e-6 || HitY > MaxY + 1e-6)
        {
            return;
        }

        if (T < BestT)
        {
            BestT = T;
        }
    }

    private static void TryRayHitCircleQuadrant(
        double OriginX,
        double OriginY,
        double DirX,
        double DirY,
        double CircleX,
        double CircleY,
        double Radius,
        int QuadrantX,
        int QuadrantY,
        ref double BestT)
    {
        double OffsetX = OriginX - CircleX;
        double OffsetY = OriginY - CircleY;
        double QuadraticA = (DirX * DirX) + (DirY * DirY);
        if (QuadraticA <= 1e-12)
        {
            return;
        }

        double QuadraticB = 2.0 * ((OffsetX * DirX) + (OffsetY * DirY));
        double QuadraticC = (OffsetX * OffsetX) + (OffsetY * OffsetY) - (Radius * Radius);
        double Discriminant = (QuadraticB * QuadraticB) - (4.0 * QuadraticA * QuadraticC);
        if (Discriminant < 0.0)
        {
            return;
        }

        double Root = Math.Sqrt(Discriminant);
        double[] Candidates = [(-QuadraticB - Root) / (2.0 * QuadraticA), (-QuadraticB + Root) / (2.0 * QuadraticA)];
        foreach (double T in Candidates)
        {
            if (T <= 1e-9 || T >= BestT)
            {
                continue;
            }

            double LocalX = OriginX + (DirX * T) - CircleX;
            double LocalY = OriginY + (DirY * T) - CircleY;
            if ((LocalX * QuadrantX) < -1e-6 || (LocalY * QuadrantY) < -1e-6)
            {
                continue;
            }

            BestT = T;
        }
    }

    private static void TryUpdateBestRayExit(
        double OriginX,
        double OriginY,
        double DirX,
        double DirY,
        double SegmentStartX,
        double SegmentStartY,
        double SegmentEndX,
        double SegmentEndY,
        ref double BestT)
    {
        double SegmentX = SegmentEndX - SegmentStartX;
        double SegmentY = SegmentEndY - SegmentStartY;
        double Denominator = (DirX * SegmentY) - (DirY * SegmentX);
        if (Math.Abs(Denominator) < 1e-9)
        {
            return;
        }

        double RayT = (((SegmentStartX - OriginX) * SegmentY) - ((SegmentStartY - OriginY) * SegmentX)) / Denominator;
        double SegmentU = (((SegmentStartX - OriginX) * DirY) - ((SegmentStartY - OriginY) * DirX)) / Denominator;
        if (RayT <= 1e-9 || SegmentU < 0.0 || SegmentU > 1.0)
        {
            return;
        }

        if (RayT < BestT)
        {
            BestT = RayT;
        }
    }

    private static string NormaliseShape(string? Shape)
    {
        if (string.IsNullOrWhiteSpace(Shape))
        {
            return "rect";
        }

        return Shape.Trim().ToLowerInvariant();
    }
}
