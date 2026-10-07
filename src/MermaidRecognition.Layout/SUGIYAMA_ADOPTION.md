# Sugiyama layout — MSAGL adoption (not verbatim vendor)

## Intent

Microsoft Automatic Graph Layout (MSAGL) is **MIT-licensed** reference code. The programme **adopts** it by copying **only the classes and files needed** for native Sugiyama layered layout into `src/MermaidRecognition.Layout/Sugiyama/`, then maintaining that code as **first-party** under our namespace and modern .NET rules.

This is **not** the same as keeping a verbatim copy under `extern/` (that was a spike shortcut only).

| Approach | Status |
| --- | --- |
| Verbatim tree under `extern/Microsoft.AGL/` | **Removed** (October 2026). It no longer compiled and nothing referenced it. It remains in Git history. |
| NuGet `Microsoft.Msagl` | **Out of scope** |
| Selective copy into `src/MermaidRecognition.Layout/Sugiyama/` | **Done (Phase 1-3)** - 503 files, flat layout. This is now the only copy. |

## Source of truth

The adopted code in `Sugiyama/` is first-party and is edited in place. There is no re-copy step any more. To compare against upstream, use the [automatic-graph-layout](https://github.com/microsoft/automatic-graph-layout) repository or the original drop on the development PC (`3rd Party/Microsoft.AGL/Microsoft.AGL/`).

```powershell
dotnet build src/MermaidRecognition.Layout/MermaidRecognition.Layout.csproj
```

## Current layout

```text
src/MermaidRecognition.Layout/
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

Product SVG uses **node centres and bounds** from MSAGL for placement. Edge paths are built separately by `FlowchartEdgeRouteResolver` and `FlowchartCurvePathBuilder` (structural routing with `curveBasis` and corridor paths). MSAGL spline routing inside `LayeredLayoutEngine` is not consumed directly in product SVG output.

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
| **3 (done)** | Remove `extern/Microsoft.AGL` | No copy of MSAGL outside `Sugiyama/` |
| **4** | Trim unused copied files; add adoption tests | Smallest maintained slice documented in this file |

## Licence

MSAGL is MIT. Copied files must retain copyright and permission notice. See [automatic-graph-layout licence](https://github.com/microsoft/automatic-graph-layout/blob/master/LICENSE).
