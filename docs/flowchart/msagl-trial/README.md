# Adopted MSAGL routing trial

This folder compares three engines on the same Mermaid sources:

| Folder | Engine |
| --- | --- |
| `custom/` | Product path: adopted Sugiyama node placement, then `FlowchartSharedEdgeRouter` |
| `msagl-rectilinear/` | Adopted `RectilinearEdgeRouter` after `LayeredLayout` with clusters |
| `msagl-spline/` | Adopted `SplineRouter` after `LayeredLayout` with clusters |

The default product render path is unchanged. Profiles pick only router style, padding, arrowhead size, and line stroke. Flowchart and enterprise use the same path on this branch. There is no ArchiMate painter here, so `enterprise-03-enterprise` is the same Mermaid and the same engine as the flowchart version.

Regenerate with:

```text
dotnet run --project tools/FlowchartLayoutFixRender -- trial-msagl
```

Gate counts are written to `gate-counts.md` after that command. The remaining-defect notes and the recommendation are filled in after the images exist.
