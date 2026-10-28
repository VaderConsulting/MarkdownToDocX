using NUnit.Framework;

namespace MermaidRecognition.Layout.Tests;

/// <summary>Retains accepted candidate identities in a failing normal test's output without changing its assertions.</summary>
[SetUpFixture]
public sealed class FinalRepairFailureTraceSetup
{
    /// <summary>Attaches a read-only trace to the final-only decision boundary.</summary>
    [OneTimeSetUp]
    public void Begin() => DiagramGridLayout.FinalRepairObserver = (Key, Reason, ExtraLength) =>
    {
        if (Reason == "accepted")
        {
            TestContext.Out.WriteLine($"FINAL_ACCEPTED|{Key}|extraLength={ExtraLength:R}");
        }
    };

    /// <summary>Restores the production observer state after all normal tests finish.</summary>
    [OneTimeTearDown]
    public void End() => DiagramGridLayout.FinalRepairObserver = null;
}
