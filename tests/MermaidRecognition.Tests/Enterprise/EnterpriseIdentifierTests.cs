using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseIdentifierTests
{
    [TestCase("order-api")]
    [TestCase("2026-orders")]
    [TestCase("注文")]
    [TestCase("e\u0301quipe")]
    [TestCase("_adapter.v2")]
    [TestCase("directionService")]
    public void BareIdentifier_IsSharedByDeclarationsContainersAndRelationships(string Id)
    {
        var Result = EnterpriseParser.Parse($"""
            enterprise
              group {Id} "Container"
              application-component child "Child" in {Id}
              {Id} -aggregation-> child
            """);
        Assert.That(Result.HasErrors, Is.False, string.Join("; ", Result.Diagnostics.Select(Item => Item.Message)));
        Assert.That(Result.Document.Elements["child"].ParentId, Is.EqualTo(Id));
        Assert.That(Result.Document.Relationships.Single().SourceId, Is.EqualTo(Id));
    }

    [TestCase(".hidden")]
    [TestCase("-hidden")]
    [TestCase("\"two words\"")]
    public void InvalidBareIdentifier_IsRejected(string Id)
    {
        Assert.That(EnterpriseParser.Parse($"enterprise\napplication-component {Id}").HasErrors, Is.True);
    }
}
