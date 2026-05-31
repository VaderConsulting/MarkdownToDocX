namespace MermaidRecognition.NaiadAdapter;

/// <summary>
/// Maps <see cref="EdgeContract"/> arrow facts to Naiad <see cref="EdgeType"/> / <see cref="EdgeStyle"/>.
/// </summary>
public static class FlowchartEdgeMapper
{
    /// <summary>
    /// Builds a Naiad edge from a recognition edge contract.
    /// </summary>
    public static Edge ToNaiadEdge(EdgeContract Source)
    {
        (EdgeType Type, EdgeStyle Style) = ResolveTypeAndStyle(Source);
        Edge Target = new Edge
        {
            SourceId = Source.SourceId,
            TargetId = Source.TargetId
        };
        Target.Label = string.IsNullOrEmpty(Source.Label)
            ? Source.Label
            : FlowchartLabelNormalizer.Normalize(Source.Label);
        Target.Type = Type;
        Target.LineStyle = Style;
        Target.MinRankSpan = Math.Max(1, Source.MinLength);

        if (string.Equals(Source.Stroke, "invisible", StringComparison.OrdinalIgnoreCase))
        {
            // `~~~` edges should participate in layout but not render a visible connector stroke.
            // Use explicit style overrides rather than introducing new Naiad enum values.
            Target.Style.Stroke = "none";
            Target.Style.StrokeWidth = 0;
        }

        return Target;
    }

    static (EdgeType Type, EdgeStyle Style) ResolveTypeAndStyle(EdgeContract Source)
    {
        string Stroke = Source.Stroke ?? "normal";
        if (string.Equals(Stroke, "invisible", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Stroke, "open", StringComparison.OrdinalIgnoreCase))
        {
            return (EdgeType.Open, EdgeStyle.Solid);
        }

        EdgeStyle LineStyle = EdgeStyle.Solid;
        if (string.Equals(Stroke, "dotted", StringComparison.OrdinalIgnoreCase))
        {
            LineStyle = EdgeStyle.Dotted;
        }
        else if (string.Equals(Stroke, "thick", StringComparison.OrdinalIgnoreCase))
        {
            LineStyle = EdgeStyle.Thick;
        }

        bool HasStartPoint = Source.MarkerStart == EdgeEndpointMarker.Point;
        bool HasEndPoint = Source.MarkerEnd == EdgeEndpointMarker.Point;
        bool HasEndCircle = Source.MarkerEnd == EdgeEndpointMarker.Circle;
        bool HasEndCross = Source.MarkerEnd == EdgeEndpointMarker.Cross;
        bool HasStartCircle = Source.MarkerStart == EdgeEndpointMarker.Circle;
        bool HasStartCross = Source.MarkerStart == EdgeEndpointMarker.Cross;

        if (HasStartCircle && HasEndCircle)
        {
            return (EdgeType.BiDirectionalCircle, LineStyle);
        }

        if (HasStartCross && HasEndCross)
        {
            return (EdgeType.BiDirectionalCross, LineStyle);
        }

        if (HasStartPoint && HasEndPoint)
        {
            return (EdgeType.BiDirectional, LineStyle);
        }

        if (HasEndCircle)
        {
            return (EdgeType.CircleEnd, LineStyle);
        }

        if (HasEndCross)
        {
            return (EdgeType.CrossEnd, LineStyle);
        }

        if (LineStyle == EdgeStyle.Dotted && HasEndPoint)
        {
            return (EdgeType.DottedArrow, LineStyle);
        }

        if (LineStyle == EdgeStyle.Dotted)
        {
            return (EdgeType.Dotted, LineStyle);
        }

        if (LineStyle == EdgeStyle.Thick && HasEndPoint)
        {
            return (EdgeType.ThickArrow, LineStyle);
        }

        if (LineStyle == EdgeStyle.Thick)
        {
            return (EdgeType.Thick, LineStyle);
        }

        if (!HasEndPoint && !HasEndCircle && !HasEndCross)
        {
            return (EdgeType.Open, LineStyle);
        }

        return (EdgeType.Arrow, LineStyle);
    }
}
