# Repair the cells-on hidden-failure groups

## Group 1 — duplicate joint-search identities

The recognition database retains a node-shaped alias when an edge names a group. Dagre excludes that alias from placed leaves and represents the identity as a cluster. The joint route candidate producer incorrectly concatenated all recognition node IDs with all group IDs, producing the same movement-unit key twice. No physical node was placed twice.

Leaf movement units now come from actual placed cell owners. Group units still come from declared groups and include their complete member regions. Recognition metadata is preserved. The dictionary remains strict: a repeated physical owner throws, and different nodes cannot occupy shared cells. Two focused alias/single-owner regressions pass.

All 23 original hidden-group cases were run with cells on and unchanged assertions: **23 pass, zero fail**. Every case reached its original assertions; no downstream failure needs deferring. The test-wide compatibility flag was changed only for this selected validation run and restored exactly afterward; no existing fixture or assertion change is committed.

Acceptance: all 673 catalogue comparisons pass against 60d1fb1, including per-scenario bend/crossing nonincrease. Catalogue **2999 bends / 2095 crossings**; original 81 **1118 / 1100**. A104 **38 / 0**, A105 **28 / 0**, same-fan crossings zero. All hard gates zero, no per-scenario finding increases; quality totals unchanged (4.2 52, 5.9 1190, 3.1 2, 4.1 0, 4.11 5, 5.8 3, shared pairs 337). Five existing native-placement cases remain explicitly unmeasured.

Unprofiled Release audit **49.87 s**, A142 **9.620626 s**. Normal layout suite: **1114 pass / the same 216 failing identities / 1330 total**. No added or removed failure identities. No rule value, hint, independent-text setting or candidate limit changed. No DOCX regenerated. The audit and normal suite retain their existing nonzero exit statuses for known quality/test failures.

Evidence under out/markup-review: hidden-group1-cells-on.log and its 23-case TRX; hidden-group1-identity-tests.log; hidden-group1-catalogue.log and JSONL; hidden-group1-audit.log/stdout/timings; hidden-group1-finding-increases.txt; hidden-group1-layout-suite.log and failure list.

Held for approval in group 1: none. Later groups have not been changed.
