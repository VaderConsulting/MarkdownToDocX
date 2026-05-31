using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests;

/// <summary>
/// Writes <c>.mmd</c> and <c>.verified.json</c> pairs for non-flowchart diagram kinds.
/// </summary>
internal static class KindFixtureFiles
{
    public static void Write(string BaseDirectory, string BaseName, string Source)
    {
        DiagramContract Contract = MermaidRecognizer.Parse(Source);
        ContractSnapshot Snapshot = ContractSnapshot.FromDiagram(Contract);
        File.WriteAllText(
            Path.Combine(BaseDirectory, BaseName + ".verified.json"),
            Snapshot.ToJson());
        File.WriteAllText(
            Path.Combine(BaseDirectory, BaseName + ".mmd"),
            Source.TrimEnd() + Environment.NewLine);
    }
}
