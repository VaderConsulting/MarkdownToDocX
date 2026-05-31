namespace MermaidRecognition.NaiadAdapter;

/// <summary>
/// Maps a flowchart <see cref="DiagramContract"/> to Naiad <see cref="FlowchartModel"/> (Stage 11.A exit glue).
/// </summary>
public static class ContractToFlowchartModel
{
    /// <summary>
    /// Converts a recognition contract to a Naiad flowchart model for rendering.
    /// </summary>
    /// <param name="Contract">Recognition output; <see cref="DiagramContract.Kind"/> must be <c>flowchart</c>.</param>
    /// <returns>A populated <see cref="FlowchartModel"/>.</returns>
    public static FlowchartModel Map(DiagramContract Contract)
    {
        if (!string.Equals(Contract.Kind, "flowchart", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "ContractToFlowchartModel only supports flowchart diagrams; got kind \"" + Contract.Kind + "\".");
        }

        FlowchartModel Model = new FlowchartModel();
        Model.Title = Contract.Accessibility.Title;
        Model.AccessibilityTitle = Contract.Accessibility.Title;
        Model.AccessibilityDescription = Contract.Accessibility.Description;
        Model.Direction = MapDirection(Contract.Direction);

        foreach (KeyValuePair<string, string> ConfigEntry in Contract.Configuration.EffectiveValues)
        {
            if (!Model.Config.ContainsKey(ConfigEntry.Key))
            {
                Model.Config[ConfigEntry.Key] = ConfigEntry.Value;
            }
        }

        foreach (KeyValuePair<string, string> ConfigEntry in Contract.Configuration.RawDirectiveValues)
        {
            if (!Model.Config.ContainsKey(ConfigEntry.Key))
            {
                Model.Config[ConfigEntry.Key] = ConfigEntry.Value;
            }
        }

        Dictionary<string, Node> NodeLookup = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (NodeContract SourceNode in Contract.Nodes)
        {
            Node TargetNode = MapNode(SourceNode);
            NodeLookup[TargetNode.Id] = TargetNode;
            Model.AddNode(TargetNode);

            if (HasInlineStyle(SourceNode.InlineStyle))
            {
                Model.StyleByElementId[SourceNode.Id] = RecognitionStyleMapper.ToNaiadStyle(SourceNode.InlineStyle);
            }
        }

        foreach (EdgeContract SourceEdge in Contract.Edges)
        {
            Model.AddEdge(FlowchartEdgeMapper.ToNaiadEdge(SourceEdge));
        }

        foreach (ClassDefContract ClassDef in Contract.ClassDefs)
        {
            Model.ClassDefinitions[ClassDef.ClassId] = RecognitionStyleMapper.ToNaiadStyle(ClassDef.Style);
        }

        foreach (NodeContract SourceNode in Contract.Nodes)
        {
            if (SourceNode.Classes.Count == 0)
            {
                continue;
            }

            Model.ClassesByNodeId[SourceNode.Id] = SourceNode.Classes.ToList();
        }

        foreach (LinkStyleDirective LinkStyle in Contract.LinkStyles)
        {
            Style NaiadStyle = RecognitionStyleMapper.ToNaiadStyle(LinkStyle.Style);
            if (LinkStyle.IsDefault)
            {
                // Mermaid `linkStyle default` applies to all edges by parse (creation) order.
                for (int EdgeIndex = 0; EdgeIndex < Model.Edges.Count; EdgeIndex++)
                {
                    Model.StyleByLinkIndex[EdgeIndex] = NaiadStyle;
                }

                continue;
            }

            foreach (int EdgeIndex in LinkStyle.EdgeIndexes)
            {
                Model.StyleByLinkIndex[EdgeIndex] = NaiadStyle;
            }
        }

        foreach (InteractionDirective Interaction in Contract.Interactions)
        {
            if (!NodeLookup.TryGetValue(Interaction.TargetId, out Node? TargetNode))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(Interaction.Url))
            {
                TargetNode.Link = Interaction.Url;
            }

            if (!string.IsNullOrEmpty(Interaction.Tooltip))
            {
                TargetNode.Tooltip = Interaction.Tooltip;
            }
        }

        Dictionary<string, GroupContract> GroupsById = Contract.Groups.ToDictionary(
            Group => Group.Id,
            Group => Group,
            StringComparer.Ordinal);

        foreach (GroupContract Group in Contract.Groups)
        {
            if (!string.IsNullOrEmpty(Group.ParentGroupId))
            {
                continue;
            }

            Subgraph RootSubgraph = BuildSubgraph(Group, GroupsById);
            Model.Subgraphs.Add(RootSubgraph);
            EnsureSubgraphNodes(Model, NodeLookup, RootSubgraph, null);
        }

        return Model;
    }

    static Node MapNode(NodeContract Source)
    {
        Node Target = new Node
        {
            Id = Source.Id,
            Label = Source.Label ?? Source.Id,
            Shape = FlowchartShapeMapper.ToNodeShape(Source.Shape)
        };

        if (!string.IsNullOrEmpty(Source.ParentGroupId))
        {
            Target.ParentId = Source.ParentGroupId;
        }

        foreach (string ClassName in Source.Classes)
        {
            Target.InlineClassNames.Add(ClassName);
        }

        ApplyRecognitionStyle(Source.InlineStyle, Target.Style);
        return Target;
    }

    static bool HasInlineStyle(RecognitionStyle Source)
    {
        return Source.Fill is not null
            || Source.Stroke is not null
            || Source.StrokeWidth.HasValue
            || Source.StrokeDasharray is not null
            || Source.FontFamily is not null
            || Source.FontSize.HasValue
            || Source.FontWeight is not null
            || Source.TextColor is not null;
    }

    static void ApplyRecognitionStyle(RecognitionStyle Source, Style Target)
    {
        if (Source.Fill is not null)
        {
            Target.Fill = Source.Fill;
        }

        if (Source.Stroke is not null)
        {
            Target.Stroke = Source.Stroke;
        }

        if (Source.StrokeWidth.HasValue)
        {
            Target.StrokeWidth = Source.StrokeWidth;
        }

        if (Source.StrokeDasharray is not null)
        {
            Target.StrokeDasharray = Source.StrokeDasharray;
        }

        if (Source.FontFamily is not null)
        {
            Target.FontFamily = Source.FontFamily;
        }

        if (Source.FontSize.HasValue)
        {
            Target.FontSize = Source.FontSize;
        }

        if (Source.FontWeight is not null)
        {
            Target.FontWeight = Source.FontWeight;
        }

        if (Source.TextColor is not null)
        {
            Target.TextColor = Source.TextColor;
        }
    }

    static Subgraph BuildSubgraph(GroupContract Group, Dictionary<string, GroupContract> GroupsById)
    {
        Subgraph Target = new Subgraph
        {
            Id = Group.Id,
            Title = Group.Title
        };
        Target.Direction = MapDirection(Group.Direction);

        foreach (string NodeId in Group.ChildNodeIds)
        {
            Target.NodeIds.Add(NodeId);
        }

        foreach (string ChildGroupId in Group.ChildGroupIds)
        {
            if (!GroupsById.TryGetValue(ChildGroupId, out GroupContract? ChildGroup))
            {
                continue;
            }

            Target.NestedSubgraphs.Add(BuildSubgraph(ChildGroup, GroupsById));
        }

        return Target;
    }

    static void EnsureSubgraphNodes(
        FlowchartModel Model,
        Dictionary<string, Node> NodeLookup,
        Subgraph Subgraph,
        string? ParentId)
    {
        if (!NodeLookup.TryGetValue(Subgraph.Id, out Node? GroupNode))
        {
            GroupNode = new Node
            {
                Id = Subgraph.Id,
                Label = Subgraph.Title ?? Subgraph.Id,
                Shape = NodeShape.Rectangle
            };
            NodeLookup[Subgraph.Id] = GroupNode;
            Model.AddNode(GroupNode);
        }

        GroupNode.IsGroup = true;
        GroupNode.ParentId = ParentId;
        if (string.IsNullOrWhiteSpace(GroupNode.Label))
        {
            GroupNode.Label = Subgraph.Title;
        }

        foreach (string MemberId in Subgraph.NodeIds)
        {
            if (NodeLookup.TryGetValue(MemberId, out Node? Member))
            {
                Member.ParentId = Subgraph.Id;
            }
        }

        foreach (Subgraph Nested in Subgraph.NestedSubgraphs)
        {
            EnsureSubgraphNodes(Model, NodeLookup, Nested, Subgraph.Id);
        }
    }

    static Direction MapDirection(string? DirectionValue)
    {
        if (string.IsNullOrWhiteSpace(DirectionValue))
        {
            return Direction.TopToBottom;
        }

        if (Enum.TryParse(DirectionValue, ignoreCase: true, out Direction Parsed))
        {
            return Parsed;
        }

        return DirectionValue.ToUpperInvariant() switch
        {
            "TD" or "TB" => Direction.TopToBottom,
            "BT" => Direction.BottomToTop,
            "LR" => Direction.LeftToRight,
            "RL" => Direction.RightToLeft,
            _ => Direction.TopToBottom
        };
    }
}
