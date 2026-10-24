# Fixture render spot check (rules 1.8 / 3.11 batch)

Small extract for manual Word conversion. Do not use as the full flow or enterprise catalogue.

## F124 — Nodes first: retry loop (equiv. F34)

```mermaid
flowchart LR
A[Receive]
B{OK?}
C[Done]
D[Retry]

A --> B
B -->|Yes| C
B -->|No| D
D --> A
```

## F125 — Nodes first: diamond with multiple incoming edges

```mermaid
flowchart TB
PA[Path A]
PB[Path B]
PC[Path C]
D{Merge?}
E[Continue]
F[Stop]

PA --> D
PB --> D
PC --> D
D -->|yes| E
D -->|no| F
```

## F135 — Nodes first: approval decision with a review detour (equiv. F119)

```mermaid
flowchart TB
Start([Begin])
check{Approved?}
Ship[Ship order]
review
Reject[Reject order]
End([Finish])

Start --> check
check -->|Yes| Ship
check -->|No| review
review --> Reject
Ship --> End
Reject --> End
```

## F137 — Nodes first: polling loop with back edge (equiv. F124)

```mermaid
flowchart TB
Poll[Poll queue]
Handle{Handled?}
Ack[Acknowledge]
backoff

Poll --> Handle
Handle -->|Yes| Ack
Handle -->|No| backoff
backoff --> Poll
```

## F140 — Nodes first: mixed edge styles with labels (equiv. F131)

```mermaid
flowchart LR
A[Request]
B[Primary]
C
D[Fallback]
E[Log]

A -->|try| B
A -.->|if down| D
B ==> C
D --- C
C --x E
```

## Batch 2 catalogue equivalents (reference renders)

### F34 — Simple cycle (retry loop)

```mermaid
flowchart LR
A[Receive] --> B{OK?}
B -->|Yes| C[Done]
B -->|No| D[Retry]
D --> A
```

### F131 — Nodes first: mixed edge styles declared after nodes (equiv. F18)

```mermaid
flowchart LR
A[A]
B[B]
C[C]
D[D]

A --> B
C --- D
```
