#pragma warning disable IDE0007

namespace MermaidSharp.Diagrams.EntityRelationship;

public class ERRenderer(ILayoutEngine? layoutEngine = null) :
    IDiagramRenderer<ERModel>
{
    readonly ILayoutEngine layoutEngine = layoutEngine ?? new DagreLayoutEngine();

    // ER reference fixtures in this repository treat entities like Mermaid default theme:
    // - header band: 30px
    // - attribute rows: 20px cadence, no per-row rectangles
    // - relationships: straight <line> segments with explicit cardinality primitives (no SVG <marker>).
    const double HeaderHeight = 30;
    const double AttributeRowHeight = 20;
    const double EntityPaddingBottom = 10;
    // Mermaid defaults (config.schema.yaml -> ErDiagramConfig)
    const double MinEntityWidth = 100;
    const double MinEntityHeight = 75;
    const double DiagramPadding = 20;
    const double EntityPadding = 15;
    const double KeyIndicatorX = 10;
    const double AttributeTextX = 40;

    const double RelationshipLabelHeight = 20;
    const double RelationshipFontSize = 12;
    const double ErdTextOpticalBiasEm = 0.09;
    const double ErdTextExtraVerticalOffsetPx = 4;

    const double RelationshipStrokeWidth = 1;
    const string RelationshipStroke = "#333333";
    const string EntityFill = "#ECECFF";
    const string EntityStroke = "#9370DB";
    const string EntityHeaderFill = "#ECECFF";
    const string EntityHeaderTextFill = "#333333";
    const string RelationshipLabelBackground = "transparent";
    const double EntityBorderStrokeWidth = 1;

    public SvgDocument Render(ERModel model, RenderOptions options)
    {
        // Calculate entity sizes and convert to graph model
        var graphModel = ConvertToGraphModel(model, options);

        // Run layout
        var layoutOptions = new LayoutOptions
        {
            Direction = model.Direction,
            NodeSeparation = 100,
            RankSeparation = 100
        };
        var layoutResult = layoutEngine.Layout(graphModel, layoutOptions);

        // Copy positions back to entities
        CopyPositionsToModel(model, graphModel);

        // Build SVG
        var builder = new SvgBuilder()
            .Size(layoutResult.Width, layoutResult.Height)
            .Padding(options.Padding)
            .ViewBox($"0 0 {Fmt(layoutResult.Width + options.Padding * 2)} {Fmt(layoutResult.Height + options.Padding * 2)}")
            .AddFontAwesomeImport();

        // Align ERD styling with the same default Mermaid CSS used by flowcharts.
        builder.AddStyles(MermaidStyles.FlowchartStyles);

        var EdgeCountsByEntity = ComputeEdgeConnectionCounts(model);

        // Render relationships first (behind entities)
        foreach (var relationship in model.Relationships)
        {
            RenderRelationship(builder, relationship, model, graphModel, options, EdgeCountsByEntity);
        }

        // Render entities
        foreach (var entity in model.Entities)
        {
            RenderEntity(builder, entity, options);
        }

        return builder.Build();
    }

    static Dictionary<string, List<Relationship>> BuildIncomingByEntity(ERModel model)
    {
        var incoming = new Dictionary<string, List<Relationship>>(StringComparer.Ordinal);
        foreach (var relationship in model.Relationships)
        {
            if (!incoming.TryGetValue(relationship.ToEntity, out var list))
            {
                list = new List<Relationship>();
                incoming.Add(relationship.ToEntity, list);
            }
            list.Add(relationship);
        }

        return incoming;
    }

    static Dictionary<string, List<Relationship>> BuildOutgoingByEntity(ERModel model)
    {
        var outgoing = new Dictionary<string, List<Relationship>>(StringComparer.Ordinal);
        foreach (var relationship in model.Relationships)
        {
            if (!outgoing.TryGetValue(relationship.FromEntity, out var list))
            {
                list = new List<Relationship>();
                outgoing.Add(relationship.FromEntity, list);
            }
            list.Add(relationship);
        }

        // Mermaid/Dagre effectively assigns ports in a deterministic order.
        // For the ER cardinality matrix, the visual ordering matches To-cardinality grouping more than target X.
        foreach (var kvp in outgoing)
        {
            kvp.Value.Sort((a, b) =>
            {
#pragma warning disable IDE0007
                int aKey = CardinalitySlotSortKey(a.ToCardinality);
                int bKey = CardinalitySlotSortKey(b.ToCardinality);
                int keyCmp = aKey.CompareTo(bKey);
#pragma warning restore IDE0007
                if (keyCmp != 0)
                {
                    return keyCmp;
                }

                // Tie-breaker: preserve stable parse order by returning 0.
                return 0;
            });
        }

        return outgoing;
    }

    static int CardinalitySlotSortKey(Cardinality Cardinality)
    {
        // Desired order for matrix-style fan-out/fan-in slots (top to bottom):
        // ZERO_OR_MORE, ONE_OR_MORE, EXACTLY_ONE, ZERO_OR_ONE
        switch (Cardinality)
        {
            case Cardinality.ZeroOrMore:
                return 0;
            case Cardinality.OneOrMore:
                return 1;
            case Cardinality.ExactlyOne:
                return 2;
            case Cardinality.ZeroOrOne:
                return 3;
            default:
                return 99;
        }
    }

    static GraphDiagramBase ConvertToGraphModel(ERModel model, RenderOptions options)
    {
        var graph = new ERLayoutGraph { Direction = model.Direction };

        foreach (var entity in model.Entities)
        {
            var Size = CalculateEntitySize(entity, options);
            entity.Width = Size.width;
            entity.Height = Size.height;

            var node = new Node
            {
                Id = entity.Name,
                Label = entity.Name,
                Width = Size.width,
                Height = Size.height
            };
            graph.AddNode(node);
        }

        foreach (var rel in model.Relationships)
        {
            var edge = new Edge
            {
                SourceId = rel.FromEntity,
                TargetId = rel.ToEntity,
                Label = rel.Label
            };
            graph.AddEdge(edge);
        }

        return graph;
    }

    static (double width, double height) CalculateEntitySize(Entity entity, RenderOptions options)
    {
        const double FIXED_ENTITY_FONT_SIZE = 14.0;
        const double ENTITY_WIDTH_PADDING_PX = EntityPadding * 2.0;

        var HeaderTextWidth = MeasureText(entity.Name, FIXED_ENTITY_FONT_SIZE, bold: false);
        var MaxRowTextWidth = 0.0;

        for (var i = 0; i < entity.Attributes.Count; i++)
        {
            var Row = FormatAttribute(entity.Attributes[i]);
            MaxRowTextWidth = Math.Max(MaxRowTextWidth, MeasureText(Row, FIXED_ENTITY_FONT_SIZE, bold: false));
        }

        var Width = Math.Max(MinEntityWidth, Math.Round(Math.Max(HeaderTextWidth, MaxRowTextWidth) + ENTITY_WIDTH_PADDING_PX, 2));
        var Height = HeaderHeight;
        if (entity.Attributes.Count > 0)
        {
            Height = HeaderHeight + (entity.Attributes.Count * AttributeRowHeight) + EntityPaddingBottom;
            Height = Math.Max(MinEntityHeight, Height);
            return (Width, Height);
        }

        // Mermaid erBox.ts uses labelPaddingX = diagramPadding and labelPaddingY = diagramPadding * 1.5
        // for entities with no attributes.
        var LabelPaddingX = DiagramPadding;
        var LabelPaddingY = DiagramPadding * 1.5;
        var ApproxTextHeight = FIXED_ENTITY_FONT_SIZE * 1.2;

        Width = Math.Max(Width, Math.Round(HeaderTextWidth + (LabelPaddingX * 2.0), 2));
        Height = Math.Max(MinEntityHeight, Math.Round(ApproxTextHeight + (LabelPaddingY * 2.0), 2));
        return (Width, Height);
    }

    static void CopyPositionsToModel(ERModel model, GraphDiagramBase graph)
    {
        foreach (var entity in model.Entities)
        {
            var node = graph.GetNode(entity.Name);
            if (node != null)
            {
                entity.Position = node.Position;
            }
        }
    }

    static void RenderEntity(SvgBuilder builder, Entity entity, RenderOptions options)
    {
        var X = entity.Position.X - entity.Width / 2.0;
        var Y = entity.Position.Y - entity.Height / 2.0;

        // Mermaid renders entities without attributes as a single box with centred label.
        if (entity.Attributes.Count == 0)
        {
            builder.AddRect(X, Y, entity.Width, entity.Height, rx: 0, fill: EntityFill, stroke: EntityStroke, strokeWidth: EntityBorderStrokeWidth);
            builder.AddBoxCenteredText(
                X,
                Y,
                entity.Width,
                entity.Height,
                entity.Name,
                fontSize: "14px",
                fontFamily: options.FontFamily,
                fontWeight: "normal",
                fill: EntityHeaderTextFill,
                opticalVerticalBiasEm: ErdTextOpticalBiasEm,
                extraVerticalOffsetPx: ErdTextExtraVerticalOffsetPx);
            return;
        }

        builder.AddRect(X, Y, entity.Width, entity.Height, rx: 0, fill: EntityFill, stroke: EntityStroke, strokeWidth: EntityBorderStrokeWidth);
        builder.AddRect(X, Y, entity.Width, HeaderHeight, rx: 0, fill: EntityHeaderFill, stroke: EntityStroke, strokeWidth: EntityBorderStrokeWidth);

        builder.AddBoxCenteredText(
            X,
            Y,
            entity.Width,
            HeaderHeight,
            entity.Name,
            fontSize: "14px",
            fontFamily: options.FontFamily,
            fontWeight: "normal",
            fill: EntityHeaderTextFill,
            opticalVerticalBiasEm: ErdTextOpticalBiasEm,
            extraVerticalOffsetPx: ErdTextExtraVerticalOffsetPx);

        builder.AddLine(X, Y + HeaderHeight, X + entity.Width, Y + HeaderHeight, stroke: EntityStroke, strokeWidth: EntityBorderStrokeWidth);

        for (var i = 0; i < entity.Attributes.Count; i++)
        {
            var Attr = entity.Attributes[i];
            var RowTopY = Y + HeaderHeight + (i * AttributeRowHeight);
            var FontSizePx = 14.0;
            var OpticalDown = Math.Max(2.0, FontSizePx * ErdTextOpticalBiasEm) + ErdTextExtraVerticalOffsetPx;
            var TextY = RowTopY + (AttributeRowHeight / 2.0) + OpticalDown;
            var Key = GetKeyIndicator(Attr.KeyType);
            if (!string.IsNullOrEmpty(Key))
            {
                var KeyFontSizePx = 12.0;
                var KeyOpticalDown = Math.Max(2.0, KeyFontSizePx * ErdTextOpticalBiasEm) + ErdTextExtraVerticalOffsetPx;
                var KeyY = RowTopY + (AttributeRowHeight / 2.0) + KeyOpticalDown;
                builder.AddText(X + KeyIndicatorX, KeyY, Key,
                    anchor: "start",
                    baseline: "middle",
                    fontSize: "12px",
                    fontFamily: options.FontFamily,
                    fill: "#666");
            }

            builder.AddText(X + AttributeTextX, TextY, FormatAttribute(Attr),
                anchor: "start",
                baseline: "middle",
                fontSize: "14px",
                fontFamily: options.FontFamily,
                fill: "#333");
        }
    }

    static void RenderRelationship(
        SvgBuilder builder,
        Relationship rel,
        ERModel model,
        GraphDiagramBase graphModel,
        RenderOptions options,
        Dictionary<string, EdgeConnectionCounts> EdgeCountsByEntity)
    {
        var FromEntity = model.Entities.Find(_ => _.Name == rel.FromEntity);
        var ToEntity = model.Entities.Find(_ => _.Name == rel.ToEntity);

        if (FromEntity == null || ToEntity == null)
        {
            return;
        }

        var Edge = graphModel.Edges.Find(_ => _.SourceId == rel.FromEntity && _.TargetId == rel.ToEntity);

        Position StartPoint;
        Position EndPoint;

        // Source-of-truth rule: relationship anchors come from deterministic layout-time anchor allocation.
        // Therefore, use the routed edge points directly rather than re-computing attach points here.
        if (Edge != null && Edge.Points.Count >= 2)
        {
            StartPoint = Edge.Points[0];
            EndPoint = Edge.Points[^1];
        }
        else
        {
            StartPoint = IntersectEntityBoundary(FromEntity, Toward: ToEntity.Position);
            EndPoint = IntersectEntityBoundary(ToEntity, Toward: FromEntity.Position);
        }

        var StartX = StartPoint.X;
        var StartY = StartPoint.Y;
        var EndX = EndPoint.X;
        var EndY = EndPoint.Y;

        var DashArray = rel.Identifying ? null : "5,5";

        // Source-of-truth: for the gold-standard ER layout, curved relationships should be cubic Beziers with explicit
        // pull points so endpoint tangents are stable (the curve must not decide anchors).
        //
        // Keep Dagre’s endpoint anchors, but for simple two-endpoint relationships prefer an explicit cubic Bezier that:
        // - leaves a bottom anchor straight down
        // - approaches an upper-left / upper-right target shoulder with the correct final tangent
        //
        // For more complex multi-point routes (dummy nodes / obstacle avoidance), keep curveBasis for now.
        var Points = new List<Position>();
        if (Edge != null && Edge.Points.Count >= 2)
        {
            Points.AddRange(Edge.Points);
        }
        else
        {
            Points.Add(StartPoint);
            Points.Add(EndPoint);
        }

        ErCurveBasisPath Path;
        if (Points.Count == 2)
        {
            Path = BuildErExplicitCubicOrLinePath(StartPoint, EndPoint);
        }
        else
        {
            Path = BuildErCurveBasisPath(Points);
        }

        builder.AddPath(Path.PathData, fill: "none", stroke: RelationshipStroke, strokeWidth: RelationshipStrokeWidth, strokeDasharray: DashArray);

        // Pin embellishments onto the curve by sampling points ON the curve at Mermaid's offsets.
        DrawCardinalityOnCurveBasis(builder, rel.FromCardinality, isStart: true, EndPoint: StartPoint, EndEntity: FromEntity, BasisPath: Path);
        DrawCardinalityOnCurveBasis(builder, rel.ToCardinality, isStart: false, EndPoint: EndPoint, EndEntity: ToEntity, BasisPath: Path);

        if (!string.IsNullOrEmpty(rel.Label))
        {
#pragma warning disable IDE0007
            Position LabelPoint = ComputePathLabelPoint(Path, Points);
#pragma warning restore IDE0007
            var MidX = LabelPoint.X;
            var MidY = LabelPoint.Y;
            var LabelWidth = Math.Round(Math.Max(20.0, MeasureText(rel.Label, RelationshipFontSize, bold: false) + 4.96), 2);

            builder.AddRect(MidX - (LabelWidth / 2.0), MidY - (RelationshipLabelHeight / 2.0), LabelWidth, RelationshipLabelHeight, fill: RelationshipLabelBackground, stroke: "none");
            var OpticalDown = Math.Max(2.0, RelationshipFontSize * ErdTextOpticalBiasEm) + ErdTextExtraVerticalOffsetPx;
            builder.AddText(MidX, MidY + OpticalDown, rel.Label,
                anchor: "middle",
                baseline: "middle",
                fontSize: $"{RelationshipFontSize}px",
                fontFamily: options.FontFamily,
                fill: "#333");
        }
    }

    static Position ComputePathLabelPoint(ErCurveBasisPath Path, IReadOnlyList<Position> FallbackPolyline)
    {
#pragma warning disable IDE0007
        if (Path.Segments.Count == 1)
        {
            CubicBezierSegment Seg = Path.Segments[0];
            return BezierPoint(Seg, 0.5);
        }

        return ComputePolylineMidpoint(FallbackPolyline);
#pragma warning restore IDE0007
    }

    static ErCurveBasisPath BuildErExplicitCubicOrLinePath(Position P0, Position P3)
    {
#pragma warning disable IDE0007
        var Result = new ErCurveBasisPath();

        double Dx = P3.X - P0.X;
        double Dy = P3.Y - P0.Y;

        // Straight line for near-vertical relationships (e.g., CUSTOMER -> ORDER).
        if (Math.Abs(Dx) < 0.75)
        {
            Result.PathData = $"M{Fmt(P0.X)},{Fmt(P0.Y)} L{Fmt(P3.X)},{Fmt(P3.Y)}";
            return Result;
        }

        double AbsDx = Math.Abs(Dx);
        double AbsDy = Math.Abs(Dy);

        double VerticalPull = Clamp(0.30 * AbsDy, 24.0, 70.0);
        double HorizontalPull = Clamp(0.35 * AbsDx, 24.0, 80.0);
        double SmallVerticalPull = Clamp(0.15 * AbsDy, 12.0, 40.0);

        // Choose approach side based on whether the source is left or right of the target.
        // Source left of target => approach from left => C2.X < P3.X.
        // Source right of target => approach from right => C2.X > P3.X.
        double SignX = P0.X < P3.X ? -1.0 : 1.0;

        Position C1 = new Position(P0.X, P0.Y + VerticalPull);
        Position C2 = new Position(P3.X + (SignX * HorizontalPull), P3.Y - SmallVerticalPull);

        Result.PathData =
            $"M{Fmt(P0.X)},{Fmt(P0.Y)} C{Fmt(C1.X)},{Fmt(C1.Y)} {Fmt(C2.X)},{Fmt(C2.Y)} {Fmt(P3.X)},{Fmt(P3.Y)}";

        Result.Segments.Add(new CubicBezierSegment
        {
            P0 = P0,
            P1 = C1,
            P2 = C2,
            P3 = P3
        });

        return Result;
#pragma warning restore IDE0007
    }

    static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }
        if (value > max)
        {
            return max;
        }
        return value;
    }

    static Position ComputePolylineMidpoint(IReadOnlyList<Position> Points)
    {
        if (Points.Count == 0)
        {
            return Position.Zero;
        }
        if (Points.Count == 1)
        {
            return Points[0];
        }

        var MidIndex = Points.Count / 2;
        if (Points.Count % 2 == 0)
        {
            var P1 = Points[MidIndex - 1];
            var P2 = Points[MidIndex];
            return new((P1.X + P2.X) / 2.0, (P1.Y + P2.Y) / 2.0);
        }

        return Points[MidIndex];
    }

    static Position IntersectEntityBoundary(Entity Entity, Position Toward)
    {
        var HalfWidth = Entity.Width / 2.0;
        var HalfHeight = Entity.Height / 2.0;

        var Dx = Toward.X - Entity.Position.X;
        var Dy = Toward.Y - Entity.Position.Y;

        // If degenerate, fall back to top.
        if (Math.Abs(Dx) < 0.001 && Math.Abs(Dy) < 0.001)
        {
            return new(Entity.Position.X, Entity.Position.Y - HalfHeight);
        }

        // Intersect ray from centre with the rectangle boundary.
        var AbsDx = Math.Abs(Dx);
        var AbsDy = Math.Abs(Dy);

        // Scale the direction so that either x hits +/-HalfWidth or y hits +/-HalfHeight first.
        var Tx = AbsDx < 0.001 ? double.PositiveInfinity : HalfWidth / AbsDx;
        var Ty = AbsDy < 0.001 ? double.PositiveInfinity : HalfHeight / AbsDy;

        var T = Math.Min(Tx, Ty);

        return new(
            Entity.Position.X + (Dx * T),
            Entity.Position.Y + (Dy * T));
    }

    sealed class CubicBezierSegment
    {
        public Position P0;
        public Position P1;
        public Position P2;
        public Position P3;
    }

    sealed class ErCurveBasisPath
    {
        public string PathData = string.Empty;
        public List<CubicBezierSegment> Segments { get; } = new List<CubicBezierSegment>();
    }

    // Exact d3.curveBasis conversion (see https://raw.githubusercontent.com/d3/d3-shape/main/src/curve/basis.js)
    static ErCurveBasisPath BuildErCurveBasisPath(IReadOnlyList<Position> Points)
    {
        var Result = new ErCurveBasisPath();
        if (Points.Count < 2)
        {
            return Result;
        }

        if (Points.Count == 2)
        {
            Result.PathData = $"M{Fmt(Points[0].X)},{Fmt(Points[0].Y)} L{Fmt(Points[1].X)},{Fmt(Points[1].Y)}";
            return Result;
        }

        var Sb = new StringBuilder();

        // d3 keeps a 2-point state; we emulate the output path by constructing equivalent cubic segments.
        var X0 = Points[0].X;
        var Y0 = Points[0].Y;
        var X1 = Points[0].X;
        var Y1 = Points[0].Y;

        // point 0: moveTo
        Sb.Append('M').Append(Fmt(Points[0].X)).Append(',').Append(Fmt(Points[0].Y));

        // point 1: only updates state
        X1 = Points[1].X;
        Y1 = Points[1].Y;

        // point 2: lineTo((5*x0 + x1)/6, (5*y0 + y1)/6)
        if (Points.Count >= 3)
        {
            var P2 = Points[2];
            var Lx = ((5.0 * X0) + X1) / 6.0;
            var Ly = ((5.0 * Y0) + Y1) / 6.0;
            Sb.Append('L').Append(Fmt(Lx)).Append(',').Append(Fmt(Ly));

            // Now run basis.point for point2 and subsequent points.
            for (var i = 2; i < Points.Count; i++)
            {
                var Px = Points[i].X;
                var Py = Points[i].Y;

                // basis.js point() emits:
                // bezierCurveTo(
                //  (2*x0 + x1)/3,
                //  (2*y0 + y1)/3,
                //  (x0 + 2*x1)/3,
                //  (y0 + 2*y1)/3,
                //  (x0 + 4*x1 + x)/6,
                //  (y0 + 4*y1 + y)/6 )
                var C1x = ((2.0 * X0) + X1) / 3.0;
                var C1y = ((2.0 * Y0) + Y1) / 3.0;
                var C2x = (X0 + (2.0 * X1)) / 3.0;
                var C2y = (Y0 + (2.0 * Y1)) / 3.0;
                var Ex = (X0 + (4.0 * X1) + Px) / 6.0;
                var Ey = (Y0 + (4.0 * Y1) + Py) / 6.0;

                // Segment start is the current pen position. For our bookkeeping, use previous Ex/Ey as P0.
                // We can approximate by reading the last emitted point (start with Lx/Ly).
                var P0 = i == 2 ? new Position(Lx, Ly) : Result.Segments[^1].P3;
                var Seg = new CubicBezierSegment
                {
                    P0 = P0,
                    P1 = new Position(C1x, C1y),
                    P2 = new Position(C2x, C2y),
                    P3 = new Position(Ex, Ey)
                };
                Result.Segments.Add(Seg);

                Sb.Append('C')
                    .Append(Fmt(C1x)).Append(',').Append(Fmt(C1y)).Append(' ')
                    .Append(Fmt(C2x)).Append(',').Append(Fmt(C2y)).Append(' ')
                    .Append(Fmt(Ex)).Append(',').Append(Fmt(Ey));

                // advance state
                X0 = X1;
                Y0 = Y1;
                X1 = Px;
                Y1 = Py;
            }

            // lineEnd behaviour:
            // case 3: point(this, x1, y1); case 2: lineTo(x1,y1)
            // We emulate by adding one extra basis "point" at the last point, then a final lineTo to the last point.
            var C1xe = ((2.0 * X0) + X1) / 3.0;
            var C1ye = ((2.0 * Y0) + Y1) / 3.0;
            var C2xe = (X0 + (2.0 * X1)) / 3.0;
            var C2ye = (Y0 + (2.0 * Y1)) / 3.0;
            var Exe = (X0 + (4.0 * X1) + X1) / 6.0;
            var Eye = (Y0 + (4.0 * Y1) + Y1) / 6.0;

            var SegEnd = new CubicBezierSegment
            {
                P0 = Result.Segments.Count > 0 ? Result.Segments[^1].P3 : new Position(Lx, Ly),
                P1 = new Position(C1xe, C1ye),
                P2 = new Position(C2xe, C2ye),
                P3 = new Position(Exe, Eye)
            };
            Result.Segments.Add(SegEnd);

            Sb.Append('C')
                .Append(Fmt(C1xe)).Append(',').Append(Fmt(C1ye)).Append(' ')
                .Append(Fmt(C2xe)).Append(',').Append(Fmt(C2ye)).Append(' ')
                .Append(Fmt(Exe)).Append(',').Append(Fmt(Eye));

            Sb.Append('L').Append(Fmt(X1)).Append(',').Append(Fmt(Y1));
        }

        Result.PathData = Sb.ToString();
        return Result;
    }

    static void DrawCardinalityOnCurveBasis(SvgBuilder Builder, Cardinality Cardinality, bool isStart, Position EndPoint, Entity EndEntity, ErCurveBasisPath BasisPath)
    {
        // Mermaid offsets (ERMarkers *_END) along the path.
        const double BAR_HALF_LEN = 4.0;
        const double ONLY_ONE_BAR_OFFSET_NEAR = 9.0;
        const double ONLY_ONE_BAR_OFFSET_FAR = 15.0;
        const double ZERO_OR_ONE_CIRCLE_OFFSET = 21.0;
        const double ZERO_OR_ONE_BAR_OFFSET = 9.0;
        const double ONE_OR_MORE_BAR_OFFSET = 24.0;
        const double CROWFOOT_VERTEX_OFFSET = 18.0;
        const double ZERO_OR_MORE_CIRCLE_OFFSET = 30.0;
        const double CIRCLE_RADIUS = 4.0;
        const double CROWFOOT_SPREAD = CIRCLE_RADIUS * 2.0;

        if (BasisPath.Segments.Count == 0)
        {
            return;
        }

        if (Cardinality == Cardinality.ExactlyOne)
        {
            var Near = PointAtDistanceOnCurve(BasisPath, isStart, ONLY_ONE_BAR_OFFSET_NEAR, out var Tx1, out var Ty1);
            AddBar(Builder, Near.X, Near.Y, Tx1, Ty1, -Ty1, Tx1, 0.0, BAR_HALF_LEN);
            var Far = PointAtDistanceOnCurve(BasisPath, isStart, ONLY_ONE_BAR_OFFSET_FAR, out var Tx2, out var Ty2);
            AddBar(Builder, Far.X, Far.Y, Tx2, Ty2, -Ty2, Tx2, 0.0, BAR_HALF_LEN);
            return;
        }

        if (Cardinality == Cardinality.ZeroOrOne)
        {
            var Circle = PointAtDistanceOnCurve(BasisPath, isStart, ZERO_OR_ONE_CIRCLE_OFFSET, out _, out _);
            Builder.AddCircle(Circle.X, Circle.Y, CIRCLE_RADIUS, fill: "#fff", stroke: RelationshipStroke, strokeWidth: 1);
            var Bar = PointAtDistanceOnCurve(BasisPath, isStart, ZERO_OR_ONE_BAR_OFFSET, out var Tx, out var Ty);
            AddBar(Builder, Bar.X, Bar.Y, Tx, Ty, -Ty, Tx, 0.0, BAR_HALF_LEN);
            return;
        }

        if (Cardinality == Cardinality.OneOrMore)
        {
            var Bar = PointAtDistanceOnCurve(BasisPath, isStart, ONE_OR_MORE_BAR_OFFSET, out var Tx, out var Ty);
            AddBar(Builder, Bar.X, Bar.Y, Tx, Ty, -Ty, Tx, 0.0, BAR_HALF_LEN);
            var Vertex = PointAtDistanceOnCurve(BasisPath, isStart, CROWFOOT_VERTEX_OFFSET, out var Txc, out var Tyc);
            DrawCrowfootAtEndpoint(Builder, EndPoint, Vertex, Txc, Tyc, EndEntity, CROWFOOT_SPREAD);
            return;
        }

        if (Cardinality == Cardinality.ZeroOrMore)
        {
            var Circle = PointAtDistanceOnCurve(BasisPath, isStart, ZERO_OR_MORE_CIRCLE_OFFSET, out _, out _);
            Builder.AddCircle(Circle.X, Circle.Y, CIRCLE_RADIUS, fill: "#fff", stroke: RelationshipStroke, strokeWidth: 1);
            var Vertex = PointAtDistanceOnCurve(BasisPath, isStart, CROWFOOT_VERTEX_OFFSET, out var Txc, out var Tyc);
            DrawCrowfootAtEndpoint(Builder, EndPoint, Vertex, Txc, Tyc, EndEntity, CROWFOOT_SPREAD);
        }
    }

    static Position PointAtDistanceOnCurve(ErCurveBasisPath Path, bool isStart, double distancePx, out double Tx, out double Ty)
    {
        const int SAMPLES = 20;

        Tx = 1.0;
        Ty = 0.0;

        if (Path.Segments.Count == 0)
        {
            return Position.Zero;
        }

        var Remaining = distancePx;
        if (isStart)
        {
            for (var si = 0; si < Path.Segments.Count; si++)
            {
                var Seg = Path.Segments[si];
                var Prev = Seg.P0;
                for (var i = 1; i <= SAMPLES; i++)
                {
                    var t = i / (double)SAMPLES;
                    var Cur = BezierPoint(Seg, t);
                    var Dx = Cur.X - Prev.X;
                    var Dy = Cur.Y - Prev.Y;
                    var Len = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                    if (Len >= Remaining)
                    {
                        var ratio = Remaining / Math.Max(0.0001, Len);
                        var Px = Prev.X + (Dx * ratio);
                        var Py = Prev.Y + (Dy * ratio);
                        var Der = BezierDerivative(Seg, t);
                        Normalise(Der.X, Der.Y, out Tx, out Ty);
                        return new Position(Px, Py);
                    }
                    Remaining -= Len;
                    Prev = Cur;
                }
            }

            var Last = Path.Segments[^1];
            var EndDer = BezierDerivative(Last, 1.0);
            Normalise(EndDer.X, EndDer.Y, out Tx, out Ty);
            return Last.P3;
        }

        for (var si = Path.Segments.Count - 1; si >= 0; si--)
        {
            var Seg = Path.Segments[si];
            var Prev = Seg.P3;
            for (var i = 1; i <= SAMPLES; i++)
            {
                var t = 1.0 - (i / (double)SAMPLES);
                var Cur = BezierPoint(Seg, t);
                var Dx = Cur.X - Prev.X;
                var Dy = Cur.Y - Prev.Y;
                var Len = Math.Sqrt((Dx * Dx) + (Dy * Dy));
                if (Len >= Remaining)
                {
                    var ratio = Remaining / Math.Max(0.0001, Len);
                    var Px = Prev.X + (Dx * ratio);
                    var Py = Prev.Y + (Dy * ratio);
                    var Der = BezierDerivative(Seg, t);
                    Normalise(-Der.X, -Der.Y, out Tx, out Ty);
                    return new Position(Px, Py);
                }
                Remaining -= Len;
                Prev = Cur;
            }
        }

        var First = Path.Segments[0];
        var StartDer = BezierDerivative(First, 0.0);
        Normalise(StartDer.X, StartDer.Y, out Tx, out Ty);
        return First.P0;
    }

    static Position BezierPoint(CubicBezierSegment Seg, double t)
    {
        var mt = 1.0 - t;
        var mt2 = mt * mt;
        var t2 = t * t;

        var x =
            (Seg.P0.X * mt2 * mt) +
            (3.0 * Seg.P1.X * mt2 * t) +
            (3.0 * Seg.P2.X * mt * t2) +
            (Seg.P3.X * t2 * t);

        var y =
            (Seg.P0.Y * mt2 * mt) +
            (3.0 * Seg.P1.Y * mt2 * t) +
            (3.0 * Seg.P2.Y * mt * t2) +
            (Seg.P3.Y * t2 * t);

        return new Position(x, y);
    }

    static Position BezierDerivative(CubicBezierSegment Seg, double t)
    {
        var mt = 1.0 - t;
        var x =
            (3.0 * mt * mt * (Seg.P1.X - Seg.P0.X)) +
            (6.0 * mt * t * (Seg.P2.X - Seg.P1.X)) +
            (3.0 * t * t * (Seg.P3.X - Seg.P2.X));

        var y =
            (3.0 * mt * mt * (Seg.P1.Y - Seg.P0.Y)) +
            (6.0 * mt * t * (Seg.P2.Y - Seg.P1.Y)) +
            (3.0 * t * t * (Seg.P3.Y - Seg.P2.Y));

        return new Position(x, y);
    }

    static void Normalise(double x, double y, out double nx, out double ny)
    {
        var len = Math.Sqrt((x * x) + (y * y));
        if (len < 0.001)
        {
            nx = 1.0;
            ny = 0.0;
            return;
        }
        nx = x / len;
        ny = y / len;
    }

    static void DrawCrowfootAtEndpoint(SvgBuilder Builder, Position EndPoint, Position VertexPoint, double Tx, double Ty, Entity EndEntity, double Spread)
    {
        // Toe tips should meet the entity boundary point (EndPoint) offset along the entity edge.
        // Determine entity edge tangent (horizontal for top/bottom, vertical for left/right).
        var HalfWidth = EndEntity.Width / 2.0;
        var HalfHeight = EndEntity.Height / 2.0;
        var LeftX = EndEntity.Position.X - HalfWidth;
        var RightX = EndEntity.Position.X + HalfWidth;
        var TopY = EndEntity.Position.Y - HalfHeight;
        var BottomY = EndEntity.Position.Y + HalfHeight;

        var IsTopOrBottom = Math.Abs(EndPoint.Y - TopY) < 0.01 || Math.Abs(EndPoint.Y - BottomY) < 0.01;
        var EdgeTangentX = IsTopOrBottom ? 1.0 : 0.0;
        var EdgeTangentY = IsTopOrBottom ? 0.0 : 1.0;

        var IsLeftCorner = Math.Abs(EndPoint.X - LeftX) < 0.01;
        var IsRightCorner = Math.Abs(EndPoint.X - RightX) < 0.01;
        var IsTopEdge = Math.Abs(EndPoint.Y - TopY) < 0.01;
        var IsBottomEdge = Math.Abs(EndPoint.Y - BottomY) < 0.01;
        var IsCornerOnTopOrBottom = (IsTopEdge || IsBottomEdge) && (IsLeftCorner || IsRightCorner);

        // For corner attachments, one toe lands on the top/bottom edge, the outer toe lands on the vertical edge.
        if (IsCornerOnTopOrBottom)
        {
            var VerticalSign = IsTopEdge ? 1.0 : -1.0;
            var HorizontalSign = IsLeftCorner ? 1.0 : -1.0;

            Builder.AddLine(VertexPoint.X, VertexPoint.Y, EndPoint.X + (HorizontalSign * Spread), EndPoint.Y, stroke: RelationshipStroke, strokeWidth: 1);
            Builder.AddLine(VertexPoint.X, VertexPoint.Y, EndPoint.X, EndPoint.Y + (VerticalSign * Spread), stroke: RelationshipStroke, strokeWidth: 1);
            return;
        }

        Builder.AddLine(VertexPoint.X, VertexPoint.Y, EndPoint.X + (EdgeTangentX * Spread), EndPoint.Y + (EdgeTangentY * Spread), stroke: RelationshipStroke, strokeWidth: 1);
        Builder.AddLine(VertexPoint.X, VertexPoint.Y, EndPoint.X - (EdgeTangentX * Spread), EndPoint.Y - (EdgeTangentY * Spread), stroke: RelationshipStroke, strokeWidth: 1);
    }


    enum AttachSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    sealed class EdgeConnectionCounts
    {
        public int TopCount;
        public int BottomCount;
        public int LeftCount;
        public int RightCount;
    }

    static EdgeConnectionCounts GetCounts(Dictionary<string, EdgeConnectionCounts> EdgeCountsByEntity, string EntityName)
    {
        if (EdgeCountsByEntity.TryGetValue(EntityName, out var Counts) && Counts != null)
        {
            return Counts;
        }
        return new EdgeConnectionCounts();
    }

    static Dictionary<string, EdgeConnectionCounts> ComputeEdgeConnectionCounts(ERModel model)
    {
        var EdgeCountsByEntity = new Dictionary<string, EdgeConnectionCounts>(StringComparer.Ordinal);

        foreach (var rel in model.Relationships)
        {
            var FromEntity = model.Entities.Find(_ => _.Name == rel.FromEntity);
            var ToEntity = model.Entities.Find(_ => _.Name == rel.ToEntity);

            if (FromEntity == null || ToEntity == null)
            {
                continue;
            }

            var FromSide = GetAttachSide(FromEntity, Toward: ToEntity.Position);
            var ToSide = GetAttachSide(ToEntity, Toward: FromEntity.Position);

            var FromCounts = GetOrCreateCounts(EdgeCountsByEntity, FromEntity.Name);
            IncrementSideCount(FromCounts, FromSide);

            var ToCounts = GetOrCreateCounts(EdgeCountsByEntity, ToEntity.Name);
            IncrementSideCount(ToCounts, ToSide);
        }

        return EdgeCountsByEntity;
    }

    static EdgeConnectionCounts GetOrCreateCounts(Dictionary<string, EdgeConnectionCounts> EdgeCountsByEntity, string EntityName)
    {
        if (!EdgeCountsByEntity.TryGetValue(EntityName, out var Counts) || Counts == null)
        {
            Counts = new EdgeConnectionCounts();
            EdgeCountsByEntity[EntityName] = Counts;
        }
        return Counts;
    }

    static void IncrementSideCount(EdgeConnectionCounts Counts, AttachSide Side)
    {
        if (Side == AttachSide.Top)
        {
            Counts.TopCount++;
            return;
        }
        if (Side == AttachSide.Bottom)
        {
            Counts.BottomCount++;
            return;
        }
        if (Side == AttachSide.Left)
        {
            Counts.LeftCount++;
            return;
        }
        Counts.RightCount++;
    }

    static int GetSideCount(EdgeConnectionCounts Counts, AttachSide Side)
    {
        if (Side == AttachSide.Top)
        {
            return Counts.TopCount;
        }
        if (Side == AttachSide.Bottom)
        {
            return Counts.BottomCount;
        }
        if (Side == AttachSide.Left)
        {
            return Counts.LeftCount;
        }
        return Counts.RightCount;
    }

    static AttachSide GetAttachSide(Entity Entity, Position Toward)
    {
        var Dx = Toward.X - Entity.Position.X;
        var Dy = Toward.Y - Entity.Position.Y;

        if (Math.Abs(Dy) >= Math.Abs(Dx))
        {
            if (Dy >= 0)
            {
                return AttachSide.Bottom;
            }
            return AttachSide.Top;
        }

        if (Dx >= 0)
        {
            return AttachSide.Right;
        }
        return AttachSide.Left;
    }

    static (double X, double Y) ComputeAttachPoint(Entity Entity, Position Toward, AttachSide Side, EdgeConnectionCounts Counts)
    {
        var HalfWidth = Entity.Width / 2.0;
        var HalfHeight = Entity.Height / 2.0;

        var Dx = Toward.X - Entity.Position.X;
        var Dy = Toward.Y - Entity.Position.Y;

        var SideCount = GetSideCount(Counts, Side);

        if (Side == AttachSide.Top || Side == AttachSide.Bottom)
        {
            // If this edge has only one relationship, it attaches in the middle (W/2).
            if (SideCount <= 1)
            {
                if (Side == AttachSide.Bottom)
                {
                    return (Entity.Position.X, Entity.Position.Y + HalfHeight);
                }
                return (Entity.Position.X, Entity.Position.Y - HalfHeight);
            }

            // Four-point model on the top/bottom edge (plus corners for extreme approaches):
            //
            // 1T = (X + W/5,   Y)
            // 2T = (X + 2W/5,  Y)
            // 3T = (X + 3W/5,  Y)
            // 4T = (X + 4W/5,  Y)
            //
            // When the incoming line approaches from far left/right (e.g. from above-left / above-right),
            // Mermaid effectively lands on the corner (0,0) or (W,0) in destination-local space.
            var LeftX = Entity.Position.X - HalfWidth;
            var Width = Entity.Width;
            var Normalised = (Dx + HalfWidth) / (2.0 * HalfWidth); // 0..1 across the box
            if (Normalised < 0.0)
            {
                Normalised = 0.0;
            }
            if (Normalised > 1.0)
            {
                Normalised = 1.0;
            }

            var SnapLeftCornerThreshold = 0.10;
            var SnapRightCornerThreshold = 0.90;

            var AttachX = LeftX + (Width * 0.5);
            if (Normalised <= SnapLeftCornerThreshold)
            {
                AttachX = LeftX;
            }
            else if (Normalised >= SnapRightCornerThreshold)
            {
                AttachX = LeftX + Width;
            }
            else
            {
                var P1 = 0.20;
                var P2 = 0.40;
                var P3 = 0.60;
                var P4 = 0.80;

                var Nearest = P1;
                var NearestDistance = Math.Abs(Normalised - P1);

                var Distance2 = Math.Abs(Normalised - P2);
                if (Distance2 < NearestDistance)
                {
                    Nearest = P2;
                    NearestDistance = Distance2;
                }

                var Distance3 = Math.Abs(Normalised - P3);
                if (Distance3 < NearestDistance)
                {
                    Nearest = P3;
                    NearestDistance = Distance3;
                }

                var Distance4 = Math.Abs(Normalised - P4);
                if (Distance4 < NearestDistance)
                {
                    Nearest = P4;
                }

                AttachX = LeftX + (Width * Nearest);
            }

            if (Dy >= 0)
            {
                return (AttachX, Entity.Position.Y + HalfHeight);
            }
            return (AttachX, Entity.Position.Y - HalfHeight);
        }

        if (Dx >= 0)
        {
            return (Entity.Position.X + HalfWidth, Entity.Position.Y);
        }
        return (Entity.Position.X - HalfWidth, Entity.Position.Y);
    }

    static void DrawCardinality(SvgBuilder Builder, Cardinality Cardinality, double EndX, double EndY, double OtherX, double OtherY, Entity EndEntity)
    {
#pragma warning disable IDE0007
        double Dx = OtherX - EndX;
        double Dy = OtherY - EndY;
        double Len = Math.Sqrt(Dx * Dx + Dy * Dy);
        if (Len < 0.001)
        {
            return;
        }

        double Ux = Dx / Len;
        double Uy = Dy / Len;
        double Vx = -Uy;
        double Vy = Ux;

        // Crow's-foot toe separation should be measured along the entity edge, not perpendicular to the relationship line.
        // For top/bottom edges the tangent is horizontal; for left/right edges the tangent is vertical.
        bool IsTopOrBottom = Math.Abs(Dy) >= Math.Abs(Dx);
        double EdgeTangentX = IsTopOrBottom ? 1.0 : 0.0;
        double EdgeTangentY = IsTopOrBottom ? 0.0 : 1.0;

        // Detect whether we landed on a corner (used to place the "outer" toe on the vertical edge).
        double HalfWidth = EndEntity.Width / 2.0;
        double HalfHeight = EndEntity.Height / 2.0;
        double LeftX = EndEntity.Position.X - HalfWidth;
        double RightX = EndEntity.Position.X + HalfWidth;
        double TopY = EndEntity.Position.Y - HalfHeight;
        double BottomY = EndEntity.Position.Y + HalfHeight;

        bool IsLeftCorner = Math.Abs(EndX - LeftX) < 0.01;
        bool IsRightCorner = Math.Abs(EndX - RightX) < 0.01;
        bool IsTopEdge = Math.Abs(EndY - TopY) < 0.01;
        bool IsBottomEdge = Math.Abs(EndY - BottomY) < 0.01;

        bool IsCornerOnTopOrBottom = (IsTopEdge || IsBottomEdge) && (IsLeftCorner || IsRightCorner);

        // Match Mermaid's ER marker geometry (mermaid/src/diagrams/er/erMarkers.js) for *_END markers.
        //
        // ONLY_ONE_END (refX=18): bars at x=9 and x=3 => offsets 9 and 15 from endpoint
        // ZERO_OR_ONE_END (refX=30): circle at x=9 => offset 21; bar at x=21 => offset 9
        // ONE_OR_MORE_END (refX=27): bar at x=3 => offset 24; crowfoot "join" at x=9 => offset 18
        // ZERO_OR_MORE_END (refX=39): circle at x=9 => offset 30; crowfoot "join" at x=21 => offset 18
        const double BAR_HALF_LEN = 4.0;

        const double ONLY_ONE_BAR_OFFSET_NEAR = 9.0;
        const double ONLY_ONE_BAR_OFFSET_FAR = 15.0;

        const double ZERO_OR_ONE_CIRCLE_OFFSET = 21.0;
        const double ZERO_OR_ONE_BAR_OFFSET = 9.0;

        const double ONE_OR_MORE_BAR_OFFSET = 24.0;
        const double CROWFOOT_VERTEX_OFFSET = 18.0;

        const double ZERO_OR_MORE_CIRCLE_OFFSET = 30.0;

        const double CIRCLE_RADIUS = 4.0;
        // Tips should meet the entity boundary (EndX/EndY).
        const double CROWFOOT_TIP_OFFSET = 0.0;
        // Toe tips should land +/- (circle diameter) from the relationship centreline (measured along the entity edge).
        const double CROWFOOT_SPREAD = CIRCLE_RADIUS * 2.0;

        if (Cardinality == Cardinality.ExactlyOne)
        {
            AddBar(Builder, EndX, EndY, Ux, Uy, Vx, Vy, ONLY_ONE_BAR_OFFSET_NEAR, BAR_HALF_LEN);
            AddBar(Builder, EndX, EndY, Ux, Uy, Vx, Vy, ONLY_ONE_BAR_OFFSET_FAR, BAR_HALF_LEN);
            return;
        }

        if (Cardinality == Cardinality.ZeroOrOne)
        {
            Builder.AddCircle(EndX + (Ux * ZERO_OR_ONE_CIRCLE_OFFSET), EndY + (Uy * ZERO_OR_ONE_CIRCLE_OFFSET), CIRCLE_RADIUS, fill: "#fff", stroke: RelationshipStroke, strokeWidth: 1);
            AddBar(Builder, EndX, EndY, Ux, Uy, Vx, Vy, ZERO_OR_ONE_BAR_OFFSET, BAR_HALF_LEN);
            return;
        }

        if (Cardinality == Cardinality.OneOrMore)
        {
            AddBar(Builder, EndX, EndY, Ux, Uy, Vx, Vy, ONE_OR_MORE_BAR_OFFSET, BAR_HALF_LEN);
            AddCrowfootToes(Builder, EndX, EndY, Ux, Uy, EdgeTangentX, EdgeTangentY, CROWFOOT_VERTEX_OFFSET, CROWFOOT_TIP_OFFSET, CROWFOOT_SPREAD, IsCornerOnTopOrBottom, IsLeftCorner, IsTopEdge);
            return;
        }

        if (Cardinality == Cardinality.ZeroOrMore)
        {
            Builder.AddCircle(EndX + (Ux * ZERO_OR_MORE_CIRCLE_OFFSET), EndY + (Uy * ZERO_OR_MORE_CIRCLE_OFFSET), CIRCLE_RADIUS, fill: "#fff", stroke: RelationshipStroke, strokeWidth: 1);
            AddCrowfootToes(Builder, EndX, EndY, Ux, Uy, EdgeTangentX, EdgeTangentY, CROWFOOT_VERTEX_OFFSET, CROWFOOT_TIP_OFFSET, CROWFOOT_SPREAD, IsCornerOnTopOrBottom, IsLeftCorner, IsTopEdge);
        }
#pragma warning restore IDE0007
    }

    static void AddBar(SvgBuilder Builder, double EndX, double EndY, double Ux, double Uy, double Vx, double Vy, double Offset, double HalfLen)
    {
        var Cx = EndX + (Ux * Offset);
        var Cy = EndY + (Uy * Offset);
        // Match fixture ordering (x1,y1 is the "positive V" endpoint).
        Builder.AddLine(Cx - (Vx * HalfLen), Cy - (Vy * HalfLen), Cx + (Vx * HalfLen), Cy + (Vy * HalfLen), stroke: RelationshipStroke, strokeWidth: 1);
    }

    static void AddCrowfootToes(SvgBuilder Builder, double EndX, double EndY, double Ux, double Uy, double EdgeTangentX, double EdgeTangentY, double VertexOffset, double TipOffset, double Spread, bool IsCornerOnTopOrBottom, bool IsLeftCorner, bool IsTopEdge)
    {
        // Mermaid/VS Code: the relationship line itself is the "middle toe".
        // Draw the two visible side-toes so Word doesn't anti-alias them away.
        // Geometry: tips land on the endpoint (entity boundary), vertex sits out on the edge.
        // This avoids "arrowhead" appearance and ensures the crow’s-foot meets the box.
        var VtxX = EndX + (Ux * VertexOffset);
        var VtxY = EndY + (Uy * VertexOffset);

        var TipBaseX = EndX + (Ux * TipOffset);
        var TipBaseY = EndY + (Uy * TipOffset);

        if (!IsCornerOnTopOrBottom)
        {
            Builder.AddLine(VtxX, VtxY, TipBaseX + (EdgeTangentX * Spread), TipBaseY + (EdgeTangentY * Spread), stroke: RelationshipStroke, strokeWidth: 1);
            Builder.AddLine(VtxX, VtxY, TipBaseX - (EdgeTangentX * Spread), TipBaseY - (EdgeTangentY * Spread), stroke: RelationshipStroke, strokeWidth: 1);
            return;
        }

        // Corner case: one toe lands along the top/bottom edge (x offset), the "outer" toe lands down/up the vertical edge (y offset)
        // with the same magnitude as the x offset (Spread).
        var VerticalSign = IsTopEdge ? 1.0 : -1.0;
        var HorizontalSign = IsLeftCorner ? 1.0 : -1.0;

        // Inner toe: along the top/bottom edge towards the box centre.
        Builder.AddLine(VtxX, VtxY, TipBaseX + (HorizontalSign * Spread), TipBaseY, stroke: RelationshipStroke, strokeWidth: 1);
        // Outer toe: down/up along the left/right edge.
        Builder.AddLine(VtxX, VtxY, TipBaseX, TipBaseY + (VerticalSign * Spread), stroke: RelationshipStroke, strokeWidth: 1);
    }

    static string FormatAttribute(EntityAttribute attr)
    {
        var result = $"{attr.Type} {attr.Name}";
        if (!string.IsNullOrEmpty(attr.Comment))
        {
            result += $" \"{attr.Comment}\"";
        }
        return result;
    }

    static string GetKeyIndicator(AttributeKeyType keyType) =>
        keyType switch
        {
            AttributeKeyType.PrimaryKey => "PK",
            AttributeKeyType.ForeignKey => "FK",
            AttributeKeyType.UniqueKey => "UK",
            _ => ""
        };

    static double MeasureText(string text, double fontSize, bool bold = false)
    {
        // Use a wider estimate so boxes match Mermaid’s comfortable padding in Word.
        var factor = bold ? 0.72 : 0.62;
        return text.Length * fontSize * factor;
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

// Graph model for layout (public so layout can apply ER-specific port rules).
public sealed class ERLayoutGraph : GraphDiagramBase;
