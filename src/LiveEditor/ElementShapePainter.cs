using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// Draws an enterprise element in its catalogue colour and outer shape.
/// </summary>
internal static class ElementShapePainter
{
    /// <summary>
    /// Draws the element box and corner mark into <paramref name="Bounds"/>.
    /// </summary>
    /// <param name="Graphics">Target surface.</param>
    /// <param name="Bounds">Shape bounds.</param>
    /// <param name="Metadata">Element type.</param>
    public static void Draw(Graphics Graphics, Rectangle Bounds, EnterpriseElementMetadata Metadata)
    {
        if (Graphics == null || Metadata == null)
        {
            return;
        }

        SmoothingMode Previous = Graphics.SmoothingMode;
        Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color Fill = ColorTranslator.FromHtml(EnterpriseElementCatalog.GetLayerFill(Metadata.Layer));
        using Pen Outline = new Pen(Color.Black, 1F);
        using SolidBrush Brush = new SolidBrush(Fill);
        DrawBox(Graphics, Bounds, Metadata, Outline, Brush);
        if (!string.IsNullOrEmpty(Metadata.Icon) && Metadata.BoxKind != EnterpriseBoxKind.Junction)
        {
            int IconTop = Metadata.BoxKind == EnterpriseBoxKind.Object ? Bounds.Y + 7 : Bounds.Y + 1;
            Rectangle Icon = new Rectangle(Bounds.Right - 16, IconTop, 15, 11);
            DrawIcon(Graphics, Icon, Metadata.Icon, Fill);
        }

        Graphics.SmoothingMode = Previous;
    }

    /// <summary>
    /// Turns a kebab-case token into a short display name.
    /// </summary>
    /// <param name="KebabName">Catalogue token.</param>
    /// <returns>Spaced name.</returns>
    public static string DisplayName(string KebabName)
    {
        if (string.IsNullOrEmpty(KebabName))
        {
            return string.Empty;
        }

        string[] Parts = KebabName.Split('-');
        StringBuilder Builder = new StringBuilder();
        for (int Index = 0; Index < Parts.Length; Index++)
        {
            if (Parts[Index].Length == 0)
            {
                continue;
            }

            if (Builder.Length > 0)
            {
                Builder.Append(' ');
            }

            Builder.Append(char.ToUpper(Parts[Index][0], CultureInfo.InvariantCulture));
            if (Parts[Index].Length > 1)
            {
                Builder.Append(Parts[Index].Substring(1));
            }
        }

        return Builder.ToString();
    }

    private static void DrawBox(Graphics Graphics, Rectangle Bounds, EnterpriseElementMetadata Metadata, Pen Outline, Brush Fill)
    {
        if (Metadata.BoxKind == EnterpriseBoxKind.Junction)
        {
            int Diameter = Math.Min(Bounds.Width, Bounds.Height) - 2;
            Graphics.FillEllipse(Brushes.Black, Bounds.X + 1, Bounds.Y + 1, Diameter, Diameter);
            Graphics.DrawEllipse(Outline, Bounds.X + 1, Bounds.Y + 1, Diameter, Diameter);
            return;
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Ellipse)
        {
            Graphics.FillEllipse(Fill, Bounds);
            Graphics.DrawEllipse(Outline, Bounds);
            return;
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Grouping)
        {
            using Pen Dashed = new Pen(Color.Black, 1F);
            Dashed.DashStyle = DashStyle.Dash;
            Graphics.DrawLine(Dashed, Bounds.X, Bounds.Y + 6, Bounds.X, Bounds.Y);
            Graphics.DrawLine(Dashed, Bounds.X, Bounds.Y, Bounds.X + 14, Bounds.Y);
            Graphics.DrawLine(Dashed, Bounds.X + 14, Bounds.Y, Bounds.X + 14, Bounds.Y + 6);
            Graphics.FillRectangle(Fill, Bounds.X, Bounds.Y + 6, Bounds.Width - 1, Bounds.Height - 7);
            Graphics.DrawRectangle(Dashed, Bounds.X, Bounds.Y + 6, Bounds.Width - 1, Bounds.Height - 7);
            return;
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Contract)
        {
            int Notch = 5;
            using GraphicsPath Path = new GraphicsPath();
            Path.AddLines(
            [
                new Point(Bounds.Left, Bounds.Top),
                new Point(Bounds.Right, Bounds.Top),
                new Point(Bounds.Right - Notch, Bounds.Top + Notch),
                new Point(Bounds.Right - Notch, Bounds.Bottom),
                new Point(Bounds.Left + Notch, Bounds.Bottom),
                new Point(Bounds.Left + Notch, Bounds.Top + Notch)
            ]);
            Path.CloseFigure();
            Graphics.FillPath(Fill, Path);
            Graphics.DrawPath(Outline, Path);
            Graphics.DrawLine(Outline, Bounds.Left + Notch, Bounds.Top + Notch, Bounds.Right - Notch, Bounds.Top + Notch);
            return;
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Representation)
        {
            int Wave = Bounds.Bottom - 4;
            using GraphicsPath Path = new GraphicsPath();
            Path.AddLine(Bounds.Left, Bounds.Top, Bounds.Right, Bounds.Top);
            Path.AddLine(Bounds.Right, Bounds.Top, Bounds.Right, Wave);
            Path.AddBezier(Bounds.Right, Wave, Bounds.Left + (Bounds.Width * 3 / 4), Wave + 4, Bounds.Left + (Bounds.Width / 2), Wave, Bounds.Left + (Bounds.Width / 2), Wave);
            Path.AddBezier(Bounds.Left + (Bounds.Width / 2), Wave, Bounds.Left + (Bounds.Width / 4), Wave - 4, Bounds.Left, Wave, Bounds.Left, Wave);
            Path.CloseFigure();
            Graphics.FillPath(Fill, Path);
            Graphics.DrawPath(Outline, Path);
            return;
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Box3d)
        {
            int Depth = 5;
            using GraphicsPath Path = new GraphicsPath();
            Path.AddPolygon(
            [
                new Point(Bounds.Left, Bounds.Top + Depth),
                new Point(Bounds.Left + Depth, Bounds.Top),
                new Point(Bounds.Right, Bounds.Top),
                new Point(Bounds.Right, Bounds.Bottom - Depth),
                new Point(Bounds.Right - Depth, Bounds.Bottom),
                new Point(Bounds.Left, Bounds.Bottom)
            ]);
            Graphics.FillPath(Fill, Path);
            Graphics.DrawPath(Outline, Path);
            Graphics.DrawLine(Outline, Bounds.Left, Bounds.Top + Depth, Bounds.Right - Depth, Bounds.Top + Depth);
            Graphics.DrawLine(Outline, Bounds.Right - Depth, Bounds.Top + Depth, Bounds.Right - Depth, Bounds.Bottom);
            Graphics.DrawLine(Outline, Bounds.Right - Depth, Bounds.Top + Depth, Bounds.Right, Bounds.Top);
            return;
        }

        if (Metadata.BoxKind == EnterpriseBoxKind.Object)
        {
            Graphics.FillRectangle(Fill, Bounds);
            Graphics.DrawRectangle(Outline, Bounds);
            Graphics.DrawLine(Outline, Bounds.Left, Bounds.Top + 6, Bounds.Right, Bounds.Top + 6);
            return;
        }

        int Radius = Metadata.Aspect == EnterpriseAspect.Behaviour ? 6 : 0;
        if (Radius == 0)
        {
            Graphics.FillRectangle(Fill, Bounds);
            Graphics.DrawRectangle(Outline, Bounds);
            return;
        }

        using GraphicsPath Rounded = RoundedRectangle(Bounds, Radius);
        Graphics.FillPath(Fill, Rounded);
        Graphics.DrawPath(Outline, Rounded);
    }

    private static GraphicsPath RoundedRectangle(Rectangle Bounds, int Radius)
    {
        int Diameter = Radius * 2;
        GraphicsPath Path = new GraphicsPath();
        Path.AddArc(Bounds.Left, Bounds.Top, Diameter, Diameter, 180, 90);
        Path.AddArc(Bounds.Right - Diameter, Bounds.Top, Diameter, Diameter, 270, 90);
        Path.AddArc(Bounds.Right - Diameter, Bounds.Bottom - Diameter, Diameter, Diameter, 0, 90);
        Path.AddArc(Bounds.Left, Bounds.Bottom - Diameter, Diameter, Diameter, 90, 90);
        Path.CloseFigure();
        return Path;
    }

    private static void DrawIcon(Graphics Graphics, Rectangle Bounds, string Icon, Color Fill)
    {
        GraphicsState State = Graphics.Save();
        Graphics.TranslateTransform(Bounds.X, Bounds.Y);
        Graphics.ScaleTransform(Bounds.Width / 20F, Bounds.Height / 14F);
        using Pen Pen = new Pen(Color.Black, 1.2F);
        Pen.LineJoin = LineJoin.Round;
        using SolidBrush IconFill = new SolidBrush(Fill);
        switch (Icon)
        {
            case "component":
                Graphics.FillRectangle(IconFill, 6, 1, 12, 12);
                Graphics.DrawRectangle(Pen, 6, 1, 12, 12);
                Graphics.FillRectangle(IconFill, 3, 3.5F, 6, 2.5F);
                Graphics.DrawRectangle(Pen, 3, 3.5F, 6, 2.5F);
                Graphics.FillRectangle(IconFill, 3, 8, 6, 2.5F);
                Graphics.DrawRectangle(Pen, 3, 8, 6, 2.5F);
                break;
            case "collaboration":
                Graphics.DrawEllipse(Pen, 2.5F, 2, 10, 10);
                Graphics.DrawEllipse(Pen, 7.5F, 2, 10, 10);
                break;
            case "interface":
                Graphics.DrawLine(Pen, 2, 7, 9, 7);
                Graphics.DrawEllipse(Pen, 9, 2.5F, 9, 9);
                break;
            case "service":
                Graphics.DrawRectangle(Pen, 2, 3, 16, 8);
                break;
            case "actor":
                Graphics.DrawEllipse(Pen, 8, 1, 4, 4);
                Graphics.DrawLine(Pen, 10, 5, 10, 9);
                Graphics.DrawLine(Pen, 6, 7, 14, 7);
                Graphics.DrawLine(Pen, 10, 9, 7, 13);
                Graphics.DrawLine(Pen, 10, 9, 13, 13);
                break;
            case "event":
                Graphics.DrawLines(Pen, [new PointF(3, 2), new PointF(12, 2), new PointF(12, 12), new PointF(3, 12), new PointF(6, 7), new PointF(3, 2)]);
                break;
            case "process":
                Graphics.DrawPolygon(Pen, [new PointF(2, 5), new PointF(11, 5), new PointF(11, 2), new PointF(18, 7), new PointF(11, 12), new PointF(11, 9), new PointF(2, 9)]);
                break;
            case "goal":
                Graphics.DrawEllipse(Pen, 5, 2, 10, 10);
                Graphics.DrawEllipse(Pen, 7, 4, 6, 6);
                Graphics.FillEllipse(Brushes.Black, 9, 6, 2, 2);
                break;
            case "node":
                Graphics.DrawRectangle(Pen, 4, 4, 10, 9);
                Graphics.DrawLine(Pen, 4, 4, 7, 1);
                Graphics.DrawLine(Pen, 7, 1, 17, 1);
                Graphics.DrawLine(Pen, 17, 1, 17, 10);
                Graphics.DrawLine(Pen, 17, 10, 14, 13);
                Graphics.DrawLine(Pen, 14, 4, 17, 1);
                break;
            case "device":
                Graphics.DrawRectangle(Pen, 4, 1, 12, 8);
                Graphics.DrawLines(Pen, [new PointF(6, 9), new PointF(3, 13), new PointF(17, 13), new PointF(14, 9)]);
                break;
            default:
                Graphics.DrawRectangle(Pen, 4, 2, 12, 10);
                break;
        }

        Graphics.Restore(State);
    }
}
