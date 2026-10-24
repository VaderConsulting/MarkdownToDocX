# Enable authoritative cell regions in production

The strengthened checkpoint assertions and report were already committed as
`4e0f05e`. Production closed-mode layouts now bind the same measured common-span
cell regions used by the last audited cells-on pipeline. Independent text spans
remain disabled. Open mode remains unbound.

`FlowchartRenderContext.UseCellRegions` defaults to true. A host can explicitly
choose false per render, or set the `MermaidRecognition.Layout.UseCellRegions`
AppContext switch to false for compatibility. Existing pixel-mode tests select
that compatibility mode; the production parity suite and production audits
explicitly exercise the enabled public pipeline. Strict rejected-write failures
remain enabled in tests and Debug builds.

The binding routine is shared by production and the reference probe. It uses
logical ranks/columns and the already measured common box dimensions; it does
not infer regions from finished pixel positions. Every later converted repair
continues to operate on these authoritative regions with atomic assignment and
exact rollback. No candidate limit, rule, score weight or hint behaviour changes.

## Validation

All **673 production/reference parity tests pass**. Exact node/frame geometry,
route coordinates, sides/normals, final findings and original hint states match
the previously audited cell pipeline. Thus no completed scenario adds bends.
The same five native-ineligible/empty cases remain unmeasured, rather than being
invented as zero-bend successes. The six open-mode scenarios also match.

The production unprofiled audit takes **51.07 s**, A142 **9.803666 s**. All 673
ordered summaries match the last cells-on audit byte for byte. The separate
explicitly disabled audit takes **39.34 s**, A142 **12.9082 s**; all 673 ordered
summaries match the original cells-off capture byte for byte.

| Gate | Production cells on | Cells explicitly off |
| --- | ---: | ---: |
| Illegal paths | 0 | 0 |
| Missing routes / no legal route | 0 / 0 | 0 / 0 |
| Title bands / unrelated frame overlaps | 0 / 0 | 0 / 0 |
| Grid / off-grid nodes / off-grid frame edges | 0 / 0 / 0 | 0 / 0 / 0 |
| Off-slot ports / off-slot groups | 0 / 0 | 0 / 0 |
| Hint-accounting failures / cleanup corrections | 0 / 0 | 0 / 0 |

Quality counts stay unchanged: cells on 4.2=52, 5.9=1231, 3.1=2, 4.1=0,
4.11=5, 5.8=3 and shared-track pairs=343. Cells off retains 44, 1694, 4, 0,
3, 0 and 2089 respectively. Known quality findings and five native skips still
make the audit exit nonzero; the hard gates above are zero.

The 328 focused tests pass, including two new public activation/opt-out tests.
Source regression and DOCX conversion results are appended after completion.
The parked F32/EIP261 routing refinement remains open. The about-45-second
audit target remains open; A142 meets the about-fifteen-second target.

## DOCX regeneration

The current ARM64 WinForms converter is built in Release and invoked through
its paired `-in` / `-out` CLI arguments, with no test-host compatibility switch.
Both outputs overwrite their normal repository-root files. DOCX files are
excluded from commits.

Commands, timings and fallback inspection results are recorded below after
both conversions complete.

Converter executable:
`src/Converter/bin/Release/net10.0-windows/Converter.exe`.

```powershell
& ./src/Converter/bin/Release/net10.0-windows/Converter.exe -in enterprise.md -out enterprise.docx
& ./src/Converter/bin/Release/net10.0-windows/Converter.exe -in flow.md -out flow.docx
```

Both commands exited zero. Enterprise took **151.2645499 s** and flow
**51.2965622 s**. Files are in
`C:\Users\windo\OneDrive\Documents\Development\MarkdownToDocX`.
The generated packages contain 161 and 148 embedded images respectively.
Flow has zero render-failure placeholders. Enterprise retains 18 under the
intentionally invalid catalogue examples: A78–A84, A86–A94, A102 and A130.
The prior committed enterprise DOCX also contains eighteen such placeholders.
No DOCX file is committed.

All 90 retained cell/source regressions also pass; no existing passing test was removed or weakened.
