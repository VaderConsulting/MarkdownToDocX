namespace MermaidRecognition.Layout;

/// <summary>Dispatches supported native diagram adapters without consuming the legacy fallback.</summary>
public static class NativeDiagramRenderBridge
{
    /// <summary>Returns false when a source needs another renderer; malformed Enterprise input reports its diagnostic.</summary>
    public static bool TryRenderSvg(string Source, out string Svg)
    {
        if (Enterprise.EnterpriseRenderBridge.TryRenderEnterpriseSvg(Source, out Svg)
            || NativePieRenderBridge.TryRenderSvg(Source, out Svg)) return true;
        if (System.Text.RegularExpressions.Regex.IsMatch(Source, @"^\s*(?:%%[^\r\n]*[\r\n]\s*)*pie\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var Contract = MermaidRecognition.Core.MermaidRecognizer.Parse(Source);
            foreach (var Diagnostic in Contract.Diagnostics)
                if (Diagnostic.Code == "recognition.pie.unclassified-line")
                    throw new System.InvalidOperationException("Pie diagram contains an unsupported statement: " + Diagnostic.Message);
            int Sections = 0;
            foreach (var Statement in Contract.SourceStatements)
                if (Statement.LineTypeId == "pie.section") Sections++;
            if (Sections != Contract.Nodes.Count)
                throw new System.InvalidOperationException("Pie diagram contains an invalid section value.");
        }
        return false;
    }
}
