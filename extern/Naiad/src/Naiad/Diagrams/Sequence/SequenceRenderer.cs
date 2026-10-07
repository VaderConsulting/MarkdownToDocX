using MermaidSharp.Models;

namespace MermaidSharp.Diagrams.Sequence;

public class SequenceRenderer : IDiagramRenderer<SequenceModel>
{
    /// <summary>Mermaid default sequence config: <c>actorFontSize</c>.</summary>
    const double MermaidSequenceActorFontSizePx = 14;

    /// <summary>Mermaid default sequence config: <c>messageFontSize</c>.</summary>
    const double MermaidSequenceMessageFontSizePx = 16;

    /// <summary>Mermaid default sequence config: <c>noteFontSize</c>.</summary>
    const double MermaidSequenceNoteFontSizePx = 14;

    const string MermaidSequenceMessageFontFamily = "trebuchet ms, verdana, arial, sans-serif";

    /// <summary>Mermaid default sequence config: <c>diagramMarginX</c>.</summary>
    const double MermaidSequenceDiagramMarginXPx = 50;

    /// <summary>Mermaid default sequence config: <c>diagramMarginY</c>.</summary>
    const double MermaidSequenceDiagramMarginYPx = 10;

    /// <summary>Mermaid default sequence config: <c>actorMargin</c> (gap between actor columns).</summary>
    const double MermaidSequenceActorMarginPx = 50;

    /// <summary>Mermaid default sequence config: <c>messageMargin</c> (vertical spacing between messages).</summary>
    const double MermaidSequenceMessageMarginPx = 35;

    /// <summary>
    /// Minimum actor/participant box width. Mermaid’s default sequence config uses <c>width = 150</c>.
    /// </summary>
    const double MinParticipantBoxWidth = 150;

    /// <summary>
    /// Minimum actor/participant box height. Mermaid’s default sequence config uses <c>height = 50</c>.
    /// </summary>
    const double MinParticipantBoxHeight = 50;

    /// <summary>
    /// Padding inside participant boxes. Mermaid’s default sequence config uses <c>wrapPadding = 10</c> (sides only),
    /// and the rendered actor boxes are noticeably tighter than flowchart nodes.
    /// </summary>
    const double ParticipantPaddingX = 10;

    const double ParticipantPaddingY = 10;

    /// <summary>Mermaid live editor uses ~16px body text; measuring at least this wide avoids undersized boxes when <see cref="RenderOptions.FontSize"/> is 14.</summary>
    const double ParticipantLayoutMeasureFontFloor = 16;

    /// <summary>
    /// Count of regular space characters (U+0020) prepended and appended to participant header text so centred labels sit slightly in from the box edges (Word’s SVG text metrics can look tight otherwise).
    /// </summary>
    const int ParticipantLabelHorizontalSpacePadCount = 0;

    /// <summary>Horizontal gap between adjacent participant columns (between box edges).</summary>
    const double ParticipantColumnGap = MermaidSequenceActorMarginPx;

    const double ParticipantActorVisualHeight = 40;

    const double MessageSpacing = MermaidSequenceMessageMarginPx;

    /// <summary>Distance from message arrow (y) up to the label baseline (larger matches Mermaid spacing).</summary>
    const double MessageLabelOffsetAboveArrow = 15;

    /// <summary>
    /// Visual gap between a message arrow tip and the next diagram element.
    /// Derived from <see cref="MessageSpacing"/> minus label and arrowhead extents.
    /// </summary>
    static double SequenceInterElementClearancePx =>
        MessageSpacing - MessageLabelOffsetAboveArrow - GetSequenceMessageMarkerTipBeyondRefXPx(MessageType.SolidArrow);

    /// <summary>
    /// Bottom row margin below a note so the next message label clears the note box (Rule S1).
    /// </summary>
    static double NoteBottomRowMarginPx =>
        MessageLabelOffsetAboveArrow + MermaidSequenceMessageFontSizePx + SequenceInterElementClearancePx;

    /// <summary>Extra clearance so message arrow tips stop before destination lifelines in Word.</summary>
    const double SequenceMessageLifelineClearancePx = 3.0;

    const double ActivationWidth = 10;
    const double NoteHorizontalPaddingPx = 12;
    const double NoteVerticalPaddingPx = 10;
    const double NoteMaxTextWidthPx = 180;
    const double ActorHeadRadius = 15;

    const double SequenceLabelOpticalVerticalBiasEm = 0.09;
    const double SequenceNodeLabelExtraVerticalOffsetPx = 3;

    sealed record SequenceParticipantLayout(
        Dictionary<string, double> Centers,
        Dictionary<string, double> Widths,
        Dictionary<string, double> Heights,
        double MaxHeaderHeight,
        double TotalWidth);

    public SvgDocument Render(SequenceModel model, RenderOptions options)
    {
#pragma warning disable IDE0007
        RenderOptions messageOptions = CloneOptions(options, MermaidSequenceMessageFontFamily, MermaidSequenceMessageFontSizePx);
        RenderOptions actorOptions = CloneOptions(options, MermaidSequenceMessageFontFamily, MermaidSequenceActorFontSizePx);
#pragma warning restore IDE0007

        var layout = CalculateParticipantLayout(model, actorOptions);
        var (height, elementYPositions) = CalculateHeight(model, messageOptions, layout.MaxHeaderHeight);
        var width = layout.TotalWidth;

#pragma warning disable IDE0007
        MermaidSharp.Rendering.MermaidThemeVariables Theme = MermaidSharp.Rendering.MermaidThemeResolver.Resolve(model, options);
#pragma warning restore IDE0007

        var builder = new SvgBuilder()
            .Size(width, height)
            .AddMermaidSequenceMessageMarkers("mermaid-svg_sequence", strokeColor: Theme.ArrowheadColor)
            .AddStyles(MermaidStyles.ApplyThemeVariables(MermaidStyles.SequenceStyles, Theme));

        // Add title if present
        var titleOffset = 0.0;
        if (!string.IsNullOrEmpty(model.Title))
        {
            titleOffset = 30;
            builder.AddText(width / 2, 20, model.Title,
                anchor: "middle",
                fontSize: "16px",
                fontFamily: options.FontFamily,
                fontWeight: "bold");
        }

        var startY = MermaidSequenceDiagramMarginYPx + titleOffset;

        // Draw participants (top)
        DrawParticipants(builder, model, layout, startY, actorOptions);

        // Draw lifelines
        var lifelineStartY = startY + layout.MaxHeaderHeight;
        var lifelineEndY = height - MermaidSequenceDiagramMarginYPx - layout.MaxHeaderHeight;
        DrawLifelines(builder, model, layout.Centers, lifelineStartY, lifelineEndY);

        // Draw elements (messages, notes, activations)
        var activations = new Dictionary<string, List<(double startY, double endY)>>();
        DrawElements(builder, model, layout, elementYPositions, lifelineStartY, messageOptions, activations);

        // Draw activation boxes
        DrawActivations(builder, activations, layout.Centers);

        // Draw participants (bottom) - optional, mimics Mermaid behavior
        DrawParticipants(builder, model, layout, lifelineEndY, actorOptions);

        return builder.Build();
    }

    static RenderOptions CloneOptions(RenderOptions options, string fontFamily, double fontSizePx)
    {
#pragma warning disable IDE0007
        RenderOptions clone = new RenderOptions
        {
            Padding = options.Padding,
            Theme = options.Theme,
            FontFamily = fontFamily,
            ShowBoundingBox = options.ShowBoundingBox,
            FontSize = fontSizePx
        };
        return clone;
#pragma warning restore IDE0007
    }

    static SequenceParticipantLayout CalculateParticipantLayout(SequenceModel model, RenderOptions options)
    {
        var widths = new Dictionary<string, double>();
        var heights = new Dictionary<string, double>();

        var measureFont = ParticipantMeasureFontSize(options);

        foreach (var participant in model.Participants)
        {
            if (participant.Type == ParticipantType.Actor)
            {
                var paddedLabel = PadParticipantLabelForSequence(participant.DisplayName);
                var labelSize = MeasureParticipantLabel(paddedLabel, measureFont);
                var columnWidth = Math.Max(MinParticipantBoxWidth, labelSize.Width + ParticipantPaddingX * 2);
                widths[participant.Id] = columnWidth;
                heights[participant.Id] = ParticipantActorVisualHeight;
            }
            else
            {
                var paddedLabel = PadParticipantLabelForSequence(participant.DisplayName);
                var textSize = MeasureParticipantLabel(paddedLabel, measureFont);
                var boxWidth = Math.Max(MinParticipantBoxWidth, textSize.Width + ParticipantPaddingX * 2);
                var boxHeight = Math.Max(MinParticipantBoxHeight, textSize.Height + ParticipantPaddingY * 2);
                widths[participant.Id] = boxWidth;
                heights[participant.Id] = boxHeight;
            }
        }

        if (model.Participants.Count == 0)
        {
            return new SequenceParticipantLayout(
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                MinParticipantBoxHeight,
                options.Padding * 2 + MinParticipantBoxWidth);
        }

        var centers = new Dictionary<string, double>();
        var leftEdge = MermaidSequenceDiagramMarginXPx;
        for (var index = 0; index < model.Participants.Count; index++)
        {
            var participant = model.Participants[index];
            var columnWidth = widths[participant.Id];
            centers[participant.Id] = leftEdge + columnWidth / 2;
            leftEdge += columnWidth;
            if (index < model.Participants.Count - 1)
            {
                leftEdge += ParticipantColumnGap;
            }
        }

        var maxHeaderHeight = 0.0;
        foreach (var participant in model.Participants)
        {
            maxHeaderHeight = Math.Max(maxHeaderHeight, heights[participant.Id]);
        }

        var totalWidth = leftEdge + MermaidSequenceDiagramMarginXPx;
        return new SequenceParticipantLayout(centers, widths, heights, maxHeaderHeight, totalWidth);
    }

    static (double height, Dictionary<int, double> elementYPositions) CalculateHeight(
        SequenceModel model, RenderOptions options, double maxParticipantHeaderHeight)
    {
        var elementYPositions = new Dictionary<int, double>();
        var y = MermaidSequenceDiagramMarginYPx + maxParticipantHeaderHeight + MessageSpacing;
        var titleOffset = string.IsNullOrEmpty(model.Title) ? 0 : 30;

        for (var i = 0; i < model.Elements.Count; i++)
        {
            elementYPositions[i] = y + titleOffset;
            y += GetElementHeight(model.Elements[i], options);
        }

        var totalHeight = y + maxParticipantHeaderHeight + MermaidSequenceDiagramMarginYPx + titleOffset;
        return (totalHeight, elementYPositions);
    }

    static double GetElementHeight(SequenceElement element, RenderOptions options) =>
        element switch
        {
            Message => MessageSpacing,
            Note note => MeasureNoteSize(note).Height + NoteBottomRowMarginPx,
            Activation => 0, // Activations don't add height
            _ => MessageSpacing
        };

    /// <summary>Uses a slightly wide per-glyph estimate so boxes stay comfortable in Word (SVG text often renders wider than 0.55×).</summary>
    static double ParticipantMeasureFontSize(RenderOptions options) =>
        Math.Max(MermaidSequenceActorFontSizePx, ParticipantLayoutMeasureFontFloor);

    /// <summary>
    /// Adds symmetric ASCII spaces around participant header text so SVG renderers reserve visible separation from the rectangle stroke (layout uses the same string).
    /// </summary>
    static string PadParticipantLabelForSequence(string displayName)
    {
        if (string.IsNullOrEmpty(displayName) || ParticipantLabelHorizontalSpacePadCount <= 0)
        {
            return displayName;
        }

        var pad = new string(' ', ParticipantLabelHorizontalSpacePadCount);
        return pad + displayName + pad;
    }

    static Size MeasureParticipantLabel(string text, double fontSize)
    {
        // Mermaid’s sequence diagrams render actor labels with tighter glyph metrics than Word’s SVG text.
        // Use a narrower estimate to keep participant boxes closer to Mermaid defaults.
        var width = text.Length * fontSize * 0.50;
        var height = fontSize * 1.55;
        return new Size(width, height);
    }

    static void DrawParticipants(SvgBuilder builder, SequenceModel model,
        SequenceParticipantLayout layout, double y, RenderOptions options)
    {
        foreach (var participant in model.Participants)
        {
            var x = layout.Centers[participant.Id];

            if (participant.Type == ParticipantType.Actor)
            {
                DrawActor(builder, x, y, participant.DisplayName, options);
            }
            else
            {
                var boxWidth = layout.Widths[participant.Id];
                var boxHeight = layout.Heights[participant.Id];
                DrawParticipantBox(builder, x, y, PadParticipantLabelForSequence(participant.DisplayName), options, boxWidth, boxHeight);
            }
        }
    }

    static void DrawParticipantBox(SvgBuilder builder, double cx, double y,
        string paddedHeaderText, RenderOptions options, double boxWidth, double boxHeight)
    {
        builder.AddRect(
            cx - boxWidth / 2, y,
            boxWidth, boxHeight,
            rx: 3,
            fill: "#ECECFF",
            stroke: "#9370DB",
            strokeWidth: 1,
            cssClass: "actor");

        builder.AddBoxCenteredText(
            cx - boxWidth / 2, y,
            boxWidth, boxHeight,
            paddedHeaderText,
            fontSize: $"{options.FontSize}px",
            fontFamily: options.FontFamily,
            opticalVerticalBiasEm: SequenceLabelOpticalVerticalBiasEm,
            extraVerticalOffsetPx: SequenceNodeLabelExtraVerticalOffsetPx);
    }

    static void DrawActor(SvgBuilder builder, double cx, double y,
        string text, RenderOptions options)
    {
        // Stick figure
        var headY = y + ActorHeadRadius;
        var bodyTop = headY + ActorHeadRadius;
        var bodyBottom = bodyTop + 15;
        var armY = bodyTop + 5;
        var legBottom = y + ParticipantActorVisualHeight;

        // Head
        builder.AddCircle(cx, headY, ActorHeadRadius,
            fill: "#ECECFF",
            stroke: "#9370DB",
            strokeWidth: 1,
            cssClass: "actor-head");

        // Body
        builder.AddLine(cx, bodyTop, cx, bodyBottom,
            stroke: "#9370DB",
            strokeWidth: 1);

        // Arms
        builder.AddLine(cx - 15, armY, cx + 15, armY,
            stroke: "#9370DB",
            strokeWidth: 1);

        // Legs
        builder.AddLine(cx, bodyBottom, cx - 10, legBottom,
            stroke: "#9370DB",
            strokeWidth: 1);
        builder.AddLine(cx, bodyBottom, cx + 10, legBottom,
            stroke: "#9370DB",
            strokeWidth: 1);

        // Label below
        builder.AddText(
            cx, y + ParticipantActorVisualHeight + 15,
            text,
            anchor: "middle",
            baseline: "top",
            fontSize: $"{options.FontSize}px",
            fontFamily: options.FontFamily);
    }

    static void DrawLifelines(SvgBuilder builder, SequenceModel model,
        Dictionary<string, double> positions, double startY, double endY)
    {
        foreach (var participant in model.Participants)
        {
            var x = positions[participant.Id];
            builder.AddLine(x, startY, x, endY,
                stroke: "#9370DB",
                strokeWidth: 1,
                strokeDasharray: "5,5");
        }
    }

    static void DrawElements(SvgBuilder builder, SequenceModel model,
        SequenceParticipantLayout layout,
        Dictionary<int, double> yPositions,
        double lifelineStartY,
        RenderOptions options,
        Dictionary<string, List<(double startY, double endY)>> activations)
    {
        var messageNumber = 0;
        var activeLifelines = new Dictionary<string, double>(); // participantId -> activation start Y

        for (var i = 0; i < model.Elements.Count; i++)
        {
            var element = model.Elements[i];
            var y = yPositions[i];

            switch (element)
            {
                case Message msg:
                    messageNumber++;
                    DrawMessage(builder, msg, layout.Centers, y, options,
                        model.AutoNumber ? messageNumber : null);

                    // Handle activation on message
                    if (msg.Activate)
                    {
                        activeLifelines[msg.ToId] = y;
                    }

                    if (msg.Deactivate && activeLifelines.TryGetValue(msg.ToId, out var startY))
                    {
                        if (!activations.ContainsKey(msg.ToId))
                            activations[msg.ToId] = [];
                        activations[msg.ToId].Add((startY, y));
                        activeLifelines.Remove(msg.ToId);
                    }

                    break;

                case Note note:
                    DrawNote(builder, note, layout, y, options);
                    break;

                case Activation activation:
                    if (activation.IsActivate)
                    {
                        activeLifelines[activation.ParticipantId] = y;
                    }
                    else if (activeLifelines.TryGetValue(activation.ParticipantId, out var actStartY))
                    {
                        if (!activations.ContainsKey(activation.ParticipantId))
                            activations[activation.ParticipantId] = [];
                        activations[activation.ParticipantId].Add((actStartY, y));
                        activeLifelines.Remove(activation.ParticipantId);
                    }

                    break;
            }
        }

        // Close any remaining activations
        // ReSharper disable once UseIndexFromEndExpression
        var lastY = yPositions.Count > 0 ? yPositions[yPositions.Count - 1] + MessageSpacing : 0;
        foreach (var (participantId, startY) in activeLifelines)
        {
            if (!activations.TryGetValue(participantId, out var value))
            {
                value = [];
                activations[participantId] = value;
            }

            value.Add((startY, lastY));
        }
    }

    static double GetSequenceMessageMarkerTipBeyondRefXPx(MessageType MessageType) =>
        // How far the drawn tip extends past the path end along the arrow axis (viewBox geometry).
        MessageType switch
        {
            MessageType.SolidCross or MessageType.DottedCross => 2.0,
            MessageType.SolidArrow or MessageType.DottedArrow or MessageType.SolidAsync or MessageType.DottedAsync => 2.5,
            _ => 2.1
        };

    static double GetSequenceMessageEndShortenPx(MessageType MessageType) =>
        GetSequenceMessageMarkerTipBeyondRefXPx(MessageType) + SequenceMessageLifelineClearancePx;

    static void DrawMessage(SvgBuilder builder, Message msg,
        Dictionary<string, double> positions, double y,
        RenderOptions options, int? number)
    {
        var fromX = positions[msg.FromId];
        var toX = positions[msg.ToId];
        var isSelfMessage = msg.FromId == msg.ToId;

        var isDotted = msg.Type is MessageType.Dotted or MessageType.DottedArrow
            or MessageType.DottedOpen or MessageType.DottedCross or MessageType.DottedAsync;

        var markerEnd = msg.Type switch
        {
            MessageType.SolidCross or MessageType.DottedCross => "mermaid-svg_sequence-crosshead",
            MessageType.SolidArrow or MessageType.DottedArrow or MessageType.SolidAsync or MessageType.DottedAsync => "mermaid-svg_sequence-filled-head",
            _ => "mermaid-svg_sequence-arrowhead"
        };

        var dashArray = isDotted ? "5,5" : null;

        if (isSelfMessage)
        {
#pragma warning disable IDE0007
            // Mermaid default (rightAngles=false) draws a single cubic Bezier "hook" for self messages:
            // M x,y  C x+60,y-10  x+60,y+30  x,y+20
            double ControlDx = 60.0;
            double ControlDyUp = -10.0;
            double ControlDyDown = 30.0;
            double EndDy = 20.0;

            var path = $"M{Fmt(fromX)},{Fmt(y)} " +
                       $"C{Fmt(fromX + ControlDx)},{Fmt(y + ControlDyUp)} " +
                       $"{Fmt(fromX + ControlDx)},{Fmt(y + ControlDyDown)} " +
                       $"{Fmt(fromX)},{Fmt(y + EndDy)}";
            builder.AddPath(path,
                fill: "none",
                stroke: "#333",
                strokeWidth: 1,
                strokeDasharray: dashArray,
                markerEnd: $"url(#{markerEnd})");
#pragma warning restore IDE0007

            // Mermaid centres self-message labels on the lifeline, not to the right of the loop.
            if (!string.IsNullOrEmpty(msg.Text))
            {
                var labelText = number.HasValue ? $"{number}. {msg.Text}" : msg.Text;
                builder.AddText(fromX, y - MessageLabelOffsetAboveArrow, labelText,
                    anchor: "middle",
                    baseline: "bottom",
                    fontSize: $"{options.FontSize}px",
                    fontFamily: options.FontFamily);
            }
        }
        else
        {
            var Direction = toX >= fromX ? 1.0 : -1.0;
            var EndShortenPx = GetSequenceMessageEndShortenPx(msg.Type);
            var SpanPx = Math.Abs(toX - fromX);
            var LineEndX = SpanPx > EndShortenPx ? toX - (Direction * EndShortenPx) : toX;
            builder.AddLine(
                fromX,
                y,
                LineEndX,
                y,
                stroke: "#333",
                strokeWidth: 1,
                strokeDasharray: dashArray,
                markerEnd: markerEnd);

            // Text above the line
            if (!string.IsNullOrEmpty(msg.Text) || number.HasValue)
            {
                var labelText = number.HasValue && !string.IsNullOrEmpty(msg.Text)
                    ? $"{number}. {msg.Text}"
                    : number.HasValue
                        ? $"{number}."
                        : msg.Text!;

                var midX = (fromX + toX) / 2;
                builder.AddText(midX, y - MessageLabelOffsetAboveArrow, labelText,
                    anchor: "middle",
                    baseline: "bottom",
                    fontSize: $"{options.FontSize}px",
                    fontFamily: options.FontFamily);
            }
        }
    }

    static void DrawNote(SvgBuilder builder, Note note,
        SequenceParticipantLayout layout, double y, RenderOptions options)
    {
#pragma warning disable IDE0007
        Size noteSize = MeasureNoteSize(note);
        var participantX = layout.Centers[note.ParticipantId];
        var participantHalfWidth = layout.Widths[note.ParticipantId] / 2;
        double noteX;

        switch (note.Position)
        {
            case NotePosition.RightOf:
                noteX = participantX + participantHalfWidth + 10;
                break;
            case NotePosition.LeftOf:
                noteX = participantX - participantHalfWidth - noteSize.Width - 10;
                break;
            case NotePosition.Over:
            default:
                if (!string.IsNullOrEmpty(note.OverParticipantId2) &&
                    layout.Centers.TryGetValue(note.OverParticipantId2, out var participant2X))
                {
                    noteX = (participantX + participant2X) / 2 - noteSize.Width / 2;
                }
                else
                {
                    noteX = participantX - noteSize.Width / 2;
                }

                break;
        }

        // Note box (folded corner style)
        var foldSize = 8;
        var path = $"M{Fmt(noteX)},{Fmt(y)} " +
                   $"L{Fmt(noteX + noteSize.Width - foldSize)},{Fmt(y)} " +
                   $"L{Fmt(noteX + noteSize.Width)},{Fmt(y + foldSize)} " +
                   $"L{Fmt(noteX + noteSize.Width)},{Fmt(y + noteSize.Height)} " +
                   $"L{Fmt(noteX)},{Fmt(y + noteSize.Height)} Z";

        builder.AddPath(path, fill: "#FFF2B3", stroke: "#999922", strokeWidth: 1);

        // Fold line
        builder.AddLine(noteX + noteSize.Width - foldSize, y,
            noteX + noteSize.Width - foldSize, y + foldSize,
            stroke: "#999922", strokeWidth: 1);
        builder.AddLine(noteX + noteSize.Width - foldSize, y + foldSize,
            noteX + noteSize.Width, y + foldSize,
            stroke: "#999922", strokeWidth: 1);

        // Note text
        string wrapped = WrapPlainTextToWidth(note.Text, MermaidSequenceNoteFontSizePx, NoteMaxTextWidthPx);
        builder.AddForeignObject(
            noteX + NoteHorizontalPaddingPx,
            y + NoteVerticalPaddingPx,
            noteSize.Width - (NoteHorizontalPaddingPx * 2.0),
            noteSize.Height - (NoteVerticalPaddingPx * 2.0),
            "<p>" + wrapped.Replace("\n", "<br/>", StringComparison.Ordinal) + "</p>",
            className: "noteText",
            fontSizePx: MermaidSequenceNoteFontSizePx,
            opticalVerticalBiasEm: SequenceLabelOpticalVerticalBiasEm,
            extraVerticalOffsetPx: SequenceNodeLabelExtraVerticalOffsetPx);
#pragma warning restore IDE0007
    }

    static Size MeasureNoteSize(Note note)
    {
#pragma warning disable IDE0007
        string wrapped = WrapPlainTextToWidth(note.Text, MermaidSequenceNoteFontSizePx, NoteMaxTextWidthPx);
        string[] lines = wrapped.Split('\n');

        int maxLen = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            maxLen = Math.Max(maxLen, lines[i].Trim().Length);
        }

        double estimatedTextWidth = maxLen * MermaidSequenceNoteFontSizePx * 0.62;
        double textWidth = Math.Min(NoteMaxTextWidthPx, Math.Max(40.0, estimatedTextWidth));
        double lineHeight = Math.Max(MermaidSequenceNoteFontSizePx * 1.2, MermaidSequenceNoteFontSizePx + 2);
        double textHeight = Math.Max(1, lines.Length) * lineHeight;

        double width = textWidth + (NoteHorizontalPaddingPx * 2.0);
        double height = textHeight + (NoteVerticalPaddingPx * 2.0);
        return new Size(width, height);
#pragma warning restore IDE0007
    }

    static string WrapPlainTextToWidth(string text, double fontSizePx, double maxWidthPx)
    {
#pragma warning disable IDE0007
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        double maxChars = Math.Max(1.0, maxWidthPx / (fontSizePx * 0.62));
        int maxCharsPerLine = (int)Math.Floor(maxChars);

        string paragraph = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        string[] words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<string> lines = new List<string>();
        string current = string.Empty;

        for (int w = 0; w < words.Length; w++)
        {
            string word = words[w];
            if (word.Length > maxCharsPerLine)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                int index = 0;
                while (index < word.Length)
                {
                    int take = Math.Min(maxCharsPerLine, word.Length - index);
                    lines.Add(word.Substring(index, take));
                    index += take;
                }

                continue;
            }

            if (string.IsNullOrEmpty(current))
            {
                current = word;
                continue;
            }

            int proposed = current.Length + 1 + word.Length;
            if (proposed <= maxCharsPerLine)
            {
                current = current + " " + word;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return string.Join("\n", lines);
#pragma warning restore IDE0007
    }

    static void DrawActivations(SvgBuilder builder,
        Dictionary<string, List<(double startY, double endY)>> activations,
        Dictionary<string, double> positions)
    {
        foreach (var (participantId, ranges) in activations)
        {
            var x = positions[participantId];
            foreach (var (startY, endY) in ranges)
            {
                builder.AddRect(
                    x - ActivationWidth / 2, startY,
                    ActivationWidth, endY - startY,
                    fill: "#F4F4F4",
                    stroke: "#666",
                    strokeWidth: 1);
            }
        }
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}