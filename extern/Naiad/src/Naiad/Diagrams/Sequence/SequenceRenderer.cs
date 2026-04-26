using MermaidSharp.Models;

namespace MermaidSharp.Diagrams.Sequence;

public class SequenceRenderer : IDiagramRenderer<SequenceModel>
{
    /// <summary>Minimum box width (reference Mermaid leaves generous space; Word also renders text wider than our estimate).</summary>
    const double MinParticipantBoxWidth = 140;

    /// <summary>Minimum box height for participant rectangles.</summary>
    const double MinParticipantBoxHeight = 60;

    /// <summary>Padding inside participant boxes (Mermaid-style breathing room).</summary>
    const double ParticipantPaddingX = 32;

    const double ParticipantPaddingY = 20;

    /// <summary>Mermaid live editor uses ~16px body text; measuring at least this wide avoids undersized boxes when <see cref="RenderOptions.FontSize"/> is 14.</summary>
    const double ParticipantLayoutMeasureFontFloor = 16;

    /// <summary>
    /// Count of regular space characters (U+0020) prepended and appended to participant header text so centred labels sit slightly in from the box edges (Word’s SVG text metrics can look tight otherwise).
    /// </summary>
    const int ParticipantLabelHorizontalSpacePadCount = 3;

    /// <summary>Horizontal gap between adjacent participant columns (between box edges).</summary>
    const double ParticipantColumnGap = 52;

    const double ParticipantActorVisualHeight = 40;

    const double MessageSpacing = 52;

    /// <summary>Distance from message arrow (y) up to the label baseline (larger matches Mermaid spacing).</summary>
    const double MessageLabelOffsetAboveArrow = 15;

    const double ActivationWidth = 10;
    const double NoteWidth = 120;
    const double NoteHeight = 40;
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
        var layout = CalculateParticipantLayout(model, options);
        var (height, elementYPositions) = CalculateHeight(model, options, layout.MaxHeaderHeight);
        var width = layout.TotalWidth;

        var builder = new SvgBuilder()
            .Size(width, height)
            .AddMermaidSequenceMessageMarkers("mermaid-svg_sequence")
            .AddNodeDropShadowFilter()
            .AddStyles(MermaidStyles.SequenceStyles);

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

        var startY = options.Padding + titleOffset;

        // Draw participants (top)
        DrawParticipants(builder, model, layout, startY, options);

        // Draw lifelines
        var lifelineStartY = startY + layout.MaxHeaderHeight;
        var lifelineEndY = height - options.Padding - layout.MaxHeaderHeight;
        DrawLifelines(builder, model, layout.Centers, lifelineStartY, lifelineEndY);

        // Draw elements (messages, notes, activations)
        var activations = new Dictionary<string, List<(double startY, double endY)>>();
        DrawElements(builder, model, layout, elementYPositions, options, activations);

        // Draw activation boxes
        DrawActivations(builder, activations, layout.Centers);

        // Draw participants (bottom) - optional, mimics Mermaid behavior
        DrawParticipants(builder, model, layout, lifelineEndY, options);

        return builder.Build();
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
        var leftEdge = options.Padding;
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

        var totalWidth = leftEdge + options.Padding;
        return new SequenceParticipantLayout(centers, widths, heights, maxHeaderHeight, totalWidth);
    }

    static (double height, Dictionary<int, double> elementYPositions) CalculateHeight(
        SequenceModel model, RenderOptions options, double maxParticipantHeaderHeight)
    {
        var elementYPositions = new Dictionary<int, double>();
        var y = options.Padding + maxParticipantHeaderHeight + MessageSpacing;
        var titleOffset = string.IsNullOrEmpty(model.Title) ? 0 : 30;

        for (var i = 0; i < model.Elements.Count; i++)
        {
            elementYPositions[i] = y + titleOffset;
            y += GetElementHeight(model.Elements[i]);
        }

        var totalHeight = y + maxParticipantHeaderHeight + options.Padding + titleOffset;
        return (totalHeight, elementYPositions);
    }

    static double GetElementHeight(SequenceElement element) =>
        element switch
        {
            Message => MessageSpacing,
            Note => NoteHeight + 10,
            Activation => 0, // Activations don't add height
            _ => MessageSpacing
        };

    /// <summary>Uses a slightly wide per-glyph estimate so boxes stay comfortable in Word (SVG text often renders wider than 0.55×).</summary>
    static double ParticipantMeasureFontSize(RenderOptions options) =>
        Math.Max(options.FontSize, ParticipantLayoutMeasureFontFloor);

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
        var width = text.Length * fontSize * 0.62;
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
            cssClass: "actor",
            filter: SvgBuilder.NaiadNodeShadowFilterReference);

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
            cssClass: "actor-head",
            filter: SvgBuilder.NaiadNodeShadowFilterReference);

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
            // Self-referencing message - draw as a loop
            var loopWidth = 40;
            var loopHeight = 30;
            var path = $"M{Fmt(fromX)},{Fmt(y)} " +
                       $"L{Fmt(fromX + loopWidth)},{Fmt(y)} " +
                       $"L{Fmt(fromX + loopWidth)},{Fmt(y + loopHeight)} " +
                       $"L{Fmt(fromX)},{Fmt(y + loopHeight)}";
            builder.AddPath(path,
                fill: "none",
                stroke: "#333",
                strokeWidth: 1,
                strokeDasharray: dashArray,
                markerEnd: $"url(#{markerEnd})");

            // Text above
            if (!string.IsNullOrEmpty(msg.Text))
            {
                var labelText = number.HasValue ? $"{number}. {msg.Text}" : msg.Text;
                builder.AddText(fromX + loopWidth + 5, y + loopHeight / 2, labelText,
                    anchor: "start",
                    baseline: "middle",
                    fontSize: $"{options.FontSize}px",
                    fontFamily: options.FontFamily);
            }
        }
        else
        {
            builder.AddLine(fromX, y, toX, y,
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
        var participantX = layout.Centers[note.ParticipantId];
        var participantHalfWidth = layout.Widths[note.ParticipantId] / 2;
        double noteX;

        switch (note.Position)
        {
            case NotePosition.RightOf:
                noteX = participantX + participantHalfWidth + 10;
                break;
            case NotePosition.LeftOf:
                noteX = participantX - participantHalfWidth - NoteWidth - 10;
                break;
            case NotePosition.Over:
            default:
                if (!string.IsNullOrEmpty(note.OverParticipantId2) &&
                    layout.Centers.TryGetValue(note.OverParticipantId2, out var participant2X))
                {
                    noteX = (participantX + participant2X) / 2 - NoteWidth / 2;
                }
                else
                {
                    noteX = participantX - NoteWidth / 2;
                }

                break;
        }

        // Note box (folded corner style)
        var foldSize = 8;
        var path = $"M{Fmt(noteX)},{Fmt(y)} " +
                   $"L{Fmt(noteX + NoteWidth - foldSize)},{Fmt(y)} " +
                   $"L{Fmt(noteX + NoteWidth)},{Fmt(y + foldSize)} " +
                   $"L{Fmt(noteX + NoteWidth)},{Fmt(y + NoteHeight)} " +
                   $"L{Fmt(noteX)},{Fmt(y + NoteHeight)} Z";

        builder.AddPath(path, fill: "#FFFFCC", stroke: "#AAAA33", strokeWidth: 1);

        // Fold line
        builder.AddLine(noteX + NoteWidth - foldSize, y,
            noteX + NoteWidth - foldSize, y + foldSize,
            stroke: "#AAAA33", strokeWidth: 1);
        builder.AddLine(noteX + NoteWidth - foldSize, y + foldSize,
            noteX + NoteWidth, y + foldSize,
            stroke: "#AAAA33", strokeWidth: 1);

        // Note text
        builder.AddText(noteX + NoteWidth / 2, y + NoteHeight / 2, note.Text,
            anchor: "middle",
            baseline: "middle",
            fontSize: $"{options.FontSize}px",
            fontFamily: options.FontFamily);
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