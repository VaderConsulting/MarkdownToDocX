using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Protects the exact existing stress classification and ordinary 59-second allowance without invoking the layout engine.</summary>
[TestFixture]
public sealed class CatalogueWatchdogPolicyTests
{
    /// <summary>Checks every canonical stress identity and its discovery-visible test description.</summary>
    [Test]
    public void OnlyTheExistingStressCasesReceive180Seconds()
    {
        Assert.That(CatalogueScenarioPolicy.StressScenarios.Count, Is.EqualTo(27));
        foreach (string Id in CatalogueScenarioPolicy.StressScenarios)
        {
            Assert.That(CatalogueScenarioPolicy.WatchdogSeconds(Id), Is.EqualTo(180));
            TestCaseData Case = CatalogueScenarioPolicy.Apply(new TestCaseData(Id), Id);
            Assert.That(Case.Properties.Get("CatalogueWatchdogSeconds"), Is.EqualTo(180));
            Assert.That(Case.Properties.Get("Description"), Does.Contain("Stress watchdog: 180 seconds"));
        }
    }

    /// <summary>Checks normal scenarios, including identities whose number matches a stress case in a different catalogue.</summary>
    /// <param name="Id">Non-stress identity or ordinary fixture.</param>
    [TestCase("F01"), TestCase("F142"), TestCase("EIP142"), TestCase("A113"), TestCase("A114"), TestCase("A117"), TestCase(null)]
    public void NonStressCasesKeep59Seconds(string? Id)
        => Assert.That(CatalogueScenarioPolicy.WatchdogSeconds(Id), Is.EqualTo(59));
}
