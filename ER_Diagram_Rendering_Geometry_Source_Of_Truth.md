# ER Diagram Rendering Geometry Source Of Truth

## 1. Purpose

This document defines the authoritative geometry rules for rendering Mermaid-style ER diagrams as SVG.

The intent is not to copy every inconsistent artefact of an existing renderer. The intent is to define a deterministic, visually correct, mathematically describable rendering model that reproduces the desired ER diagram behaviour:

- entity boxes are placed deliberately;
- relationship lines attach to deterministic anchors;
- relationship markers are geometric shapes, not text;
- paths are routed before they are smoothed;
- Bezier pull points preserve the intended endpoint tangents;
- fan-in and fan-out layouts distribute anchors across the boundary of the hub entity that faces the related entity group.

This document is the source of truth for implementing the renderer.

---

## 2. Core principle

An ER diagram renderer must not draw relationships by simply connecting entity centres.

It must also not rely on generic nearest-boundary intersection as the final relationship anchor. That produces unstable geometry, bottom-edge attachment errors, marker overlap, and incorrect curve bulges.

The renderer must instead follow this order:

1. Place entities.
2. Classify each relationship group around each entity.
3. Determine the facing direction of each group.
4. Allocate anchors on the entity boundary that faces the group.
5. Build route points using those anchors.
6. Add side-aware or direction-aware guide points.
7. Create Bezier or spline paths from the route points.
8. Draw endpoint markers using endpoint tangents.
9. Place labels along the resolved path.

The curve algorithm is not allowed to decide the effective anchor. The anchor must already be correct before smoothing occurs.

---

## 3. Coordinate system

Use standard SVG coordinates:

```text
X increases to the right.
Y increases downward.
```

For an entity rectangle:

```text
X      = left coordinate
Y      = top coordinate
W      = width
H      = height
Left   = X
Right  = X + W
Top    = Y
Bottom = Y + H
CX     = X + W / 2
CY     = Y + H / 2
```

The entity centre is:

```text
Centre = (CX, CY)
```

---

## 4. Entity box rendering

Each entity is rendered as a rectangle containing centred text.

Recommended visual style:

```text
Fill colour: pale lavender / pale blue
Stroke colour: purple or lavender-purple
Stroke width: approximately 1 px
Corner radius: 0 to 2 px
Shadow: soft, slight down-right offset
Text alignment: centred horizontally and vertically
```

Entity box size may be fixed or text-driven.

A practical fixed/default box model is:

```text
MinimumEntityWidth  = 120 px
MinimumEntityHeight = 84 px
HorizontalPadding   = 24 px
VerticalPadding     = 20 px
```

If text is measured:

```text
W = max(TextWidth  + 2 * HorizontalPadding, MinimumEntityWidth)
H = max(TextHeight + 2 * VerticalPadding,   MinimumEntityHeight)
```

---

## 5. Canonical anchor points

Each entity exposes deterministic anchor points on all four sides.

These points are used as primitives for relationship routing.

### 5.1 Left side anchors

Left side anchors are ordered from top to bottom:

```text
1L = (X, Y + H / 5)
2L = (X, Y + 2H / 5)
3L = (X, Y + 3H / 5)
4L = (X, Y + 4H / 5)
LC = (X, Y + H / 2)
```

### 5.2 Right side anchors

Right side anchors are ordered from top to bottom:

```text
1R = (X + W, Y + H / 5)
2R = (X + W, Y + 2H / 5)
3R = (X + W, Y + 3H / 5)
4R = (X + W, Y + 4H / 5)
RC = (X + W, Y + H / 2)
```

### 5.3 Top side anchors

Top side anchors are ordered from left to right:

```text
1T = (X + W / 5,   Y)
2T = (X + 2W / 5, Y)
3T = (X + 3W / 5, Y)
4T = (X + 4W / 5, Y)
TC = (X + W / 2,   Y)
```

### 5.4 Bottom side anchors

Bottom side anchors are ordered from left to right:

```text
1B = (X + W / 5,   Y + H)
2B = (X + 2W / 5, Y + H)
3B = (X + 3W / 5, Y + H)
4B = (X + 4W / 5, Y + H)
BC = (X + W / 2,   Y + H)
```

### 5.5 Important constraint

Bottom anchors must not be used merely because a related entity is lower on the page.

If the related entity is below but also clearly to the left or right, the relationship may need a lower-left or lower-right facing perimeter anchor instead of a bottom-centre anchor.

---

## 6. Relationship grouping is mandatory

Anchor selection must be performed at the relationship-group level, not by considering each relationship independently.

For each entity, collect its incident relationships and group them by the direction of the opposite entity group.

For entity `E` and opposite entity `O`:

```text
DeltaX = O.CX - E.CX
DeltaY = O.CY - E.CY
```

Use these values to classify the opposite entity as being generally:

```text
Up
Down
Left
Right
```

A practical default classification is:

```text
If abs(DeltaX) > abs(DeltaY):
    DeltaX < 0 -> Left
    DeltaX > 0 -> Right

If abs(DeltaY) >= abs(DeltaX):
    DeltaY < 0 -> Up
    DeltaY > 0 -> Down
```

However, this classification is not the same thing as choosing a single side anchor. It only identifies the facing direction of the relationship group.

The actual anchor is selected from a facing perimeter.

---

## 7. Facing perimeter model

A hub entity does not always use a single side. It uses the part of its perimeter that faces the related entity group.

This is the most important rule in this document.

### 7.1 Down-facing group

A down-facing group means the related entities are below the hub.

Use the lower-facing perimeter:

```text
lower-left side + bottom edge + lower-right side
```

This includes anchors such as:

```text
3L, 4L, 1B, 2B, BC, 3B, 4B, 4R, 3R
```

The order should run visually from left to right around the lower-facing perimeter.

### 7.2 Up-facing group

An up-facing group means the related entities are above the hub.

Use the upper-facing perimeter:

```text
upper-left side + top edge + upper-right side
```

This includes anchors such as:

```text
2L, 1L, 1T, 2T, TC, 3T, 4T, 1R, 2R
```

The order should run visually from left to right around the upper-facing perimeter.

### 7.3 Left-facing group

A left-facing group means the related entities are to the left of the hub.

Use the left-facing perimeter:

```text
upper-left top/side + left edge + lower-left bottom/side
```

This includes anchors such as:

```text
1T, 2T, 1L, 2L, LC, 3L, 4L, 1B, 2B
```

The order should run visually from top to bottom around the left-facing perimeter.

### 7.4 Right-facing group

A right-facing group means the related entities are to the right of the hub.

Use the right-facing perimeter:

```text
upper-right top/side + right edge + lower-right bottom/side
```

This includes anchors such as:

```text
4T, 3T, 1R, 2R, RC, 3R, 4R, 4B, 3B
```

The order should run visually from top to bottom around the right-facing perimeter.

---

## 8. Slot distribution across a facing perimeter

For a relationship group with `N` relationships, do not consume anchors from one end of the perimeter.

Distribute anchors evenly and symmetrically across the relevant facing perimeter.

Let:

```text
M = number of available slots in the facing-perimeter slot list
N = number of relationships in the group
i = relationship index after sorting, zero-based
```

Use this selection formula:

```text
SlotIndex(i) = round((i + 1) * (M + 1) / (N + 1)) - 1
```

Clamp the result to:

```text
0 <= SlotIndex < M
```

This produces the desired behaviour:

```text
N = 1 -> centre-facing slot
N = 2 -> balanced left/right or top/bottom slots
N = 3 -> left/centre/right or top/centre/bottom slots
N = many -> broad distribution across the facing perimeter
```

Relationships must be sorted before slots are assigned.

For Up and Down groups:

```text
Sort by opposite entity centre X, ascending.
```

For Left and Right groups:

```text
Sort by opposite entity centre Y, ascending.
```

---

## 9. Fan-in and fan-out clarification

Anchor selection is not determined by semantic cardinality direction alone.

The renderer must not use separate hard-coded geometry rules for “one-to-many” and “many-to-one” purely because of the relationship meaning.

Instead, geometry is determined by:

```text
1. the hub entity,
2. the number of relationships in the hub group,
3. the direction of the opposite entity cluster,
4. the sorted position of each opposite entity within that cluster.
```

A source entity fanning out to many lower targets and a target entity receiving many incoming relationships from lower entities can both require down-facing perimeter distribution on the hub entity.

The correct question is:

```text
Which boundary of this entity faces the related group?
```

not:

```text
Is this semantically one-to-many or many-to-one?
```

---

## 10. Required behaviour for incoming relationship examples

The following rules must be satisfied.

### 10.1 One incoming relationship

For:

```text
TARGET
  |
CHILD
```

`TARGET` has one relationship to a child below.

Use:

```text
TARGET anchor = BC
CHILD anchor  = TC
```

The path should be straight vertical unless obstacle avoidance requires otherwise.

### 10.2 Two incoming relationships

For:

```text
     TARGET
    /      \
CHILD_A  CHILD_B
```

`TARGET` has two relationships to children below.

Use balanced lower-facing anchors:

```text
Left child  -> lower-left-facing anchor on TARGET
Right child -> lower-right-facing anchor on TARGET
```

Acceptable anchors include:

```text
Left child  -> 4L or a lower-left perimeter slot
Right child -> 4R or a lower-right perimeter slot
```

Do not route both relationships into `TARGET.BC`.

The same pattern applies in the inverted vertical direction. If one lower target receives two relationships from two entities above it, the lower target must use balanced upper-facing anchors rather than collapsing both relationships into `TC`.

For example:

```text
PARENT_A   PARENT_B
    \       /
      TARGET
```

Use:

```text
PARENT_A -> TARGET uses an upper-left-facing target anchor
PARENT_B -> TARGET uses an upper-right-facing target anchor
```

Do not route both relationships into `TARGET.TC`.

### 10.3 Three incoming relationships

For:

```text
       TARGET
   /      |      \
CHILD_A CHILD_B CHILD_C
```

Use:

```text
Left child   -> lower-left-facing anchor
Centre child -> BC
Right child  -> lower-right-facing anchor
```

This establishes that the lower-facing perimeter is a mixture of side and bottom anchors, not only bottom anchors.

### 10.4 Eight incoming relationships

For eight children below a target, distribute all relationships broadly across the lower-facing perimeter.

Use:

```text
lower-left side anchors,
bottom-left anchors,
bottom-centre anchors,
bottom-right anchors,
lower-right side anchors.
```

Do not collapse multiple relationships onto the same bottom-centre point.

---

## 11. Gold standard CUSTOMER / ORDER / PRODUCT / LINE_ITEM layout

The gold standard diagram contains four entities:

```text
CUSTOMER
   |
ORDER        PRODUCT
   \          /
    LINE_ITEM
```

The intended relationships are:

```text
CUSTOMER places ORDER
ORDER contains LINE_ITEM
PRODUCT appears_in LINE_ITEM
```

### 11.1 Entity placement

The layout should be:

```text
CUSTOMER directly above ORDER.
PRODUCT to the right of ORDER.
LINE_ITEM below the gap between ORDER and PRODUCT.
```

`CUSTOMER` and `ORDER` share approximately the same centre X.

`LINE_ITEM` is below both `ORDER` and `PRODUCT`, with its centre between them.

### 11.2 CUSTOMER -> ORDER

This is a simple vertical relationship.

Use:

```text
P0 = CUSTOMER.BC
P1 = ORDER.TC
```

Draw a straight path:

```svg
M P0.X P0.Y L P1.X P1.Y
```

No Bezier curve is required.

### 11.3 ORDER -> LINE_ITEM

This relationship must not leave from the right side of `ORDER`.

`LINE_ITEM` is below `ORDER`, so the relationship should leave from the lower-facing boundary of `ORDER`, usually:

```text
P0 = ORDER.BC
```

It should approach `LINE_ITEM` from the upper-left region, not from the same top-centre point used by other relationships.

Recommended target anchor:

```text
P3 = LINE_ITEM upper-left side or upper-left top/perimeter anchor
```

A practical anchor is:

```text
P3 = (LINE_ITEM.X, LINE_ITEM.Y + LINE_ITEM.H * 0.25)
```

or a selected upper-left facing perimeter anchor.

The curve is a cubic Bezier:

```svg
M P0.X P0.Y C C1.X C1.Y, C2.X C2.Y, P3.X P3.Y
```

Control points:

```text
C1 = P0 + (0, VerticalPull)
C2 = P3 + (-HorizontalPull, -SmallVerticalPull)
```

This ensures:

```text
The curve leaves ORDER downward.
The curve approaches LINE_ITEM from the left/upper-left.
```

### 11.4 PRODUCT -> LINE_ITEM

This is the mirrored relationship.

Use:

```text
P0 = PRODUCT.BC
P3 = LINE_ITEM upper-right side or upper-right top/perimeter anchor
```

A practical anchor is:

```text
P3 = (LINE_ITEM.X + LINE_ITEM.W, LINE_ITEM.Y + LINE_ITEM.H * 0.25)
```

Control points:

```text
C1 = P0 + (0, VerticalPull)
C2 = P3 + (HorizontalPull, -SmallVerticalPull)
```

This ensures:

```text
The curve leaves PRODUCT downward.
The curve approaches LINE_ITEM from the right/upper-right.
```

### 11.5 LINE_ITEM as a two-parent target hub

The `LINE_ITEM` entity is a **1 -> 2 target case from the target's perspective**: two relationships approach the same target entity from above, one from `ORDER` and one from `PRODUCT`.

This must be handled as a grouped target-anchor allocation problem, not as two unrelated pairwise routes.

For `LINE_ITEM`, collect the two incoming relationships whose opposite entities are above it:

```text
ORDER   -> LINE_ITEM
PRODUCT -> LINE_ITEM
```

The opposite entity cluster is above `LINE_ITEM`, therefore `LINE_ITEM` uses its **upper-facing perimeter**.

For two incoming relationships from above, do not route both relationships into `LINE_ITEM.TC` and do not use the same top-centre marker location.

Use balanced upper-facing perimeter anchors:

```text
ORDER   -> LINE_ITEM target anchor = upper-left-facing perimeter anchor
PRODUCT -> LINE_ITEM target anchor = upper-right-facing perimeter anchor
```

Acceptable concrete anchors are:

```text
ORDER   -> LINE_ITEM target anchor = upper-left corner-adjacent anchor, such as 1T or 1L/2L depending on curve approach
PRODUCT -> LINE_ITEM target anchor = upper-right corner-adjacent anchor, such as 4T or 1R/2R depending on curve approach
```

The gold-standard image visually favours corner-adjacent upper perimeter anchors rather than the exact top-centre slots:

```text
ORDER   -> LINE_ITEM approaches the upper-left shoulder of LINE_ITEM
PRODUCT -> LINE_ITEM approaches the upper-right shoulder of LINE_ITEM
```

This means the target endpoint tangent must be consistent with the selected upper-facing anchor:

```text
ORDER -> LINE_ITEM:
    target anchor is on the upper-left facing perimeter;
    the final Bezier tangent approaches from upper-left / left-outside.

PRODUCT -> LINE_ITEM:
    target anchor is on the upper-right facing perimeter;
    the final Bezier tangent approaches from upper-right / right-outside.
```

The renderer must therefore allocate `LINE_ITEM` target anchors as a group before constructing either Bezier curve.

Do not do this:

```text
ORDER   -> LINE_ITEM target = LINE_ITEM.TC
PRODUCT -> LINE_ITEM target = LINE_ITEM.TC
```

Do this instead:

```text
ORDER   -> LINE_ITEM target = upper-left-facing slot on LINE_ITEM
PRODUCT -> LINE_ITEM target = upper-right-facing slot on LINE_ITEM
```

This is the specific 1 -> 2 case shown in the gold-standard diagram.

### 11.6 Bezier pull length calculation

Recommended pull lengths:

```text
VerticalPull      = clamp(0.30 * abs(P3.Y - P0.Y), 24, 70)
HorizontalPull    = clamp(0.35 * abs(P3.X - P0.X), 24, 80)
SmallVerticalPull = clamp(0.15 * abs(P3.Y - P0.Y), 12, 40)
```

The `clamp` function is:

```text
clamp(value, minimum, maximum) = min(max(value, minimum), maximum)
```

---

## 12. Bezier geometry rules

For a cubic Bezier:

```text
M P0
C C1, C2, P3
```

The initial tangent is:

```text
InitialTangent = C1 - P0
```

The final tangent is:

```text
FinalTangent = P3 - C2
```

Therefore endpoint behaviour is controlled directly by the pull points.

### 12.1 Bottom source

For a bottom source anchor:

```text
C1.X = P0.X
C1.Y > P0.Y
```

This makes the relationship leave downward.

### 12.2 Top target

For a top target anchor:

```text
C2.X = P3.X
C2.Y < P3.Y
```

This makes the relationship enter from above.

### 12.3 Left-side target

For a left-side target anchor:

```text
C2.X < P3.X
```

Usually also:

```text
C2.Y <= P3.Y
```

This makes the relationship approach from the left or upper-left.

### 12.4 Right-side target

For a right-side target anchor:

```text
C2.X > P3.X
```

Usually also:

```text
C2.Y <= P3.Y
```

This makes the relationship approach from the right or upper-right.

---

## 13. Side-aware guide point model

When building smoothed routes, use explicit guide points so the path leaves and enters entities correctly.

A route should usually be constructed as:

```text
SourceAnchor
SourceGuide
OptionalBulgePoint
TargetGuide
TargetAnchor
```

Guide points enforce endpoint tangents.

For a source anchor:

```text
Left side:   SourceGuide = SourceAnchor + (-T, 0)
Right side:  SourceGuide = SourceAnchor + ( T, 0)
Top side:    SourceGuide = SourceAnchor + (0, -T)
Bottom side: SourceGuide = SourceAnchor + (0,  T)
```

For a target anchor, the guide point is placed outside the target in the direction from which the curve should approach.

Examples:

```text
Target top:        TargetGuide = TargetAnchor + (0, -T)
Target bottom:     TargetGuide = TargetAnchor + (0,  T)
Target left side:  TargetGuide = TargetAnchor + (-T, 0)
Target right side: TargetGuide = TargetAnchor + ( T, 0)
```

A practical value is:

```text
T = clamp(0.15 * distance(SourceAnchor, TargetAnchor), 16, 40)
```

---

## 14. Bulge direction rules

Do not choose curve bulge direction solely by testing which candidate is farther from the diagram centre.

That rule can select the wrong side, especially for the right-most relationship in a fan-out group.

If a two-point or guide-point route requires a synthetic bulge point, calculate the two normal candidates:

```text
dx = P1.X - P0.X
dy = P1.Y - P0.Y
len = sqrt(dx^2 + dy^2)

NormalA = (-dy / len,  dx / len)
NormalB = ( dy / len, -dx / len)

Midpoint = ((P0.X + P1.X) / 2, (P0.Y + P1.Y) / 2)

CandidateA = Midpoint + NormalA * Bulge
CandidateB = Midpoint + NormalB * Bulge
```

Then select the candidate using the source or facing direction:

```text
Source/facing Left:   choose smaller X
Source/facing Right:  choose larger X
Source/facing Top:    choose smaller Y
Source/facing Bottom: choose larger Y
```

For the right-most line leaving the right side of a hub:

```text
choose the bulge candidate with the larger X
```

This makes the line arc outward to the right instead of bending inward across the diagram.

---

## 15. Cardinality markers

Cardinality markers must be drawn geometrically. They must not be rendered as text.

Common ER marker components:

```text
|| = exactly one
o| = zero or one
|{ = one or more
o{ = zero or more
```

The marker is placed relative to the endpoint tangent.

For an endpoint:

```text
Endpoint = anchor point
Tangent  = local direction of the line outside the entity
Normal   = perpendicular to Tangent
```

Calculate:

```text
Length      = sqrt(Tangent.X^2 + Tangent.Y^2)
UnitTangent = (Tangent.X / Length, Tangent.Y / Length)
UnitNormal  = (-UnitTangent.Y, UnitTangent.X)
```

Recommended marker dimensions:

```text
MarkerGapFromBox = 8 px
BarSpacing       = 5 px
BarHalfLength    = 9 px
CircleRadius     = 7 px
CrowFootLength   = 13 px
CrowFootSpread   = 8 px
StrokeWidth      = 1.5 px
```

### 15.1 Double bar

A double bar consists of two short lines perpendicular to the tangent.

For each bar centre:

```text
Start = BarCentre - UnitNormal * BarHalfLength
End   = BarCentre + UnitNormal * BarHalfLength
```

The two bar centres are separated along `UnitTangent` by `BarSpacing`.

### 15.2 Circle

A circle is centred on the tangent line:

```text
CircleCentre = Endpoint + UnitTangent * MarkerDistance
Radius       = CircleRadius
```

### 15.3 Crow's foot

A crow's foot is three strokes meeting at an apex on the tangent path.

```text
Apex = Endpoint + UnitTangent * MarkerDistance
```

The three endpoints are:

```text
Apex - UnitTangent * CrowFootLength
Apex - UnitTangent * CrowFootLength + UnitNormal * CrowFootSpread
Apex - UnitTangent * CrowFootLength - UnitNormal * CrowFootSpread
```

---

## 16. Marker placement constraints

Markers must be placed just outside the entity boundary.

They must not:

- appear inside the entity rectangle;
- be clipped by the entity rectangle;
- overlap the entity text;
- collapse onto another marker on the same side;
- be rendered as literal relationship-label text.

For a bottom source anchor, the marker tangent points downward.

For a top target anchor, the marker tangent points upward.

For a left-side anchor, the marker tangent points left.

For a right-side anchor, the marker tangent points right.

For curved side approaches, use the actual Bezier tangent at the endpoint.

---

## 17. Relationship label placement

Relationship labels are placed along the resolved path.

For a cubic Bezier, use:

```text
B(t) =
    (1 - t)^3 P0
  + 3(1 - t)^2 t C1
  + 3(1 - t)t^2 C2
  + t^3 P3
```

The default label point is:

```text
LabelPosition = B(0.5)
```

Apply a small normal offset if the label overlaps the path or marker:

```text
LabelOffset = 10 to 16 px along local normal
```

For a straight line, place the label near the midpoint, offset only if required for readability.

Do not include Mermaid syntax quote characters in rendered labels.

For example, this source label:

```mermaid
CUSTOMER ||--o{ ORDER : "places"
```

renders as:

```text
places
```

not:

```text
"places"
```

---

## 18. SVG drawing order

Recommended drawing order:

1. Relationship paths.
2. Entity shadows.
3. Entity rectangles.
4. Entity text.
5. Cardinality markers.
6. Relationship labels.

This keeps entity boxes clean while ensuring markers and labels remain visible.

---

## 19. Implementation checklist

The renderer must implement the following:

- [ ] Entity boxes with deterministic coordinates and measured/fixed dimensions.
- [ ] Canonical anchors on all four sides.
- [ ] Incident relationship grouping per entity.
- [ ] Facing direction classification for each relationship group.
- [ ] Facing perimeter slot lists for Up, Down, Left, and Right groups.
- [ ] Sorting of relationships within each group.
- [ ] Even slot distribution using the perimeter slot formula.
- [ ] Straight routing for simple aligned vertical/horizontal relationships.
- [ ] Cubic Bezier routing for curved relationships.
- [ ] Direction-aware Bezier pull points.
- [ ] Side-aware guide points.
- [ ] Source/facing-aware bulge direction selection.
- [ ] Geometric cardinality markers.
- [ ] Label placement from resolved path geometry.
- [ ] Explicit two-parent target handling: two relationships entering the same lower target from above must use upper-left and upper-right facing target anchors, not a shared top-centre anchor.
- [ ] Collision avoidance for labels and markers.
- [ ] No literal Mermaid quote characters in labels.

---

## 20. Common rendering errors to avoid

Do not:

1. connect entity centres directly;
2. use nearest-boundary intersection as the final ER anchor;
3. attach all lower relationships to bottom-centre;
4. attach all incoming relationships to the same target anchor;
5. treat fan-in and fan-out as the only geometric distinction;
6. ignore the facing direction of the related entity group;
7. let Bezier smoothing move the effective endpoint;
8. choose bulge direction only by distance from the diagram centre;
9. route `ORDER -> LINE_ITEM` from the right side of `ORDER` when the child is below;
10. route both lower relationships into the same top-centre point of `LINE_ITEM`;
11. draw cardinality markers as text;
12. render label quote characters.

---

## 21. Summary

The correct ER rendering model is based on deterministic geometry.

Each entity has canonical anchors. Relationships are grouped by hub entity and by the direction of the opposite entity cluster. Anchors are selected from the perimeter that faces that cluster and are distributed across that perimeter based on the number and order of relationships.

For a single vertical relationship, use centre top/bottom anchors and a straight line.

For two or more relationships facing the same hub boundary, distribute anchors across the relevant facing perimeter.

For the gold standard `CUSTOMER`, `ORDER`, `PRODUCT`, and `LINE_ITEM` diagram:

- `CUSTOMER -> ORDER` is straight vertical;
- `ORDER -> LINE_ITEM` leaves from the bottom of `ORDER` and approaches the upper-left of `LINE_ITEM`;
- `PRODUCT -> LINE_ITEM` leaves from the bottom of `PRODUCT` and approaches the upper-right of `LINE_ITEM`;
- the two lower relationships must have distinct `LINE_ITEM` target anchors and distinct endpoint markers;
- because `LINE_ITEM` receives two relationships from above, it must allocate balanced upper-left and upper-right facing target anchors rather than a shared top-centre anchor.

For the incoming relationship examples:

- 1 incoming relationship uses the centre-facing anchor;
- 2 incoming relationships use balanced left/right facing anchors;
- 3 incoming relationships use left/centre/right facing anchors;
- many incoming relationships are distributed broadly across the facing perimeter.

This is the authoritative model for reconstructing the diagrams as SVG.
