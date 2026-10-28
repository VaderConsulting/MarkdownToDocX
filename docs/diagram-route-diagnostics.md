# Route measurements (AG)

Enable **AG Route-aware diagram construction** in the editor's experimental settings to add route measurements to the diagnostics panel. AG works with AF and AH off. AG uses authoritative cell geometry and the shared retained source worker; AF still owns node and endpoint drag gestures. Auto layout starts a new arrangement explicitly. Open-layout and legacy previews do not publish retained route measurements.

Each row identifies a stable relationship, its endpoints and current source line. Double-click a row to visit that line. The summary counts each crossing or shared-track pair once. Partner lists are structural identities, including parallel relationships; they are not obtained by splitting a display string. Invisible layout constraints are excluded.

The shared `DiagramRouteDiagnostics` service measures stored paths without placement or routing:

- **Bends** use the existing scorer's corner predicate.
- **Crossing partners** use the independent checker's proper-crossing predicate and parallel-link exception.
- **Shared-track partners** use the existing positive-length overlap predicate.
- **Length** is the sum of stored segment lengths. **Extra** is length above the Manhattan distance between allocated ports, clamped at zero. **Detour** is the ratio to that distance; it is unavailable for coincident ports or nonfinite/missing geometry. These describe the stored polyline, before rounded rendering.
- **Invalid** means the independent route checker found a route/endpoint/pair failure, or the stored geometry cannot be measured. Findings retain their rule and explanation. Node-port findings are associated with incident relationships.
- **Investigate (improvement unverified)** means more than two measured corners or a detour ratio above 1.5, with no independent legality finding. This is a search prompt, not a new routing rule or proof of avoidable work. Hints, loops and obstacles may require the path.
- **Measured** means neither condition above applies. A diagnostic report never claims a verified improvement. Verification requires a separate complete candidate trial against the same accepted scene and constraints.

Reports are immutable values produced on the render worker. Publication shares the preview's revision and document checks. A source edit immediately removes previous measurements; failed, stale, legacy or flag-off requests cannot publish a replacement. Comment edits retain relationship identities and refresh line numbers. Measurements do not depend on display labels.

The service validates unique relationship identities. It keeps missing paths visible and reports nonfinite geometry explicitly. Its pair scan is quadratic and retained candidates still copy the model and perform whole-model validation. Large-diagram latency and memory remain benchmark work; this feature makes no speed claim. Placement suggestions and route-aware initial construction are subsequent AG tasks. All experiment defaults remain off.
