using System.Text;

using MermaidRecognition.Core;
using MermaidRecognition.Detection;
using MermaidRecognition.Layout;

using MermaidSharp;
using MermaidSharp.Rendering;

namespace MermaidRecognition.NaiadAdapter;

/// <summary>
/// Renders flowcharts via MermaidRecognition — default path uses native Sugiyama layout;
/// legacy Naiad render remains available for comparison and regression tests.
/// </summary>
public static class FlowchartRecognitionRenderBridge
{
    /// <summary>
    /// Returns true when the source text is detected as a flowchart/graph diagram.
    /// </summary>
    public static bool IsFlowchartSource(string Source)
    {
        string DetectionText = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(DetectionText, null);
        if (Detection == null)
        {
            return false;
        }

        string ProgrammeKindId = RecognitionProgramme.NormalizeDetectorId(Detection.ProgrammeKindId);
        return string.Equals(ProgrammeKindId, "flowchart", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses with <see cref="MermaidRecognizer"/> and renders SVG when the kind is flowchart.
    /// Uses native Sugiyama layout by default; falls back to legacy Naiad when native render fails.
    /// </summary>
    /// <param name="Source">Mermaid source (may include leading <c>%%{init}%%</c> blocks).</param>
    /// <param name="Options">Naiad render options (legacy fallback only).</param>
    /// <param name="Svg">Rendered SVG XML when this method returns true.</param>
    /// <returns>True when recognition + render succeeded for a flowchart.</returns>
    public static bool TryRenderFlowchartSvg(string Source, RenderOptions Options, out string Svg) => TryRenderFlowchartSvg(Source, Options, UseNativeSugiyamaRenderer: true, out Svg);

    /// <summary>
    /// Parses with <see cref="MermaidRecognizer"/> and renders SVG when the kind is flowchart.
    /// </summary>
    /// <param name="Source">Mermaid source.</param>
    /// <param name="Options">Naiad render options (legacy fallback only).</param>
    /// <param name="UseNativeSugiyamaRenderer">When true, tries native Sugiyama before legacy Naiad.</param>
    /// <param name="Svg">Rendered SVG XML when this method returns true.</param>
    /// <returns>True when recognition + render succeeded for a flowchart.</returns>
    public static bool TryRenderFlowchartSvg(
        string Source,
        RenderOptions Options,
        bool UseNativeSugiyamaRenderer,
        out string Svg)
    {
        Svg = string.Empty;
        if (!IsFlowchartSource(Source))
        {
            return false;
        }

        if (UseNativeSugiyamaRenderer
            && NativeFlowchartRenderBridge.TryRenderFlowchartSvg(Source, out string NativeSvg))
        {
            Svg = NativeSvg;
            return true;
        }

        return TryRenderLegacyNaiadFlowchartSvg(Source, Options, out Svg);
    }

    /// <summary>
    /// Renders a flowchart via recognition contract mapped to legacy Naiad <see cref="FlowchartModel"/>.
    /// </summary>
    public static bool TryRenderLegacyNaiadFlowchartSvg(string Source, RenderOptions Options, out string Svg)
    {
        Svg = string.Empty;
        if (!IsFlowchartSource(Source))
        {
            return false;
        }

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        if (!IsFlowchartKind(Contract.Kind))
        {
            return false;
        }

        FlowchartModel Model = ContractToFlowchartModel.Map(Contract);
        FlowchartRenderer Renderer = new FlowchartRenderer();
        SvgDocument Document = Renderer.Render(Model, Options ?? RenderOptions.Default);
        StringBuilder Builder = new StringBuilder();
        Document.ToXml(Builder);
        Svg = Builder.ToString();
        return true;
    }

    /// <summary>
    /// Renders a flowchart via the recognition path or throws when the source is not a flowchart.
    /// </summary>
    public static string RenderFlowchartSvg(string Source, RenderOptions? Options)
    {
        if (!TryRenderFlowchartSvg(Source, Options ?? RenderOptions.Default, out string Svg))
        {
            throw new InvalidOperationException("Source is not a flowchart diagram recognised by MermaidRecognition.");
        }

        return Svg;
    }

    private static bool IsFlowchartKind(string? Kind)
    {
        if (string.IsNullOrEmpty(Kind))
        {
            return false;
        }

        return Kind.Equals("flowchart", StringComparison.OrdinalIgnoreCase)
            || Kind.Equals("graph", StringComparison.OrdinalIgnoreCase);
    }
}
