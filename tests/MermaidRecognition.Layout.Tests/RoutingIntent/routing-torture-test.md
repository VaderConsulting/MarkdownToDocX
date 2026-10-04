# Routing intent torture test

This Markdown fixture exercises generic flowchart routing behaviours for the routing-intent diagnostic layer.
Each diagram is preceded by a `DiagramId` HTML comment used by the test runner and `ruleset.json`.

---

## Regression — full pipeline stress (F58)

<!-- DiagramId: RegressionFlow001 -->

```mermaid
flowchart TD
Start([Start]) --> Ingest[Ingest file]
Ingest --> Validate{Valid markdown?}
Validate -->|No| Reject[Reject with error]
Validate -->|Yes| Parse[Parse blocks]
Parse --> HasMermaid{Mermaid fence?}
HasMermaid -->|Yes| Render[Render SVG]
HasMermaid -->|No| Word[Build DOCX]
Render --> Word
DoArchive{Archive?}
Word --> DoArchive
DoArchive -->|Yes| Persist
DoArchive -->|No| Done
Reject --> Done
Persist[(Optional archive)] --> Done
```

---

## Straight centre-spine flow

<!-- DiagramId: CentreSpineLinear -->

```mermaid
flowchart TD
A[Receive] --> B[Validate]
B --> C[Transform]
C --> D[Publish]
```

---

## Single decision branch

<!-- DiagramId: SingleDecisionBranch -->

```mermaid
flowchart TD
Root[Start] --> Gate{Ready?}
Gate -->|Yes| Proceed[Proceed]
Gate -->|No| Stop[Stop]
```

---

## Nested decision branches

<!-- DiagramId: NestedDecisionBranch -->

```mermaid
flowchart TD
S[Start] --> A{Level A?}
A -->|Yes| B{Level B?}
A -->|No| X[Abort]
B -->|Yes| Y[Complete]
B -->|No| Z[Retry]
```

---

## Multiple branches merging into one node

<!-- DiagramId: MultiMergeCollector -->

```mermaid
flowchart TD
A[Branch A] --> Sink[Collector]
B[Branch B] --> Sink
C[Branch C] --> Sink
```

---

## Long bypass over several ranks

<!-- DiagramId: LongBypassSkip -->

```mermaid
flowchart TD
Top[Top] --> Mid{Skip gate?}
Mid -->|Yes| Bottom[Bottom]
Mid -->|No| Step1[Step 1]
Step1 --> Step2[Step 2]
Step2 --> Bottom
```

---

## Exception path around spine

<!-- DiagramId: LongExceptionPath -->

```mermaid
flowchart TD
Start[Start] --> Check{Valid?}
Check -->|Yes| Continue[Continue]
Check -->|No| Error[Error handler]
Error --> End[End]
Continue --> End
```

---

## Cross-lane link

<!-- DiagramId: CrossLaneLink -->

```mermaid
flowchart TD
Left[Left lane] --> Right[Right lane]
Centre[Centre] --> Left
```

---

## Dense local branches

<!-- DiagramId: DenseLocalBranches -->

```mermaid
flowchart TD
Hub{Hub}
Hub -->|A| A1[Alpha]
Hub -->|B| B1[Beta]
Hub -->|C| C1[Gamma]
Hub -->|D| D1[Delta]
```

---

## Labels on short and long edges

<!-- DiagramId: LabelledEdges -->

```mermaid
flowchart TD
A[Input] --> B{Check}
B -->|short yes| C[Fast path]
B -->|long no| D[Exception path]
D --> E[Recover]
E --> F[Finish]
C --> F
```

---

## Multiple incoming and outgoing edges

<!-- DiagramId: MultiFanInOut -->

```mermaid
flowchart TD
In1[In 1] --> Hub[Hub]
In2[In 2] --> Hub
Hub --> Out1[Out 1]
Hub --> Out2[Out 2]
```

---

## Left-to-right direction

<!-- DiagramId: DirectionLR -->

```mermaid
flowchart LR
L[Left] --> M[Middle]
M --> R[Right]
```

---

## Bottom-to-top direction

<!-- DiagramId: DirectionBT -->

```mermaid
flowchart BT
Low[Low] --> High[High]
```

---

## Right-to-left direction

<!-- DiagramId: DirectionRL -->

```mermaid
flowchart RL
Right[Right] --> Left[Left]
```
