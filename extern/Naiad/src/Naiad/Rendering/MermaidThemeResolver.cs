using MermaidSharp.Models;

namespace MermaidSharp.Rendering;

/// <summary>
/// Resolves Mermaid theme variables for a diagram instance from init config and render options.
/// </summary>
public static class MermaidThemeResolver
{
#pragma warning disable IDE0007 // Naiad prefers explicit types for clarity and debugging.
#pragma warning disable IDE0022 // Naiad prefers block bodies over expression bodies.
    /// <summary>
    /// Resolves theme variables for the given diagram.
    /// </summary>
    /// <param name="Diagram">The parsed diagram model (contains init config in <see cref="DiagramBase.Config"/>).</param>
    /// <param name="Options">Render options (fallback values when init config does not override).</param>
    public static MermaidThemeVariables Resolve(DiagramBase Diagram, RenderOptions Options)
    {
        MermaidThemeVariables Theme = new MermaidThemeVariables();

        // Allow RenderOptions to override typography defaults without requiring init blocks.
        if (!string.IsNullOrWhiteSpace(Options.FontFamily))
        {
            Theme.FontFamily = NormaliseFontFamilyForCss(Options.FontFamily);
        }

        if (Options.FontSize > 0.0)
        {
            Theme.FontSize = $"{Options.FontSize:0.##}px";
        }

        ApplyThemeVariableOverride(Diagram, Theme);
        return Theme;
    }

    static void ApplyThemeVariableOverride(DiagramBase Diagram, MermaidThemeVariables Theme)
    {
        // Mermaid init config flattening uses dotted keys, e.g. "themeVariables.lineColor".
        Theme.Background = GetConfigString(Diagram, "themeVariables.background", Theme.Background);
        Theme.MainBkg = GetConfigString(Diagram, "themeVariables.mainBkg", Theme.MainBkg);
        Theme.SecondBkg = GetConfigString(Diagram, "themeVariables.secondBkg", Theme.SecondBkg);
        Theme.LineColor = GetConfigString(Diagram, "themeVariables.lineColor", Theme.LineColor);
        Theme.Border1 = GetConfigString(Diagram, "themeVariables.border1", Theme.Border1);
        Theme.Border2 = GetConfigString(Diagram, "themeVariables.border2", Theme.Border2);
        Theme.ArrowheadColor = GetConfigString(Diagram, "themeVariables.arrowheadColor", Theme.ArrowheadColor);
        Theme.FontFamily = GetConfigString(Diagram, "themeVariables.fontFamily", Theme.FontFamily);
        Theme.FontSize = GetConfigString(Diagram, "themeVariables.fontSize", Theme.FontSize);
        Theme.LabelBackground = GetConfigString(Diagram, "themeVariables.labelBackground", Theme.LabelBackground);
        Theme.TextColor = GetConfigString(Diagram, "themeVariables.textColor", Theme.TextColor);

        Theme.Radius = GetConfigDouble(Diagram, "themeVariables.radius", Theme.Radius);
        Theme.StrokeWidth = GetConfigDouble(Diagram, "themeVariables.strokeWidth", Theme.StrokeWidth);

        // Gantt theme variables (theme-default.js).
        Theme.SectionBkgColor = GetConfigString(Diagram, "themeVariables.sectionBkgColor", Theme.SectionBkgColor);
        Theme.AltSectionBkgColor = GetConfigString(Diagram, "themeVariables.altSectionBkgColor", Theme.AltSectionBkgColor);
        Theme.SectionBkgColor2 = GetConfigString(Diagram, "themeVariables.sectionBkgColor2", Theme.SectionBkgColor2);
        Theme.TaskBorderColor = GetConfigString(Diagram, "themeVariables.taskBorderColor", Theme.TaskBorderColor);
        Theme.TaskBkgColor = GetConfigString(Diagram, "themeVariables.taskBkgColor", Theme.TaskBkgColor);
        Theme.TaskTextLightColor = GetConfigString(Diagram, "themeVariables.taskTextLightColor", Theme.TaskTextLightColor);
        Theme.TaskTextDarkColor = GetConfigString(Diagram, "themeVariables.taskTextDarkColor", Theme.TaskTextDarkColor);
        Theme.ActiveTaskBorderColor = GetConfigString(Diagram, "themeVariables.activeTaskBorderColor", Theme.ActiveTaskBorderColor);
        Theme.ActiveTaskBkgColor = GetConfigString(Diagram, "themeVariables.activeTaskBkgColor", Theme.ActiveTaskBkgColor);
        Theme.GridColor = GetConfigString(Diagram, "themeVariables.gridColor", Theme.GridColor);
        Theme.DoneTaskBkgColor = GetConfigString(Diagram, "themeVariables.doneTaskBkgColor", Theme.DoneTaskBkgColor);
        Theme.DoneTaskBorderColor = GetConfigString(Diagram, "themeVariables.doneTaskBorderColor", Theme.DoneTaskBorderColor);
        Theme.CritBorderColor = GetConfigString(Diagram, "themeVariables.critBorderColor", Theme.CritBorderColor);
        Theme.CritBkgColor = GetConfigString(Diagram, "themeVariables.critBkgColor", Theme.CritBkgColor);
        Theme.TodayLineColor = GetConfigString(Diagram, "themeVariables.todayLineColor", Theme.TodayLineColor);
        Theme.VertLineColor = GetConfigString(Diagram, "themeVariables.vertLineColor", Theme.VertLineColor);
    }

    static string GetConfigString(DiagramBase Diagram, string Key, string Fallback)
    {
        if (!Diagram.Config.TryGetValue(Key, out string? Value) || string.IsNullOrWhiteSpace(Value))
        {
            return Fallback;
        }

        return Value;
    }

    static double GetConfigDouble(DiagramBase Diagram, string Key, double Fallback)
    {
        if (!Diagram.Config.TryGetValue(Key, out string? Value) || string.IsNullOrWhiteSpace(Value))
        {
            return Fallback;
        }

        if (double.TryParse(Value, out double Parsed))
        {
            return Parsed;
        }

        return Fallback;
    }

    static string NormaliseFontFamilyForCss(string FontFamily)
    {
        // Mermaid default includes quoted "trebuchet ms" but allows unquoted input.
        // We keep the input string, but ensure it can be embedded safely in a CSS font-family property.
        return FontFamily.Replace("trebuchet ms", "\"trebuchet ms\"", StringComparison.OrdinalIgnoreCase);
    }
#pragma warning restore IDE0022
#pragma warning restore IDE0007
}

