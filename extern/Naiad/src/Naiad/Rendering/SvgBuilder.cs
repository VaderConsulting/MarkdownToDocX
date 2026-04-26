namespace MermaidSharp.Rendering;

public class SvgBuilder
{
    SvgDocument document = new();
    Stack<SvgGroup> groupStack = new();
    double padding;
    double contentWidth;
    double contentHeight;

    public SvgBuilder Size(double width, double height)
    {
        contentWidth = width;
        contentHeight = height;
        document.Width = width;
        document.Height = height;
        return this;
    }

    public SvgBuilder Padding(double padding)
    {
        this.padding = padding;
        // Adjust document size to include padding on all sides
        document.Width = contentWidth + padding * 2;
        document.Height = contentHeight + padding * 2;
        return this;
    }

    public SvgBuilder ViewBox(string viewBox)
    {
        document.ViewBoxOverride = viewBox;
        return this;
    }

    public SvgBuilder DiagramType(string diagramClass, string ariaRoledescription)
    {
        document.DiagramClass = diagramClass;
        document.AriaRoledescription = ariaRoledescription;
        return this;
    }

    public SvgBuilder AddStyles(string css)
    {
        document.CssStyles = css;
        return this;
    }

    public SvgBuilder AddFontAwesomeImport()
    {
        document.FontAwesomeImport = "@import url(\"https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.7.2/css/all.min.css\");";
        return this;
    }

    public SvgBuilder AddMarker(string id, string path, double width, double height,
        double refX, double refY, string? fill = null)
    {
        document.Defs.Markers.Add(
            new()
            {
                Id = id,
                Path = path,
                MarkerWidth = width,
                MarkerHeight = height,
                RefX = refX,
                RefY = refY,
                Fill = fill
            });
        return this;
    }

    public SvgBuilder AddRawMarker(
        string id,
        string rawContent,
        double width,
        double height,
        double refX,
        double refY,
        string? className = null,
        string orient = "auto",
        string? viewBox = null,
        string? markerUnits = null)
    {
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = id,
            Path = "",
            RawContent = rawContent,
            MarkerWidth = width,
            MarkerHeight = height,
            RefX = refX,
            RefY = refY,
            ClassName = className,
            Orient = orient,
            ViewBox = viewBox,
            MarkerUnits = markerUnits
        });
        return this;
    }

    public SvgBuilder AddArrowMarker(string id = "arrowhead", string fill = "#333333") =>
        AddMarker(id, "M0,0 L10,3.5 L0,7 Z", 10, 7, 9, 3.5, fill);

    public SvgBuilder AddCircleMarker(string id = "circle", string fill = "#333333")
    {
        document.Defs.Markers.Add(new()
        {
            Id = id,
            Path = "M4,4 m-3,0 a3,3 0 1,0 6,0 a3,3 0 1,0 -6,0",
            MarkerWidth = 8,
            MarkerHeight = 8,
            RefX = 4,
            RefY = 4,
            Fill = fill
        });
        return this;
    }

    public SvgBuilder AddCrossMarker(string id = "cross", string stroke = "#333333")
    {
        document.Defs.Markers.Add(new()
        {
            Id = id,
            Path = "M1,1 L7,7 M7,1 L1,7",
            MarkerWidth = 8,
            MarkerHeight = 8,
            RefX = 4,
            RefY = 4,
            Fill = "none"
        });
        return this;
    }

    /// <summary>
    /// Filter attribute value for use with <see cref="AddNodeDropShadowFilter"/> (match Mermaid-style soft node shadows).
    /// </summary>
    public const string NaiadNodeShadowFilterReference = "url(#naiad-node-shadow)";

    /// <summary>
    /// Registers a subtle drop shadow for diagram nodes (flowchart shapes, sequence participants).
    /// </summary>
    public SvgBuilder AddNodeDropShadowFilter(string id = "naiad-node-shadow")
    {
        document.Defs.Filters.Add(new SvgFilter
        {
            Id = id,
            Content =
                // Avoid <feDropShadow> because Word’s SVG renderer can omit filtered elements entirely.
                // This primitive-based shadow is widely supported in SVG 1.1 renderers.
                "<feGaussianBlur in=\"SourceAlpha\" stdDeviation=\"3\" result=\"blur\"/>" +
                "<feOffset in=\"blur\" dx=\"2\" dy=\"4\" result=\"offsetBlur\"/>" +
                "<feFlood flood-color=\"#000000\" flood-opacity=\"0.22\" result=\"shadowColor\"/>" +
                "<feComposite in=\"shadowColor\" in2=\"offsetBlur\" operator=\"in\" result=\"shadow\"/>" +
                "<feMerge>" +
                "<feMergeNode in=\"shadow\"/>" +
                "<feMergeNode in=\"SourceGraphic\"/>" +
                "</feMerge>"
        });
        return this;
    }

    public SvgBuilder AddMermaidArrowMarker()
    {
        /*
         * Marker positioning primer (SVG):
         *
         * - Markers are drawn in their own coordinate system defined by ViewBox.
         * - The marker is then scaled to MarkerWidth/MarkerHeight (because MarkerUnits=userSpaceOnUse here,
         *   these are in SVG user units, not relative to stroke-width).
         * - RefX/RefY is the anchor point *inside the marker's ViewBox* that will be placed at the end of the path.
         *
         * Practical effect:
         * - Increasing RefX moves the marker glyph LEFT relative to the path end (more of the glyph sits "behind" the end).
         * - Decreasing RefX moves the marker glyph RIGHT relative to the path end (more of the glyph sits "in front of" the end).
         *
         * For Mermaid parity in Word:
         * - We intentionally allow a small amount of marker overlap past the line end, then separately trim the rendered path
         *   (see FlowchartRenderer.RenderEdge) so the connector stroke doesn't visually run through the glyph.
         */
        // Mermaid upstream (packages/mermaid/src/rendering-util/rendering-elements/markers.js):
        // - pointEnd: viewBox 0 0 10 10, refX/refY 5/5, markerWidth/Height 8/8, path "M 0 0 L 10 5 L 0 10 z".
        //
        // Word parity note:
        // We use the non-margin marker definitions here and control stroke-to-glyph clearance via endpoint trimming
        // in FlowchartRenderer.
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-pointEnd",
            Path = "M 0 0 L 10 5 L 0 10 z",
            Fill = "#333333",
            MarkerWidth = 8,
            MarkerHeight = 8,
            RefX = 5,
            RefY = 5,
            ViewBox = "0 0 10 10",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker flowchart-v2",
            StrokeWidth = 1
        });

        // Naiad-specific marker id retained for compatibility, using Mermaid upstream pointEnd geometry.
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-pointEndThick",
            Path = "M 0 0 L 10 5 L 0 10 z",
            Fill = "#333333",
            MarkerWidth = 8,
            MarkerHeight = 8,
            RefX = 5,
            RefY = 5,
            ViewBox = "0 0 10 10",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker flowchart-v2",
            StrokeWidth = 1
        });

        // Mermaid upstream pointStart: viewBox 0 0 10 10, refX/refY 4.5/5, path "M 0 5 L 10 10 L 10 0 z".
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-pointStart",
            Path = "M 0 5 L 10 10 L 10 0 z",
            Fill = "#333333",
            MarkerWidth = 8,
            MarkerHeight = 8,
            RefX = 4.5,
            RefY = 5,
            ViewBox = "0 0 10 10",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker flowchart-v2",
            StrokeWidth = 1
        });

        // Naiad-specific marker id retained for bi-directional start usage; align to Mermaid upstream pointStart geometry.
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-pointStartBiDir",
            Path = "M 0 5 L 10 10 L 10 0 z",
            Fill = "#333333",
            MarkerWidth = 8,
            MarkerHeight = 8,
            RefX = 4.5,
            RefY = 5,
            ViewBox = "0 0 10 10",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker flowchart-v2",
            StrokeWidth = 1
        });
        return this;
    }

    public SvgBuilder AddMermaidCircleMarker()
    {
        /*
         * Circle terminal markers:
         *
         * Mermaid uses a filled circle terminal (on flowchart links like "--o" and bi-directional "o--o").
         * We represent the circle as a marker with a <circle> primitive.
         *
         * Geometry notes:
         * - ViewBox is 0..10, with CircleCx/Cy=5 and CircleR=5 (touches edges at x=0 and x=10).
         * - MarkerWidth/Height is 11 to match the visual diameter used by Mermaid (slightly larger than the 10-unit ViewBox),
         *   which keeps the circle visually balanced next to a 2px stroke.
         *
         * Positioning notes (RefX):
         * - circleEnd anchors at the right-most point of the circle (RefX=10) so the circle sits in front of the line end.
         * - circleStart anchors at the left-most point of the circle (RefX=0) so the circle sits in front of the line start.
         *
         * As with arrowheads, we also trim the path endpoints in FlowchartRenderer so the connector stroke does not run
         * under the filled circle.
         */
        // Mermaid upstream circleEnd: viewBox 0 0 10 10, refX/refY 11/5, markerWidth/Height 11/11, stroke-width 1.
        //
        // Word parity tweak:
        // In Word, using Mermaid's refX values can leave a visible 1px stroke protrusion beyond the filled circle.
        // Anchor the circle so its right-most edge aligns with the path endpoint.
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-circleEnd",
            Path = "",
            UseCircle = true,
            CircleCx = 5,
            CircleCy = 5,
            CircleR = 5,
            MarkerWidth = 11,
            MarkerHeight = 11,
            RefX = 10,
            RefY = 5,
            ViewBox = "0 0 10 10",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker flowchart-v2",
            StrokeWidth = 1
        });
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-circleStart",
            Path = "",
            UseCircle = true,
            CircleCx = 5,
            CircleCy = 5,
            CircleR = 5,
            MarkerWidth = 11,
            MarkerHeight = 11,
            // Word parity tweak: align left-most edge to the path start.
            RefX = 0,
            RefY = 5,
            ViewBox = "0 0 10 10",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker flowchart-v2",
            StrokeWidth = 1
        });
        return this;
    }

    public SvgBuilder AddMermaidCrossMarker()
    {
        /*
         * Cross ("X") terminal markers:
         *
         * Mermaid renders a cross terminal for flowchart links like "--x" and bi-directional "x--x".
         * The key parity requirements (in Word) are:
         * - The cross glyph size matches Mermaid (not too small and not overly thick).
         * - The connector stroke stops before the cross (handled by endpoint trimming in FlowchartRenderer).
         * - The cross is positioned so it does not intrude into the node border (marker anchoring via RefX/RefY).
         *
         * Geometry:
         * - ViewBox is 0..16 (larger than arrow/circle) to give us room for a visually substantial X.
         * - The X lines run from (3,3) to (13,13) and (13,3) to (3,13).
         * - StrokeWidth=2 for the marker (see MermaidStyles.BaseStyles: .marker.cross sets stroke but not width).
         *
         * Positioning (RefX):
         * - For end markers, larger RefX shifts the cross LEFT relative to the path end; smaller RefX shifts it RIGHT.
         * - For start markers, same rule, but the marker is attached at the start of the path.
         *
         * The specific RefX values here have been tuned by side-by-side Word comparisons against Mermaid.js preview.
         */
        // Mermaid upstream crossEnd: viewBox 0 0 11 11, refX/refY 12/5.2, markerWidth/Height 11/11, path "M 1,1 l 9,9 M 10,1 l -9,9".
        //
        // Mermaid upstream crossEnd: refX/refY 12/5.2.
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-crossEnd",
            Path = "M 1,1 l 9,9 M 10,1 l -9,9",
            MarkerWidth = 11,
            MarkerHeight = 11,
            RefX = 12,
            RefY = 5.2,
            ViewBox = "0 0 11 11",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker cross flowchart-v2",
            StrokeWidth = 2
        });
        document.Defs.Markers.Add(new()
        {
            Id = "mermaid-svg_flowchart-v2-crossStart",
            Path = "M 1,1 l 9,9 M 10,1 l -9,9",
            MarkerWidth = 11,
            MarkerHeight = 11,
            RefX = -1,
            RefY = 5.2,
            ViewBox = "0 0 11 11",
            MarkerUnits = "userSpaceOnUse",
            ClassName = "marker cross flowchart-v2",
            StrokeWidth = 2
        });
        return this;
    }

    /// <summary>
    /// Adds Mermaid sequence diagram message markers matching Mermaid upstream `packages/mermaid/src/diagrams/sequence/svgDraw.js`.
    /// </summary>
    public SvgBuilder AddMermaidSequenceMessageMarkers(string IdPrefix = "mermaid-svg_sequence")
    {
        // insertArrowHead: id `${IdPrefix}-arrowhead`, refX=7.9, refY=5, markerUnits=userSpaceOnUse,
        // markerWidth/Height=12/12, orient=auto-start-reverse, path `M -1 0 L 10 5 L 0 10 z`.
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = $"{IdPrefix}-arrowhead",
            Path = string.Empty,
            RefX = 7.9,
            RefY = 5,
            MarkerWidth = 12,
            MarkerHeight = 12,
            MarkerUnits = "userSpaceOnUse",
            Orient = "auto-start-reverse",
            ViewBox = "0 0 12 12",
            RawContent = "<path d=\"M -1 0 L 10 5 L 0 10 z\" fill=\"none\" stroke=\"#333333\" stroke-width=\"1\" stroke-linejoin=\"round\"/>"
        });

        // insertArrowFilledHead: id `${IdPrefix}-filled-head`, refX=15.5, refY=7, markerWidth/Height=20/28, orient=auto,
        // path `M 18,7 L9,13 L14,7 L9,1 Z`.
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = $"{IdPrefix}-filled-head",
            Path = string.Empty,
            RefX = 15.5,
            RefY = 7,
            MarkerWidth = 20,
            MarkerHeight = 28,
            Orient = "auto",
            ViewBox = "0 0 20 28",
            RawContent = "<path d=\"M 18,7 L9,13 L14,7 L9,1 Z\" fill=\"#333333\" stroke=\"#333333\" stroke-width=\"1\" stroke-linejoin=\"round\"/>"
        });

        // insertArrowCrossHead: id `${IdPrefix}-crosshead`, markerWidth=15, markerHeight=8, orient=auto, refX=4, refY=4.5,
        // path `M 1,2 L 6,7 M 6,2 L 1,7` with stroke-width 1pt.
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = $"{IdPrefix}-crosshead",
            Path = string.Empty,
            RefX = 4,
            RefY = 4.5,
            MarkerWidth = 15,
            MarkerHeight = 8,
            Orient = "auto",
            ViewBox = "0 0 15 8",
            RawContent = "<path fill=\"none\" stroke=\"#333333\" style=\"stroke-dasharray: 0, 0\" stroke-width=\"1pt\" d=\"M 1,2 L 6,7 M 6,2 L 1,7\"/>"
        });

        return this;
    }

    /// <summary>
    /// Adds Mermaid ER diagram cardinality markers (onlyOne, zeroOrOne, oneOrMore, zeroOrMore) matching Mermaid upstream
    /// `packages/mermaid/src/diagrams/er/erMarkers.js`.
    /// </summary>
    public SvgBuilder AddMermaidErMarkers(string StrokeColor)
    {
        var NormalisedStrokeColor = string.IsNullOrWhiteSpace(StrokeColor) ? "#333333" : StrokeColor;

        // Mermaid upstream ER cardinality markers (erMarkers.js), pinned into explicit viewBox/markerUnits for
        // stable rendering across SVG engines (including Word).

        // MD_PARENT_START
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "MD_PARENT_START",
            Path = string.Empty,
            RefX = 0,
            RefY = 7,
            MarkerWidth = 190,
            MarkerHeight = 240,
            Orient = "auto",
            ViewBox = "0 0 20 14",
            MarkerUnits = "userSpaceOnUse",
            RawContent = "<path d=\"M 18,7 L9,13 L1,7 L9,1 Z\"/>"
        });

        // MD_PARENT_END
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "MD_PARENT_END",
            Path = string.Empty,
            RefX = 19,
            RefY = 7,
            MarkerWidth = 20,
            MarkerHeight = 28,
            Orient = "auto",
            ViewBox = "0 0 20 14",
            MarkerUnits = "userSpaceOnUse",
            RawContent = "<path d=\"M 18,7 L9,13 L1,7 L9,1 Z\"/>"
        });

        // ONLY_ONE_START
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ONLY_ONE_START",
            Path = string.Empty,
            RefX = 0,
            RefY = 9,
            MarkerWidth = 18,
            MarkerHeight = 18,
            Orient = "auto",
            ViewBox = "0 0 18 18",
            MarkerUnits = "userSpaceOnUse",
            RawContent = $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M9,0 L9,18 M15,0 L15,18\"/>"
        });

        // ONLY_ONE_END
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ONLY_ONE_END",
            Path = string.Empty,
            RefX = 18,
            RefY = 9,
            MarkerWidth = 18,
            MarkerHeight = 18,
            Orient = "auto",
            ViewBox = "0 0 18 18",
            MarkerUnits = "userSpaceOnUse",
            RawContent = $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M3,0 L3,18 M9,0 L9,18\"/>"
        });

        // ZERO_OR_ONE_START (circle + bar)
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ZERO_OR_ONE_START",
            Path = string.Empty,
            RefX = 0,
            RefY = 9,
            MarkerWidth = 30,
            MarkerHeight = 18,
            Orient = "auto",
            ViewBox = "0 0 30 18",
            MarkerUnits = "userSpaceOnUse",
            RawContent =
                $"<circle stroke=\"{NormalisedStrokeColor}\" fill=\"white\" cx=\"21\" cy=\"9\" r=\"6\"/>" +
                $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M9,0 L9,18\"/>"
        });

        // ZERO_OR_ONE_END
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ZERO_OR_ONE_END",
            Path = string.Empty,
            RefX = 30,
            RefY = 9,
            MarkerWidth = 30,
            MarkerHeight = 18,
            Orient = "auto",
            ViewBox = "0 0 30 18",
            MarkerUnits = "userSpaceOnUse",
            RawContent =
                $"<circle stroke=\"{NormalisedStrokeColor}\" fill=\"white\" cx=\"9\" cy=\"9\" r=\"6\"/>" +
                $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M21,0 L21,18\"/>"
        });

        // ONE_OR_MORE_START (crowfoot + bar)
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ONE_OR_MORE_START",
            Path = string.Empty,
            RefX = 18,
            RefY = 18,
            MarkerWidth = 45,
            MarkerHeight = 36,
            Orient = "auto",
            ViewBox = "0 0 45 36",
            MarkerUnits = "userSpaceOnUse",
            RawContent =
                $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M0,18 Q 18,0 36,18 Q 18,36 0,18 M42,9 L42,27\"/>"
        });

        // ONE_OR_MORE_END
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ONE_OR_MORE_END",
            Path = string.Empty,
            RefX = 27,
            RefY = 18,
            MarkerWidth = 45,
            MarkerHeight = 36,
            Orient = "auto",
            ViewBox = "0 0 45 36",
            MarkerUnits = "userSpaceOnUse",
            RawContent =
                $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M3,9 L3,27 M9,18 Q27,0 45,18 Q27,36 9,18\"/>"
        });

        // ZERO_OR_MORE_START (circle + crowfoot)
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ZERO_OR_MORE_START",
            Path = string.Empty,
            RefX = 18,
            RefY = 18,
            MarkerWidth = 57,
            MarkerHeight = 36,
            Orient = "auto",
            ViewBox = "0 0 57 36",
            MarkerUnits = "userSpaceOnUse",
            RawContent =
                $"<circle stroke=\"{NormalisedStrokeColor}\" fill=\"white\" cx=\"48\" cy=\"18\" r=\"6\"/>" +
                $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M0,18 Q18,0 36,18 Q18,36 0,18\"/>"
        });

        // ZERO_OR_MORE_END
        document.Defs.Markers.Add(new SvgMarker
        {
            Id = "ZERO_OR_MORE_END",
            Path = string.Empty,
            RefX = 39,
            RefY = 18,
            MarkerWidth = 57,
            MarkerHeight = 36,
            Orient = "auto",
            ViewBox = "0 0 57 36",
            MarkerUnits = "userSpaceOnUse",
            RawContent =
                $"<circle stroke=\"{NormalisedStrokeColor}\" fill=\"white\" cx=\"9\" cy=\"18\" r=\"6\"/>" +
                $"<path stroke=\"{NormalisedStrokeColor}\" fill=\"none\" d=\"M21,18 Q39,0 57,18 Q39,36 21,18\"/>"
        });

        return this;
    }

    public SvgBuilder AddForeignObject(double x, double y, double width, double height,
        string htmlContent, string? className = null, double fontSizePx = 16,
        double opticalVerticalBiasEm = 0.12, double extraVerticalOffsetPx = 0, string? fill = null)
    {
        var foreignObject = new SvgForeignObject
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            HtmlContent = htmlContent,
            Class = className,
            FontSizePx = fontSizePx,
            OpticalVerticalBiasEm = opticalVerticalBiasEm,
            ExtraVerticalOffsetPx = extraVerticalOffsetPx,
            Fill = fill
        };
        AddElement(foreignObject);
        return this;
    }

    public SvgBuilder BeginGroup(string? id = null, string? cssClass = null, string? transform = null)
    {
        var group = new SvgGroup
        {
            Id = id,
            Class = cssClass,
            Transform = transform
        };

        if (groupStack.Count > 0)
        {
            groupStack.Peek().Children.Add(group);
        }
        else
        {
            document.Elements.Add(group);
        }

        groupStack.Push(group);
        return this;
    }

    public SvgBuilder EndGroup()
    {
        if (groupStack.Count > 0)
        {
            groupStack.Pop();
        }

        return this;
    }

    public SvgBuilder AddRect(double x, double y, double width, double height,
        double rx = 0, string? fill = null, string? stroke = null, double? strokeWidth = null,
        string? id = null, string? cssClass = null, string? style = null, string? filter = null)
    {
        var rect = new SvgRect
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Rx = rx,
            Ry = rx,
            Fill = fill,
            Stroke = stroke,
            StrokeWidth = strokeWidth,
            Id = id,
            Class = cssClass,
            Style = style,
            Filter = filter
        };
        AddElement(rect);
        return this;
    }

    public SvgBuilder AddRectNoXY(double width, double height, string? style = null)
    {
        var rect = new SvgRectNoXY
        {
            Width = width,
            Height = height,
            Style = style
        };
        AddElement(rect);
        return this;
    }

    public SvgBuilder AddCircle(double cx, double cy, double r,
        string? fill = null, string? stroke = null, double? strokeWidth = null, string? cssClass = null,
        string? filter = null)
    {
        var circle = new SvgCircle
        {
            Cx = cx,
            Cy = cy,
            R = r,
            Fill = fill,
            Stroke = stroke,
            StrokeWidth = strokeWidth,
            Class = cssClass,
            Filter = filter
        };
        AddElement(circle);
        return this;
    }

    public SvgBuilder AddEllipse(double cx, double cy, double rx, double ry,
        string? fill = null, string? stroke = null)
    {
        var ellipse = new SvgEllipse
        {
            Cx = cx,
            Cy = cy,
            Rx = rx,
            Ry = ry,
            Fill = fill,
            Stroke = stroke
        };
        AddElement(ellipse);
        return this;
    }

    public SvgBuilder AddLine(double x1, double y1, double x2, double y2,
        string? stroke = null, double? strokeWidth = null, string? strokeDasharray = null,
        string? markerStart = null, string? markerEnd = null)
    {
        var line = new SvgLine
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeWidth = strokeWidth,
            StrokeDasharray = strokeDasharray,
            MarkerStart = markerStart,
            MarkerEnd = markerEnd
        };
        AddElement(line);
        return this;
    }

    public SvgBuilder AddPath(string d, string? fill = null, string? stroke = null,
        double? strokeWidth = null, string? strokeDasharray = null,
        string? markerStart = null, string? markerEnd = null, double? opacity = null,
        string? cssClass = null, string? filter = null)
    {
        var path = new SvgPath
        {
            D = d,
            Fill = fill,
            Stroke = stroke,
            StrokeWidth = strokeWidth,
            StrokeDasharray = strokeDasharray,
            MarkerStart = markerStart,
            MarkerEnd = markerEnd,
            Opacity = opacity,
            Class = cssClass,
            Filter = filter
        };
        AddElement(path);
        return this;
    }

    public SvgBuilder AddPolygon(IEnumerable<Position> points,
        string? fill = null, string? stroke = null)
    {
        var polygon = new SvgPolygon {Fill = fill, Stroke = stroke};
        polygon.Points.AddRange(points);
        AddElement(polygon);
        return this;
    }

    public SvgBuilder AddPolyline(IEnumerable<Position> points,
        string? fill = null, string? stroke = null, double? strokeWidth = null,
        string? strokeDasharray = null, string? markerEnd = null)
    {
        var polyline = new SvgPolyline
        {
            Fill = fill,
            Stroke = stroke,
            StrokeWidth = strokeWidth,
            StrokeDasharray = strokeDasharray,
            MarkerEnd = markerEnd
        };
        polyline.Points.AddRange(points);
        AddElement(polyline);
        return this;
    }

    public SvgBuilder AddText(double x, double y, string content,
        string? anchor = null, string? baseline = null,
        string? fontSize = null, string? fontFamily = null, string? fontWeight = null,
        string? fill = null, string? id = null, string? cssClass = null,
        string? transform = null, string? style = null, bool omitXY = false)
    {
        var text = new SvgText
        {
            X = x,
            Y = y,
            OmitXY = omitXY,
            Content = content,
            TextAnchor = anchor,
            DominantBaseline = baseline,
            FontSize = fontSize,
            FontFamily = fontFamily,
            FontWeight = fontWeight,
            Fill = fill,
            Id = id,
            Class = cssClass,
            Transform = transform,
            Style = style
        };
        AddElement(text);
        return this;
    }

    public SvgBuilder AddBoxCenteredText(double x, double y, double width, double height, string content,
        string? fontSize = null, string? fontFamily = null, string? fontWeight = null,
        string? fill = null, string? id = null, string? cssClass = null,
        double opticalVerticalBiasEm = 0.12, double extraVerticalOffsetPx = 0)
    {
        var fontSizePx = 16.0;
        if (!string.IsNullOrWhiteSpace(fontSize))
        {
            var trimmed = fontSize.Trim();
            if (trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^2];
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                fontSizePx = parsed;
            }
        }

        var centerX = x + width / 2.0;
        var opticalDown = Math.Max(2.0, fontSizePx * opticalVerticalBiasEm);
        var centerY = y + height / 2.0 + opticalDown + extraVerticalOffsetPx;

        return AddText(centerX, centerY, content,
            anchor: "middle",
            baseline: "middle",
            fontSize: fontSize,
            fontFamily: fontFamily,
            fontWeight: fontWeight,
            fill: fill,
            id: id,
            cssClass: cssClass);
    }

    void AddElement(SvgElement element)
    {
        if (groupStack.Count > 0)
        {
            groupStack.Peek().Children.Add(element);
        }
        else
        {
            document.Elements.Add(element);
        }
    }

    public SvgDocument Build()
    {
        // If padding is set, wrap all elements in a transform group
        if (padding > 0 && document.Elements.Count > 0)
        {
            var paddingGroup = new SvgGroup
            {
                Transform = $"translate({Fmt(padding)},{Fmt(padding)})"
            };
            paddingGroup.Children.AddRange(document.Elements);
            document.Elements.Clear();
            document.Elements.Add(paddingGroup);
        }

        return document;
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
