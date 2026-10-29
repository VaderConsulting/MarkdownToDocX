using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class SourceLineMapTests
{
    private const string Accepted = "a\nb\nc\nd\ne\n";

    [TestCase("a\nb\nc\nd\ne\n", 3, 3)]
    [TestCase("a\nb\nX\nd\ne\n", 3, 3)]
    [TestCase("a\nb\nc\nd\ne\n%% tail\n", 5, 5)]
    [TestCase("a\nc\nd\ne\n", 4, 3)]
    [TestCase("a\nb\nnew\nc\nd\ne\n", 5, 6)]
    [TestCase("a\r\nb\r\nc\r\nd\r\ne\r\n", 4, 4)]
    public void MapsAcceptedLinesOntoTheCurrentText(string Current, int AcceptedLine, int Expected)
        => Assert.That(SourceLineMap.Map(Accepted, Current, AcceptedLine), Is.EqualTo(Expected));

    [Test]
    public void ReturnsNullWhenTheChangedRegionHasNoLineForLineCorrespondence()
    {
        Assert.That(SourceLineMap.Map(Accepted, "a\nb\nX\nY\nd\ne\n", 3), Is.Null);
        Assert.That(SourceLineMap.Map(Accepted, Accepted, 99), Is.Null);
        Assert.That(SourceLineMap.Map(Accepted, "a\ne\n", 3), Is.Null);
    }
}
