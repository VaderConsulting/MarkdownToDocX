using MermaidRecognition.Contract;
using MermaidRecognition.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

public class FlowchartVerifiedJsonTests
{
    [Test]
    public void Chained_A_B_C_D_Matches_Verified_Json()
    {
        string FixturePath = GetFixturePath("chained-a-b-c-d.verified.json");
        if (!File.Exists(FixturePath))
        {
            Assert.Ignore("Fixture not generated yet. Run FlowchartEdgeExpansionFixtures.Write_EdgeExpansion_Snapshots.");
        }

        string Source = File.ReadAllText(GetFixturePath("chained-a-b-c-d.mmd"));
        DiagramContract Actual = FlowchartRecognizer.Parse(Source);
        ContractSnapshot Expected = ContractSnapshot.FromJson(File.ReadAllText(FixturePath));
        ContractSnapshot ActualSnapshot = ContractSnapshot.FromDiagram(Actual);
        Assert.That(ActualSnapshot.ToJson(), Is.EqualTo(Expected.ToJson()));
    }

    static string GetFixturePath(string FileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", FileName);
    }
}
