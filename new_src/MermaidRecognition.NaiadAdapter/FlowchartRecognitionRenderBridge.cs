using System.Text;
using MermaidRecognition;
using MermaidRecognition.Detection;
using MermaidSharp;
using MermaidSharp.Rendering;

namespace MermaidRecognition.NaiadAdapter;

/// <summary>
/// Renders flowcharts via recognition contract → Naiad <see cref="FlowchartModel"/> (§11.10 exit glue).
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
    /// </summary>
    /// <param name="Source">Mermaid source (may include leading <c>%%{init}%%</c> blocks).</param>
    /// <param name="Options">Naiad render options.</param>
    /// <param name="Svg">Rendered SVG XML when this method returns true.</param>
    /// <returns>True when recognition + render succeeded for a flowchart.</returns>
    public static bool TryRenderFlowchartSvg(string Source, RenderOptions Options, out string Svg)
    {
        Svg = string.Empty;
        if (!IsFlowchartSource(Source))
        {
            return false;
        }

        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        if (!string.Equals(Contract.Kind, "flowchart", StringComparison.OrdinalIgnoreCase))
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
}
