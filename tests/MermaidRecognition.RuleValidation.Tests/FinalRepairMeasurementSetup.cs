using MermaidRecognition.Layout;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Scopes an explicit local cap experiment to the final pass without changing production configuration or any checker.</summary>
[SetUpFixture]
public sealed class FinalRepairMeasurementSetup
{
    /// <summary>Reads only the measurement harness switch; an ordinary audit retains the canonical 61 px value.</summary>
    [OneTimeSetUp]
    public void Begin() => DiagramGridLayout.FinalRepairLengthCapForTests =
        double.TryParse(Environment.GetEnvironmentVariable("FINAL_REPAIR_CAP"), out var Cap) ? Cap : null;

    /// <summary>Prevents an override from leaking into any subsequent run in the same test host.</summary>
    [OneTimeTearDown]
    public void End() => DiagramGridLayout.FinalRepairLengthCapForTests = null;
}
