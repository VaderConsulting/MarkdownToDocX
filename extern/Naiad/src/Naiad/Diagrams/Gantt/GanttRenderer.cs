using System.Text;

namespace MermaidSharp.Diagrams.Gantt;

public class GanttRenderer : IDiagramRenderer<GanttModel>
{
    // Mermaid defaults (config.schema.yaml + ganttRenderer.js).
    const double DEFAULT_WIDTH_PX = 1200;
    const double TITLE_TOP_MARGIN_PX = 25;
    const double TOP_PADDING_PX = 50;
    const double LEFT_PADDING_PX = 75;
    const double RIGHT_PADDING_PX = 75;
    const double GRID_LINE_START_PADDING_PX = 35;
    const double taskBarHeight = 20; // Mermaid barHeight
    const double BAR_GAP_PX = 4;     // Mermaid barGap
    const double rowHeight = taskBarHeight + BAR_GAP_PX;
    const double milestoneSize = 12;

    // Mermaid default theme (theme-default.js) values for Gantt.
    const string TASK_BORDER_COLOR = "#534fbc";
    const string TASK_BKG_COLOR = "#8a90dd";
    const string ACTIVE_TASK_BKG_COLOR = "#bfc7ff";
    const string DONE_TASK_BKG_COLOR = "lightgrey";
    const string CRIT_TASK_BKG_COLOR = "red";
    const string DONE_TASK_BORDER_COLOR = "grey";
    const string CRIT_TASK_BORDER_COLOR = "#ff8888";

    // Mermaid uses per-row section fills with opacity 0.2.
    // Word does not reliably respect `rgba(..., alpha)` in SVG fills, so emit solid fills and apply `fill-opacity`.
    const string SECTION0_ROW_COLOR = "rgb(102, 102, 255)";
    const string SECTION1_ROW_COLOR = "white";
    const string SECTION2_ROW_COLOR = "#fff400";
    const string SECTION3_ROW_COLOR = "white";
    const string SECTION_ROW_FILL_OPACITY_STYLE = "fill-opacity: 0.2";
    const double RIGHT_LABEL_PADDING_PX = 60;
    const double FORCE_LABEL_INSIDE_BAR_WIDTH_PX = 140;

    const string milestoneColor = "#FF9800";

    /// <summary>Word tends to paint <c>dominant-baseline: middle</c> high for row/section titles; slight positive Y.</summary>
    static double GanttWordRowTitleDown(RenderOptions options) =>
        Math.Max(2, options.FontSize * 0.11);

    /// <summary>Thin task bars often render bar-centre labels low in Word; slight negative Y on the anchor.</summary>
    static double GanttWordBarLabelUp(RenderOptions options)
    {
        var fs = Math.Max(8, options.FontSize - 2);
        return Math.Max(1.5, fs * 0.11);
    }

    #pragma warning disable IDE0022
    static double GanttWordBarLabelDown(double fontSizePx)
    {
        // Word’s `dominant-baseline="middle"` renders slightly high for these fonts.
        // This value is empirically tuned for DOCX parity.
        return Math.Max(3.0, fontSizePx * 0.25);
    }
    #pragma warning restore IDE0022

    public SvgDocument Render(GanttModel model, RenderOptions options)
    {
        // Compute task dates
        var tasks = ComputeTaskDates(model);

        if (tasks.Count == 0)
        {
            // Empty chart
            var emptyBuilder = new SvgBuilder().Size(200, 100);
            emptyBuilder.AddText(100, 50 + GanttWordRowTitleDown(options), "No tasks", anchor: "middle", baseline: "middle",
                fontSize: $"{options.FontSize}px", fontFamily: options.FontFamily);
            return emptyBuilder.Build();
        }

        // Calculate date range.
        var minDate = tasks.Min(_ => _.ComputedStart);
        var maxDate = tasks.Max(_ => _.ComputedEnd);
        var totalDays = (maxDate - minDate).Days + 1;

        // Mermaid height is based on task rows (not section header rows).
#pragma warning disable IDE0007
        int totalTaskCount = model.Sections.Sum(_ => _.Tasks.Count);
#pragma warning restore IDE0007
        var height = (2 * TOP_PADDING_PX) + (totalTaskCount * rowHeight);
        if (tasks.Any(_ => _.IsVert))
        {
            // Mermaid places the vert label below the grid; keep space for it.
            height = Math.Max(height, GRID_LINE_START_PADDING_PX + (totalTaskCount * rowHeight) + 60 + TOP_PADDING_PX);
        }

        // Mermaid width defaults to 1200 (unless gantt.useWidth overrides).
        var width = DEFAULT_WIDTH_PX;

        var chartWidth = width - LEFT_PADDING_PX - RIGHT_PADDING_PX;
        var dayWidthPx = chartWidth / Math.Max(1, totalDays - 1);

        // Ensure the viewBox is wide enough for long section titles and task labels placed outside bars.
        var maxLeftLabelWidthPx = EstimateMaxLeftLabelWidthPx(model, options);
        var minWidthForLeftLabels = 10 + maxLeftLabelWidthPx + RIGHT_LABEL_PADDING_PX;

        // If task labels are placed outside to the right, the SVG must be wide enough or Word will clip them.
        var maxRightLabelWidthPx = EstimateMaxRightLabelWidthPx(model, options);
        var minWidthForRightLabels = LEFT_PADDING_PX + chartWidth + 6 + maxRightLabelWidthPx + RIGHT_LABEL_PADDING_PX;

        width = Math.Max(width, Math.Max(minWidthForLeftLabels, minWidthForRightLabels));

        // Recompute chart metrics after widening.
        chartWidth = width - LEFT_PADDING_PX - RIGHT_PADDING_PX;
        dayWidthPx = chartWidth / Math.Max(1, totalDays - 1);

        var builder = new SvgBuilder().Size(width, height);

        var offsetX = LEFT_PADDING_PX;
        var offsetY = TOP_PADDING_PX;

        // Draw title
        if (!string.IsNullOrEmpty(model.Title))
        {
            builder.AddText(width / 2, TITLE_TOP_MARGIN_PX + GanttWordRowTitleDown(options), model.Title,
                anchor: "middle",
                baseline: "middle",
                fontSize: $"{options.FontSize + 2}px",
                fontFamily: options.FontFamily);
        }

        // Draw axis and grid (Mermaid places axis ticks at the bottom).
        DrawAxis(builder, minDate, totalDays, offsetX, height, chartWidth, dayWidthPx, options, model.AxisFormat);
        DrawGridLines(builder, totalDays, offsetX, height, chartWidth, dayWidthPx);

        // Draw section row backgrounds (Mermaid draws full-width row bands behind tasks).
        DrawSectionRowBackgrounds(builder, model, offsetY, chartWidth);

        // Mermaid does not render section header rows; it renders section titles at the left.
        var taskRowIndex = 0;
        foreach (var section in model.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Name) && section.Tasks.Count > 0)
            {
                var sectionTitleY = offsetY + (taskRowIndex * rowHeight) + (section.Tasks.Count * rowHeight / 2.0) + GanttWordRowTitleDown(options);
                builder.AddText(10, sectionTitleY, section.Name,
                    anchor: "start",
                    baseline: "middle",
                    fontSize: $"{Math.Max(8, options.FontSize - 2)}px",
                    fontFamily: options.FontFamily,
                    fontWeight: "bold",
                    fill: "#000");
            }

            foreach (var task in section.Tasks)
            {
                DrawTask(builder, task, minDate, taskRowIndex, offsetX, offsetY, dayWidthPx, totalTaskCount, height, options);
                taskRowIndex++;
            }
        }

        return builder.Build();
    }

    static void DrawAxis(SvgBuilder builder, DateTime startDate, int totalDays, double offsetX, double height,
        double chartWidth, double dayWidthPx, RenderOptions options, string? axisFormat)
    {
        // Mermaid places the axis tick labels at the bottom (approximately y = h - topPadding).
        #pragma warning disable IDE0007
        double axisY = height - TOP_PADDING_PX;
        #pragma warning restore IDE0007
        builder.AddLine(offsetX, axisY, offsetX + chartWidth, axisY, stroke: "#333", strokeWidth: 1);

        for (var i = 0; i < totalDays; i++)
        {
            var x = offsetX + i * dayWidthPx;
            var date = startDate.AddDays(i);

            // Tick mark
            builder.AddLine(x, axisY - 5, x, axisY, stroke: "#333", strokeWidth: 1);

            // Date label — honour Mermaid axisFormat when present (subset of strftime-style tokens)
            var label = FormatAxisDateLabel(date, axisFormat);
            builder.AddText(x, axisY + 13, label,
                anchor: "middle",
                baseline: "middle",
                fontSize: $"{options.FontSize - 2}px",
                fontFamily: options.FontFamily,
                fill: "#666");
        }
    }

    /// <summary>
    /// Formats a Gantt axis tick using <see cref="GanttModel.AxisFormat"/> when set.
    /// Supports common Mermaid strftime tokens: %Y %y %m %d %H %M %S %b %B and literal %%.
    /// </summary>
    static string FormatAxisDateLabel(DateTime date, string? axisFormat)
    {
        if (string.IsNullOrWhiteSpace(axisFormat))
        {
            return date.ToString("MM/dd", CultureInfo.InvariantCulture);
        }

        var sb = new StringBuilder();
        for (var i = 0; i < axisFormat.Length; i++)
        {
            if (axisFormat[i] == '%' && i + 1 < axisFormat.Length)
            {
                i++;
                switch (axisFormat[i])
                {
                    case '%':
                        sb.Append('%');
                        break;
                    case 'Y':
                        sb.Append(date.ToString("yyyy", CultureInfo.InvariantCulture));
                        break;
                    case 'y':
                        sb.Append(date.ToString("yy", CultureInfo.InvariantCulture));
                        break;
                    case 'm':
                        sb.Append(date.ToString("MM", CultureInfo.InvariantCulture));
                        break;
                    case 'd':
                        sb.Append(date.ToString("dd", CultureInfo.InvariantCulture));
                        break;
                    case 'H':
                        sb.Append(date.ToString("HH", CultureInfo.InvariantCulture));
                        break;
                    case 'M':
                        sb.Append(date.ToString("mm", CultureInfo.InvariantCulture));
                        break;
                    case 'S':
                        sb.Append(date.ToString("ss", CultureInfo.InvariantCulture));
                        break;
                    case 'b':
                        sb.Append(date.ToString("MMM", CultureInfo.InvariantCulture));
                        break;
                    case 'B':
                        sb.Append(date.ToString("MMMM", CultureInfo.InvariantCulture));
                        break;
                    default:
                        sb.Append('%');
                        sb.Append(axisFormat[i]);
                        break;
                }
            }
            else
            {
                sb.Append(axisFormat[i]);
            }
        }

        return sb.ToString();
    }

    static void DrawGridLines(SvgBuilder builder, int totalDays, double offsetX, double height,
        double chartWidth, double dayWidthPx)
    {
        #pragma warning disable IDE0007
        double axisY = height - TOP_PADDING_PX;
        #pragma warning restore IDE0007

        // Vertical grid lines (daily).
        for (var i = 0; i < totalDays; i++)
        {
            var x = offsetX + i * dayWidthPx;
            builder.AddLine(x, GRID_LINE_START_PADDING_PX, x, axisY, stroke: "lightgrey", strokeWidth: 1);
        }

        // Horizontal grid lines across the chart area (Mermaid does this via D3 tickSize).
        var numRows = (int)((axisY - GRID_LINE_START_PADDING_PX) / rowHeight);
        for (var i = 0; i <= numRows; i++)
        {
            var y = TOP_PADDING_PX + i * rowHeight;
            builder.AddLine(offsetX, y, offsetX + chartWidth, y, stroke: "#eee", strokeWidth: 1);
        }
    }

    static void DrawTask(SvgBuilder builder, GanttTask task, DateTime startDate, int row,
        double offsetX, double offsetY, double dayWidthPx, int totalTaskCount, double height, RenderOptions options)
    {
        var y = offsetY + row * rowHeight;
        var startDays = (task.ComputedStart - startDate).Days;
        var durationDays = Math.Max(1, (task.ComputedEnd - task.ComputedStart).Days);

        var taskX = offsetX + startDays * dayWidthPx;
        var taskWidth = durationDays * dayWidthPx;

        // Mermaid labels tasks primarily inside the bar (outside if it does not fit).
        // The left column is reserved for section titles rather than per-task row labels.

        // Determine task colours (Mermaid default theme variables).
        string fillColor;
        string strokeColor;
        double strokeWidth;
        string textColor;
        if (task.IsCritical)
        {
            strokeColor = CRIT_TASK_BORDER_COLOR;
            strokeWidth = 2;

            if (task.Status == GanttTaskStatus.Done)
            {
                fillColor = DONE_TASK_BKG_COLOR;
                textColor = "black";
            }
            else if (task.Status == GanttTaskStatus.Active)
            {
                fillColor = ACTIVE_TASK_BKG_COLOR;
                textColor = "black";
            }
            else
            {
                fillColor = CRIT_TASK_BKG_COLOR;
                textColor = "white";
            }
        }
        else if (task.Status == GanttTaskStatus.Done)
        {
            fillColor = DONE_TASK_BKG_COLOR;
            strokeColor = DONE_TASK_BORDER_COLOR;
            strokeWidth = 2;
            textColor = "black";
        }
        else if (task.Status == GanttTaskStatus.Active)
        {
            fillColor = ACTIVE_TASK_BKG_COLOR;
            strokeColor = TASK_BORDER_COLOR;
            strokeWidth = 2;
            textColor = "black";
        }
        else
        {
            fillColor = TASK_BKG_COLOR;
            strokeColor = TASK_BORDER_COLOR;
            strokeWidth = 2;
            textColor = "white";
        }

        if (task.IsVert)
        {
            // Mermaid: vert marker is a thin rect spanning the chart height, starting at gridLineStartPadding.
            // Width: 0.08 * barHeight.
#pragma warning disable IDE0007
            double vertWidth = 0.08 * taskBarHeight;
            double vertY = GRID_LINE_START_PADDING_PX;
            double vertHeight = (totalTaskCount * rowHeight) + (taskBarHeight * 2);

            builder.AddRect(taskX, vertY, vertWidth, vertHeight,
                rx: 0,
                fill: TASK_BKG_COLOR,
                stroke: TASK_BORDER_COLOR,
                strokeWidth: 1);

            // Label at the bottom, centred on the line.
            double labelY = GRID_LINE_START_PADDING_PX + (totalTaskCount * rowHeight) + 60;
            builder.AddText(taskX, labelY, task.Name,
                anchor: "middle",
                baseline: "middle",
                fontSize: $"{Math.Max(8, options.FontSize - 2)}px",
                fontFamily: options.FontFamily,
                fill: "#000");
#pragma warning restore IDE0007

            return;
        }

        if (task.IsMilestone)
        {
            // Draw milestone as diamond
            var cx = taskX;
            var cy = y + rowHeight / 2;
            var path = $"M {Fmt(cx)} {Fmt(cy - milestoneSize)} " +
                       $"L {Fmt(cx + milestoneSize)} {Fmt(cy)} " +
                       $"L {Fmt(cx)} {Fmt(cy + milestoneSize)} " +
                       $"L {Fmt(cx - milestoneSize)} {Fmt(cy)} Z";
            builder.AddPath(path, fill: milestoneColor, stroke: "#333", strokeWidth: 1);
        }
        else
        {
            // Draw task bar
            var barY = y + (rowHeight - taskBarHeight) / 2;
            builder.AddRect(taskX, barY, taskWidth, taskBarHeight,
                rx: 3,
                fill: fillColor,
                stroke: strokeColor,
                strokeWidth: strokeWidth);

            // Task name inside bar if it likely fits (Word-first heuristic).
            if (!string.IsNullOrWhiteSpace(task.Name))
            {
#pragma warning disable IDE0007
                double FontSizePx = Math.Max(8, options.FontSize - 2);
                // Mermaid measures actual text width and keeps labels inside when they fit.
                // We use a conservative estimate but force inside for sufficiently wide bars to match Mermaid defaults.
                double EstimatedTextWidthPx = task.Name.Length * FontSizePx * 0.62;
                bool IsLikelyToFitInsideBar = taskWidth >= (EstimatedTextWidthPx + 10);
                if (taskWidth >= FORCE_LABEL_INSIDE_BAR_WIDTH_PX)
                {
                    IsLikelyToFitInsideBar = true;
                }

                // Word baseline adjustment: aim for visual centre in Word.
                var barLabelY = barY + taskBarHeight / 2 + GanttWordBarLabelDown(FontSizePx);
                double textX = taskX + taskWidth / 2;
                string textAnchor = "middle";
                if (!IsLikelyToFitInsideBar)
                {
                    textX = taskX + taskWidth + 6;
                    textAnchor = "start";
                }

                builder.AddText(textX, barLabelY, task.Name,
                    anchor: textAnchor,
                    baseline: "middle",
                    fontSize: $"{FontSizePx}px",
                    fontFamily: options.FontFamily,
                    fill: textColor);
#pragma warning restore IDE0007
            }
        }
    }

    static void DrawSectionRowBackgrounds(SvgBuilder builder, GanttModel model, double offsetY, double chartWidth)
    {
#pragma warning disable IDE0007
        int taskRowIndex = 0;
        int sectionIndex = 0;
        foreach (var section in model.Sections)
        {
            var sectionFill = GetSectionFill(sectionIndex);
            // Task rows in this section
            for (int i = 0; i < section.Tasks.Count; i++)
            {
                var y = offsetY + (taskRowIndex + i) * rowHeight - 2;
                builder.AddRect(0, y, LEFT_PADDING_PX + chartWidth + RIGHT_PADDING_PX, rowHeight,
                    fill: sectionFill, stroke: "none", style: SECTION_ROW_FILL_OPACITY_STYLE);
            }

            taskRowIndex += section.Tasks.Count;
            sectionIndex++;
        }
#pragma warning restore IDE0007
    }

    static string GetSectionFill(int sectionIndex)
    {
        var slot = ((sectionIndex % 4) + 4) % 4;
        return slot switch
        {
            0 => SECTION0_ROW_COLOR,
            1 => SECTION1_ROW_COLOR,
            2 => SECTION2_ROW_COLOR,
            _ => SECTION3_ROW_COLOR
        };
    }

    static double EstimateMaxLeftLabelWidthPx(GanttModel model, RenderOptions options)
    {
        // Very rough estimate (Word-first) to avoid clipping long section titles.
#pragma warning disable IDE0007
        double fontSizePx = options.FontSize;
        double maxWidth = 0;

        foreach (var section in model.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Name))
            {
                maxWidth = Math.Max(maxWidth, EstimateTextWidthPx(section.Name, fontSizePx, isBold: true));
            }

            foreach (var task in section.Tasks)
            {
                if (!string.IsNullOrWhiteSpace(task.Name))
                {
                    maxWidth = Math.Max(maxWidth, EstimateTextWidthPx(task.Name, Math.Max(8, options.FontSize - 2), isBold: false));
                }
            }
        }

        return maxWidth;
#pragma warning restore IDE0007
    }

    static double EstimateTextWidthPx(string text, double fontSizePx, bool isBold)
    {
        // Word text metrics can be wider than 0.6em per character for these fonts; keep a generous estimate.
#pragma warning disable IDE0007
        double weightFactor = isBold ? 1.08 : 1.0;
        return text.Length * fontSizePx * 0.75 * weightFactor;
#pragma warning restore IDE0007
    }

    static double EstimateMaxRightLabelWidthPx(GanttModel model, RenderOptions options)
    {
        // Task labels placed outside use the smaller bar label font size.
#pragma warning disable IDE0007
        double fontSizePx = Math.Max(8, options.FontSize - 2);
        double maxWidth = 0;

        foreach (var section in model.Sections)
        {
            foreach (var task in section.Tasks)
            {
                if (!string.IsNullOrWhiteSpace(task.Name))
                {
                    maxWidth = Math.Max(maxWidth, EstimateTextWidthPx(task.Name, fontSizePx, isBold: false));
                }
            }
        }

        return maxWidth;
#pragma warning restore IDE0007
    }

    static List<GanttTask> ComputeTaskDates(GanttModel model)
    {
        var allTasks = model.Sections.SelectMany(_ => _.Tasks).ToList();
        var taskMap = allTasks.Where(_ => _.Id != null).ToDictionary(_ => _.Id!, _ => _);

        // Default start date if none specified
        var defaultStart = DateTime.Today;

        // First pass: compute tasks without dependencies
        foreach (var task in allTasks)
        {
            if (task.StartDate.HasValue)
            {
                task.ComputedStart = task.StartDate.Value;
            }
            else if (string.IsNullOrEmpty(task.AfterTaskId))
            {
                task.ComputedStart = defaultStart;
            }

            if (task.EndDate.HasValue)
            {
                task.ComputedEnd = task.EndDate.Value;
            }
            else if (task.Duration.HasValue)
            {
                task.ComputedEnd = task.ComputedStart.Add(task.Duration.Value);
            }
            else
            {
                task.ComputedEnd = task.ComputedStart.AddDays(1);
            }
        }

        // Second pass: resolve dependencies
        var changed = true;
        var maxIterations = 100;
        while (changed && maxIterations-- > 0)
        {
            changed = false;
            foreach (var task in allTasks)
            {
                if (!string.IsNullOrEmpty(task.AfterTaskId))
                {
                    if (taskMap.TryGetValue(task.AfterTaskId, out var dependsOn))
                    {
                        var newStart = dependsOn.ComputedEnd;
                        if (newStart != task.ComputedStart)
                        {
                            task.ComputedStart = newStart;
                            if (task.Duration.HasValue)
                            {
                                task.ComputedEnd = task.ComputedStart.Add(task.Duration.Value);
                            }
                            else if (!task.EndDate.HasValue)
                            {
                                task.ComputedEnd = task.ComputedStart.AddDays(1);
                            }
                            changed = true;
                        }
                    }
                }
            }
        }

        return allTasks;
    }

    static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
