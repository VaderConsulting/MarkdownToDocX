using System;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

public static partial class EnterpriseSvgRenderer
{
    /// <summary>Renders with immutable per-request options, restoring the caller's ambient state afterwards.</summary>
    public static string Render(EnterpriseDocument Document, DiagramLayoutOptions LayoutOptions)
    {
        ArgumentNullException.ThrowIfNull(LayoutOptions);
        // The existing Enterprise layout boundary uses the ambient bridge. Keep
        // its options scoped here; the router passes them explicitly to layout.
        using var Scope = FlowchartRenderContext.Activate(new() { LayoutOptions = LayoutOptions });
        return RenderCore(Document, LayoutOptions);
    }
}
