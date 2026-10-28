using System;

namespace MermaidRecognition.Contract;

/// <summary>Lets a shape renderer supply geometry without teaching the router individual shape names.</summary>
public interface INodeGeometryProvider
{
    /// <summary>Creates immutable local geometry measured from the node centre, with page-down Y.</summary>
    /// <param name="Width">Measured node width.</param>
    /// <param name="Height">Measured node height.</param>
    /// <returns>The visual extent, eligible connection boundary and occupied obstacle geometry.</returns>
    NodeGeometry Create(double Width, double Height);
}

/// <summary>Separates rendered extent, eligible ports and occupied geometry for one measured node.</summary>
/// <param name="VisualBounds">The complete visible extent, including decorative projections.</param>
/// <param name="ConnectionBoundary">The eligible boundary on which connections terminate.</param>
/// <param name="ObstacleGeometry">The occupied body and decorations that unrelated paths must avoid.</param>
public sealed record NodeGeometry(NodeGeometryBounds VisualBounds,
    INodeConnectionBoundary ConnectionBoundary, INodeObstacleGeometry ObstacleGeometry)
{
    /// <summary>Creates the rectangular default for a shape that has not supplied its own definitions.</summary>
    /// <param name="Width">Measured visual width.</param>
    /// <param name="Height">Measured visual height.</param>
    /// <returns>One rectangle shared by all three geometry roles.</returns>
    public static NodeGeometry Rectangle(double Width, double Height)
    {
        if (!double.IsFinite(Width) || Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width));
        }

        if (!double.IsFinite(Height) || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height));
        }

        NodeGeometryBounds Bounds = new NodeGeometryBounds(-Width / 2, -Height / 2, Width / 2, Height / 2);
        RectangularNodeGeometry Rectangle = new RectangularNodeGeometry(Bounds);
        return new(Bounds, Rectangle, Rectangle);
    }
}

/// <summary>A local page-down bounding rectangle supplied by a shape renderer.</summary>
/// <param name="Left">Minimum X.</param>
/// <param name="Top">Minimum Y.</param>
/// <param name="Right">Maximum X.</param>
/// <param name="Bottom">Maximum Y.</param>
public sealed record NodeGeometryBounds(double Left, double Top, double Right, double Bottom)
{
    /// <summary>The visual width.</summary>
    public double Width => Right - Left;
    /// <summary>The visual height, positive in page coordinates.</summary>
    public double Height => Bottom - Top;
}

/// <summary>A connection-boundary point and its outward unit normal in local page coordinates.</summary>
/// <param name="X">Port X relative to the node centre.</param>
/// <param name="Y">Port Y relative to the node centre.</param>
/// <param name="NormalX">Outward horizontal component.</param>
/// <param name="NormalY">Outward page-down vertical component.</param>
public readonly record struct NodeGeometryPort(double X, double Y, double NormalX, double NormalY);

/// <summary>Describes eligible connection segments independently of visible decorations.</summary>
public interface INodeConnectionBoundary
{
    /// <summary>Allocates a port on an eligible segment of the requested page compass side.</summary>
    /// <param name="Side">Top, bottom, left or right.</param>
    /// <param name="Fraction">Position strictly between zero and one in side order.</param>
    /// <param name="Port">The exact boundary point and outward normal.</param>
    /// <returns>False when that side or position is ineligible.</returns>
    bool TryPort(string Side, double Fraction, out NodeGeometryPort Port);

    /// <summary>Checks a terminal against eligible segments rather than the outer visual bounding box.</summary>
    /// <param name="X">Local terminal X.</param>
    /// <param name="Y">Local terminal Y.</param>
    /// <param name="Tolerance">Permitted geometric attachment error.</param>
    /// <returns>Whether the terminal belongs to the connection boundary.</returns>
    bool Contains(double X, double Y, double Tolerance);
}

/// <summary>Describes occupied shape geometry used for collision checks.</summary>
public interface INodeObstacleGeometry
{
    /// <summary>Checks the open segment against occupied interior, allowing an endpoint exactly on its boundary.</summary>
    /// <param name="StartX">Local segment start X.</param>
    /// <param name="StartY">Local segment start Y.</param>
    /// <param name="EndX">Local segment end X.</param>
    /// <param name="EndY">Local segment end Y.</param>
    /// <returns>Whether any positive-length part of the segment enters occupied interior.</returns>
    bool IntersectsSegmentInterior(double StartX, double StartY, double EndX, double EndY);
}

/// <summary>Provides rectangular connection and obstacle geometry, also usable as the body of a decorated shape.</summary>
/// <param name="Bounds">The rectangle occupied by this geometry role.</param>
public sealed class RectangularNodeGeometry(NodeGeometryBounds Bounds) : INodeConnectionBoundary, INodeObstacleGeometry
{
    /// <inheritdoc />
    public bool TryPort(string Side, double Fraction, out NodeGeometryPort Port)
    {
        Port = default;
        if (!double.IsFinite(Fraction) || Fraction <= 0 || Fraction >= 1)
        {
            return false;
        }

        double X = Bounds.Left + (Fraction * Bounds.Width);
        double Y = Bounds.Top + (Fraction * Bounds.Height);
        switch (Side)
        {
            case "top": Port = new(X, Bounds.Top, 0, -1); return true;
            case "bottom": Port = new(X, Bounds.Bottom, 0, 1); return true;
            case "left": Port = new(Bounds.Left, Y, -1, 0); return true;
            case "right": Port = new(Bounds.Right, Y, 1, 0); return true;
            default: return false;
        }
    }

    /// <inheritdoc />
    public bool Contains(double X, double Y, double Tolerance)
        => ((Math.Abs(X - Bounds.Left) <= Tolerance || Math.Abs(X - Bounds.Right) <= Tolerance)
                && Y >= Bounds.Top - Tolerance && Y <= Bounds.Bottom + Tolerance)
            || ((Math.Abs(Y - Bounds.Top) <= Tolerance || Math.Abs(Y - Bounds.Bottom) <= Tolerance)
                && X >= Bounds.Left - Tolerance && X <= Bounds.Right + Tolerance);

    /// <inheritdoc />
    public bool IntersectsSegmentInterior(double StartX, double StartY, double EndX, double EndY)
    {
        double Lower = 0, Upper = 1;
        if (!Clip(StartX, EndX - StartX, Bounds.Left, Bounds.Right, ref Lower, ref Upper)
            || !Clip(StartY, EndY - StartY, Bounds.Top, Bounds.Bottom, ref Lower, ref Upper)
            || Upper <= Lower)
        {
            return false;
        }

        double Middle = (Lower + Upper) / 2;
        double X = StartX + (Middle * (EndX - StartX)), Y = StartY + (Middle * (EndY - StartY));
        return X > Bounds.Left && X < Bounds.Right && Y > Bounds.Top && Y < Bounds.Bottom;
    }

    /// <summary>Clips one segment coordinate to the open rectangle slab.</summary>
    /// <param name="Start">Segment origin on this axis.</param>
    /// <param name="Delta">Segment displacement on this axis.</param>
    /// <param name="Minimum">The lower boundary.</param>
    /// <param name="Maximum">The upper boundary.</param>
    /// <param name="Lower">Lowest remaining segment parameter.</param>
    /// <param name="Upper">Highest remaining segment parameter.</param>
    /// <returns>Whether the segment has a nonempty interval within the slab.</returns>
    private static bool Clip(double Start, double Delta, double Minimum, double Maximum, ref double Lower, ref double Upper)
    {
        if (Delta == 0)
        {
            return Start > Minimum && Start < Maximum;
        }

        double A = (Minimum - Start) / Delta, B = (Maximum - Start) / Delta;
        Lower = Math.Max(Lower, Math.Min(A, B));
        Upper = Math.Min(Upper, Math.Max(A, B));
        return Upper > Lower;
    }
}
