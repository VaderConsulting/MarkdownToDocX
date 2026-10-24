using System.Text.Json;
using MermaidRecognition.Contract;
using MermaidRecognition.Core;
using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Enterprise;

namespace MermaidRecognition.RuleValidation.Tests;

/// <summary>Captures source hint declarations independently of the mutable layout contract so dropped hints cannot make a regression pass.</summary>
internal static class CellFrameHintEvidence
{
    /// <summary>Parses a fresh original source, before any placement or repair stage can alter a hint.</summary>
    /// <param name="Scenario">Unchanged catalogue source.</param>
    /// <returns>Stable identities and complete placement/routing hint values.</returns>
    internal static Dictionary<string, string> FromSource(RouteRuleCatalogueAuditTests.Scenario Scenario) =>
        Capture(Scenario.Enterprise
            ? EnterpriseFlowchartContractMapper.Build(EnterpriseValidator.Analyze(Scenario.Source).Document!)
            : MermaidRecognizer.Parse(Scenario.Source));

    /// <summary>Includes active state and declared directions/offsets so neither disabling nor modifying a hint can hide a conflict.</summary>
    /// <param name="Contract">Fresh source or final mutable contract.</param>
    /// <returns>Deterministic key/value evidence, including inactive declarations.</returns>
    internal static Dictionary<string, string> Capture(DiagramContract Contract)
    {
        var Result = Contract.Nodes.Where(Node => Node.PlacementHint != null)
            .Select(Node => (Key: "node:" + Node.Id, Hint: Node.PlacementHint!))
            .Concat(Contract.Groups.Where(Group => Group.PlacementHint != null)
                .Select(Group => (Key: "group:" + Group.Id, Hint: Group.PlacementHint!)))
            .ToDictionary(Item => Item.Key, Item => JsonSerializer.Serialize(new
                { Item.Hint.Kind, Item.Hint.PeerId, Item.Hint.Offset, Item.Hint.Direction, Item.Hint.IsActive }));
        foreach (var Item in Contract.Edges.Select((Edge, Index) => (Edge, Index)).Where(Item => Item.Edge.RoutingHint != null))
        {
            var Hint = Item.Edge.RoutingHint!;
            Result.Add("edge:" + Item.Index, JsonSerializer.Serialize(new { Hint.Leave, Hint.Enter, Hint.Travel, Hint.IsActive }));
        }
        return Result;
    }
}
