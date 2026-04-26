namespace MermaidSharp.Diagrams.Class;

public class ClassRenderer(ILayoutEngine? layoutEngine = null) :
    IDiagramRenderer<ClassModel>
{
    readonly ILayoutEngine _layoutEngine = layoutEngine ?? new DagreLayoutEngine();

    const double ClassScale = 2.0;

    const double ClassPadding = 10 * ClassScale;
    const double LineHeight = 20 * ClassScale;
    const double MinWidth = 100 * ClassScale;
    const double SeparatorHeight = 1 * ClassScale;

    /// <summary>Extra vertical inset below a compartment divider so row text clears the rule (helps Word SVG).</summary>
    const double CompartmentInnerPaddingTop = 8 * ClassScale;

    /// <summary>
    /// Additional vertical adjustment applied to method rows only to match Mermaid’s baseline placement in Word.
    /// Negative values move text up.
    /// </summary>
    const double MethodRowTextYOffsetPx = -6;

    public SvgDocument Render(ClassModel model, RenderOptions options)
    {
        var scaledOptions = new RenderOptions
        {
            Padding = options.Padding,
            Theme = options.Theme,
            FontFamily = options.FontFamily,
            FontSize = options.FontSize * ClassScale,
            ShowBoundingBox = options.ShowBoundingBox
        };

        // Convert to graph diagram for layout
        var graphModel = ConvertToGraphModel(model, scaledOptions);

        // Run layout
        var layoutOptions = new LayoutOptions
        {
            Direction = model.Direction,
            NodeSeparation = 60 * ClassScale,
            RankSeparation = 80 * ClassScale
        };
        var layoutResult = _layoutEngine.Layout(graphModel, layoutOptions);

        // Build SVG
        var builder = new SvgBuilder()
            .Size(layoutResult.Width, layoutResult.Height)
            .Padding(options.Padding)
            .AddArrowMarker()
            .AddArrowMarker("arrowhead-open", "#333");

        // Add relationship markers
        AddRelationshipMarkers(builder);

        // Render edges first (behind nodes)
        foreach (var relationship in model.Relationships)
        {
            var fromNode = graphModel.GetNode(relationship.FromId);
            var toNode = graphModel.GetNode(relationship.ToId);
            if (fromNode != null && toNode != null)
            {
                RenderRelationship(builder, relationship, fromNode, toNode, options);
            }
        }

        // Render class boxes
        foreach (var classDef in model.Classes)
        {
            var node = graphModel.GetNode(classDef.Id);
            if (node != null)
            {
                RenderClassBox(builder, classDef, node, scaledOptions);
            }
        }

        return builder.Build();
    }

    static GraphDiagramBase ConvertToGraphModel(ClassModel model, RenderOptions options)
    {
        var graph = new FlowchartModel {Direction = model.Direction};

        // Create nodes for each class
        foreach (var classDef in model.Classes)
        {
            var (width, height) = CalculateClassSize(classDef, options);
            var node = new Node
            {
                Id = classDef.Id,
                Label = classDef.Name,
                Width = width,
                Height = height
            };
            graph.AddNode(node);
        }

        // Create edges for each relationship
        foreach (var rel in model.Relationships)
        {
            var edge = new Edge
            {
                SourceId = rel.FromId,
                TargetId = rel.ToId,
                Label = rel.Label,
                Type = EdgeType.Arrow
            };
            graph.AddEdge(edge);
        }

        return graph;
    }

    static (double width, double height) CalculateClassSize(ClassDefinition classDef, RenderOptions options)
    {
        // Calculate width based on longest text
        var maxTextWidth = MeasureText(classDef.Name, options.FontSize, true);

        if (classDef.Annotation.HasValue)
        {
            var annotationText = $"<<{classDef.Annotation.Value.ToString().ToLower()}>>";
            maxTextWidth = Math.Max(maxTextWidth, MeasureText(annotationText, options.FontSize - 2));
        }

        foreach (var member in classDef.Members)
        {
            var text = FormatMember(member);
            maxTextWidth = Math.Max(maxTextWidth, MeasureText(text, options.FontSize));
        }

        foreach (var method in classDef.Methods)
        {
            var text = FormatMethod(method);
            maxTextWidth = Math.Max(maxTextWidth, MeasureText(text, options.FontSize));
        }

        var width = Math.Max(MinWidth, maxTextWidth + ClassPadding * 2);

        // Calculate height
        var height = ClassPadding; // Top padding
        if (classDef.Annotation.HasValue)
            height += LineHeight;
        height += LineHeight; // Class name
        height += ClassPadding; // After name

        var hasMembers = classDef.Members.Count > 0;
        var hasMethods = classDef.Methods.Count > 0;

        // When both attribute and method compartments exist, keep them visually symmetric by allocating
        // the same compartment height to each (based on the larger row count). This matches Mermaid’s
        // class box appearance more closely in Word when one compartment has fewer rows.
        var compartmentRowCount = (hasMembers && hasMethods)
            ? Math.Max(classDef.Members.Count, classDef.Methods.Count)
            : 0;

        if (hasMembers)
        {
            height += SeparatorHeight;
            height += (hasMethods ? compartmentRowCount : classDef.Members.Count) * LineHeight;
        }

        if (hasMethods)
        {
            height += SeparatorHeight;
            height += (hasMembers ? compartmentRowCount : classDef.Methods.Count) * LineHeight;
        }

        height += ClassPadding; // Bottom padding

        return (width, height);
    }

    static void RenderClassBox(SvgBuilder builder, ClassDefinition classDef, Node node, RenderOptions options)
    {
        var x = node.Position.X - node.Width / 2;
        var y = node.Position.Y - node.Height / 2;
        var width = node.Width;
        var height = node.Height;

        // Background (default fill/stroke aligned with Mermaid classDiagram defaults / flowchart node styling)
        var fillColor = classDef.Annotation switch
        {
            ClassAnnotation.Interface => "#E8F4FD",
            ClassAnnotation.Abstract => "#FFF3E0",
            ClassAnnotation.Enumeration => "#E8F5E9",
            _ => "#ECECFF"
        };

        builder.AddRect(x, y, width, height, rx: 0,
            fill: fillColor, stroke: "#9370DB", strokeWidth: 1);

        var currentY = y + ClassPadding;
        var centerX = node.Position.X;

        // Annotation
        if (classDef.Annotation.HasValue)
        {
            var annotationText = $"<<{classDef.Annotation.Value.ToString().ToLower()}>>";
            builder.AddText(centerX, currentY + LineHeight / 2, annotationText,
                anchor: "middle",
                baseline: "middle",
                fontSize: $"{options.FontSize - 2}px",
                fontFamily: options.FontFamily,
                fontWeight: "normal",
                fill: "#666");
            currentY += LineHeight;
        }

        // Class name: vertically centre in the full title band (name row + padding before the next rule)
        var nameBandTop = currentY;
        var classNameY = nameBandTop + (LineHeight + ClassPadding) / 2;
        builder.AddText(centerX, classNameY, classDef.Name,
            anchor: "middle",
            baseline: "middle",
            fontSize: $"{options.FontSize}px",
            fontFamily: options.FontFamily,
            fontWeight: "bold");
        currentY += LineHeight + ClassPadding;

        var hasMembers = classDef.Members.Count > 0;
        var hasMethods = classDef.Methods.Count > 0;
        var compartmentRowCount = (hasMembers && hasMethods)
            ? Math.Max(classDef.Members.Count, classDef.Methods.Count)
            : 0;

        // Members separator and list
        if (hasMembers)
        {
            builder.AddLine(x, currentY, x + width, currentY, stroke: "#9370DB", strokeWidth: 1);
            currentY += SeparatorHeight + CompartmentInnerPaddingTop;

            foreach (var member in classDef.Members)
            {
                var memberText = FormatMember(member);
                builder.AddText(x + ClassPadding, currentY + LineHeight / 2, memberText,
                    anchor: "start",
                    baseline: "central",
                    fontSize: $"{options.FontSize}px",
                    fontFamily: options.FontFamily);
                currentY += LineHeight;
            }

            // Pad out the compartment so the methods compartment can match its height.
            if (hasMethods && classDef.Members.Count < compartmentRowCount)
            {
                currentY += (compartmentRowCount - classDef.Members.Count) * LineHeight;
            }
        }

        // Methods separator and list
        if (hasMethods)
        {
            builder.AddLine(x, currentY, x + width, currentY, stroke: "#9370DB", strokeWidth: 1);
            currentY += SeparatorHeight + CompartmentInnerPaddingTop;

            foreach (var method in classDef.Methods)
            {
                var methodText = FormatMethod(method);
                builder.AddText(x + ClassPadding, currentY + LineHeight / 2 + MethodRowTextYOffsetPx, methodText,
                    anchor: "start",
                    baseline: "central",
                    fontSize: $"{options.FontSize}px",
                    fontFamily: options.FontFamily);
                currentY += LineHeight;
            }

            // If members exist, the overall height already includes any required padding on the members side.
            // If methods is the shorter list, pad to keep the bottom border consistent with the allocated height.
            if (hasMembers && classDef.Methods.Count < compartmentRowCount)
            {
                currentY += (compartmentRowCount - classDef.Methods.Count) * LineHeight;
            }
        }
    }

    static void RenderRelationship(SvgBuilder builder, ClassRelationship rel, Node fromNode, Node toNode, RenderOptions options)
    {
        // Calculate connection points
        var (startX, startY) = GetConnectionPoint(fromNode, toNode);
        var (endX, endY) = GetConnectionPoint(toNode, fromNode);

        var isDotted = rel.Type is RelationshipType.DependencyLeft or RelationshipType.DependencyRight or RelationshipType.Realization;
        var dashArray = isDotted ? "5,5" : null;

        builder.AddLine(startX, startY, endX, endY,
            stroke: "#333",
            strokeWidth: 1,
            strokeDasharray: dashArray);

        // Hollow triangle on generalisation / realisation: <|-- puts the marker on FromId; --|> on ToId.
        if (rel.Type is RelationshipType.Inheritance or RelationshipType.Realization)
        {
            if (rel.InheritanceMarkerOnFromEndpoint)
            {
                DrawRelationshipMarker(builder, rel.Type, startX, startY, endX, endY);
            }
            else
            {
                DrawRelationshipMarker(builder, rel.Type, endX, endY, startX, startY);
            }
        }
        else
        {
            DrawRelationshipMarker(builder, rel.Type, endX, endY, startX, startY);
        }

        // Draw label if present
        if (!string.IsNullOrEmpty(rel.Label))
        {
            var labelX = (startX + endX) / 2;
            var labelY = (startY + endY) / 2 - 10;
            builder.AddText(labelX, labelY, rel.Label,
                anchor: "middle",
                baseline: "bottom",
                fontSize: $"{options.FontSize - 2}px",
                fontFamily: options.FontFamily);
        }

        // Draw cardinalities
        if (!string.IsNullOrEmpty(rel.FromCardinality))
        {
            builder.AddText(startX + 10, startY - 10, rel.FromCardinality,
                anchor: "start",
                baseline: "bottom",
                fontSize: $"{options.FontSize - 2}px",
                fontFamily: options.FontFamily);
        }

        if (!string.IsNullOrEmpty(rel.ToCardinality))
        {
            builder.AddText(endX - 10, endY - 10, rel.ToCardinality,
                anchor: "end",
                baseline: "bottom",
                fontSize: $"{options.FontSize - 2}px",
                fontFamily: options.FontFamily);
        }
    }

    static (double x, double y) GetConnectionPoint(Node from, Node to)
    {
        // Calculate the point on the edge of 'from' node facing 'to' node
        var dx = to.Position.X - from.Position.X;
        var dy = to.Position.Y - from.Position.Y;

        // Determine which edge to use based on direction
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            // Horizontal connection
            return dx > 0
                ? (from.Position.X + from.Width / 2, from.Position.Y)
                : (from.Position.X - from.Width / 2, from.Position.Y);
        }

        // Vertical connection
        return dy > 0
            ? (from.Position.X, from.Position.Y + from.Height / 2)
            : (from.Position.X, from.Position.Y - from.Height / 2);
    }

    static void DrawRelationshipMarker(SvgBuilder builder, RelationshipType type, double x, double y, double fromX, double fromY)
    {
        var angle = Math.Atan2(y - fromY, x - fromX);
        var markerSize = 10.0;

        switch (type)
        {
            case RelationshipType.Inheritance:
            case RelationshipType.Realization:
                // Hollow triangle
                var points = GetTrianglePoints(x, y, angle, markerSize);
                builder.AddPolygon(points, fill: "#fff", stroke: "#333");
                break;

            case RelationshipType.Composition:
                // Filled diamond
                var diamondPoints = GetDiamondPoints(x, y, angle, markerSize);
                builder.AddPolygon(diamondPoints, fill: "#333", stroke: "#333");
                break;

            case RelationshipType.Aggregation:
                // Hollow diamond
                var aggDiamondPoints = GetDiamondPoints(x, y, angle, markerSize);
                builder.AddPolygon(aggDiamondPoints, fill: "#fff", stroke: "#333");
                break;

            case RelationshipType.Association:
            case RelationshipType.DependencyLeft:
            case RelationshipType.DependencyRight:
                // Arrow
                var arrowPoints = GetArrowPoints(x, y, angle, markerSize);
                builder.AddPolygon(arrowPoints, fill: "#333");
                break;
        }
    }

    static Position[] GetTrianglePoints(double x, double y, double angle, double size)
    {
        var backAngle1 = angle + Math.PI - Math.PI / 6;
        var backAngle2 = angle + Math.PI + Math.PI / 6;

        return
        [
            new(x, y),
            new(x + size * Math.Cos(backAngle1), y + size * Math.Sin(backAngle1)),
            new(x + size * Math.Cos(backAngle2), y + size * Math.Sin(backAngle2))
        ];
    }

    static Position[] GetDiamondPoints(double x, double y, double angle, double size)
    {
        var halfSize = size / 2;
        return
        [
            new(x, y),
            new(x - halfSize * Math.Cos(angle) + halfSize * Math.Sin(angle),
                y - halfSize * Math.Sin(angle) - halfSize * Math.Cos(angle)),
            new(x - size * Math.Cos(angle), y - size * Math.Sin(angle)),
            new(x - halfSize * Math.Cos(angle) - halfSize * Math.Sin(angle),
                y - halfSize * Math.Sin(angle) + halfSize * Math.Cos(angle))
        ];
    }

    static Position[] GetArrowPoints(double x, double y, double angle, double size)
    {
        var backAngle1 = angle + Math.PI - Math.PI / 6;
        var backAngle2 = angle + Math.PI + Math.PI / 6;

        return
        [
            new(x, y),
            new(x + size * Math.Cos(backAngle1), y + size * Math.Sin(backAngle1)),
            new(x + size * Math.Cos(backAngle2), y + size * Math.Sin(backAngle2))
        ];
    }

    static void AddRelationshipMarkers(SvgBuilder builder)
    {
        // Inheritance marker (hollow triangle)
        builder.AddMarker("inheritance", "M0,0 L10,5 L0,10 Z", 12, 12, 10, 5, "#fff");
        // Composition marker (filled diamond)
        builder.AddMarker("composition", "M0,5 L5,0 L10,5 L5,10 Z", 12, 12, 10, 5, "#333");
        // Aggregation marker (hollow diamond)
        builder.AddMarker("aggregation", "M0,5 L5,0 L10,5 L5,10 Z", 12, 12, 10, 5, "#fff");
    }

    static string GetMarkerId(RelationshipType type) =>
        type switch
        {
            RelationshipType.Inheritance or RelationshipType.Realization => "inheritance",
            RelationshipType.Composition => "composition",
            RelationshipType.Aggregation => "aggregation",
            _ => "arrowhead"
        };

    static string FormatMember(ClassMember member)
    {
        var staticPrefix = member.IsStatic ? "$ " : "";
        if (member.MemberDeclaresTypeBeforeName && !string.IsNullOrEmpty(member.Type))
        {
            var prefix = GetVisibilityPrefix(member.Visibility);
            return $"{prefix}{staticPrefix}{member.Type} {member.Name}";
        }

        var visibility = GetVisibilityPrefix(member.Visibility);
        var typeStr = !string.IsNullOrEmpty(member.Type) ? $" : {member.Type}" : "";
        return $"{visibility}{staticPrefix}{member.Name}{typeStr}";
    }

    static string GetVisibilityPrefix(Visibility visibility) =>
        visibility switch
        {
            Visibility.Public => "+",
            Visibility.Private => "-",
            Visibility.Protected => "#",
            Visibility.PackagePrivate => "~",
            _ => "+"
        };

    static string FormatMethod(ClassMethod method)
    {
        var visibility = GetVisibilityPrefix(method.Visibility);
        var staticPrefix = method.IsStatic ? "$ " : "";
        var abstractPrefix = method.IsAbstract ? "* " : "";
        var returnTypeStr = !string.IsNullOrEmpty(method.ReturnType) ? $" : {method.ReturnType}" : "";
        return $"{visibility}{staticPrefix}{abstractPrefix}{method.Name}(){returnTypeStr}";
    }

    static double MeasureText(string text, double fontSize, bool bold = false)
    {
        var factor = bold ? 0.65 : 0.55;
        return text.Length * fontSize * factor;
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

// Temporary model for layout - reusing flowchart structure
file class FlowchartModel : GraphDiagramBase;
