# LiveEditor complete-sample render choke points

Measured on 2026-10-25 against **Samples → Enterprise → Clauses and relationships (Complete)** in LiveEditor (`SampleCatalog` `FullSource`, diagram title `All options`), cold geometry (no style-only cache hit). Bench: `@liveeditor-enterprise-complete` in `tools/LayoutCatalogueBench`.

| Stage | ms (approx.) | Share |
|---|---:|---:|
| Total `TryRenderPreviewSvg` | 1914 | 100% |
| `enterprise-layout` / `enterprise-shared-flowchart-layout` | 1686 / 1666 | ~88% |
| `allocation/free-side-comparison` | 364 | ~19% of total |
| `routing/initial-completion` (cell-cost) | 289 | ~15% of total |
| `enterprise-analysis` | 164 | ~9% of total |
| `enterprise-edge-routing` | 10 | under 1% |
| `enterprise-svg-build` | 9 | under 1% |

## Reading

1. Almost all time is native Sugiyama / grid layout, not SVG paint or PNG.
2. Inside layout, free-side port comparison and the first routing completion pass are the largest named buckets.
3. Style-only edits already reuse geometry (`EnterpriseSvgRenderer` geometry cache). Geometry edits still pay the full layout cost.
4. The hover / selection index is built after a successful render. While the diagram is broken, the last good picture remains, but caret sync used to refuse to update. That is fixed separately by resolving the declaration at the caret against the last drawn objects.

## Refactor order (not haphazard)

Do not start with SVG, rasterisation, or a broad rewrite of the layout engine.

1. Keep measuring with `LayoutStageTimingObserver` / `EnterpriseSvgRenderer.StageTimingObserver` on the complete sample before and after each change.
2. Profile `ChooseFreeSidesByRouteCost` (`allocation/free-side-comparison`) for repeated full route scoring and cache or narrow the candidate set when the side choice cannot change the outcome.
3. Profile the initial routing completion cell-cost path next; only then consider larger layout pipeline splits.
4. Treat analysis (164 ms) as a later pass: it is visible, but an order of magnitude below layout.
5. Keep geometry-cache behaviour for colour / class edits; do not weaken correctness of port or crossing repairs to chase speed.

## Junction notes tied to this work

- And/or junctions are drawn and hit-tested; caret selection failed mainly when the preview index was stale after a parse error.
- Source tokens are `and-junction` / `or-junction`; the selection panel and type-change path must use those tokens, not the rules-matrix name `junction`.
