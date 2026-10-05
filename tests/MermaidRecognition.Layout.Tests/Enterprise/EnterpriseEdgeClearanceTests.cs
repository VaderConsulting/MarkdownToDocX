using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.Layout.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseEdgeClearanceTests
{
    [TestCase("01-application-layer.mmd")]
    [TestCase("02-technology-layer.mmd")]
    [TestCase("03-cross-layer-webshop.mmd")]
    [TestCase("06-business-layer.mmd")]
    [TestCase("07-motivation.mmd")]
    [TestCase("08-strategy.mmd")]
    [TestCase("09-physical.mmd")]
    [TestCase("10-implementation-migration.mmd")]
    [TestCase("11-grouping-location-junction.mmd")]
    [TestCase("12-motivation-to-technology.mmd")]
    public void ValidSamples_EdgesDoNotCrossNonEndpointNodes(string FileName)
    {
        string Source = ReadSample(FileName);
        IReadOnlyList<string> Violations = EnterpriseEdgeClearance.FindViolations(Source);
        Assert.That(Violations, Is.Empty, FileName + ":" + Environment.NewLine + string.Join(Environment.NewLine, Violations));
    }

    [Test]
    public void Sample12_HostingDoesNotShareServingAndRealizationEnds()
    {
        IReadOnlyList<string> Violations = EnterpriseEdgeClearance.FindViolations(
            ReadSample("12-motivation-to-technology.mmd"));
        Assert.That(Violations, Is.Empty, string.Join(Environment.NewLine, Violations));
    }

    [Test]
    public void Sample12_TakeOrderRealizationEndsOnSellingBorder()
    {
        IReadOnlyList<string> Violations = EnterpriseEdgeClearance.FindViolations(
            ReadSample("12-motivation-to-technology.mmd"));
        Assert.That(Violations, Is.Empty, string.Join(Environment.NewLine, Violations));
        foreach (string Violation in Violations)
        {
            Assert.That(Violation, Does.Not.Contain("does not end on selling"));
        }
    }

    [Test]
    public void ValidSamples_RespectBendBudgetAndChannelGap()
    {
        string[] Files =
        {
            "01-application-layer.mmd",
            "03-cross-layer-webshop.mmd",
            "11-grouping-location-junction.mmd",
            "12-motivation-to-technology.mmd"
        };
        foreach (string FileName in Files)
        {
            IReadOnlyList<string> Violations = EnterpriseEdgeClearance.FindViolations(ReadSample(FileName));
            Assert.That(Violations, Is.Empty, FileName + ":" + Environment.NewLine + string.Join(Environment.NewLine, Violations));
        }
    }

    private static string ReadSample(string FileName)
    {
        DirectoryInfo? DirectoryCursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (DirectoryCursor != null)
        {
            string Candidate = Path.Combine(DirectoryCursor.FullName, "docs", "enterprise", "samples", FileName);
            if (File.Exists(Candidate))
            {
                return File.ReadAllText(Candidate);
            }

            DirectoryCursor = DirectoryCursor.Parent;
        }

        string Copied = Path.Combine(AppContext.BaseDirectory, "EnterpriseSamples", FileName);
        if (File.Exists(Copied))
        {
            return File.ReadAllText(Copied);
        }

        throw new FileNotFoundException("Sample not found: " + FileName);
    }
}
