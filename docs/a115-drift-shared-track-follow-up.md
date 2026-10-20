# A115 drift shared-track follow-up

The drift change itself is not rerun. Saved production captures establish its
cause. Before level rows A115 had 76 bends, 122 crossings and six shared-track
findings; after level rows it had 79, 122 and seven. Two new identities replaced
one old identity, hence the net increase of one.

Both new overlaps are terminal runs at Y=795:

- `replication->sydneyData:8` has a horizontal stub X=1050..1070;
  `mqSvc->orderMgr:22` has a horizontal stub X=1080..1055. Overlap: 15 px.
- `perthMq->mqSvc:17` ends along X=1435..1410;
  `drSvc->shop:23` starts along X=1440..1420. Overlap: 15 px.

The row repair correctly levelled the nodes, but the resulting nearby allocated
ports gave their independent terminal runs the same track. This was not an
extra physical crossing counted differently, and not a diagonal-drift fault in
the checker. The prior unguarded SeparateTracks trial was rejected: it added
crossings and a title-band violation. It remains rejected.

The already completed mixed-band partner-placement changes remove both pairs
without another routing or drift experiment. Ungrouped services now follow all
actual partner medians, including same-band hosts, and frame clearance uses
projected rectangles instead of treating frame columns as full-band walls.
Current A115 has 82 bends, 66 crossings and two different shared-track findings:
`httpsIn->lbSvc:12 x backupDue->backup:29` and
`drSvc->shop:23 x shopImg2->shop:25`. They are still open, not zero.

Evidence is in `out/markup-review/band-drift/before/A115.json`,
`out/markup-review/band-drift/retained/A115.json` and
`out/markup-review/band-partners-v3/final/A115.json`. The latter's production
geometry hash is
`9A5D4867A84E4410BCBFFFEF014264F05EFA8E7DABC4E4F2F286F64125BE2973`.

A new production-path regression,
`EnterprisePartnerPlacementTests.A115LevelRowsDoNotRestoreTheDriftSharedTracks`,
asserts zero positive-length overlap for both original pairs and retains the
existing route/grid/frame checks. Its result is recorded in the stress follow-up
report after execution. No engine change is needed for these two pairs in the
current tree; no claim is made that the remaining shared-track gate is zero.
