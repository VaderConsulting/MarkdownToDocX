using MermaidRecognition.Core;
using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Preserves membership lookup semantics while a port-only repair reuses immutable declarations.</summary>
[TestFixture]
public sealed class FixedMembershipLookupTests
{
    /// <summary>Nested scopes restore their owner and never supply cached answers for another contract.</summary>
    [Test]
    public void FixedScopesKeepExactMembershipAndIsolateOtherContracts()
    {
        var First = MermaidRecognizer.Parse("flowchart TB\nsubgraph outer\nsubgraph inner\nx[X]\nend\ny[Y]\nend\nz[Z]");
        var Second = MermaidRecognizer.Parse("flowchart TB\nsubgraph elsewhere\nx[X]\nend");
        string[] Ids = First.Nodes.Select(Node => Node.Id).Concat(First.Groups.Select(Group => Group.Id)).Append("absent").ToArray();
        var Expected = Ids.ToDictionary(Id => Id, Id => FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Id, First));
        string? OtherParent = FlowchartSubgraphMembershipHelper.FindInnermostGroupId("x", Second);
        using (FlowchartSubgraphMembershipHelper.ReuseFixedMembership(First))
        {
            foreach (string Id in Ids)
                Assert.That(FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Id, First), Is.EqualTo(Expected[Id]));
            Assert.That(FlowchartSubgraphMembershipHelper.FindInnermostGroupId("x", Second), Is.EqualTo(OtherParent));
            Assert.That(FlowchartSubgraphMembershipHelper.FindInnermostGroupId(null!, First), Is.Null);
            using (FlowchartSubgraphMembershipHelper.ReuseFixedMembership(Second))
            {
                Assert.That(FlowchartSubgraphMembershipHelper.FindInnermostGroupId("x", Second), Is.EqualTo(OtherParent));
                Assert.That(FlowchartSubgraphMembershipHelper.FindInnermostGroupId("x", First), Is.EqualTo(Expected["x"]));
            }
            Assert.That(FlowchartSubgraphMembershipHelper.FindInnermostGroupId("x", First), Is.EqualTo(Expected["x"]));
        }
        foreach (string Id in Ids)
            Assert.That(FlowchartSubgraphMembershipHelper.FindInnermostGroupId(Id, First), Is.EqualTo(Expected[Id]));
    }
}
