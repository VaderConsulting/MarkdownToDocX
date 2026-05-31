using System.Text.RegularExpressions;

namespace MermaidRecognition.Detection;

/// <summary>
/// Detects diagram kind using vendored Mermaid detector order
/// (<c>diagram-orchestration.ts</c> <c>registerLazyLoadedDiagrams</c>).
/// </summary>
public static class DiagramKindDetector
{
    private delegate bool DetectorFunc(string Text, MermaidDetectorConfig? Config);

    private sealed class DetectorEntry
    {
        public DetectorEntry(string MermaidDetectorId, DetectorFunc Detector)
        {
            _MermaidDetectorId = MermaidDetectorId;
            _Detector = Detector;
        }

        private readonly string _MermaidDetectorId;

        public string MermaidDetectorId
        {
            get
            {
                return _MermaidDetectorId;
            }
        }

        private readonly DetectorFunc _Detector;

        public bool TryDetect(string Text, MermaidDetectorConfig? Config)
        {
            return _Detector(Text, Config);
        }
    }

    private static readonly DetectorEntry[] OrderedDetectors = BuildOrderedDetectors();

    /// <summary>
    /// Attempts to detect the diagram kind from preprocessed source text.
    /// </summary>
    public static DiagramDetectionResult? TryDetect(string PreprocessedSource, MermaidDetectorConfig? Config)
    {
        string Text = PreprocessedSource;
        foreach (DetectorEntry Entry in OrderedDetectors)
        {
            if (Entry.TryDetect(Text, Config))
            {
                DiagramDetectionResult Result = new DiagramDetectionResult();
                Result.MermaidDetectorId = Entry.MermaidDetectorId;
                Result.ProgrammeKindId = RecognitionProgramme.NormalizeDetectorId(Entry.MermaidDetectorId);
                return Result;
            }
        }

        return null;
    }

    static DetectorEntry[] BuildOrderedDetectors()
    {
        List<DetectorEntry> Detectors = new List<DetectorEntry>();

        Detectors.Add(new DetectorEntry("error", (Text, _) => Text.Trim().Equals("error", StringComparison.OrdinalIgnoreCase)));
        Detectors.Add(new DetectorEntry("---", (Text, _) => Text.TrimStart().StartsWith("---", StringComparison.Ordinal)));

        Detectors.Add(new DetectorEntry("flowchart-elk", FlowchartElkDetector));
        Detectors.Add(new DetectorEntry("c4", (Text, _) => Regex.IsMatch(Text, @"^\s*C4Context|C4Container|C4Component|C4Dynamic|C4Deployment", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("kanban", (Text, _) => Regex.IsMatch(Text, @"^\s*kanban", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("classDiagram", ClassDiagramV2Detector));
        Detectors.Add(new DetectorEntry("class", ClassDiagramLegacyDetector));
        Detectors.Add(new DetectorEntry("erDiagram", (Text, _) => Regex.IsMatch(Text, @"^\s*erDiagram", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("gantt", (Text, _) => Regex.IsMatch(Text, @"^\s*gantt", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("info", (Text, _) => Regex.IsMatch(Text, @"^\s*info", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("pie", (Text, _) => Regex.IsMatch(Text, @"^\s*pie", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("requirement", (Text, _) => Regex.IsMatch(Text, @"^\s*requirement(Diagram)?", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("sequence", (Text, _) => Regex.IsMatch(Text, @"^\s*sequenceDiagram", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("flowchart-v2", FlowchartV2Detector));
        Detectors.Add(new DetectorEntry("flowchart", FlowchartLegacyDetector));
        Detectors.Add(new DetectorEntry("timeline", (Text, _) => Regex.IsMatch(Text, @"^\s*timeline", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("gitGraph", (Text, _) => Regex.IsMatch(Text, @"^\s*gitGraph", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("stateDiagram", StateV2Detector));
        Detectors.Add(new DetectorEntry("state", StateLegacyDetector));
        Detectors.Add(new DetectorEntry("journey", (Text, _) => Regex.IsMatch(Text, @"^\s*journey", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("quadrantChart", (Text, _) => Regex.IsMatch(Text, @"^\s*quadrantChart", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("sankey", (Text, _) => Regex.IsMatch(Text, @"^\s*sankey(-beta)?", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("packet", (Text, _) => Regex.IsMatch(Text, @"^\s*packet(-beta)?", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("xychart", (Text, _) => Regex.IsMatch(Text, @"^\s*xychart(-beta)?", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("block", (Text, _) => Regex.IsMatch(Text, @"^\s*block(-beta)?", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("eventmodeling", (Text, _) => Regex.IsMatch(Text, @"^\s*eventmodeling", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("treeView", (Text, _) => Regex.IsMatch(Text, @"^\s*treeView-beta", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("radar", (Text, _) => Regex.IsMatch(Text, @"^\s*radar-beta", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("ishikawa", (Text, _) => Regex.IsMatch(Text, @"^\s*ishikawa(-beta)?\b", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("treemap", (Text, _) => Regex.IsMatch(Text, @"^\s*treemap", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("venn", (Text, _) => Regex.IsMatch(Text, @"^\s*venn-beta", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("wardley", (Text, _) => Regex.IsMatch(Text, @"^\s*wardley-beta", RegexOptions.IgnoreCase)));

        Detectors.Add(new DetectorEntry("mindmap", (Text, _) => Regex.IsMatch(Text, @"^\s*mindmap", RegexOptions.IgnoreCase)));
        Detectors.Add(new DetectorEntry("architecture", (Text, _) => Regex.IsMatch(Text, @"^\s*architecture", RegexOptions.IgnoreCase)));

        return Detectors.ToArray();
    }

    static bool FlowchartElkDetector(string Text, MermaidDetectorConfig? Config)
    {
        if (Regex.IsMatch(Text, @"^\s*flowchart-elk", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(Text, @"^\s*(flowchart|graph)", RegexOptions.IgnoreCase)
            && string.Equals(Config?.FlowchartDefaultRenderer, "elk", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    static bool FlowchartV2Detector(string Text, MermaidDetectorConfig? Config)
    {
        if (string.Equals(Config?.FlowchartDefaultRenderer, "dagre-d3", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Regex.IsMatch(Text, @"^\s*graph", RegexOptions.IgnoreCase)
            && string.Equals(Config?.FlowchartDefaultRenderer, "dagre-wrapper", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Regex.IsMatch(Text, @"^\s*flowchart", RegexOptions.IgnoreCase);
    }

    static bool FlowchartLegacyDetector(string Text, MermaidDetectorConfig? Config)
    {
        if (string.Equals(Config?.FlowchartDefaultRenderer, "dagre-wrapper", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Config?.FlowchartDefaultRenderer, "elk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(Text, @"^\s*graph", RegexOptions.IgnoreCase);
    }

    static bool ClassDiagramV2Detector(string Text, MermaidDetectorConfig? Config)
    {
        if (Regex.IsMatch(Text, @"^\s*classDiagram-v2", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(Text, @"^\s*classDiagram", RegexOptions.IgnoreCase)
            && string.Equals(Config?.ClassDefaultRenderer, "dagre-wrapper", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    static bool ClassDiagramLegacyDetector(string Text, MermaidDetectorConfig? Config)
    {
        if (string.Equals(Config?.ClassDefaultRenderer, "dagre-wrapper", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(Text, @"^\s*classDiagram", RegexOptions.IgnoreCase);
    }

    static bool StateV2Detector(string Text, MermaidDetectorConfig? Config)
    {
        if (Regex.IsMatch(Text, @"^\s*stateDiagram-v2", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (Regex.IsMatch(Text, @"^\s*stateDiagram", RegexOptions.IgnoreCase)
            && string.Equals(Config?.StateDefaultRenderer, "dagre-wrapper", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    static bool StateLegacyDetector(string Text, MermaidDetectorConfig? Config)
    {
        if (string.Equals(Config?.StateDefaultRenderer, "dagre-wrapper", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(Text, @"^\s*stateDiagram", RegexOptions.IgnoreCase);
    }
}
