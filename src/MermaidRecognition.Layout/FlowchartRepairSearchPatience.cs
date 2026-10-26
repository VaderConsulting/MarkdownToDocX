namespace MermaidRecognition.Layout;

/// <summary>Stops rule 5.10 candidate search after consecutive non-improving scored trials.</summary>
internal sealed class FlowchartRepairSearchPatience
{
    private readonly int _Limit;
    private int _Stale;

    /// <summary>Creates a counter for the effective patience on this layout pass.</summary>
    /// <param name="EdgeRouteCount">Number of routes on the layout result.</param>
    /// <param name="EnterpriseBands">Whether enterprise band placement is active.</param>
    public FlowchartRepairSearchPatience(int EdgeRouteCount, bool EnterpriseBands)
    {
        _Limit = ResolveLimit(EdgeRouteCount, EnterpriseBands);
    }

    /// <summary>Clears the non-improving streak after a kept candidate.</summary>
    public void Reset()
    {
        _Stale = 0;
    }

    /// <summary>Records one fully scored trial that did not beat the incumbent.</summary>
    /// <returns>True when the configured patience limit is reached and search should stop.</returns>
    public bool RegisterNonImprovement()
    {
        if (_Limit <= 0)
        {
            return false;
        }

        _Stale++;
        return _Stale >= _Limit;
    }

    private static int ResolveLimit(int EdgeRouteCount, bool EnterpriseBands)
    {
        if (EnterpriseBands)
        {
            return FlowchartLayoutRules.RepairSearchPatience;
        }

        int Stress = FlowchartLayoutRules.RepairSearchPatienceStress;
        int MinimumRoutes = FlowchartLayoutRules.RepairSearchPatienceStressMinRoutes;
        if (Stress > 0 && MinimumRoutes > 0 && EdgeRouteCount >= MinimumRoutes)
        {
            return Stress;
        }

        return FlowchartLayoutRules.RepairSearchPatience;
    }
}
