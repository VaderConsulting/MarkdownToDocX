# Sugiyama layout — MSAGL adoption (not verbatim vendor)

## Intent

Microsoft Automatic Graph Layout (MSAGL) is **MIT-licensed** reference code. The programme **adopts** it by copying **only the classes and files needed** for native Sugiyama layered layout into `new_src/`, then maintaining that code as **first-party** under our namespace and modern .NET rules.

This is **not** the same as keeping a verbatim copy under `extern/` (that was a spike shortcut only).

| Approach | Status |
| --- | --- |
| Verbatim tree under `extern/Microsoft.AGL/` | **Transitional** — re-copy reference only; no product project reference |
| NuGet `Microsoft.Msagl` | **Out of scope** |
| Selective copy into `new_src/MermaidRecognition.Layout/Sugiyama/` | **Done (Phase 1–2)** — 503 files, flat layout |

## Source of truth for copying

Original MSAGL sources (read-only reference while migrating):

```text
3rd Party/Microsoft.AGL/Microsoft.AGL/     # original drop on this PC
extern/Microsoft.AGL/src/Microsoft.AGL/    # transitional net10.0 reference for re-copy
```

```powershell
# Re-copy from extern reference after upstream slice changes (preserves folder structure):
powershell -File tools/Copy-SugiyamaSlice.ps1
dotnet build new_src/MermaidRecognition.Layout/MermaidRecognition.Layout.csproj
```

The copy script writes directly to `Sugiyama/` (not a nested vendor folder).

## Current layout

```text
new_src/MermaidRecognition.Layout/
  Sugiyama/                          # adopted MSAGL slice (first-party, flat)
    Core/                            # geometry, layout model, projection solver, graph algorithms
    Layout/                          # Layered (Sugiyama engine), MDS, LargeGraphLayout, …
    Routing/                         # rectilinear, spline, visibility (compile closure)
    DebugHelpers/                    # debug curves, persistence, recovery
    Miscellaneous/                   # ranking, phylo, layout editing, …
    ValidateArg.cs
    Strings.resx, Strings.Designer.cs
  FlowchartGeometryGraphBuilder.cs   # adapter (DiagramContract → our graph types)
  FlowchartSugiyamaLayoutEngine.cs
  FlowchartSugiyamaSvgRenderer.cs
  NativeFlowchartRenderBridge.cs
  THIRD_PARTY_NOTICES.md             # MIT attribution for copied MSAGL files
```

**Namespace mapping (normative for new code):**

| MSAGL (reference) | Adopted namespace |
| --- | --- |
| `Microsoft.AGL.Core` | `MermaidRecognition.Layout.Sugiyama.Core` |
| `Microsoft.AGL.Core.Geometry` | `MermaidRecognition.Layout.Sugiyama.Core.Geometry` |
| `Microsoft.AGL.Core.Layout` | `MermaidRecognition.Layout.Sugiyama.Core.Layout` |
| `Microsoft.AGL.Layout.Layered` | `MermaidRecognition.Layout.Sugiyama.Layout.Layered` |
| `Microsoft.AGL.Routing` | `MermaidRecognition.Layout.Sugiyama.Routing` |
| `Microsoft.AGL.DebugHelpers` | `MermaidRecognition.Layout.Sugiyama.DebugHelpers` |
| … | extend only as needed for compile closure |

Adapter and product code stay in `MermaidRecognition.Layout` and call the adopted types directly.

## What we use today (R1 pilot)

`FlowchartSugiyamaLayoutEngine` calls:

- `GeometryGraph`, `Node`, `Edge`, `CurveFactory`, `Point`, `Rectangle`
- `SugiyamaLayoutSettings`, `LayeredLayout` → `LayeredLayoutEngine.Run()`

R1 reads **node centres and bounds** only; full edge routing from MSAGL is not yet consumed in SVG output. When copying, prefer the **smallest compile closure** that satisfies `LayeredLayout.Run()` for node placement; add routing types only when product SVG uses MSAGL edge curves.

## Modernisation rules (apply while copying)

When a class is copied into `Sugiyama/`:

1. **Retarget** to `net10.0` SDK-style csproj (already on `MermaidRecognition.Layout`).
2. **Rename namespace** to `MermaidRecognition.Layout.Sugiyama.*` (update `using` in the copied file and dependents).
3. **Remove .NET Framework baggage** as touched:
   - `#if NET4`, `PARALLEL35`, `System.Runtime.Remoting`, `app.config`
   - `Properties/AssemblyInfo.cs` (SDK generates attributes)
   - Debug-only `BinaryFormatter` paths unless still required for a test harness we keep
4. **Do not copy** unrelated MSAGL subsystems: LargeGraphLayout, MDS, routing bundling, WPF/GDI viewers, persistence/recovery unless a compile error forces a minimal stub.
5. **Preserve MIT licence** header on copied files; summarise in `THIRD_PARTY_NOTICES.md`.
6. **Match programme C# conventions** when editing for modernisation (explicit types, braces, `_Field` naming on new wrapper code — adopt MSAGL internals gradually, do not mass-rename MSAGL in one pass unless required).

## Migration phases

| Phase | Deliverable | Exit |
| --- | --- | --- |
| **0 (done)** | Adapter + spike via `extern/Microsoft.AGL` | R1 layout tests green |
| **1–2 (done)** | Copy MSAGL into flat `Sugiyama/`; `MermaidRecognition.Layout` builds **without** `extern` project reference | `dotnet build MermaidRecognition.Layout.csproj` |
| **3** | Remove or archive `extern/Microsoft.AGL` from routine builds; trim unused subsystems | No product dependency on `extern/` |
| **4** | Trim unused copied files; add adoption tests | Smallest maintained slice documented in this file |

## Licence

MSAGL is MIT. Copied files must retain copyright and permission notice. See [automatic-graph-layout licence](https://github.com/microsoft/automatic-graph-layout/blob/master/LICENSE).
