# Flowchart layout fixes — before and after

Shared Sugiyama layout changes on `flowchart-layout-fixes` (from `master`; this repo has no `main`). Enterprise diagrams keep using this layout exactly. PR #1 (`enterprise-diagram`) was not modified.

**Befores** are native renders from `master` @ `6da24f1`, except `enterprise-NN-enterprise.png`, which are the existing PR #1 parity rasters. **Afters** are this branch. Enterprise after-pairs were produced on a local `enterprise-diagram` worktree with these layout files copied in; that worktree was not committed or pushed.

Geometry parity on that local worktree: samples 01–03 and 06–12 match flowchart vs enterprise within 2 px (0 mismatches).

## What to eyeball first

| Diagram | Before | After | What changed |
| --- | --- | --- | --- |
| Sample 03 flowchart | [before/enterprise-03-flowchart.png](before/enterprise-03-flowchart.png) | [after/enterprise-03-flowchart.png](after/enterprise-03-flowchart.png) | HTTPS sits on its own edge. Checkout → Order is one path. Web Shop verticals are separated. Edges keep ~12 px from group borders or cross them cleanly. One flowchart style throughout. Database Service ports are spread. |
| Sample 03 enterprise | [before/enterprise-03-enterprise.png](before/enterprise-03-enterprise.png) | [after/enterprise-03-enterprise.png](after/enterprise-03-enterprise.png) | Same geometry as the flowchart after, with ArchiMate markers. |
| Sample 11 flowchart | [before/enterprise-11-flowchart.png](before/enterprise-11-flowchart.png) | [after/enterprise-11-flowchart.png](after/enterprise-11-flowchart.png) | Store Cluster slack reduced; London and the junctions stay outside the box. |
| Sample 12 flowchart | [before/enterprise-12-flowchart.png](before/enterprise-12-flowchart.png) | [after/enterprise-12-flowchart.png](after/enterprise-12-flowchart.png) | Board sits above Raise Revenue; the upward edge through the stack is gone. |
| F111 | [before/F111.png](before/F111.png) | [after/F111.png](after/F111.png) | Tighter cluster insets (slack 8). Mesh routing unchanged. |
| F60 | [before/F60.png](before/F60.png) | [after/F60.png](after/F60.png) | Tighter stage boxes. Dotted audit corridor unchanged. |
| F40 | [before/F40.png](before/F40.png) | [after/F40.png](after/F40.png) | Tighter sibling subgraph boxes. |

## Behaviour changes Dave should review

1. **Subgraph membership (03, F43–F47, nested fixtures).** A cluster box contains only members. Non-members are packed out of member holes or evicted. Dummy subgraph-id nodes are not placed or painted.
2. **Root ranking (12).** Each root is one rank beyond its facing successor. The old “all roots share the bottom rank” pass is gone; no catalogue test required it.
3. **Compound edges (03).** Edges to/from a subgraph id attach to the facing rectangle. A clear chord stays a two-point clip. An obstructed chord keeps those attach points and uses an inside corridor so it does not pierce unrelated nodes or wrap outside the canvas.
4. **LAN → Load Balancer (03).** Source is LAN, target is Load Balancer.
5. **Cluster slack (11, F111, F59, F60).** Bottom content slack is 8 px (was 26 px). Title band stays 26 px. Column-led spine centring (F59) is unchanged unless the expand would cover a non-member.
6. **Shared route quality (03).** Non-endpoint nodes are obstacles with 16 px clearance. Incoming and outgoing ports that share a face are spread. Labels sit on their own edge (HTTPS). Near-aligned noisy guides collapse so `checkout --> order` is one path. Parallel overlapping runs stay at least 12 px apart (Web Shop → Hosting / Database Service). Routes keep 12 px from group borders or cross them cleanly (Shop Web UI, PostgreSQL → orders table, DC LAN, shop.war). One diagram uses one flowchart style — straight when aligned, smooth `curveBasis` when not — not mixed orthogonal corridors and loose curves. Designed retry, decision, and grouped routes stay on their builders.

## `flow.md` fixtures

Remarked (not rendered): F65, F70, F87, F88.

Unchanged (byte-identical PNG vs the previous after): F01–F23, F25–F30, F33–F35, F48–F57, F61–F64, F66–F69, F71, F72, F75–F77, F79–F83, F85, F86, F89–F97, F99–F105, F110, F111, F114–F120, F123–F125, F130, F131.

Changed (tighter boxes and/or membership/root/compound routing):

| Id | Change |
| --- | --- |
| F36, F126 | Slightly tighter subgraph padding. |
| F37, F127 | Slightly tighter subgraph padding. |
| F38, F128 | Slightly tighter subgraph padding. |
| F39 | Tighter nested/sibling boxes. |
| F40 | Tighter sibling subgraph boxes. |
| F41, F129 | Tighter subgraph padding. |
| F42 | Tighter nested boxes. |
| F43, F132 | Tighter nested subgraph boxes. |
| F44 | Tighter subgraph padding. |
| F45 | Tighter subgraph padding. |
| F46 | Tighter subgraph padding. |
| F47 | Tighter subgraph padding. |
| F24 | Approve/reject labels sit closer to their own curves. |
| F31, F32 | Hub fan-out/fan-in S-curves kept; side exits shift slightly. |
| F58 | Decision corridors unchanged in shape; tiny stroke/label movement. |
| F59 | Worker-pool column still centred; slack reduced. |
| F60 | Tighter stage boxes. Designed audit corridor kept. |
| F98 | Minor label or corridor offset. |
| F113, F121, F122 | Slight curve or label movement from the shared quality pass. |
| F73 | Tighter subgraph padding. |
| F74 | Tighter subgraph padding. |
| F78 | Tighter subgraph padding. |
| F84 | Tighter subgraph padding. |
| F111 | Tighter Left/Right insets. |
| F112 | Minor slack change. |
| F133 | Minor slack change. |

## Recognition fixtures (`tests/MermaidRecognition.Tests/FlowchartRecognition`)

Unchanged except:

| Stem | Change |
| --- | --- |
| subgraph-membership | Dummy group node omitted; X→Y stays a straight vertical. Box slack reduced. |
| subgraph-nested | Tighter nested boxes. |
| subgraph-direction | Tighter box slack. |

## Enterprise parity pairs (01–03, 06–12)

| Id | Flowchart | Enterprise |
| --- | --- | --- |
| 01 | Tighter application-layer boxes. | Unchanged raster vs PR #1 before. |
| 02 | Compound/group attach and slack. | Re-rendered on the shared layout. |
| 03 | Membership, compound edges, LAN direction, shared route quality. | Same geometry as the flowchart after. |
| 06 | Tighter business-layer boxes. | Re-rendered on the shared layout. |
| 07 | Tighter motivation boxes. | Re-rendered on the shared layout. |
| 08 | Minor slack. | Minor slack. |
| 09 | Minor slack on the flowchart. | Unchanged raster vs PR #1 before. |
| 10 | Minor slack on the flowchart. | Unchanged raster vs PR #1 before. |
| 11 | Store Cluster slack; junctions outside. | Same geometry as the flowchart after. |
| 12 | Board above Raise Revenue. | Same geometry as the flowchart after. |

Local 2 px geometry check after the layout copy (including the shared route-quality pass): **0 mismatches** on 01–03 and 06–12.

## How to regenerate

```text
dotnet run --project tools/FlowchartLayoutFixRender/FlowchartLayoutFixRender.csproj -c Release -- rasterize docs/flowchart/layout-fixes/before
dotnet run --project tools/FlowchartLayoutFixRender/FlowchartLayoutFixRender.csproj -c Release -- render-flow docs/flowchart/layout-fixes/after
```
