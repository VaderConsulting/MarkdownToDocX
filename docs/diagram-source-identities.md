# Source relationship identities

`DiagramRelationshipIdentityMap` implements the engine-side identity reconciliation portion of CORE-2. It accepts the flowchart contract produced by `EnterpriseFlowchartContractMapper`, including semantic relationships and plain drawing connections. It performs no placement, routing or rendering and changes no feature defaults.

## Ownership and publication

`Capture(documentId, sourceRevision, model)` creates a document-local immutable map. Each binding records a stable `relationship:N` identity, the parsed edge's list index, parser ID, creation index and endpoints. The edge index joins the binding to the full parsed source metadata without retaining mutable parser objects. Generated IDs, creation indices and source locations are not matching evidence.

`Reconcile(sourceRevision, model)` returns a detached successor, added/removed identity lists and policy notices, or a failure reason with no successor. It never mutates the accepted map or either parsed model. The revision must be newer than the map's accepted source revision. Identity allocation belongs to the successor, so discarded candidates do not consume accepted identities. Removed identities are not reused within an accepted lineage. Reloading a document starts a new lineage; these identities are not persisted in source.

The host must publish the successor with its corresponding accepted model and geometry, after checking document ownership, layout options and the latest observed source/layout revisions. Revision validation in this map alone cannot detect text edited after reconciliation began. Invalid intermediate source revisions need no identity map: the next valid revision compares against the last accepted map. Keeping the old map on an ambiguous or failed local operation preserves that comparison baseline.

Bindings expose the IDs that an adapter must use consistently for routing contracts, retained routes, selection and diagnostics. Reconciliation itself leaves parser-owned IDs untouched. The editor update service and atomic publication of identity maps with `DiagramLayoutSession` are subsequent integration work.

## Matching policy

The service applies these rules in order, using ordinal, case-sensitive IDs:

1. Unique explicit user IDs preserve identity even if both endpoints or all content change. Blank or duplicate explicit IDs reject the input. Adding, removing or changing an explicit ID constitutes removal/addition; it cannot accidentally match a generated parser ID.
2. Exact endpoint/content matches preserve identity across reordering and unrelated insertions/deletions. Content includes labels, markers, Enterprise relationship kind or plain-connection category, rank length, curve, stroke and requested routing directions. Source locations, generated IDs, classes and routing-hint diagnostics are excluded.
3. If exactly one unmatched old and new declaration share an endpoint pair, they retain identity through label, kind or port-hint edits. This also handles one edited label among otherwise matched parallel relationships.
4. A reconnect retains identity when unchanged content and one unchanged endpoint give a unique match in **both** old and new inventories. Several possible old owners, or one old declaration claimed by several new declarations, reject reconciliation.
5. Remaining unmatched declarations are removals/additions. In particular, changing both endpoints without an explicit ID is a removal/addition; source text alone supplies no stable ownership evidence.

Indistinguishable parallel declarations with unchanged multiplicity retain identities in declaration occurrence order, with a visible policy notice. Their individual historical ownership is unknowable from text. If their multiplicity changes, or several unmatched parallel declarations change content, reconciliation rejects the edit with an ambiguity reason. An adapter may resolve ownership with explicit command identities or perform a separately reported session rebuild. Ambiguity must never silently turn a failed local routing operation into a rebuild.

Matching uses grouped signatures and endpoint inventories; reconnect ambiguity stops after finding two possible owners. It does not construct an all-pairs edge comparison.

## Verification and remaining work

`DiagramRelationshipIdentityMapTests` covers Enterprise reparsing, comment/generated-ID changes, relationship kinds and plain connections, parallel insertions/deletions/reordering, reconnects, explicit-ID changes, self-loops, presentation changes, ambiguity, skipped invalid revisions, rejected/discarded candidates and mutable parser isolation.

On 28 October 2026, all 28 identity tests and all 39 selected retained-session, layout-option and width regression cases pass (67 total). The command and output are recorded in `out/identity-map-regressions.log`:

```powershell
dotnet test tests/MermaidRecognition.Layout.Tests/MermaidRecognition.Layout.Tests.csproj --no-restore --filter 'FullyQualifiedName~DiagramRelationshipIdentityMapTests|FullyQualifiedName~DiagramLayoutSessionTests|FullyQualifiedName~DiagramLayoutOptionsTests|FullyQualifiedName~WidthConstrainedLayoutTests' --verbosity minimal -m:1 -nodeReuse:false
```

Next: the shared source update service must apply these bindings, classify geometry effects, resolve placement hints to cell transactions, and publish matching source metadata and accepted geometry. LiveEditor worker integration, stale-preview reporting, GUI commands, undo/redo and export remain pending. CORE-2 stays open until its editor-facing acceptance criteria are verified.
