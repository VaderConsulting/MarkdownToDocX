# Approved grid routing decisions

The owner approved these decisions on 10 October 2026. They supplement the
supplied rulebook, which is left unchanged in the original working folder.
Rules depend on topology and geometry, never catalogue numbers or node text.

## 1. Spacing and routing defaults

1. Offset children in the next rank use downstream-to-upstream Z routes while
   downstream ports are free. Facing-side elbows follow when those ports are taken.
2. Diamond branches need 50 px before their first corner. Other ends reserve
   the corner radius at departure and arrowhead length at arrival. Base rank
   clearance is 60 px; an explicit larger rank-spacing setting remains a minimum.
3. Tracks are 6 px apart. Exhausted gaps grow by whole 30 px steps.
4. Nodes that rejoin the spine remain on the spine.
5. Ties use branches left, returns right, facing diamond vertex first, one
   rendered “n” of label clearance, and at least 30 px frame padding.
6. Arrangement checks permit the specific reordering in rulebook section 2.

Rank growth is local: label, Z-route, frame or track requirements can enlarge
one gap without enlarging every gap. Mixed shape dimensions still preserve
whole grid coordinates. Labels use text height on vertical runs and text width
on horizontal runs; their stroke breaks reserve the same measured clearance.

## 2. Replacement for the held frame-midline rule

Fit each frame around its members and title on the shared grid. Grow shared
gaps to preserve padding and routing clearance. Treat the frame edge as an
obstacle rather than reserving the gap's centre track.

The former centre-track reservation is superseded. Internal return tracks
must fit within every frame shared by their endpoints. Frame padding can grow
to accommodate those tracks. Moving an outside column or row preserves the
alignment of its other nodes.

## 3. Acceptance boundaries

These decisions resolve policy choices; they do not certify implementation
completeness or authorise a merge. Local gap growth, internal-return cases,
frame movement and sequence spacing have structural regression coverage.
Collision-free label planning across all connectors, crowded multi-return
track allocation, complete frame-border routing and the remaining placement,
port and crossing failures still require implementation or correction.

See [current solution status](../currentstatus.md) for the measured checkpoint
and [review section 18](../grid-layout-review.md#18-approved-decisions-and-native-renderers--10-october-2026)
for evidence and bounded investigations. Existing acceptance thresholds are
retained; the owner authorised the uniform-gap expectation changes explicitly.
