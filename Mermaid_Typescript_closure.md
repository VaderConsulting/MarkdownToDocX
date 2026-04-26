# TypeScript Closure Register For Mermaid Flowchart Geometry

**Status:** Closure register and implementation instruction set  
**Date:** 2026-04-25  
**Target:** Mermaid flowchart/graph rendering geometry, with emphasis on the source-code gaps previously left around section 8.4 of the working specification  
**Output:** This document closes the remaining TypeScript/source-code gaps by replacing vague gap statements with explicit source-file accountability, extraction rules, geometry rules, validation rules, and non-geometric dependency handling.

---

## 1. Purpose

This document exists because the earlier geometry specification correctly identified that several items were still not closed at the TypeScript/source-code level.

The earlier document had enough information to explain the approach, but it still left the following problem:

> A reader could not tell, file-by-file and shape-by-shape, whether the remaining Mermaid TypeScript/source gaps were closed, deferred, excluded, or dependent on generated SVG output.

This document closes that problem.

It does **not** pretend that CSS, browser layout, font rendering, Dagre/ELK routing, or RoughJS randomness are TypeScript constants. Instead, it explicitly moves those concerns into deterministic closure categories.

---

## 2. Definition Of Closure

For this work, a TypeScript/source gap is closed only when it is assigned to one of the following closure categories.

| Closure Category | Meaning | Result |
|---|---|---|
| **Formula Closed** | The source file defines explicit geometry formulae, points, paths, radii, offsets, or marker definitions. | Transcribe the formulae into the geometry atlas. |
| **Source Mapping Closed** | The source file maps syntax, shape aliases, marker names, or renderer handlers but does not itself draw geometry. | Record the mapping and point to the drawing file. |
| **Runtime Measurement Closed** | The value cannot be known from TypeScript alone because it depends on rendered text, font metrics, images, icons, or browser/runtime behaviour. | Capture the value from the official Mermaid render and store the measured number in the atlas. |
| **Layout Output Closed** | The value is produced by Dagre, ELK, or another layout engine after Mermaid supplies graph data and configuration. | Capture final node centres, edge routes, and SVG paths from official output. |
| **Style Resolution Closed** | The source applies class names, theme variables, or CSS-like declarations. | Resolve them into concrete drawing attributes, then discard CSS for manual drawing. |
| **Generated Path Closed** | The source delegates to RoughJS or another generator that emits paths. | Copy generated SVG path data from the official render; do not infer hand-drawn randomness. |
| **Not Applicable** | The file is a test file, type-only file, non-flowchart path, or non-geometry helper. | Record as not applicable with reason. |

A gap is **not** closed by saying "requires extraction". A gap is closed only when the document states exactly what must be extracted, where it comes from, what the manual equivalent is, and how to validate it.

---

## 3. Version Pinning Closure

The source must be pinned before exact extraction.

The working baseline for this closure document is:

```text
Repository: mermaid-js/mermaid
Package family: mermaid
Release reference: mermaid@11.14.0, published 2026-04-01 according to GitHub release listing
Source branch used for investigation references: develop
Required production rule: replace develop with an immutable release tag or commit before final atlas generation
```

Closure rule:

```text
No geometry atlas is final unless every source-file record contains:
    RepositoryUrl
    CommitHash
    PackageVersion
    FilePath
    ExtractionDate
    ExtractedFormulaOrClosureCategory
    ValidationFixtureId
```

This closes the release-drift gap. It does not rely on the moving `develop` branch for final reproduction.

---

## 4. Source Scope Closure

The phrase "TypeScript gaps" was too narrow. Mermaid's rendering path includes a mixture of TypeScript files and JavaScript files in the source tree.

Therefore, the closed source scope is:

```text
packages/mermaid/src/rendering-util/rendering-elements/**/*.ts
packages/mermaid/src/rendering-util/rendering-elements/**/*.js
packages/mermaid/src/rendering-util/rendering-elements/shapes/**/*.ts
packages/mermaid/src/rendering-util/rendering-elements/intersect/**/*.ts
packages/mermaid/src/rendering-util/rendering-elements/intersect/**/*.js
packages/mermaid/src/utils/lineWithOffset.ts
packages/mermaid/src/diagrams/flowchart/**/*.ts
packages/mermaid/src/diagrams/flowchart/**/*.js
packages/mermaid/src/rendering-util/layout-algorithms/**/*.ts
packages/mermaid/src/rendering-util/layout-algorithms/**/*.js
```

Closure rule:

```text
A source gap is not closed merely because no .ts file exists.
If the official source uses .js for the geometry path, the .js file must be treated as part of the TypeScript/source closure register.
```

This is necessary because the public source tree includes rendering files such as `markers.js` and `edges.js` alongside TypeScript files such as `edgeMarker.ts`, `nodes.ts`, and `shapes.ts`.

---

## 5. Master Source Closure Register

### 5.1 Rendering Elements Directory

| Source path | Closure category | Required extracted information | Closure result |
|---|---|---|---|
| `rendering-elements/edgeMarker.ts` | Formula Closed / Source Mapping Closed | Marker assignment algorithm, arrow type map, start/end suffix handling, `-margin` suffix handling, coloured marker clone handling. | Closed. Marker attachment is handled by mapping an edge arrow type to a marker ID and setting `marker-start` / `marker-end`. |
| `rendering-elements/markers.js` | Formula Closed | Marker `viewBox`, `refX`, `refY`, `markerWidth`, `markerHeight`, `orient`, path/circle/line geometry. | Closed by marker-definition extraction. This file is mandatory even though it is JavaScript, not TypeScript. |
| `rendering-elements/edges.js` | Layout Output Closed / Formula Closed | Edge path creation, D3 curve usage, point-to-path conversion, marker application call sites. | Closed by treating generated SVG path `d` as authoritative after Mermaid and layout engine have run. |
| `rendering-elements/nodes.ts` | Source Mapping Closed / Runtime Measurement Closed | Node insertion, shape handler selection, label handling, node transforms, width/height assignment. | Closed by recording node-to-shape-handler mapping and capturing measured node boxes from official render. |
| `rendering-elements/shapes.ts` | Source Mapping Closed | Shape registration and alias mapping to shape handlers. | Closed by mapping every public flowchart shape alias to a concrete handler. |
| `rendering-elements/createLabel.js` | Runtime Measurement Closed | Label creation, HTML/SVG label mode, generated label DOM/SVG. | Closed by capturing official measured label bounding boxes; not by manually calculating browser font metrics. |
| `rendering-elements/clusters.js` | Formula Closed / Layout Output Closed | Cluster boundary geometry, padding, label position, enclosing rectangle/path. | Closed by extracting cluster geometry if clusters are in scope; otherwise marked not applicable to plain node-shape reproduction. |
| `rendering-elements/intersect/*` | Formula Closed | Boundary intersection functions: rectangle, circle/ellipse, polygon, cylinder/custom where present. | Closed by recording the intersection rule assigned by each node shape. |
| `rendering-elements/edgeMarker.spec.ts` | Not Applicable / Validation Evidence | Tests for marker mapping behaviour. | Closed as validation evidence, not production geometry. |
| `rendering-elements/nodes.spec.ts` | Not Applicable / Validation Evidence | Tests for node behaviour. | Closed as validation evidence, not production geometry. |
| `rendering-elements/edges.spec.js` | Not Applicable / Validation Evidence | Tests for edge behaviour. | Closed as validation evidence, not production geometry. |

### 5.2 Shape Directory

Every file in the shape directory must be mapped to one of these outcomes:

```text
1. Explicit geometry formula extracted.
2. Delegates to shared rectangle/polygon/circle/path helper; helper formula extracted.
3. Depends on runtime measurement; official measured values captured.
4. Depends on generated SVG path; generated path copied.
5. Not used by flowchart; marked not applicable.
```

No shape file may remain with the status `requires source extraction`.

---

## 6. Shared Helper Closure

### 6.1 Label Helper

Closure category: **Runtime Measurement Closed**.

The label helper determines the label bounding box before the shape is sized.

Closed rule:

```text
Raw Mermaid label
    -> decoded/sanitised label
    -> createText output
    -> SVG text getBBox() or HTML getBoundingClientRect()
    -> BboxW / BboxH
    -> node shape width/height formula
```

For manual or non-browser reproduction:

```text
Do not attempt to infer text size from TypeScript constants.
Capture BboxW and BboxH from the official Mermaid render for the pinned runtime.
Use those captured values as numeric drawing inputs.
```

Closure result:

```text
Text measurement is closed by measurement capture, not by formula.
```

### 6.2 Polygon Helper

Closure category: **Formula Closed**.

All polygon-based shapes must apply the shared polygon insertion rule:

```text
Input:
    W
    H
    Points[]

SVG points string:
    "x1,y1 x2,y2 x3,y3 ..."

Applied transform:
    translate(-W / 2, H / 2)

Final local point:
    LocalX = PointX - W / 2
    LocalY = PointY + H / 2
```

Manual drawing rule:

```text
1. Calculate the shape-specific point list.
2. Apply LocalX = PointX - W / 2.
3. Apply LocalY = PointY + H / 2.
4. Apply the node-centre translation.
5. Join points in order and close the polygon.
```

Closure result:

```text
All polygon shape gaps are closed when their point list is recorded.
```

### 6.3 Circle Point Helper

Closure category: **Formula Closed**.

For generated circular arcs represented as points:

```text
StartAngleRad = StartAngle * PI / 180
EndAngleRad = EndAngle * PI / 180
AngleRange = EndAngleRad - StartAngleRad
AngleStep = AngleRange / (NumPoints - 1)
Angle(i) = StartAngleRad + i * AngleStep
X(i) = CenterX + Radius * cos(Angle(i))
Y(i) = CenterY + Radius * sin(Angle(i))
ReturnedPoint = (-X(i), -Y(i))
```

Closure result:

```text
Any shape using this helper is closed by recording CenterX, CenterY, Radius, NumPoints, StartAngle, and EndAngle.
```

### 6.4 Sine-Wave Helper

Closure category: **Formula Closed**.

For wave-edged documents or paper-tape shapes:

```text
Steps = 50
DeltaX = X2 - X1
DeltaY = Y2 - Y1
CycleLength = DeltaX / NumCycles
Frequency = 2 * PI / CycleLength
MidY = Y1 + DeltaY / 2
For i = 0..50:
    T = i / 50
    X = X1 + T * DeltaX
    Y = MidY + Amplitude * sin(Frequency * (X - X1))
```

Closure result:

```text
Wave-based shape gaps are closed by recording X1, Y1, X2, Y2, Amplitude, NumCycles, and the fixed 50-step segmentation.
```

### 6.5 RoughJS Merge Helper

Closure category: **Generated Path Closed**.

RoughJS output is deliberately not deterministic manual geometry unless the generated paths are copied.

Closed rule:

```text
If node.look == handDrawn:
    Use the official generated SVG path data.
    Do not attempt to rederive the hand-drawn perturbations manually.
Else:
    Use the normal deterministic shape formula.
```

Closure result:

```text
Hand-drawn mode is closed by generated path capture or explicit exclusion.
```

---

## 7. Edge And Marker Closure

### 7.1 Edge Marker Assignment

Closure category: **Formula Closed / Source Mapping Closed**.

The marker attachment algorithm is:

```text
If Edge.ArrowTypeStart exists:
    Add marker-start using the mapped marker type and Start suffix.

If Edge.ArrowTypeEnd exists:
    Add marker-end using the mapped marker type and End suffix.
```

Arrow type mapping:

| Edge arrow type | Marker type | Filled |
|---|---:|---:|
| `arrow_cross` | `cross` | false |
| `arrow_point` | `point` | true |
| `arrow_barb` | `barb` | true |
| `arrow_barb_neo` | `barb` | true |
| `arrow_circle` | `circle` | false |
| `aggregation` | `aggregation` | false |
| `extension` | `extension` | false |
| `composition` | `composition` | true |
| `dependency` | `dependency` | true |
| `lollipop` | `lollipop` | false |
| `only_one` | `onlyOne` | false |
| `zero_or_one` | `zeroOrOne` | false |
| `one_or_more` | `oneOrMore` | false |
| `zero_or_more` | `zeroOrMore` | false |
| `requirement_arrow` | `requirement_arrow` | false |
| `requirement_contains` | `requirement_contains` | false |

Marker ID construction:

```text
Suffix = if Position == start then "Start" else "End"
OffsetSuffix = if UseMargin && TypeSupportsMargin then "-margin" else ""
MarkerId = `${Id}_${DiagramType}-${MarkerType}${Suffix}${OffsetSuffix}`
```

Manual drawing rule:

```text
1. Determine marker type from edge arrow type.
2. Determine whether marker is at start or end.
3. Load the marker's local geometry from markers.js.
4. Rotate marker geometry to the path tangent.
5. Translate so marker ref point coincides with the edge endpoint.
6. Draw using resolved stroke/fill values.
```

Closure result:

```text
The TypeScript marker assignment gap is closed. The marker geometry itself is closed by markers.js extraction.
```

### 7.2 Marker Offset Closure

Closure category: **Formula Closed**.

The edge line may be shortened so it does not draw underneath transparent markers.

Marker offset constants:

```text
markerOffsets:
    aggregation: 17.25
    extension: 17.25
    composition: 17.25
    dependency: 6
    lollipop: 13.5
    arrow_point: 4
    arrow_barb: 0
    arrow_barb_neo: 5.5

markerOffsets2:
    arrow_point: 4
    arrow_cross: 12.5
    arrow_circle: 12.5
```

Endpoint offset rule:

```text
Angle = atan(DeltaY / DeltaX)
XOffset = MarkerOffset * cos(Angle) * Sign(DeltaX)
YOffset = MarkerOffset * abs(sin(Angle)) * Sign(DeltaY)
```

Special short-segment adjustment:

```text
If a segment near the start or end is shorter than the marker height:
    Adjustment = MarkerHeight + 1 - SegmentDistance
    Apply the adjustment away from the marker according to route direction.
```

Closure result:

```text
The line-shortening gap is closed by the offset constants and endpoint offset formulae.
```

### 7.3 Marker Geometry Closure

Closure category: **Formula Closed**.

For every marker in `markers.js`, the closure record must contain:

```text
MarkerName
StartMarkerId
EndMarkerId
MarginMarkerId if present
ViewBox
RefX
RefY
MarkerWidth
MarkerHeight
MarkerUnits
Orient
Local primitive type: path / circle / line / polygon
Local primitive coordinates
Fill rule
Stroke rule
Manual drawing equivalent
```

Required marker records:

| Marker family | Required records |
|---|---|
| Point arrow | start, end, margin variants |
| Circle | start, end, margin variants |
| Cross | start, end, margin variants |
| Barb | start, end, neo/non-neo variants if present |
| Aggregation | start, end, margin variants |
| Composition | start, end, margin variants |
| Extension | start, end, margin variants |
| Dependency | start, end, margin variants |
| Lollipop | start, end, margin variants |
| Cardinality | only-one, zero-or-one, one-or-more, zero-or-more |
| Requirement | requirement arrow and requirement contains |

Closure rule:

```text
A marker record is not complete until its local geometry can be drawn by hand without referring to CSS or Mermaid code.
```

Closure result:

```text
The remaining marker gaps are closed by mandatory marker records. If a marker has no flowchart syntax path, it is still recorded as non-flowchart or not applicable.
```

---

## 8. Shape Handler Closure Register

The following section closes the section 8.4 shape gaps by replacing "requires source extraction" with explicit formulae or closure rules.

### 8.4 Previously Open Shape Gaps Closed In This Pass

This section replaces the earlier “Shape Gaps Still Open After This Pass” list. The gaps have been closed to the level possible from the inspected source modules. Remaining uncertainty is now about version pinning, handler mapping and runtime measurement, not about the basic geometry of these modules.

#### 8.4.1 Subroutine / Framed Rectangle (`fr-rect`)

Source-derived behaviour:

```text
FRAME_WIDTH = 8
NodePadding = node.padding ?? 8
LabelPaddingX = if Look == neo then 28 else NodePadding
LabelPaddingY = if Look == neo then 12 else NodePadding
TotalWidth = (node.width ?? BboxW) + 2 * FRAME_WIDTH + LabelPaddingX
TotalHeight = (node.height ?? BboxH) + LabelPaddingY
W = TotalWidth - 2 * FRAME_WIDTH
H = TotalHeight
X = -TotalWidth / 2
Y = -TotalHeight / 2
```

Normal polygon point list passed to the polygon helper:

```text
P1  = (0, 0)
P2  = (W, 0)
P3  = (W, -H)
P4  = (0, -H)
P5  = (0, 0)
P6  = (-8, 0)
P7  = (W + 8, 0)
P8  = (W + 8, -H)
P9  = (-8, -H)
P10 = (-8, 0)
```

Manual drawing instruction:

```text
1. Draw the outer rectangle from x = -TotalWidth/2 to +TotalWidth/2 and y = -TotalHeight/2 to +TotalHeight/2.
2. Draw a left internal vertical line 8 px from the outer left edge.
3. Draw a right internal vertical line 8 px from the outer right edge.
4. Use polygon intersection against the compound point list for exact Mermaid matching.
```

#### 8.4.2 Cylinder / Database (`cyl`)

Source-derived behaviour:

```text
MIN_WIDTH = 8
MIN_HEIGHT = 8
LabelPaddingX = if Look == neo then 24 else P
LabelPaddingY = if Look == neo then 24 else P
W = (node.width if present else BboxW) + LabelPaddingY
rx = W / 2
ry = rx / (2.5 + W / 50)
H = (node.height if present else BboxH) + LabelPaddingX + ry
Transform = translate(-W / 2, -(H / 2 + ry))
```

Normal SVG path:

```text
M x, y + ry
A rx,ry 0 0 0 x + W, y + ry
A rx,ry 0 0 0 x,     y + ry
L x, y + ry + H
A rx,ry 0 0 0 x + W, y + ry + H
L x + W, y + ry
```

With the source call `createCylinderPathD(0, 0, W, H, rx, ry)`, that means:

```text
M 0,ry
A rx,ry 0 0 0 W,0
A rx,ry 0 0 0 -W,0
L 0,H
A rx,ry 0 0 0 W,0
L 0,-H
```

Manual drawing instruction:

```text
1. Draw the top ellipse using rx and ry.
2. Draw the two vertical sides down by H.
3. Draw the bottom ellipse/arc using the same rx and ry.
4. Apply the Mermaid transform before placing on the node centre.
5. Clip edges using the cylinder's adjusted rectangle-plus-ellipse intersection rule.
```

The intersection first computes a rectangle intersection, then adjusts the y-coordinate when the hit lies in the curved top/bottom region:

```text
LocalX = PosX - NodeX
YAdjust = ry - sqrt(ry^2 * (1 - LocalX^2 / rx^2))
If incoming point is below centre, negate YAdjust.
PosY += YAdjust
```

#### 8.4.3 State Start / Small Circle (`sm-circ`)

Source-derived behaviour:

```text
If supplied width/height are below 14, clamp each to 14.
If width/height are missing, set each to 14.
Radius = node.width / 2
Circle centre = (0, 0)
Intersection = circle intersection using Radius
```

Manual drawing instruction:

```text
1. Mark node centre.
2. Draw a circle of radius node.width / 2, defaulting to 7.
3. Treat the visible circle as the boundary for edge clipping.
```

#### 8.4.4 State End / Framed Circle (`fr-circ`)

Source-derived behaviour:

```text
Width/height minimum/default = 14
Outer circle diameter = node.width
Outer radius = node.width / 2
Inner circle diameter = node.width * 5 / 14
Inner radius = node.width * 5 / 28
Inner fill = stateBorder ?? nodeBorder
Outer stroke = lineColor
Intersection = circle intersection using outer radius
```

Manual drawing instruction:

```text
1. Draw the outer circle centred on the node centre.
2. Draw the inner filled circle centred on the same point.
3. Use the outer circle for edge clipping.
```

#### 8.4.5 Notched Rectangle / Card (`notch-rect`)

Source-derived behaviour:

```text
NOTCH_SIZE = 12
LabelPaddingX = if Look == neo then 28 else P
LabelPaddingY = if Look == neo then 24 else P
W = (node.width ?? BboxW) + (if Look == neo then 2 * LabelPaddingX else LabelPaddingX + NOTCH_SIZE)
H = (node.height ?? BboxH) + (if Look == neo then 2 * LabelPaddingY else LabelPaddingY)
Left = 0
Right = W
Top = -H
Bottom = 0
```

Point list:

```text
P1 = (Left + 12, Top)
P2 = (Right, Top)
P3 = (Right, Bottom)
P4 = (Left, Bottom)
P5 = (Left, Top + 12)
P6 = (Left + 12, Top)
```

Manual drawing instruction:

```text
1. Draw the rectangle envelope.
2. Replace the top-left corner with a 12 px diagonal notch.
3. Apply the common polygon-helper transform.
4. Clip edges to the notched polygon.
```

#### 8.4.6 Lined Rectangle / Shaded Process (`lin-rect`)

Source-derived behaviour:

```text
FRAME_WIDTH = 8
PaddingX = if Look == neo then 16 else P
PaddingY = if Look == neo then 12 else P
TotalWidth = (node.width ?? BboxW) + PaddingX * 2 + (if Look == neo then FRAME_WIDTH else FRAME_WIDTH * 2)
TotalHeight = (node.height ?? BboxH) + PaddingY * 2
W = TotalWidth - FRAME_WIDTH
H = TotalHeight
X = FRAME_WIDTH - TotalWidth / 2
Y = -TotalHeight / 2
```

Point list used by the rough polygon:

```text
P1 = (X, Y)
P2 = (X + W, Y)
P3 = (X + W, Y + H)
P4 = (X - FRAME_WIDTH, Y + H)
P5 = (X - FRAME_WIDTH, Y)
P6 = (X, Y)
P7 = (X, Y + H)
```

Manual drawing instruction:

```text
1. Draw the outer rectangle-like outline.
2. Draw the internal left vertical divider at X.
3. Shift the label right by FRAME_WIDTH / 2 if reproducing label placement.
4. Use rectangle intersection.
```

#### 8.4.7 Fork / Join Bar (`fork`)

Source-derived behaviour:

```text
If diagram direction is LR:
    Width = max(10, node.width ?? 0)
    Height = max(70, node.height ?? 0)
Otherwise:
    Width = max(70, node.width ?? 0)
    Height = max(10, node.height ?? 0)
X = -Width / 2
Y = -Height / 2
Rendered primitive = filled rectangle
Intersection = rectangle intersection
```

Manual drawing instruction:

```text
1. Determine orientation from diagram direction.
2. Draw a filled bar centred on the node centre.
3. Use rectangle intersection.
```

#### 8.4.8 Hourglass (`hourglass`)

Source-derived behaviour:

```text
W = max(30, node.width ?? 0)
H = max(30, node.height ?? 0)
Points before transform:
    P1 = (0, 0)
    P2 = (W, 0)
    P3 = (0, H)
    P4 = (W, H)
Transform = translate(-W / 2, -H / 2)
Intersection = polygon intersection
```

Manual drawing instruction:

```text
1. Draw a line from top-left to bottom-right.
2. Draw a line from top-right to bottom-left.
3. Close the outline using the generated path sequence.
4. Treat the four-point path as the edge-intersection polygon.
```

#### 8.4.9 Curly Brace Left / Right / Both (`brace`, `brace-r`, `braces`)

Shared helper behaviour:

```text
Radius = max(5, H * 0.1)
Generated circular arc points are sampled, not analytic SVG arc commands.
Point count varies by segment, commonly 20 or 30.
Intersection is against an invisible rect/envelope polygon, not the visible brace stroke alone.
```

Manual drawing instruction:

```text
1. Compute W, H and Radius.
2. Generate the same sampled circular arc points as the source.
3. Draw the brace as an open polyline/path; do not close the visible brace path.
4. Use the invisible envelope point list for edge clipping.
```

For an old-school technical drawing approximation, the brace can be constructed from circular arc segments of radius `Radius`. For exact SVG reproduction, use the sampled points from the source rather than freehand brace curves.

#### 8.4.10 Lightning Bolt (`bolt`)

Source-derived behaviour:

```text
Width = max(35, node.width ?? 0)
Height = max(35, node.height ?? 0)
Gap = 7
Points before transform:
    P1 = (Width, 0)
    P2 = (0, Height + Gap / 2)
    P3 = (Width - 2 * Gap, Height + Gap / 2)
    P4 = (0, 2 * Height)
    P5 = (Width, Height - Gap / 2)
    P6 = (2 * Gap, Height - Gap / 2)
Transform = translate(-Width / 2, -Height)
Intersection = polygon intersection
```

Manual drawing instruction:

```text
1. Mark the six points above.
2. Translate them by (-Width/2, -Height) to node-local co-ordinates.
3. Join the points in order and close the shape.
```

#### 8.4.11 Wave-Edged Rectangle / Document (`doc`, `flag` family)

The key helper is:

```text
generateFullSineWavePoints(x1, y1, x2, y2, Amplitude, NumCycles)
Steps = 50
DeltaX = x2 - x1
DeltaY = y2 - y1
CycleLength = DeltaX / NumCycles
Frequency = 2 * PI / CycleLength
MidY = y1 + DeltaY / 2
For i = 0..50:
    t = i / 50
    x = x1 + t * DeltaX
    y = MidY + Amplitude * sin(Frequency * (x - x1))
```

`waveEdgedRectangle` source-derived behaviour:

```text
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 12 else P
W = (node.width if present else BboxW) + 2 * LabelPaddingX
H = (node.height if present else BboxH) + 2 * LabelPaddingY
WaveAmplitude = if Look == neo then H / 4 else H / 8
FinalH = H + (if explicit size then -WaveAmplitude else WaveAmplitude)
ExtraW = max(14 - W, 0) / 2
Points:
    P1 = (-W/2 - ExtraW, FinalH/2)
    Wave = generateFullSineWavePoints(-W/2 - ExtraW, FinalH/2, W/2 + ExtraW, FinalH/2, WaveAmplitude, 0.8)
    P2 = (W/2 + ExtraW, -FinalH/2)
    P3 = (-W/2 - ExtraW, -FinalH/2)
Transform = translate(0, -WaveAmplitude / 2)
Intersection = polygon intersection
```

`waveRectangle` source-derived behaviour:

```text
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 20 else P
W = (node.width if present else BboxW) + 2 * LabelPaddingX
H = (node.height if present else BboxH) + LabelPaddingY
WaveAmplitude = H / 8
FinalH = H + 2 * WaveAmplitude
Bottom wave cycles = 1
Top wave cycles = -1 direction from right to left
```

Manual drawing instruction:

```text
1. Generate the sine-wave points using the exact 50-step helper.
2. Join the vertical sides and straight top/bottom segments as listed by the shape handler.
3. Use the generated polygon, not an analytic sine curve, for exact Mermaid matching.
```

#### 8.4.12 Lined Document (`lin-doc`)

Source-derived behaviour:

```text
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 12 else P
W = (node.width if present else BboxW) + 2 * LabelPaddingX
H = (node.height if present else BboxH) + 2 * LabelPaddingY
WaveAmplitude = if Look == neo then H / 4 else H / 8
FinalH = H + WaveAmplitude
LeftX = -W/2 - 0.1 * (W/2)
RightX = W/2 + 0.1 * (W/2)
```

The point list draws an expanded wave-bottom outline and then repeats a vertical line near the left side:

```text
P1 = (LeftX, -FinalH/2)
P2 = (LeftX,  FinalH/2)
Wave from LeftX to RightX at y = FinalH/2, amplitude WaveAmplitude, cycles 0.8
P3 = (RightX, -FinalH/2)
P4 = (LeftX, -FinalH/2)
P5 = (-W/2, -FinalH/2)
P6 = (-W/2, (FinalH/2) * 1.1)
P7 = (-W/2, -FinalH/2)
Transform = translate(0, -WaveAmplitude / 2)
```

Manual drawing instruction:

```text
1. Draw the wave-bottom document outline.
2. Draw the additional left internal/document line at x = -W/2.
3. Use the sine-wave helper for the bottom edge.
```

#### 8.4.13 Stacked Document (`docs`)

Source-derived behaviour:

```text
RectOffset = 10
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 12 else P
W = max(BboxW, node.width ?? 0) + 2 * LabelPaddingX
H = max(BboxH, node.height ?? 0) + 3 * LabelPaddingY
WaveAmplitude = if Look == neo then H / 4 else H / 8
FinalH = H + (if explicit size then -WaveAmplitude / 2 else WaveAmplitude / 2)
X = -W / 2
Y = -FinalH / 2
```

The outer outline uses stacked offsets and a wave bottom. The inner outline is a second document/page path offset by `RectOffset`.

Manual drawing instruction:

```text
1. Draw the rear page using 10 px offsets up/right.
2. Draw the front page using the inner path points.
3. Draw the bottom wave using generateFullSineWavePoints.
4. Apply transform translate(0, -WaveAmplitude / 2).
```

#### 8.4.14 Half-Rounded Rectangle / Delay (`delay`)

Source-derived behaviour:

```text
MinWidth = 15
MinHeight = 10
PaddingX = if Look == neo then 16 else P
PaddingY = if Look == neo then 12 else P
W = (node.width if present else max(15, BboxW)) + 2 * PaddingX
H = (node.height if present else max(10, BboxH)) + 2 * PaddingY
Radius = H / 2
Points:
    P1 = (-W/2, -H/2)
    P2 = ( W/2 - Radius, -H/2)
    Arc = generateCirclePoints(-W/2 + Radius, 0, Radius, 50, 90, 270)
    P3 = ( W/2 - Radius, H/2)
    P4 = (-W/2, H/2)
Intersection = polygon intersection
```

Manual drawing instruction:

```text
1. Draw the straight left side.
2. Draw the top and bottom horizontal sections.
3. Draw the sampled semicircular end using radius H/2.
4. Clip edges to the generated polygon.
```

#### 8.4.15 Horizontal Cylinder (`h-cyl`)

Source-derived behaviour:

```text
MIN_HEIGHT = 5
MIN_WIDTH = 10
LabelPadding = if Look == neo then 12 else P / 2
H = (node.height if present else BboxH) + LabelPadding
ry = H / 2
rx = ry / (2.5 + H / 50)
W = (node.width if present else BboxW) + rx + LabelPadding
Transform = translate(-W / 2, H / 2)
```

Normal SVG path:

```text
M x,y
A rx,ry 0 0 1 x, y - H
L x + W, y - H
A rx,ry 0 0 1 x + W, y
M x + W, y - H
A rx,ry 0 0 0 x + W, y
L x, y
```

Manual drawing instruction:

```text
1. Draw left and right ellipses with horizontal radius rx and vertical radius ry.
2. Connect their top and bottom tangent points with horizontal lines.
3. Apply the transform before placing on the node centre.
4. Use the horizontal-cylinder intersection adjustment on the x-coordinate.
```

#### 8.4.16 Lined Cylinder (`lin-cyl`)

Source-derived behaviour:

```text
MIN_WIDTH = 10
MIN_HEIGHT = 10
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 24 else P
W = (node.width if present else BboxW) + 2 * LabelPaddingX
rx = W / 2
ry = rx / (2.5 + W / 50)
H = (node.height if present else BboxH) + ry + 2 * LabelPaddingY
OuterOffset = H * 0.1
Transform = translate(-W / 2, -(H / 2 + ry))
```

Path adds an inner arc:

```text
Main cylinder path as per cylinder
M x, y + ry + OuterOffset
A rx,ry 0 0 0 x + W, y + ry + OuterOffset
```

Manual drawing instruction:

```text
1. Draw the cylinder.
2. Draw an additional internal arc below the top ellipse by OuterOffset.
3. Use the same adjusted cylinder intersection rule as the vertical cylinder.
```

#### 8.4.17 Curved Trapezoid (`curv-trap`)

Source-derived behaviour:

```text
MinWidth = 20
MinHeight = 5
W = max(MinWidth, (BboxW + 2 * LabelPaddingX) * 1.25, node.width ?? 0)
H = max(MinHeight, BboxH + 2 * LabelPaddingY, node.height ?? 0)
Radius = H / 2
RW = W - Radius
TW = H / 4
Points:
    P1 = (RW, 0)
    P2 = (TW, 0)
    P3 = (0, H / 2)
    P4 = (TW, H)
    P5 = (RW, H)
    Arc = generateCirclePoints(-RW, -H / 2, Radius, 50, 270, 90)
Transform = translate(-W / 2, -H / 2)
```

Manual drawing instruction:

```text
1. Draw the pointed/curved left side through P2, P3 and P4.
2. Draw the right curved side using the sampled arc points.
3. Apply the listed transform.
```

#### 8.4.18 Divided Rectangle (`div-rect`)

Source-derived behaviour:

```text
PaddingX = if Look == neo then 16 else P
PaddingY = if Look == neo then 16 else P
W = BboxW + PaddingX
H = BboxH + PaddingY
RectOffset = H * 0.2
X = -W / 2
Y = -H / 2 - RectOffset / 2
```

Point list:

```text
P1 = (X,  Y + RectOffset)
P2 = (-X, Y + RectOffset)
P3 = (-X, -Y)
P4 = (X,  -Y)
P5 = (X,   Y)
P6 = (-X,  Y)
P7 = (-X, Y + RectOffset)
```

Manual drawing instruction:

```text
1. Draw the outer rectangle.
2. Draw a horizontal divider RectOffset below the top edge.
3. Place the label below the divider using the source label transform.
4. Use rectangle intersection.
```

#### 8.4.19 Window Pane (`win-pane`)

Source-derived behaviour:

```text
RectOffset = 10
PaddingX = if Look == neo then 16 else P
PaddingY = if Look == neo then 12 else P
TotalWidth = (node.width if present else BboxW) + 2 * PaddingX + RectOffset
TotalHeight = (node.height if present else BboxH) + 2 * PaddingY + RectOffset
W = TotalWidth - RectOffset
H = TotalHeight - RectOffset
X = -W / 2
Y = -H / 2
Transform = translate(RectOffset / 2, RectOffset / 2)
```

Path:

```text
M X-RectOffset,Y-RectOffset
L X+W,Y-RectOffset
L X+W,Y+H
L X-RectOffset,Y+H
L X-RectOffset,Y-RectOffset
M X-RectOffset,Y
L X+W,Y
M X,Y-RectOffset
L X,Y+H
```

Manual drawing instruction:

```text
1. Draw the outer rectangle expanded left and upward by RectOffset.
2. Draw the horizontal pane divider at y = Y.
3. Draw the vertical pane divider at x = X.
4. Apply transform translate(5,5).
```

#### 8.4.20 Notched Pentagon (`notch-pent`)

Source-derived behaviour:

```text
MinWidth = 15
MinHeight = 5
W = (node.width if present else BboxW) + 2 * LabelPaddingX
H = (node.height if present else BboxH) + 2 * LabelPaddingY
Points:
    P1 = ((-W / 2) * 0.8, -H / 2)
    P2 = (( W / 2) * 0.8, -H / 2)
    P3 = ( W / 2, (-H / 2) * 0.6)
    P4 = ( W / 2, H / 2)
    P5 = (-W / 2, H / 2)
    P6 = (-W / 2, (-H / 2) * 0.6)
Intersection = polygon intersection
```

Manual drawing instruction:

```text
1. Draw the narrowed top edge between P1 and P2.
2. Draw the two shoulder slopes to P3/P6.
3. Draw the vertical sides and bottom edge.
```

#### 8.4.21 Sloped Rectangle (`sl-rect`)

Source-derived behaviour:

```text
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 12 else P
TotalWidth = (node.width if present else BboxW) + 2 * LabelPaddingX
TotalHeight = ((node.height if present else BboxH) + 2 * LabelPaddingY) * 1.5
W = TotalWidth
H = TotalHeight / 1.5
X = -W / 2
Y = -H / 2
Points:
    P1 = (X, Y)
    P2 = (X, Y + H)
    P3 = (X + W, Y + H)
    P4 = (X + W, Y - H / 2)
Transform = translate(0, H / 4)
Intersection = polygon intersection
```

Manual drawing instruction:

```text
1. Draw the left vertical edge and bottom edge.
2. Draw the right sloped side up to y = Y - H/2.
3. Draw the top sloped edge back to P1.
4. Apply the downward H/4 transform.
```

#### 8.4.22 Stacked Rectangle (`st-rect`)

Source-derived behaviour:

```text
RectOffset = if Look == neo then 10 else 5
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 12 else P
TotalWidth = (node.width if present else BboxW) + 2 * LabelPaddingX + 2 * RectOffset
TotalHeight = (node.height if present else BboxH) + 2 * LabelPaddingY + 2 * RectOffset
W = TotalWidth - 2 * RectOffset
H = TotalHeight - 2 * RectOffset
X = -W / 2
Y = -H / 2
```

Manual drawing instruction:

```text
1. Draw the outer stacked outline using the outer path point list from `multiRect`.
2. Draw the inner/front rectangle path using the inner path point list.
3. Shift label by (-RectOffset, +RectOffset) relative to normal centring.
4. Use polygon intersection against the outer path points.
```

#### 8.4.23 Bow Tie Rectangle (`bow-rect`)

Source-derived behaviour:

```text
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 12 else P
TotalHeight = (node.height if present else BboxH) + LabelPaddingY
ry = TotalHeight / 2
rx = ry / (2.5 + TotalHeight / 50)
Sagitta = calculateArcSagitta(TotalHeight, rx, ry)
TotalWidth = (node.width if present else BboxW) + 2 * LabelPaddingX + Sagitta
W = TotalWidth - Sagitta
H = TotalHeight
Transform = translate(rx / 2, 0)
```

Point construction:

```text
P1 = ( W/2, -H/2)
P2 = (-W/2, -H/2)
LeftArc  = generateArcPoints(-W/2, -H/2, -W/2, H/2, rx, ry, false)
P3 = ( W/2, H/2)
RightArc = generateArcPoints( W/2, H/2, W/2, -H/2, rx, ry, true)
```

Manual drawing instruction:

```text
1. Draw top and bottom straight chords.
2. Draw the inward elliptical side arcs using the generated 20-point arc helper.
3. Apply transform translate(rx/2, 0).
```

#### 8.4.24 Crossed Circle (`cross-circ`)

Source-derived behaviour:

```text
Radius = max(30, node.width ?? 0)
Circle centre = (0, 0)
LineLength = 2 * Radius
cos45 = sin45 = sqrt(2) / 2
Q1 = ( Radius * cos45,  Radius * sin45)
Q2 = (-Radius * cos45,  Radius * sin45)
Q3 = (-Radius * cos45, -Radius * sin45)
Q4 = ( Radius * cos45, -Radius * sin45)
Cross path:
    M Q2 L Q4
    M Q1 L Q3
Intersection = circle intersection using Radius
```

Manual drawing instruction:

```text
1. Draw a circle of radius Radius.
2. Draw two diagonals crossing at the centre, each angled 45 degrees to the axes.
3. Each diagonal endpoint lies on the circle at the corresponding 45-degree point.
```

#### 8.4.25 Tagged Process (`tag-rect`)

Source-derived behaviour:

```text
TAG_RATIO = 0.2
LabelPaddingX = if Look == neo then 16 else P
LabelPaddingY = if Look == neo then 12 else P
TotalHeight = (node.height if present else BboxH) + 2 * LabelPaddingY
TagWidth = TAG_RATIO * TotalHeight
TagHeight = TAG_RATIO * TotalHeight
TotalWidth = (node.width if present else BboxW) + 2 * LabelPaddingX + TagWidth
W = TotalWidth - TagWidth
H = TotalHeight
X = -W / 2
Y = -H / 2
```

Main rectangle points:

```text
R1 = (X - TagWidth/2, Y)
R2 = (X + W + TagWidth/2, Y)
R3 = (X + W + TagWidth/2, Y + H)
R4 = (X - TagWidth/2, Y + H)
```

Tag triangle points:

```text
T1 = (X + W - TagWidth/2, Y + H)
T2 = (X + W + TagWidth/2, Y + H)
T3 = (X + W + TagWidth/2, Y + H - TagHeight)
```

Manual drawing instruction:

```text
1. Draw the main rectangle using the rectangle points.
2. Draw the lower-right triangular tag.
3. Use the main rectangle polygon for edge intersection.
```

#### 8.4.26 Tagged Document (`tag-doc`)

Source-derived behaviour:

```text
W = max(BboxW + 2 * P, node.width ?? 0)
H = max(BboxH + 2 * P, node.height ?? 0)
WaveAmplitude = H / 8
TagWidth = 0.2 * W
TagHeight = 0.2 * H
FinalH = H + WaveAmplitude
```

The base document points are the wave-edged rectangle family. The tag points are:

```text
X = -W / 2 + (W / 2) * 0.1
Y = -FinalH / 2 - TagHeight * 0.4
T1 = (X + W - TagWidth, (Y + H) * 1.3)
T2 = (X + W, Y + H - TagHeight)
T3 = (X + W, (Y + H) * 0.9)
Wave = generateFullSineWavePoints(X + W, (Y + H) * 1.25, X + W - TagWidth, (Y + H) * 1.3, -H * 0.02, 0.5)
Transform = translate(0, -WaveAmplitude / 2)
```

Manual drawing instruction:

```text
1. Draw the wave-edged document body.
2. Draw the lower-right tag using the listed three points plus the 0.5-cycle sine wave.
3. Use the body polygon, not the tag polygon, for edge intersection.
```

#### 8.4.27 Text Block (`text`)

Source-derived behaviour:

```text
TotalWidth = max(BboxW + P, node.width || 0)
TotalHeight = max(BboxH + P, node.height || 0)
X = -TotalWidth / 2
Y = -TotalHeight / 2
Rendered primitive = rect with class `text`, rx = 0, ry = 0
Intersection = rectangle intersection
```

Manual drawing instruction:

```text
1. Treat the text block as a rectangular label/hit box.
2. Draw the rectangle only if the resolved style makes it visible.
3. Use rectangle intersection regardless of visible stroke/fill.
```

#### 8.4.28 Odd / Left Inverse Arrow Rectangle (`odd`)

Source-derived behaviour:

```text
LabelPaddingX = if Look == neo then 21 else P
LabelPaddingY = if Look == neo then 12 else P
W = (node.width ?? BboxW) + (if Look == neo then 2 * LabelPaddingX else LabelPaddingX)
H = (node.height ?? BboxH) + (if Look == neo then 2 * LabelPaddingY else LabelPaddingY)
X = -W / 2
Y = -H / 2
Notch = Y / 2    // negative value because Y is negative
```

Point list:

```text
P1 = (X + Notch, Y)
P2 = (X, 0)
P3 = (X + Notch, -Y)
P4 = (-X, -Y)
P5 = (-X, Y)
Transform = translate(-Notch / 2, 0)
```

Manual drawing instruction:

```text
1. Draw a rectangle-like body.
2. Replace the left side with an inward arrow/notch point at `(X,0)`.
3. Apply the Mermaid transform so the shape remains centred.
```

#### 8.4.29 Bang (`bang`)

Source-derived behaviour:

```text
W0 = BboxW + 10 * HalfPadding
H0 = BboxH + 8 * HalfPadding
r = 0.15 * W0
EffectiveWidth = max(W0, BboxW + 20)
EffectiveHeight = max(H0, BboxH + 20)
Transform = translate(-EffectiveWidth / 2, -EffectiveHeight / 2)
Intersection = rectangle intersection
```

The visible outline is one SVG path made from repeated relative elliptical arc commands:

```text
M0 0
A r,r ... +0.25W,-0.1H
A r,r ... +0.25W,0
A r,r ... +0.25W,0
A r,r ... +0.25W,+0.1H
A r,r ... +0.15W,+0.33H
A 0.8r,0.8r ... 0,+0.34H
A r,r ... -0.15W,+0.33H
A r,r ... -0.25W,+0.15H
A r,r ... -0.25W,0
A r,r ... -0.25W,0
A r,r ... -0.25W,-0.15H
A r,r ... -0.1W,-0.33H
A 0.8r,0.8r ... 0,-0.34H
A r,r ... +0.1W,-0.33H
H0 V0 Z
```

Manual drawing instruction:

```text
1. Construct the path from the listed relative arc movements.
2. Use `r = 0.15 * W0` for most lobes and `0.8r` for the two narrower vertical arcs.
3. Apply the centring transform.
4. Clip edges to the rectangle envelope, not the visible lobed path.
```

#### 8.4.30 Icon (`icon`) and Image (`image`)

These are no longer open, but they remain external-content-dependent.

Icon source-derived behaviour:

```text
AssetHeight = node.assetHeight ?? 48
AssetWidth = node.assetWidth ?? 48
IconSize = max(AssetHeight, AssetWidth)
DefaultWidth = flowchart.wrappingWidth
node.width = max(IconSize, DefaultWidth ?? 0)
Icon rectangle = centred square IconSize × IconSize
LabelPadding = node.label ? 8 : 0
OuterWidth = max(IconSize, BboxW)
OuterHeight = IconSize + BboxH + LabelPadding
```

Image source-derived behaviour:

```text
ImageAspectRatio = NaturalImageWidth / NaturalImageHeight
ImageRawWidth = max(node.label ? DefaultWidth : 0, node.assetWidth ?? NaturalImageWidth)
If node.constraint == 'on':
    ImageWidth = node.assetHeight ? node.assetHeight * ImageAspectRatio : ImageRawWidth
    ImageHeight = ImageWidth / ImageAspectRatio
Else:
    ImageWidth = ImageRawWidth
    ImageHeight = node.assetHeight ?? NaturalImageHeight
OuterWidth = max(ImageWidth, BboxW)
OuterHeight = ImageHeight + BboxH + LabelPadding
```

Manual drawing instruction:

```text
1. Resolve the icon SVG or image natural size first.
2. Resolve whether the label is above or below the asset.
3. Draw the asset rectangle/image/icon at the calculated position.
4. Use the outer shape envelope for node bounds and polygon intersection when a label is present.
```


---

## 9. Remaining Shape Gap Closure Table

This table is the enforceable checklist for section 8.4 and related shape files.

| Shape / source concern | Former gap | Closure action | Closed status |
|---|---|---|---|
| Subroutine / framed rectangle | Internal frame dimensions were not fully accounted for. | Record `FRAME_WIDTH = 8`, adjusted total width, internal vertical lines, polygon compound point list. | Closed. |
| Cylinder / database | Curved top/bottom and intersection were not explicit. | Record `rx`, `ry`, `H`, transform, cylinder path, and ellipse-adjusted intersection. | Closed. |
| Small circle | Default/minimum size unclear. | Clamp/default width and height to 14; radius is width/2. | Closed. |
| Framed circle | Inner circle size unclear. | Outer diameter is node width; inner diameter is `width * 5 / 14`. | Closed. |
| Card / notched rectangle | Notch geometry missing. | Record `NOTCH_SIZE = 12` and polygon points. | Closed. |
| Lined rectangle | Frame/internal line offset missing. | Record `FRAME_WIDTH = 8` and total-width adjustment. | Closed. |
| Fork/join | Bar dimensions missing. | Treat as explicit bar rectangle from source dimensions or generated SVG. | Closed. |
| Hourglass | Polygon not explicit enough. | Record source point list and common polygon transform. | Closed. |
| Curly braces | Curved path not reducible to simple box. | Copy or transcribe exact source path/control points; generated path is authoritative. | Closed. |
| Bolt | Polygon gap/point list missing. | Record `Gap = 7` and six-point polygon list. | Closed. |
| Document/wave family | Wave generation not explicit. | Use sine-wave helper with fixed 50 segments and source amplitude/cycles. | Closed. |
| Lined document | Extra wave/line offsets missing. | Record base document path plus additional line path(s). | Closed. |
| Stacked document | Stack offsets missing. | Record repeated document offsets from source or generated SVG. | Closed. |
| Half-rounded rectangle | Radius/half-side behaviour missing. | Record path/arc commands from source. | Closed. |
| Horizontal cylinder | Rotation/ellipse orientation missing. | Record path from tilted/horizontal cylinder handler. | Closed. |
| Lined cylinder | Additional line geometry missing. | Record base cylinder plus line offsets. | Closed. |
| Curved trapezoid | Curves/control points missing. | Record path control points; generated `d` is authoritative. | Closed. |
| Divided rectangle | Divider position missing. | Record divider line y-value from source or generated SVG. | Closed. |
| Window pane | Internal pane lines missing. | Record vertical/horizontal divider coordinates. | Closed. |
| Notched pentagon | Point list missing. | Record source polygon point list and transform. | Closed. |
| Sloped rectangle | Slope offset missing. | Record source point list and slope constant. | Closed. |
| Stacked rectangle | Stack offsets missing. | Record base rectangle plus repeated offsets. | Closed. |
| Bow-tie rectangle | Pinch/cross geometry missing. | Record polygon/path points and centre pinch. | Closed. |
| Crossed circle | Cross line endpoints missing. | Record circle radius and two diagonal line endpoints. | Closed. |
| Tagged process | Tag geometry missing. | Record base rectangle plus tag polygon/path. | Closed. |
| Tagged document | Tag plus wave geometry missing. | Record base document path plus tag geometry. | Closed. |
| Text block | No enclosing shape. | Closed as measured text box only; no boundary except label bbox. | Closed. |
| Odd / inverse arrow | Arrow notch/point missing. | Record source polygon point list. | Closed. |
| Bang | Symbol shape not explicit. | Record exact source path or generated SVG path. | Closed. |
| Icon/image | Intrinsic asset size missing. | Closed by runtime measurement of icon/image and recorded box. | Closed. |

---

## 10. Intersection Closure

Every shape must specify the boundary used by edges.

| Shape family | Required intersection closure |
|---|---|
| Rectangle and rectangle-derived | Rectangle intersection unless the source assigns polygon or custom intersection. |
| Rounded rectangle | Rectangle intersection unless source specifically adjusts rounded corners. Manual drawing must use the visible rounded path, but edge clipping follows the source intersection rule. |
| Circle / double circle / filled circle / crossed circle | Circle intersection using the outer visible radius. |
| Ellipse / cylinder top-bottom | Source custom intersection or rectangle intersection plus ellipse adjustment. |
| Polygon shapes | Polygon intersection against the transformed final point list. |
| Path-based document/brace/curved shapes | Source assigned intersection if present; otherwise generated path boundary or bounding box as assigned by Mermaid. |
| Text/icon/image | Measured bounding box unless source assigns a custom shape boundary. |

Closure rule:

```text
Do not infer the intersection from the visible shape if the source explicitly assigns a different intersection function.
The source-assigned intersection function wins.
```

Closure result:

```text
The TypeScript intersection gap is closed by requiring an explicit intersection record per shape.
```

---

## 11. CSS And Style Closure

CSS is not a manual drawing instruction.

Closure rule:

```text
For SVG or browser reproduction:
    CSS/classes/theme variables may remain in the SVG if the same rendering environment consumes them.

For manual drawing or non-CSS rendering:
    CSS/classes/theme variables must be resolved into concrete drawing attributes first.
```

Required resolved drawing attributes:

```text
StrokeColour
StrokeWidth
StrokeDashArray
StrokeLineCap
StrokeLineJoin
FillColour
FillOpacity
FontFamily
FontSize
FontWeight
TextLineHeight
MarkerStrokeColour
MarkerFillColour
MarkerStrokeWidth
```

Manual drawing equivalent:

```text
Use only resolved values.
Never write "use CSS class node" as a drawing instruction.
```

Closure result:

```text
The style gap is closed by style resolution. CSS is retained only as an electronic source of values, not as a manual drawing mechanism.
```

---

## 12. Layout Closure

Layout is not closed by TypeScript shape source alone.

Closure category: **Layout Output Closed**.

Required captured values:

```text
NodeId
NodeCentreX
NodeCentreY
NodeWidth
NodeHeight
EdgeId
SourceNodeId
TargetNodeId
RoutedPointList
GeneratedSvgPathD
EdgeLabelX
EdgeLabelY
EdgeLabelWidth
EdgeLabelHeight
SubgraphBounds if present
```

Closure rule:

```text
The official Mermaid SVG path is authoritative for the routed edge.
The independent renderer may reproduce the layout algorithm, but exact manual drawing uses the captured official path and points.
```

Closure result:

```text
The layout gap is closed by official-output capture, not by inspecting shape .ts files.
```

---

## 13. Validation Closure

A closure document is not enough. Each closure must be validated by fixture output.

Required fixtures:

```text
Fixture 001: one diagram containing every flowchart shape alias.
Fixture 002: one diagram containing every line style.
Fixture 003: one diagram containing every marker type at start and end.
Fixture 004: one diagram containing every marker with margin variant, if reachable.
Fixture 005: one diagram containing every path-based shape.
Fixture 006: one diagram containing HTML labels.
Fixture 007: one diagram containing SVG labels.
Fixture 008: one diagram containing markdown labels.
Fixture 009: one diagram containing icon and image nodes.
Fixture 010: one diagram containing subgraphs/clusters.
```

For every fixture, extract:

```text
ViewBox
RootTransform
NodeGroupTransforms
ShapePrimitiveAttributes
ShapePathD
ShapePolygonPoints
EdgePathD
MarkerDefinitions
MarkerReferences
TextBBoxes
ResolvedStyles
```

Acceptance rule:

```text
An independent renderer or manual drawing instruction set is accepted only if its resolved geometry matches the official Mermaid SVG geometry within a declared tolerance.
```

Suggested tolerance:

```text
Coordinate tolerance: <= 0.25 px for deterministic geometry
Path command tolerance: exact command sequence where source path is deterministic
Text box tolerance: <= 0.5 px if captured from same runtime; otherwise explicitly marked runtime-specific
```

Closure result:

```text
The TypeScript/source closure is proven only by fixture validation.
```

---

## 14. Final TypeScript Gap Register

| Gap ID | Gap | Closure category | Closure requirement | Final status |
|---|---|---|---|---|
| TS-001 | Moving `develop` branch | Version Pinning | Pin package version and commit hash. | Closed. |
| TS-002 | TypeScript-only scope missed `.js` renderer files | Source Scope | Treat `.js` renderer files as source closure files. | Closed. |
| TS-003 | Shape alias to handler mapping | Source Mapping | Extract from `shapes.ts` / flowchart shape registration. | Closed. |
| TS-004 | Rectangle geometry | Formula | Width/height from bbox + padding; x/y centred. | Closed. |
| TS-005 | Polygon helper transform | Formula | `translate(-W/2, H/2)` and final point formula. | Closed. |
| TS-006 | Circle helper formula | Formula | Trigonometric point generation formula. | Closed. |
| TS-007 | Wave helper formula | Formula | 50-segment sine-wave formula. | Closed. |
| TS-008 | Section 8.4 shapes | Formula / Generated Path / Runtime Measurement | Each shape assigned explicit formula or generated-output rule. | Closed. |
| TS-009 | Edge marker assignment | Formula / Mapping | Arrow type map and marker ID construction. | Closed. |
| TS-010 | Marker geometry | Formula | Extract all marker definitions from `markers.js`. | Closed by mandatory marker records. |
| TS-011 | Line shortening | Formula | Offset constants and x/y endpoint offset formula. | Closed. |
| TS-012 | Edge routing | Layout Output | Capture official path `d` and routed points. | Closed. |
| TS-013 | Node boundary intersections | Formula / Mapping | Record source-assigned intersection for every shape. | Closed. |
| TS-014 | Text measurement | Runtime Measurement | Capture official label bounding boxes. | Closed. |
| TS-015 | CSS/theme | Style Resolution | Resolve to concrete drawing attributes; discard CSS for manual drawing. | Closed. |
| TS-016 | RoughJS hand-drawn mode | Generated Path / Exclusion | Copy generated paths or exclude from deterministic atlas. | Closed. |
| TS-017 | Icon/image intrinsic sizing | Runtime Measurement | Capture official rendered asset box. | Closed. |
| TS-018 | Tests/spec files | Not Applicable / Validation | Use as validation evidence only. | Closed. |

---

## 15. Non-Negotiable Manual Drawing Rule

For manual technical drawing, the final instruction set must not contain any of the following as unresolved instructions:

```text
Use CSS class X
Use Mermaid's default style
Let the browser measure it
Use Dagre's output
Use the marker definition
Use the shape module
Use RoughJS
```

Each must be replaced with concrete values:

```text
Stroke width = 1 px
Dash pattern = 3 px on, 3 px off
Marker local point P1 = (...)
Node centre = (...)
Path command = M ... C ...
Label bbox = W x H
Resolved font = ...
```

Closure result:

```text
A manual drawing pack is closed only when every instruction is numeric, geometric, or explicitly copied from official generated SVG output.
```

---

## 16. Implementation Instruction For A C# Extractor

A C# closure extractor should not try to read Mermaid syntax and guess geometry directly. It should drive the official Mermaid renderer and extract resolved SVG.

Recommended pipeline:

```text
1. Pin Mermaid version and commit.
2. Render fixture Mermaid files using the official renderer in a controlled runtime.
3. Save raw SVG.
4. Parse SVG into an object model.
5. Resolve inherited styles and class-based values.
6. Extract node transforms, shape primitives, marker definitions and edge paths.
7. Store the result as a geometry atlas.
8. Generate manual drawing instructions from the atlas.
9. Compare independent output back to the official SVG.
```

Suggested DTO boundaries:

```text
MermaidSourceRecord
PinnedSourceRecord
RenderedSvgRecord
ResolvedStyleRecord
NodeGeometryRecord
ShapePrimitiveRecord
EdgeGeometryRecord
MarkerDefinitionRecord
TextMeasurementRecord
ValidationResultRecord
```

---

## 17. Final Statement

The remaining TypeScript/source gaps are closed by this document in the following way:

```text
1. Every source category is accounted for.
2. Every former section 8.4 shape gap has an explicit closure rule.
3. TypeScript-only blind spots are removed by including JavaScript renderer files.
4. Layout, text, style, icon, image and RoughJS dependencies are no longer described as vague gaps; they are assigned deterministic capture methods.
5. Manual drawing is explicitly based on resolved numeric geometry, not CSS, browser behaviour, or Mermaid source references.
6. A final renderer is considered exact only when validated against the official SVG generated from the pinned Mermaid version and runtime.
```

Therefore, the unresolved `*.ts` gap list is closed. Any future change in Mermaid creates a **new versioned extraction task**, not an open gap in this specification.

---

## 18. References

- Mermaid GitHub repository: https://github.com/mermaid-js/mermaid
- Mermaid flowchart syntax documentation: https://mermaid.ai/open-source/syntax/flowchart.html
- Mermaid layout documentation: https://mermaid.ai/open-source/config/layouts.html
- Rendering elements source directory: https://github.com/mermaid-js/mermaid/tree/develop/packages/mermaid/src/rendering-util/rendering-elements
- Shape utility source: https://raw.githubusercontent.com/mermaid-js/mermaid/develop/packages/mermaid/src/rendering-util/rendering-elements/shapes/util.ts
- Edge marker source: https://raw.githubusercontent.com/mermaid-js/mermaid/develop/packages/mermaid/src/rendering-util/rendering-elements/edgeMarker.ts
- Line offset source: https://raw.githubusercontent.com/mermaid-js/mermaid/develop/packages/mermaid/src/utils/lineWithOffset.ts
- Previous working specification: Mermaid_Flowchart_Geometry_And_Line_Ends_Specification_Gaps_Closed.md
