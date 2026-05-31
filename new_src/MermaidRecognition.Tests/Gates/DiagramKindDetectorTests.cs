using MermaidRecognition.Tests.Gates;

namespace MermaidRecognition.Tests.Gates;

[TestFixture]
public sealed class DiagramKindDetectorTests
{
    [TestCaseSource(nameof(ProgrammeSmokeCases))]
    public void Detect_ProgrammeSmoke_MatchesNormalizedKind(string ExpectedProgrammeKind, string Source)
    {
        string Preprocessed = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(Preprocessed, null);

        Assert.That(Detection, Is.Not.Null);
        Assert.That(Detection.ProgrammeKindId, Is.EqualTo(ExpectedProgrammeKind));
    }

    [Test]
    public void Detect_FlowchartV2Header_UsesFlowchartProgrammeKind()
    {
        string Source = "flowchart LR\n    A --> B";
        string Preprocessed = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(Preprocessed, null);

        Assert.That(Detection, Is.Not.Null);
        Assert.That(Detection.MermaidDetectorId, Is.EqualTo("flowchart-v2"));
        Assert.That(Detection.ProgrammeKindId, Is.EqualTo("flowchart"));
    }

    [Test]
    public void Detect_LegacyGraphHeader_UsesLegacyFlowchartDetector()
    {
        string Source = "graph LR\n    A --> B";
        string Preprocessed = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(Preprocessed, null);

        Assert.That(Detection, Is.Not.Null);
        Assert.That(Detection.MermaidDetectorId, Is.EqualTo("flowchart"));
        Assert.That(Detection.ProgrammeKindId, Is.EqualTo("flowchart"));
    }

    [Test]
    public void Detect_PieBeforeFlowchart_MatchesPie()
    {
        string Source = "pie\n    \"X\" : 1";
        string Preprocessed = RecognitionSourcePreprocessor.PrepareForDetection(Source);
        DiagramDetectionResult? Detection = DiagramKindDetector.TryDetect(Preprocessed, null);

        Assert.That(Detection, Is.Not.Null);
        Assert.That(Detection.MermaidDetectorId, Is.EqualTo("pie"));
    }

    static IEnumerable<object[]> ProgrammeSmokeCases()
    {
        return L1SmokeSamples.ProgrammeKindAndSource();
    }
}
