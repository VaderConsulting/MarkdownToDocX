using System;
using System.IO;
using MermaidRecognition.Contract;
using MermaidRecognition.Layout;
using NUnit.Framework;

/// <summary>Requests the production cell path for catalogue checks, including renderer entry points that create their own context.</summary>
/// <remarks>The scope is nonparallel because renderer defaults use an application switch. Historical unit fixtures retain their separate defaults.</remarks>
[NonParallelizable]
public abstract class ProductionCatalogueTestFixture
{
    private bool PreviousCells;
    private Action<DiagramContract, FlowchartLayoutResult>? PreviousObserver;
    private bool ObserveProgress;

    /// <summary>Enables production layout before each derived test, without changing the engine's defaults.</summary>
    [SetUp]
    public void RequestProductionCataloguePath()
    {
        AppContext.TryGetSwitch(FlowchartRenderContext.CellRegionsSwitch, out PreviousCells);
        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, true);
        Assert.That(new FlowchartRenderContext().UseCellRegions, Is.True);
        string? ProgressPath = Environment.GetEnvironmentVariable("CATALOGUE_PATH_PROGRESS");
        ObserveProgress = !string.IsNullOrWhiteSpace(ProgressPath);
        if (ObserveProgress)
        {
            PreviousObserver = DiagramGridLayout.PublishedLayoutObserver;
            DiagramGridLayout.PublishedLayoutObserver = (Contract, Layout) =>
            {
                PreviousObserver?.Invoke(Contract, Layout);
                File.AppendAllText(ProgressPath!, TestContext.CurrentContext.Test.FullName + "|cells=" + Layout.RenderContext.UseCellRegions + Environment.NewLine);
            };
        }
    }

    /// <summary>Restores the test host's previous mode even when a catalogue assertion fails.</summary>
    [TearDown]
    public void RestoreCataloguePath()
    {
        if (ObserveProgress)
        {
            DiagramGridLayout.PublishedLayoutObserver = PreviousObserver;
        }

        AppContext.SetSwitch(FlowchartRenderContext.CellRegionsSwitch, PreviousCells);
    }
}
