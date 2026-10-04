using System.Reflection;
using MermaidRecognition;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;
using MermaidRecognition.Render;

namespace MermaidRecognition.Tests.Render;

/// <summary>
/// Stage 11.B: native router must not branch on any edge label text (spec §11.8, §11.13).
/// </summary>
[TestFixture]
public sealed class FlowchartRouterLabelInvarianceTests
{
    private static readonly (string First, string Second)[] LABEL_VARIANTS =
    {
        ("Yes", "No"),
        ("True", "False"),
        ("1", "0"),
        ("Approved", "Rejected"),
        ("", ""),
        ("α", "β"),
        ("very long branch label A", "very long branch label B"),
        ("Done", "End")
    };

    [Test]
    public void G12_LabelInvariance_All_Label_Variants_Produce_Identical_Route_Structure()
    {
        List<List<(FlowchartRouteKind Kind, int SourceBranch, int TargetJoin)>> Signatures =
            new List<List<(FlowchartRouteKind Kind, int SourceBranch, int TargetJoin)>>();

        foreach ((string First, string Second) Variant in LABEL_VARIANTS)
        {
            string Source = BuildDecisionFlowchart(Variant.First, Variant.Second);
            DiagramContract Contract = MermaidRecognizer.Parse(Source);

            Assert.That(FlowchartRouter.TryRouteAll(Contract, out FlowchartRouteResult Result), Is.True);
            Signatures.Add(BuildSignature(Result));
        }

        List<(FlowchartRouteKind Kind, int SourceBranch, int TargetJoin)> Baseline = Signatures[0];

        for (int Index = 1; Index < Signatures.Count; Index++)
        {
            Assert.That(Signatures[Index], Is.EqualTo(Baseline), "Route structure must not depend on edge label text");
        }

        Assert.That(Baseline.Count(Route => Route.Kind == FlowchartRouteKind.DecisionFanOut), Is.EqualTo(2));
    }

    [Test]
    public void G12_LabelInvariance_Mutating_Every_Edge_Label_Preserves_Route_Structure()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "chain-a-b-c.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(FlowchartRouter.TryRouteAll(Contract, out FlowchartRouteResult BaselineResult), Is.True);
        List<(FlowchartRouteKind Kind, int SourceBranch, int TargetJoin)> BaselineSignature =
            BuildSignature(BaselineResult);

        Dictionary<EdgeContract, string?> OriginalLabels = new Dictionary<EdgeContract, string?>();

        foreach (EdgeContract Edge in Contract.Edges)
        {
            OriginalLabels[Edge] = Edge.Label;
            Edge.Label = "mutated-" + Edge.CreationIndex + "-unicode-αβγ";
        }

        try
        {
            Assert.That(FlowchartRouter.TryRouteAll(Contract, out FlowchartRouteResult MutatedResult), Is.True);
            List<(FlowchartRouteKind Kind, int SourceBranch, int TargetJoin)> MutatedSignature =
                BuildSignature(MutatedResult);

            Assert.That(MutatedSignature, Is.EqualTo(BaselineSignature));
        }
        finally
        {
            foreach (KeyValuePair<EdgeContract, string?> Entry in OriginalLabels)
            {
                Entry.Key.Label = Entry.Value;
            }
        }
    }

    [Test]
    public void G12_LabelInvariance_Subgraph_Fixture_Ignores_Edge_Labels()
    {
        string MmdPath = Path.Combine(RecognitionFixturePaths.GetFlowchartFixtureDirectory(), "subgraph-membership.mmd");
        DiagramContract Contract = FlowchartRecognizer.Parse(File.ReadAllText(MmdPath));

        Assert.That(FlowchartRouter.TryRouteAll(Contract, out FlowchartRouteResult BaselineResult), Is.True);
        List<(FlowchartRouteKind Kind, int SourceBranch, int TargetJoin)> BaselineSignature =
            BuildSignature(BaselineResult);

        foreach (EdgeContract Edge in Contract.Edges)
        {
            Edge.Label = null;
        }

        Assert.That(FlowchartRouter.TryRouteAll(Contract, out FlowchartRouteResult ClearedResult), Is.True);
        Assert.That(BuildSignature(ClearedResult), Is.EqualTo(BaselineSignature));
    }

    [Test]
    public void G12_LabelInvariance_StaticGate_Router_Source_Does_Not_Read_Edge_Label_Property()
    {
        Assembly RouterAssembly = typeof(FlowchartRouter).Assembly;
        string RouterSourcePath = Path.Combine(
            FindRepositoryRoot(),
            "new_src",
            "MermaidRecognition",
            "Render",
            "FlowchartRouter.cs");

        Assert.That(File.Exists(RouterSourcePath), Is.True);

        string RouterSource = File.ReadAllText(RouterSourcePath);
        Assert.That(RouterSource, Does.Not.Match(@"\w+\.Label\b"), "FlowchartRouter must not read edge Label property");

        string TopologySourcePath = Path.Combine(
            FindRepositoryRoot(),
            "new_src",
            "MermaidRecognition",
            "Render",
            "FlowchartTopologyIndex.cs");

        string TopologySource = File.ReadAllText(TopologySourcePath);
        Assert.That(TopologySource, Does.Not.Match(@"\w+\.Label\b"), "FlowchartTopologyIndex must not read edge Label property");
    }

    private static string BuildDecisionFlowchart(string FirstLabel, string SecondLabel)
    {
        return "flowchart TD\n"
            + "    Choose{Choose}\n"
            + "    Choose -->|" + FirstLabel + "| Approve[Approved]\n"
            + "    Choose -->|" + SecondLabel + "| Reject[Rejected]\n";
    }

    private static List<(FlowchartRouteKind Kind, int SourceBranch, int TargetJoin)> BuildSignature(
        FlowchartRouteResult Result)
    {
        return Result.Routes
            .OrderBy(Route => Route.SourceId, StringComparer.Ordinal)
            .ThenBy(Route => Route.TargetId, StringComparer.Ordinal)
            .Select(Route => (Route.Kind, Route.SourceBranchIndex, Route.TargetJoinIndex))
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);

        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, "new_src", "MermaidRecognition", "Render", "FlowchartRouter.cs");
            if (File.Exists(Candidate))
            {
                return Current.FullName;
            }

            Current = Current.Parent;
        }

        throw new InvalidOperationException("Could not locate FlowchartRouter.cs from test output directory.");
    }
}
