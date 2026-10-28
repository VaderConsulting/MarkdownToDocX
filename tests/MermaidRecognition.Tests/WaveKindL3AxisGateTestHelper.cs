using System.Text.Json;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Tests;

/// <summary>
/// Shared assertions for wave-kind L3 axis gate tests.
/// </summary>
public static class WaveKindL3AxisGateTestHelper
{
    public static void AssertAxis(string FixtureRoot, string[] FixtureNames, Func<DiagramContract, bool> Predicate)
    {
        bool AnyPassed = false;
        List<string> Failures = [];

        foreach (string FixtureName in FixtureNames)
        {
            string MmdPath = Path.Combine(FixtureRoot, FixtureName + ".mmd");
            if (!File.Exists(MmdPath))
            {
                Failures.Add(FixtureName + " (missing .mmd)");
                continue;
            }

            DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
            if (Predicate(Contract))
            {
                AnyPassed = true;
            }
            else
            {
                Failures.Add(FixtureName);
            }
        }

        Assert.That(AnyPassed, Is.True, "No fixture satisfied axis gate: " + string.Join(", ", Failures));
    }

    public static void AssertManifestFixturesParse(string ManifestPath, string FixtureRoot, string ExpectedKindPrefix)
    {
        Assert.That(File.Exists(ManifestPath), Is.True);

        string Json = File.ReadAllText(ManifestPath);
        WaveKindGateManifest? Manifest = JsonSerializer.Deserialize<WaveKindGateManifest>(Json);
        Assert.That(Manifest, Is.Not.Null);
        Assert.That(Manifest!.Fixtures, Is.Not.Empty);

        foreach (string FixtureName in Manifest.Fixtures)
        {
            string MmdPath = Path.Combine(FixtureRoot, FixtureName + ".mmd");
            Assert.That(File.Exists(MmdPath), Is.True, "Missing fixture " + FixtureName);
            DiagramContract Contract = MermaidRecognizer.Parse(File.ReadAllText(MmdPath));
            Assert.That(Contract.Kind, Does.StartWith(ExpectedKindPrefix).Or.EqualTo(ExpectedKindPrefix));
            Assert.That(
                Contract.Diagnostics.Any(Diagnostic => Diagnostic.Severity == MermaidDiagnosticSeverity.Error),
                Is.False,
                "Fixture " + FixtureName + " must not emit error diagnostics.");
        }
    }

    sealed class WaveKindGateManifest
    {
        public List<string> Fixtures { get; set; } = [];
    }
}

