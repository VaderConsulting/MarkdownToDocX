using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout;
using MermaidRecognition.Layout.Enterprise;
using NUnit.Framework;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Isolates the gap change with the same complete graph, wrap selection, routes and score as the production trial.</summary>
[TestFixture, NonParallelizable, Explicit("Measurement replay of the former extra-cell separator; does not change any rule or audit watchdog.")]
public sealed class EnterpriseBandGapMeasurementTests
{
    /// <summary>Compares each multi-layer Enterprise view with the previous separator, writing results as each view completes.</summary>
    [Test]
    public void CompareOnlyThePreviousBandGap()
    {
        string Capture = Environment.GetEnvironmentVariable("BAND_GAP_CURRENT_CAPTURE") ?? throw new InvalidOperationException("Set BAND_GAP_CURRENT_CAPTURE.");
        string Output = Environment.GetEnvironmentVariable("BAND_GAP_MEASUREMENT") ?? throw new InvalidOperationException("Set BAND_GAP_MEASUREMENT.");
        var Current = File.ReadLines(Capture).Select(Line => JsonDocument.Parse(Line).RootElement.Clone()).ToDictionary(Row => Row.GetProperty("Id").GetString()!);
        var Only = Environment.GetEnvironmentVariable("BAND_GAP_MEASURE_IDS")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        File.WriteAllText(Output, "");
        foreach (var Scenario in RouteRuleCatalogueAuditTests.Catalogue().Select(Case => (RouteRuleCatalogueAuditTests.Scenario)Case.Arguments[0]!).Where(Scenario => Scenario.Enterprise))
        {
            if (Only != null && !Only.Contains(Scenario.Id)) continue;
            var Contract = EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!);
            if (Contract.Nodes.Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
                .Select(Node => EnterpriseElementCatalog.Elements[Enum.Parse<EnterpriseElementType>(((RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)].Layer).Distinct().Count() <= 1) continue;
            if (!Current.ContainsKey(Scenario.Id))
            {
                File.AppendAllText(Output, JsonSerializer.Serialize(new { Scenario.Id, Skipped = "No completed normal-gap audit capture after the watchdog stop." }) + Environment.NewLine);
                continue;
            }
            var Timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (!FlowchartSugiyamaLayoutEngine.TryLayout(Contract, new() { UseCellRegions = true, UsePreviousEnterpriseBandPaddingForTests = true }, out var Layout)) continue;
                var Score = FlowchartLayoutScore.Measure(Contract, Layout, false);
                var Row = Current[Scenario.Id];
                File.AppendAllText(Output, JsonSerializer.Serialize(new { Scenario.Id, PreviousPaddingEmpty = Score.EmptyShare,
                    NormalGapEmpty = Row.GetProperty("Score").GetProperty("EmptyShare").GetDouble(), Seconds = Timer.Elapsed.TotalSeconds }) + Environment.NewLine);
            }
            catch (Exception Failure)
            {
                File.AppendAllText(Output, JsonSerializer.Serialize(new { Scenario.Id, Error = Failure.GetType().Name + ": " + Failure.Message }) + Environment.NewLine);
            }
        }
    }
}
