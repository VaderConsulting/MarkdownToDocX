# Focus fixtures (extracted from Stress.md)

These diagrams are copied from `Stress.md` to enable fast, targeted SvgHarness runs.

---

## Router (layered view)

```mermaid
flowchart TD
    M[Message arrives] --> R{Router rules}
    R -->|type A| C1[Channel A]
    R -->|type B| C2[Channel B]
    R -->|no match| D[Default / dead letter]
```

---

## Load balance (layered view)

```mermaid
flowchart TD
    Q[Queue] --> L{Load balance}
    L --> C1[Instance 1]
    L --> C2[Instance 2]
    L --> C3[Instance 3]
    C1 --> DL[Shared DLQ policy]
    C2 --> DL
    C3 --> DL
```

---

## Selective consumer (primary topology)

```mermaid
flowchart LR
    A["Shared channel"] --> B{"Selected by consumer?"}
    B -- Yes --> C["Consume"]
    B -- No --> D["Leave for others / ignore"]
```

---

## Idempotent receiver (primary topology)

```mermaid
flowchart LR
    A["Receive message"] --> B{"Already processed?"}
    B -- Yes --> C["Ignore safely"]
    B -- No --> D["Process and record"]
```

---

## Detour (layered view)

```mermaid
flowchart TD
    M[Message] --> D{Detour flag?}
    D -->|on| X[Inspection / shadow]
    X --> R[Rejoin main path]
    D -->|off| R
```

