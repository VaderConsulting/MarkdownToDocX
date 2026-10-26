using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Debug checks that a cached layout entry still matches its contract.</summary>
internal static class DiagramContractLayoutCacheGuard
{
    private sealed class Signature
    {
        public int NodeCount { get; init; }

        public int GroupCount { get; init; }

        public int EdgeCount { get; init; }
    }

    private static readonly ConditionalWeakTable<DiagramContract, Signature> Signatures = new();

    internal static void AssertUnchanged(DiagramContract Contract, object CacheEntry)
    {
#if DEBUG
        Signature Current = new Signature
        {
            NodeCount = Contract.Nodes.Count,
            GroupCount = Contract.Groups.Count,
            EdgeCount = Contract.Edges.Count
        };
        if (!Signatures.TryGetValue(Contract, out Signature? Stored))
        {
            Signatures.Add(Contract, Current);
            return;
        }

        Debug.Assert(
            Stored.NodeCount == Current.NodeCount
            && Stored.GroupCount == Current.GroupCount
            && Stored.EdgeCount == Current.EdgeCount,
            "DiagramContract changed after layout cache was populated.");
#endif
    }

    internal static void AssertUnchanged(DiagramContract Contract)
    {
#if DEBUG
        AssertUnchanged(Contract, Contract);
#endif
    }
}
