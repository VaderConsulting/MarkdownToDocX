using System;
using System.Collections.Generic;
using MermaidRecognition.Diagnostics;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor;

/// <summary>
/// One diagram error, the text that describes it, and the nodes and relationships it involves.
/// </summary>
internal sealed class PreviewError
{
    private string _Description = string.Empty;

    private readonly List<PreviewObject> _Nodes = [];

    private readonly List<PreviewObject> _Relationships = [];

    /// <summary>Gets or sets the error description.</summary>
    public string Description
    {
        get
        {
            return _Description;
        }

        set
        {
            _Description = value ?? string.Empty;
        }
    }

    /// <summary>Gets the nodes this error involves.</summary>
    public IList<PreviewObject> Nodes
    {
        get
        {
            return _Nodes;
        }
    }

    /// <summary>Gets the relationships this error involves.</summary>
    public IList<PreviewObject> Relationships
    {
        get
        {
            return _Relationships;
        }
    }

    /// <summary>
    /// Reads the errors in an enterprise script and attaches the items they involve.
    /// </summary>
    /// <param name="Items">Items built from that script.</param>
    /// <param name="Source">Diagram script.</param>
    /// <returns>The errors. Empty when the script is not enterprise or has none.</returns>
    internal static List<PreviewError> FromScript(IReadOnlyList<PreviewObject> Items, string? Source)
    {
        List<PreviewError> Errors = [];
        if (Items == null || string.IsNullOrWhiteSpace(Source) || !EnterpriseParser.IsEnterpriseSource(Source))
        {
            return Errors;
        }

        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(Source);
        foreach (EnterpriseDiagnostic Diagnostic in Analysis.Diagnostics)
        {
            if (Diagnostic.Severity != MermaidDiagnosticSeverity.Error)
            {
                continue;
            }

            PreviewError Error = new PreviewError
            {
                Description = Diagnostic.Message
            };
            List<PreviewObject> NodeSeeds = [];
            foreach (PreviewObject Item in Items)
            {
                bool OnLine = Diagnostic.LineNumber > 0 && Item.LineNumber == Diagnostic.LineNumber;
                bool Named = Item.Kind != PreviewObjectKind.Relationship && MessageNamesId(Diagnostic.Message, Item.Id);
                if (!OnLine && !Named)
                {
                    continue;
                }

                if (Item.Kind == PreviewObjectKind.Relationship)
                {
                    AddRelationship(Error, Item);
                }
                else
                {
                    AddNode(Error, Item, NodeSeeds);
                }
            }

            foreach (PreviewObject Relationship in Error.Relationships)
            {
                AddEndpoint(Items, Error, Relationship.SourceId);
                AddEndpoint(Items, Error, Relationship.TargetId);
            }

            foreach (PreviewObject Seed in NodeSeeds)
            {
                foreach (PreviewObject Item in Items)
                {
                    if (Item.Kind != PreviewObjectKind.Relationship)
                    {
                        continue;
                    }

                    if (!string.Equals(Item.SourceId, Seed.Id, StringComparison.Ordinal)
                        && !string.Equals(Item.TargetId, Seed.Id, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    AddRelationship(Error, Item);
                    AddEndpoint(Items, Error, Item.SourceId);
                    AddEndpoint(Items, Error, Item.TargetId);
                }
            }

            Errors.Add(Error);
        }

        return Errors;
    }

    private static void AddNode(PreviewError Error, PreviewObject Item, List<PreviewObject> Seeds)
    {
        if (Error.Nodes.Contains(Item))
        {
            return;
        }

        Error.Nodes.Add(Item);
        Seeds.Add(Item);
    }

    private static void AddEndpoint(IReadOnlyList<PreviewObject> Items, PreviewError Error, string Id)
    {
        if (Id.Length == 0)
        {
            return;
        }

        foreach (PreviewObject Item in Items)
        {
            if (Item.Kind == PreviewObjectKind.Relationship || !string.Equals(Item.Id, Id, StringComparison.Ordinal))
            {
                continue;
            }

            if (!Error.Nodes.Contains(Item))
            {
                Error.Nodes.Add(Item);
            }
        }
    }

    private static void AddRelationship(PreviewError Error, PreviewObject Item)
    {
        if (!Error.Relationships.Contains(Item))
        {
            Error.Relationships.Add(Item);
        }
    }

    private static bool MessageNamesId(string Message, string Id)
    {
        if (Id.Length == 0)
        {
            return false;
        }

        if (Message.IndexOf("\"" + Id + "\"", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        if (Message.IndexOf(Id + "->", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        return Message.IndexOf("->" + Id, StringComparison.Ordinal) >= 0;
    }
}
