namespace MermaidSharp.Rendering;

/// <summary>
/// Resolved Mermaid theme variables used for styling SVG output.
/// Values align with Mermaid's default theme (<c>packages/mermaid/src/themes/theme-default.js</c>)
/// unless overridden by init config (<c>%%{init: {"themeVariables": {...}}}%%</c>) or render options.
/// </summary>
public sealed class MermaidThemeVariables
{
    /// <summary>Overall diagram background colour.</summary>
    public string Background { get; set; } = "white";

    /// <summary>Main node fill colour (default theme: <c>#ECECFF</c>).</summary>
    public string MainBkg { get; set; } = "#ECECFF";

    /// <summary>Secondary background colour (clusters/subgraphs) (default theme: <c>#ffffde</c>).</summary>
    public string SecondBkg { get; set; } = "#ffffde";

    /// <summary>Primary line/edge colour (default theme: <c>#333333</c>).</summary>
    public string LineColor { get; set; } = "#333333";

    /// <summary>Primary border colour (default theme: <c>#9370DB</c>).</summary>
    public string Border1 { get; set; } = "#9370DB";

    /// <summary>Secondary border colour (default theme: <c>#aaaa33</c>).</summary>
    public string Border2 { get; set; } = "#aaaa33";

    /// <summary>Arrowhead and marker colour (default theme: <c>#333333</c>).</summary>
    public string ArrowheadColor { get; set; } = "#333333";

    /// <summary>Font family applied by Mermaid base styles.</summary>
    public string FontFamily { get; set; } = "\"trebuchet ms\", verdana, arial, sans-serif";

    /// <summary>Base font size, including unit suffix (default theme: <c>16px</c>).</summary>
    public string FontSize { get; set; } = "16px";

    /// <summary>Edge label background (default theme: <c>rgba(232,232,232, 0.8)</c>).</summary>
    public string LabelBackground { get; set; } = "rgba(232,232,232, 0.8)";

    /// <summary>Default text colour (default theme: <c>#333</c>).</summary>
    public string TextColor { get; set; } = "#333";

    /// <summary>Default corner radius (default theme: <c>5</c>).</summary>
    public double Radius { get; set; } = 5.0;

    /// <summary>Default stroke width (default theme: <c>1</c>).</summary>
    public double StrokeWidth { get; set; } = 1.0;

    // Gantt theme variables (theme-default.js).
    public string SectionBkgColor { get; set; } = "rgba(102, 102, 255, 0.49)";
    public string AltSectionBkgColor { get; set; } = "white";
    public string SectionBkgColor2 { get; set; } = "#fff400";
    public string TaskBorderColor { get; set; } = "#534fbc";
    public string TaskBkgColor { get; set; } = "#8a90dd";
    public string TaskTextLightColor { get; set; } = "white";
    public string TaskTextDarkColor { get; set; } = "black";
    public string ActiveTaskBorderColor { get; set; } = "#534fbc";
    public string ActiveTaskBkgColor { get; set; } = "#bfc7ff";
    public string GridColor { get; set; } = "lightgrey";
    public string DoneTaskBkgColor { get; set; } = "lightgrey";
    public string DoneTaskBorderColor { get; set; } = "grey";
    public string CritBorderColor { get; set; } = "#ff8888";
    public string CritBkgColor { get; set; } = "red";
    public string TodayLineColor { get; set; } = "red";
    public string VertLineColor { get; set; } = "navy";
}

