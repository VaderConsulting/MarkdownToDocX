namespace MermaidRecognition.Kinds.Flowchart;

using MermaidRecognition.Config;

/// <summary>
/// Maps <see cref="FlowchartDb"/> to <see cref="DiagramContract"/>.
/// </summary>
public static class FlowchartContractMapper
{
    /// <summary>
    /// Builds the recognition contract from a populated diagram database.
    /// </summary>
    public static DiagramContract ToContract(FlowchartDb Database, DiagramConfigContract Configuration)
    {
        DiagramContract Contract = new DiagramContract
        {
            Kind = "flowchart",
            Header = Database.Header,
            Direction = Database.Direction.ToString()
        };

        foreach (KeyValuePair<string, string> Entry in Configuration.RawDirectiveValues)
        {
            Contract.Configuration.RawDirectiveValuesMutable[Entry.Key] = Entry.Value;
        }

        foreach (KeyValuePair<string, string> Entry in Configuration.EffectiveValues)
        {
            Contract.Configuration.EffectiveValuesMutable[Entry.Key] = Entry.Value;
        }

        foreach (KeyValuePair<string, ConfigValueSource> Entry in Configuration.ValueSources)
        {
            Contract.Configuration.ValueSourcesMutable[Entry.Key] = Entry.Value;
        }

        foreach (KeyValuePair<string, FlowchartNodeRecord> Entry in Database.NodesById)
        {
            FlowchartNodeRecord Source = Entry.Value;
            NodeContract Node = new NodeContract
            {
                Id = Source.Id,
                Label = string.IsNullOrEmpty(Source.Label)
                    ? Source.Label
                    : FlowchartLabelNormalizer.Normalize(Source.Label),
                LabelType = Source.LabelType,
                Shape = Source.Shape,
                ShapeSource = Source.ShapeSource,
                ParentGroupId = Source.ParentSubgraphId
            };
            foreach (MermaidDeclaration Declaration in Source.DeclarationsMutable)
            {
                Node.DeclarationsMutable.Add(Declaration);
            }

            foreach (KeyValuePair<string, MermaidMetadataValue> MetadataEntry in Source.MetadataMutable)
            {
                Node.MetadataMutable[MetadataEntry.Key] = MetadataEntry.Value;
            }

            foreach (string ClassId in Source.ClassesMutable)
            {
                Node.ClassesMutable.Add(ClassId);
            }

            RecognitionStyleMerge.Apply(Source.InlineStyle, Node.InlineStyle);
            Contract.NodesMutable.Add(Node);
        }

        foreach (KeyValuePair<string, FlowchartSubgraphRecord> Entry in Database.SubgraphsById)
        {
            FlowchartSubgraphRecord Source = Entry.Value;
            GroupContract Group = new GroupContract
            {
                Id = Source.Id,
                Title = Source.Title,
                ParentGroupId = Source.ParentSubgraphId
            };
            if (Source.DeclaredDirection.HasValue)
            {
                Group.Direction = Source.DeclaredDirection.Value.ToString();
            }

            foreach (string NodeId in Source.NodeIdsMutable)
            {
                Group.ChildNodeIdsMutable.Add(NodeId);
            }

            foreach (string ChildGroupId in Source.ChildSubgraphIdsMutable)
            {
                Group.ChildGroupIdsMutable.Add(ChildGroupId);
            }

            Contract.GroupsMutable.Add(Group);
        }

        foreach (FlowchartEdgeRecord Source in Database.Edges)
        {
            EdgeContract Edge = new EdgeContract
            {
                Id = Source.Id,
                IsUserDefinedId = Source.IsUserDefinedId,
                SourceId = Source.SourceId,
                TargetId = Source.TargetId,
                CreationIndex = Source.CreationIndex,
                MinLength = Source.MinLength,
                Stroke = Source.Stroke,
                MarkerStart = Source.MarkerStart,
                MarkerEnd = Source.MarkerEnd,
                Label = string.IsNullOrEmpty(Source.Label)
                    ? Source.Label
                    : FlowchartLabelNormalizer.Normalize(Source.Label),
                LabelType = Source.LabelType,
                ExpansionTrace = Source.ExpansionTrace
            };
            foreach (string ClassId in Source.ClassesMutable)
            {
                Edge.ClassesMutable.Add(ClassId);
            }

            foreach (KeyValuePair<string, MermaidMetadataValue> MetadataEntry in Source.MetadataMutable)
            {
                Edge.MetadataMutable[MetadataEntry.Key] = MetadataEntry.Value;
            }

            Contract.EdgesMutable.Add(Edge);
        }

        foreach (KeyValuePair<string, ClassDefContract> Entry in Database.ClassDefsById)
        {
            ClassDefContract Copy = new ClassDefContract
            {
                ClassId = Entry.Value.ClassId
            };
            RecognitionStyleMerge.Apply(Entry.Value.Style, Copy.Style);
            Contract.ClassDefsMutable.Add(Copy);
        }

        Contract.Accessibility.Title = Database.AccessibilityTitle;
        Contract.Accessibility.Description = Database.AccessibilityDescription;

        foreach (MermaidDiagnostic Diagnostic in Database.DiagnosticsMutable)
        {
            Contract.DiagnosticsMutable.Add(Diagnostic);
        }

        foreach (LinkStyleDirective LinkStyle in Database.LinkStylesMutable)
        {
            Contract.LinkStylesMutable.Add(LinkStyle);
        }

        foreach (InteractionDirective Interaction in Database.InteractionsMutable)
        {
            Contract.InteractionsMutable.Add(Interaction);
        }

        ApplyLinkStyles(Contract);
        ApplyEdgeMetadataCurves(Contract);
        ApplyEdgeClassAnimation(Contract);

        return Contract;
    }

    static void ApplyLinkStyles(DiagramContract Contract)
    {
        foreach (LinkStyleDirective LinkStyle in Contract.LinkStyles)
        {
            if (LinkStyle.IsDefault)
            {
                foreach (EdgeContract Edge in Contract.EdgesMutable)
                {
                    if (LinkStyle.Curve is not null)
                    {
                        Edge.Curve = LinkStyle.Curve;
                    }

                    if (LinkStyle.Style.Stroke is not null)
                    {
                        Edge.Stroke = LinkStyle.Style.Stroke;
                    }
                }

                continue;
            }

            foreach (int EdgeIndex in LinkStyle.EdgeIndexes)
            {
                EdgeContract? Edge = Contract.EdgesMutable.FirstOrDefault(
                    Candidate => Candidate.CreationIndex == EdgeIndex);
                if (Edge is null)
                {
                    continue;
                }

                if (LinkStyle.Curve is not null)
                {
                    Edge.Curve = LinkStyle.Curve;
                }

                if (LinkStyle.Style.Stroke is not null)
                {
                    Edge.Stroke = LinkStyle.Style.Stroke;
                }
            }
        }
    }

    static void ApplyEdgeMetadataCurves(DiagramContract Contract)
    {
        foreach (EdgeContract Edge in Contract.EdgesMutable)
        {
            if (Edge.Metadata.TryGetValue("curve", out MermaidMetadataValue? Value)
                && Value is CurveMetadata Curve)
            {
                Edge.Curve = Curve.Curve;
            }
        }
    }

    static void ApplyEdgeClassAnimation(DiagramContract Contract)
    {
        Dictionary<string, ClassDefContract> ClassDefsById = new Dictionary<string, ClassDefContract>(
            StringComparer.Ordinal);
        foreach (ClassDefContract ClassDef in Contract.ClassDefs)
        {
            ClassDefsById[ClassDef.ClassId] = ClassDef;
        }

        foreach (EdgeContract Edge in Contract.EdgesMutable)
        {
            foreach (string ClassId in Edge.Classes)
            {
                if (!ClassDefsById.TryGetValue(ClassId, out ClassDefContract? ClassDef)
                    || string.IsNullOrWhiteSpace(ClassDef.Style.Animation))
                {
                    continue;
                }

                string AnimationToken = ExtractAnimationName(ClassDef.Style.Animation);
                Edge.MetadataMutable["animate"] = new AnimationMetadata(true, AnimationToken);
            }
        }
    }

    static string ExtractAnimationName(string AnimationValue)
    {
        string Trimmed = AnimationValue.Trim();
        int SpaceIndex = Trimmed.IndexOf(' ');
        if (SpaceIndex < 0)
        {
            return Trimmed;
        }

        return Trimmed[..SpaceIndex];
    }
}
