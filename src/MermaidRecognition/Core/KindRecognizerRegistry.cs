using System.Text.RegularExpressions;

using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Kinds.Er;
using MermaidRecognition.Kinds.LinePattern;
using MermaidRecognition.Kinds.Pie;
using MermaidRecognition.Kinds.Sequence;
using MermaidRecognition.Kinds.Class;
using MermaidRecognition.Kinds.GitGraph;
using MermaidRecognition.Kinds.Gantt;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Kinds.Mindmap;
using MermaidRecognition.Kinds.State;

namespace MermaidRecognition.Core;

/// <summary>
/// Maps Mermaid detector ids to kind recognisers.
/// </summary>
public static class KindRecognizerRegistry
{
    private static readonly Dictionary<string, IKindRecognizer> RecognizersByDetectorId = BuildRegistry();

    /// <summary>
    /// Resolves the recogniser for a detected Mermaid diagram id.
    /// </summary>
    public static IKindRecognizer Get(DiagramDetectionResult Detection)
    {
        if (RecognizersByDetectorId.TryGetValue(Detection.MermaidDetectorId, out IKindRecognizer? Recognizer))
        {
            return Recognizer;
        }

        string ProgrammeKindId = Detection.ProgrammeKindId;
        if (RecognizersByDetectorId.TryGetValue(ProgrammeKindId, out IKindRecognizer? ProgrammeRecognizer))
        {
            return ProgrammeRecognizer;
        }

        return CreateFallback(ProgrammeKindId);
    }

    static Dictionary<string, IKindRecognizer> BuildRegistry()
    {
        Dictionary<string, IKindRecognizer> Registry = new Dictionary<string, IKindRecognizer>(StringComparer.OrdinalIgnoreCase);

        FlowchartKindRecognizer Flowchart = new FlowchartKindRecognizer();
        Registry["flowchart"] = Flowchart;
        Registry["flowchart-v2"] = Flowchart;
        Registry["flowchart-elk"] = Flowchart;
        Registry["graph"] = Flowchart;

        Registry["pie"] = new PieKindRecognizer();
        Registry["sequence"] = new SequenceKindRecognizer();

        ClassKindRecognizer ClassRecognizer = new ClassKindRecognizer();
        Registry["classDiagram"] = ClassRecognizer;
        Registry["class"] = ClassRecognizer;

        StateKindRecognizer StateRecognizer = new StateKindRecognizer();
        Registry["stateDiagram"] = StateRecognizer;
        Registry["state"] = StateRecognizer;

        Registry["erDiagram"] = new ErKindRecognizer();
        Registry["enterprise"] = new EnterpriseKindRecognizer();
        RegisterLinePatternWithMapper(
            Registry,
            "gantt",
            @"^\s*gantt",
            GanttContractMapper.Apply,
            KindLinePatternProfiles.TitlePattern("gantt"),
            KindLinePatternProfiles.AccTitlePattern("gantt"),
            KindLinePatternProfiles.AccDescrPattern("gantt"),
            GanttDateFormatPattern(),
            GanttInclusiveEndDatesPattern(),
            GanttExcludesPattern(),
            GanttSectionPattern(),
            GanttTaskPattern());
        RegisterLinePatternWithMapper(
            Registry,
            "gitGraph",
            @"^\s*gitGraph",
            GitGraphContractMapper.Apply,
            KindLinePatternProfiles.AccTitlePattern("gitGraph"),
            GitGraphAccDescrPattern(),
            GitGraphCommitPattern(),
            GitGraphMergePattern(),
            GitGraphBranchPattern(),
            GitGraphCheckoutPattern(),
            GitGraphSwitchPattern());
        RegisterLinePatternWithMapper(
            Registry,
            "mindmap",
            @"^\s*mindmap",
            MindmapContractMapper.Apply,
            MindmapNodePattern());
        RegisterLinePattern(Registry, "timeline", @"^\s*timeline", KindRecognizerPatterns.ForTimeline());
        RegisterLinePattern(Registry, "journey", @"^\s*journey", KindRecognizerPatterns.ForJourney());
        RegisterLinePattern(Registry, "quadrantChart", @"^\s*quadrantChart", KindRecognizerPatterns.ForQuadrantChart());
        RegisterLinePattern(Registry, "xychart", @"^\s*xychart", KindRecognizerPatterns.ForXychart());
        RegisterLinePattern(
            Registry,
            "requirement",
            @"^\s*requirementDiagram",
            KindRecognizerPatterns.ForRequirement());
        RegisterLinePattern(
            Registry,
            "architecture",
            @"^\s*architecture",
            KindRecognizerPatterns.ForArchitecture());
        RegisterLinePattern(Registry, "kanban", @"^\s*kanban", KindRecognizerPatterns.ForKanban());
        RegisterLinePattern(Registry, "sankey", @"^\s*sankey", KindRecognizerPatterns.ForSankey());
        RegisterLinePattern(Registry, "block", @"^\s*block", KindRecognizerPatterns.ForBlock());
        RegisterLinePattern(Registry, "packet", @"^\s*packet", KindRecognizerPatterns.ForPacket());
        RegisterLinePattern(Registry, "radar", @"^\s*radar", KindRecognizerPatterns.ForRadar());
        RegisterLinePattern(Registry, "treemap", @"^\s*treemap", KindRecognizerPatterns.ForTreemap());
        RegisterLinePattern(Registry, "c4", @"^\s*C4", KindRecognizerPatterns.ForC4());

        RegisterMermaidOnly(Registry, "eventmodeling", @"^\s*eventmodeling", KindRecognizerPatterns.ForEventmodeling());
        RegisterMermaidOnly(Registry, "treeView", "treeview", @"^\s*treeView-beta", KindRecognizerPatterns.ForTreeview());
        RegisterMermaidOnly(Registry, "ishikawa", @"^\s*ishikawa", KindRecognizerPatterns.ForIshikawa());
        RegisterMermaidOnly(Registry, "venn", @"^\s*venn", KindRecognizerPatterns.ForVenn());
        RegisterMermaidOnly(Registry, "wardley", "wardley", @"^\s*wardley-beta", KindRecognizerPatterns.ForWardley());
        RegisterMermaidOnly(Registry, "info", @"^\s*info", KindRecognizerPatterns.ForInfo());

        Registry["---"] = new YamlFrontMatterKindRecognizer();

        return Registry;
    }

    static void RegisterMermaidOnly(
        Dictionary<string, IKindRecognizer> Registry,
        string DetectorKey,
        string HeaderPattern,
        params KindLinePattern[] ExtraPatterns)
    {
        string ProgrammeKindId = RecognitionProgramme.NormalizeDetectorId(DetectorKey);
        MermaidOnlyKindRecognizer Recognizer = new MermaidOnlyKindRecognizer(
            ProgrammeKindId,
            HeaderPattern,
            ExtraPatterns);
        Registry[DetectorKey] = Recognizer;
        if (!string.Equals(DetectorKey, ProgrammeKindId, StringComparison.OrdinalIgnoreCase))
        {
            Registry[ProgrammeKindId] = Recognizer;
        }
    }

    static void RegisterMermaidOnly(
        Dictionary<string, IKindRecognizer> Registry,
        string DetectorKey,
        string ProgrammeKindId,
        string HeaderPattern,
        params KindLinePattern[] ExtraPatterns)
    {
        MermaidOnlyKindRecognizer Recognizer = new MermaidOnlyKindRecognizer(
            ProgrammeKindId,
            HeaderPattern,
            ExtraPatterns);
        Registry[DetectorKey] = Recognizer;
        Registry[ProgrammeKindId] = Recognizer;
    }

    static void RegisterLinePattern(
        Dictionary<string, IKindRecognizer> Registry,
        string Key,
        string HeaderPattern,
        params KindLinePattern[] ExtraPatterns)
    {
        string ProgrammeKindId = RecognitionProgramme.NormalizeDetectorId(Key);
        LinePatternKindRecognizer Recognizer = KindLinePatternProfiles.CreateDefault(ProgrammeKindId, HeaderPattern, ExtraPatterns);
        Registry[Key] = Recognizer;
    }

    static void RegisterLinePatternWithMapper(
        Dictionary<string, IKindRecognizer> Registry,
        string Key,
        string HeaderPattern,
        Action<DiagramContract> PostParseMapper,
        params KindLinePattern[] ExtraPatterns)
    {
        string ProgrammeKindId = RecognitionProgramme.NormalizeDetectorId(Key);
        LinePatternKindRecognizer Recognizer = KindLinePatternProfiles.CreateDefault(
            ProgrammeKindId,
            HeaderPattern,
            PostParseMapper,
            ExtraPatterns);
        Registry[Key] = Recognizer;
    }

    static KindLinePattern GanttSectionPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gantt.section",
            Pattern = new Regex(@"^\s*section\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GanttDateFormatPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gantt.dateFormat",
            Pattern = new Regex(@"^\s*dateFormat\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GanttInclusiveEndDatesPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gantt.inclusiveEndDates",
            Pattern = new Regex(@"^\s*inclusiveEndDates\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GanttExcludesPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gantt.excludes",
            Pattern = new Regex(@"^\s*excludes\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GanttTaskPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gantt.task",
            Pattern = new Regex(@"^\s*\w.+\s*:\s*\w", RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GitGraphCommitPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gitGraph.commit",
            Pattern = new Regex(@"^\s*commit\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GitGraphBranchPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gitGraph.branch",
            Pattern = new Regex(@"^\s*branch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GitGraphCheckoutPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gitGraph.branch",
            Pattern = new Regex(@"^\s*checkout\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GitGraphSwitchPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gitGraph.branch",
            Pattern = new Regex(@"^\s*switch\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GitGraphMergePattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gitGraph.commit",
            Pattern = new Regex(@"^\s*merge\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern GitGraphAccDescrPattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "gitGraph.accDescr",
            Pattern = new Regex(@"^\s*accDescr\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };
        return Pattern;
    }

    static KindLinePattern MindmapNodePattern()
    {
        KindLinePattern Pattern = new KindLinePattern
        {
            LineTypeId = "mindmap.node",
            Pattern = new Regex(@"^\s{2,}\S", RegexOptions.Compiled)
        };
        return Pattern;
    }

    static IKindRecognizer CreateFallback(string ProgrammeKindId)
    {
        LinePatternKindRecognizer Recognizer = KindLinePatternProfiles.CreateDefault(
            ProgrammeKindId,
            @"^\s*\S+");
        return Recognizer;
    }
}
