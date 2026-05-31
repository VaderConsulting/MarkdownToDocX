namespace MermaidRecognition.NaiadAdapter;

/// <summary>
/// Maps recognition shape strings to Naiad <see cref="NodeShape"/>.
/// </summary>
public static class FlowchartShapeMapper
{
    /// <summary>
    /// Resolves a recognition shape id to a Naiad node shape.
    /// </summary>
    public static NodeShape ToNodeShape(string? ShapeId)
    {
        if (string.IsNullOrWhiteSpace(ShapeId))
        {
            return NodeShape.Rectangle;
        }

        if (ShapeLookup.TryGetValue(ShapeId.Trim(), out NodeShape Mapped))
        {
            return Mapped;
        }

        return NodeShape.Rectangle;
    }

    private static readonly Dictionary<string, NodeShape> ShapeLookup =
        new Dictionary<string, NodeShape>(StringComparer.OrdinalIgnoreCase)
        {
            ["rect"] = NodeShape.Rectangle,
            ["rectangle"] = NodeShape.Rectangle,
            ["rounded"] = NodeShape.RoundedRectangle,
            ["round"] = NodeShape.RoundedRectangle,
            ["stadium"] = NodeShape.Stadium,
            ["subroutine"] = NodeShape.Subroutine,
            ["cylinder"] = NodeShape.Cylinder,
            ["circle"] = NodeShape.Circle,
            ["doublecircle"] = NodeShape.DoubleCircle,
            ["double-circle"] = NodeShape.DoubleCircle,
            ["dbl-circ"] = NodeShape.DoubleCircle,
            ["diamond"] = NodeShape.Diamond,
            ["hexagon"] = NodeShape.Hexagon,
            ["hex"] = NodeShape.Hexagon,
            ["prepare"] = NodeShape.Hexagon,
            ["asymmetric"] = NodeShape.Asymmetric,
            ["odd"] = NodeShape.Asymmetric,
            ["parallelogram"] = NodeShape.Parallelogram,
            ["trapezoid"] = NodeShape.Trapezoid,
            ["inv_trapezoid"] = NodeShape.TrapezoidAlt,
            ["inv-trapezoid"] = NodeShape.TrapezoidAlt,
            ["lean_right"] = NodeShape.ParallelogramAlt,
            ["lean_left"] = NodeShape.Parallelogram,
        };
}
