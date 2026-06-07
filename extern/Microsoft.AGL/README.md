# Microsoft.AGL — reference snapshot (transitional)

**Status:** temporary spike only. The programme **does not** intend to maintain a verbatim MSAGL tree here long term.

## Intended model

Adopt Sugiyama layout by **copying required classes** into:

```text
new_src/MermaidRecognition.Layout/Sugiyama/
```

See [`new_src/MermaidRecognition.Layout/SUGIYAMA_ADOPTION.md`](../../new_src/MermaidRecognition.Layout/SUGIYAMA_ADOPTION.md).

| Item | Value |
| --- | --- |
| Origin | MSAGL MIT sources (`3rd Party/Microsoft.AGL/` on this PC) |
| This folder | net10.0 reference snapshot for `Copy-SugiyamaSlice.ps1` re-copy; product code lives in `new_src/.../Sugiyama/` |
| NuGet | **Not used** |
| Delete when | Phase 3 of adoption — no routine need to re-copy; archive or remove |

## Do not

- Add product features or MarkdownToDocX adapters under `extern/Microsoft.AGL/`
- Treat this tree as frozen like `extern/Naiad`
- Copy the entire MSAGL repository verbatim into the product path

Adapter code belongs in `new_src/MermaidRecognition.Layout/`.
