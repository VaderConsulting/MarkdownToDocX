# Enterprise Integration Patterns Reference

**Document version:** 1.3  
**Issue Date:** 16-04-2026  
**Document Owner:** DFES Strategy, Planning & Architecture  

## Document history

| Version | Date       | Description |
| ------- | ---------- | ----------- |
| 1.0     | 10-04-2026 | Standalone Enterprise Integration Patterns reference document. |
| 1.1     | 13-04-2026 | Formal document control; programme monitoring SoT (**DFES monitoring architecture**, **Integration monitoring specification**) in opening section. |
| 1.2     | 15-04-2026 | **§2** pattern titles and in-document links show **book page numbers** (parentheses) for the first edition: Hohpe & Woolf, *Enterprise Integration Patterns*, Addison-Wesley, 2003 (ISBN 9780321200686). |
| 1.3     | 16-04-2026 | **§2** catalogue Markdown headings (`## 2.n. …`) include the same first-edition **book page numbers** in parentheses as **§1.2 Glossary** (for example `## 2.1. File Transfer (43)`). |

This document provides the DFES pattern reference for the full Enterprise Integration Patterns catalogue used in this standard. Authoritative external references are listed under **§1.1 Bibliography**.

The purpose of this document is to:

- provide a concise **Overview** and a fuller **Description** for each referenced pattern (aligned with the pattern numbering in §2)
- explain when the pattern should usually be considered
- keep the standard usable for developers without requiring them to infer the intended pattern vocabulary
- provide **When not to use** (as a bullet list), separate **Combinations** and **Anti-patterns** sections, and supplementary Mermaid diagrams per pattern where they add clarity

The overviews and descriptions in this document are DFES-focused guidance, not a replacement for the source material by Gregor Hohpe and Bobby Woolf.

**DFES operational telemetry (integrations and applications):** Architecture decisions and the normative implementer standard for DFES integration monitoring (OpenTelemetry, OTLP, CoreMonitor dual export, rejected collection patterns, mandatory telemetry schemas and behaviours) are defined in **3-Integration monitoring design.md**. This patterns catalogue refers to “monitoring” in the generic integration sense; it does not override that wiki standard.

## 1. About this document

This document provides a concise DFES-oriented reference to selected Enterprise Integration Patterns discussed in the Standard Error and Status Messages standard. **§1.2 Glossary** lists integration terms alphabetically (including every **§2** pattern name). Each pattern entry includes an **Overview**, a fuller **Description**, **Use when**, **When not to use** (bullet list), **Typical scenarios**, **Related patterns**, **Combinations**, **Anti-patterns**, and one or more Mermaid diagrams (flow and, where helpful, sequence or decision flows).

It is not intended to replace the original source material. For fuller detail, see **§1.1 Bibliography**.

This document summarises the patterns in a way that is intended to support practical application within DFES integration and application design. **§3** summarises cross-cutting combinations and recurring anti-patterns that span multiple catalogue entries.

## 1.1. Bibliography

Gregor Hohpe and Bobby Woolf, *Enterprise Integration Patterns: Designing, Building, and Deploying Messaging Solutions*, Addison-Wesley, 2003.

- ISBN-10: 0321200683  
- ISBN-13: 9780321200686  

Official pattern catalogue (messaging): `https://www.enterpriseintegrationpatterns.com/patterns/messaging/`

Page numbers shown beside **§2** pattern names (for example **Guaranteed Delivery (122)**) refer to the printed book above; pagination matches the Addison-Wesley first edition (later reprints use the same numbering).

This document also relates to the **Standard Error and Status Messages** standard (DFES), which references selected Enterprise Integration Patterns where messaging and integration behaviour affects error and status handling.

## 1.2. Glossary

Terms below are used in the sense of this document and the Enterprise Integration Patterns material, unless stated otherwise. Entries are sorted **A–Z** by the principal word (pattern names sort as written). **§2** catalogue patterns link to their entries.

### A

- **Aggregator (268)** - Collects related messages until a completion rule is met, then emits a combined outcome (fan-in counterpart to splitting or parallel gather).
- **Anti-patterns** - Documented integration mistakes or unsafe usages called out under each pattern so teams can recognise and avoid them.
- **Application** - A software system that participates in integration by sending, receiving, storing, or transforming data.

### B

- **Bulkhead** - Fault-isolation so one failing dependency cannot exhaust all capacity; discussed with timeouts and SLAs alongside synchronous and RPC-style calls.

### C

- **Callee** - The invoked application or service in a request/response or RPC-style call (contrast **Caller**).
- **Caller** - The application that initiates a request/response or RPC-style operation and waits for or handles the outcome.
- **Canonical Data Model (355)** - A shared, stable semantic model that applications map into and out of to limit pairwise translation explosion.
- **Catalogue** - The set of named Enterprise Integration Patterns (EIP) entries in **§2**, each with a stable title and anchor.
- **Channel** - Generic logical pathway for data or messages between participants; in products often realised as queues, topics, subscriptions, or streams (see **Message Channel (60)**).
- **Channel Adapter (127)** - Connects an application or external technology to the messaging layer so non-messaging APIs or files can participate in channels.
- **Channel Purger (572)** - Removes messages from a channel administratively, for example to recover from overload or poison-message situations (use with governance).
- **Checksum** - Data used to verify file or payload integrity; often mentioned with file-based integration and **manifests**.
- **Circuit breaker** - Stops calling a failing dependency for a cool-down period so cascading failures and retry storms are reduced; used with RPC-style integration.
- **Claim Check (346)** - Stores a large payload externally and passes a lightweight reference (claim) on the message bus to avoid oversized messages.
- **Command Message (145)** - A message that requests a single action at a receiver, usually with one logical handler.
- **Combinations** - Common ways to use a pattern together with other patterns or controls, often with links to related catalogue entries.
- **Competing Consumers (502)** - Multiple consumer instances share one point-to-point channel so each message is handled by one instance and load is spread.
- **Composed Message Processor (294)** - Orchestrates split, route, translate, and aggregate steps as one logical pattern for composite messages.
- **Consumer** - Application component or process that receives and processes messages from a channel (see also **Polling Consumer (494)**, **Event-Driven Consumer (498)**).
- **Content Enricher (336)** - Adds missing data to a message by lookup or computation before downstream processing.
- **Content Filter (342)** - Removes unnecessary fields for a downstream partner while preserving a richer form elsewhere if needed.
- **Content-Based Router (230)** - Routes a message to one of several channels based on its content or metadata.
- **Control Bus (540)** - Uses messaging to manage, probe, or reconfigure integration components across the bus.
- **Correlation Identifier (163)** - A token carried on related messages so replies, events, and logs can be tied to one logical operation or transaction.

### D

- **Datatype Channel (111)** - A channel dedicated to one payload type or contract so consumers do not share incompatible message shapes on one queue.
- **Dead Letter Channel (119)** - Holds messages that cannot be processed successfully after retries (often called a **DLQ**).
- **DLQ** - See **Dead Letter Channel**.
- **Detour (545)** - Temporarily routes messages through an alternate path (for example maintenance or debugging) before returning to the main flow.
- **Document Message (147)** - A message that carries a structured business document (versioned contract) rather than a single imperative command.
- **Drop zone** - A shared location (often **SFTP** or a folder) where files are deposited for pickup and downstream processing.
- **Durable store** - Persistent storage in the messaging path so messages survive broker or client restarts (related to **Guaranteed Delivery (122)**).
- **Durable Subscriber (522)** - A subscriber that can disconnect and later receive messages it missed, according to product semantics.
- **Dynamic Router (243)** - Determines target channels at runtime using external configuration or registry data rather than only static rules.

### E

- **Enterprise Integration Patterns (EIP)** - The integration design vocabulary published by Hohpe and Woolf; **§2** in this document is a DFES-oriented catalogue of those patterns. See **§1.1 Bibliography** for the authoritative text.
- **Envelope Wrapper (330)** - Wraps domain payload in a standard envelope for routing, security, or versioning while keeping the inner body stable.
- **Event Message (151)** - Announces that something happened; one event may interest many subscribers without naming a single handler.
- **Event-Driven Consumer (498)** - A consumer activated asynchronously when messages arrive, rather than by polling.
- **External system** - Partner, legacy, or third-party application connected via file, API, or messaging.

### F

- **File Transfer (43)** - Integration style where applications exchange files on agreed schedules or locations instead of direct online calls.
- **Fallback** - Alternate behaviour when the primary path fails (for example after timeout); discussed with **Retry** and RPC resilience.
- **Fan-in** - Combining many partial results or message streams into one outcome (see **Aggregator (268)** and **Scatter-Gather (297)**).
- **Fan-out** - Delivering one message to many recipients (see **Publish-Subscribe Channel (106)** and **Recipient List (249)**).
- **Format Indicator (180)** - Metadata that names message schema or version so consumers can interpret or migrate payloads safely.

### G

- **Guaranteed Delivery (122)** - Ensures messages are not lost in normal failure scenarios, using persistence, acknowledgements, and redelivery semantics.

### H

- **Handler** - Application logic that executes in response to a **Command Message** or similar inbound message.
- **Headers** - Message metadata (routing, correlation, type, security, etc.) carried with the body (see **Message (66)**).

### I

- **Idempotent Receiver (528)** - Processing that yields the same effect if a message is delivered more than once, which safe retries require.
- **Invalid Message Channel (115)** - Receives messages that fail validation or cannot be parsed so they do not block the main consumer.

### M

- **Manifest** - Accompanying file or metadata listing transferred files or batches, often with **checksums**, for file-based integration.
- **Message (66)** - A discrete unit (payload and usually **headers**) sent through a **Messaging system**.
- **Message Broker (322)** - Middleware that routes and may transform messages between producers and consumers.
- **Message Bus (137)** - A common messaging backbone with agreed topics, schemas, and operational standards across producers.
- **Message Channel (60)** - The logical conduit from sender to receiver; the core abstraction for queues and topics in this catalogue.
- **Message Dispatcher (508)** - Dispatches messages to workers or handlers according to load or type while sharing one consumer entry point.
- **Message Endpoint (95)** - The bridge between application code and the **Messaging system** (connect, send, receive, acknowledge).
- **Message Expiration (176)** - Time-to-live or discard rules so stale messages do not accumulate or replay indefinitely.
- **Message Filter (237)** - Drops messages that do not meet criteria so downstream consumers remain simple.
- **Message History (551)** - Records a trail of processing steps or systems a message passed through for support and audit.
- **Message Router (78)** - Decides which channel should receive an incoming message while keeping producers and consumers decoupled.
- **Message Sequence (170)** - Identifies ordered fragments of a logical message so they can be reassembled or validated.
- **Message Store (555)** - Persists messages for replay, audit, or stateful patterns such as correlation-heavy processing.
- **Message Translator (85)** - Maps between message formats or models (including to or from a **Canonical Data Model**).
- **Messaging (53)** - Integration style that exchanges **messages** through middleware for time and location decoupling.
- **Messaging Bridge (133)** - Connects two messaging systems or namespaces, translating protocols or destinations as needed.
- **Messaging Gateway (468)** - Encapsulates messaging behind a simple API or facade so application code stays messaging-naive.
- **Messaging Mapper (477)** - Maps between domain objects and message representations at the boundary of an application.
- **Messaging system** - Infrastructure (brokers, queues, topics) that accepts, stores, routes, and delivers **messages**.

### N

- **Normalizer (352)** - Converts heterogeneous messages into a common format on ingress so a single processor can handle many sources.

### O

- **Observability** - Operational visibility through logging, metrics, tracing, and dead-letter handling; often referenced with topics, **DLQ**, and standards on a **Message Bus**.

### P

- **Partner** - External organisation or system exchanging data under a contract (for example reduced payloads or B2B channels).
- **Pattern** - A named, reusable integration building block described in its own **§2** subsection.
- **Pipes and Filters (70)** - Chains processing stages (**filters**) connected by **channels**, each with a narrow responsibility.
- **Polling Consumer (494)** - A consumer that repeatedly requests or fetches messages from a channel on an interval or loop.
- **Point-to-Point Channel (103)** - A channel where each message is consumed by one receiver (or one competing consumer path).
- **Process Manager (312)** - Coordinates long-lived or multi-step flows driven by events and timeouts (a process-centric router).
- **Producer** - Application or component that sends **messages** to a channel (often interchangeable with **Sender** in diagrams).
- **Publish-Subscribe Channel (106)** - A channel where publishers fan out events to multiple subscribers.
- **Publisher** - Sender that emits messages to a **Publish-Subscribe Channel** (often a **topic**).

### Q

- **Queue** - Point-to-point destination; messages are typically consumed once by one consumer or competing consumer group (**work queue**).

### R

- **Receiver** - Application or endpoint that accepts messages from a channel (may be implemented by a **Consumer**).
- **Recipient List (249)** - Sends a copy of the same message to a configured set of channels for parallel or multi-party processing.
- **Related patterns** - Other catalogue entries usually considered alongside the current pattern.
- **Remote Procedure Invocation (50)** - Integration style that calls another application as a remote operation with a synchronous-style response.
- **Reply channel** - Channel carrying the response in a **Request-Reply** exchange.
- **Request channel** - Channel carrying the outbound request in a **Request-Reply** exchange.
- **Request-Reply (154)** - Pairing of request and response messages (often with **Correlation Identifier** and **Return Address**).
- **Resequencer (283)** - Restores intended order when related messages arrive out of sequence.
- **Retry** - Redelivery or caller repetition after failure; must be paired with **Idempotent Receiver** and limits to avoid storms (**Circuit breaker**).
- **Return Address (159)** - Tells the replier where to send the response (reply-to destination) in asynchronous **Request-Reply**.
- **Routing Slip (301)** - Message-attached instructions listing the sequence of processing steps or recipients to visit.

### S

- **Scatter-Gather (297)** - Broadcasts to multiple recipients and aggregates replies into one result (often with timeouts and partial-failure rules).
- **Schema owner** - Team or role accountable for a schema or **Canonical Data Model** projection.
- **Schema registry** - Governed store of message contracts or schemas referenced in bus standards and **Observability** discussions.
- **Selective Consumer (515)** - Consumer that receives from a shared channel but processes only messages matching its criteria.
- **Sender** - Originates a message onto a channel (see **Producer**).
- **Service** - Often the **Callee** in RPC diagrams; an exposed set of operations another application invokes.
- **Service Activator (532)** - Connects messaging to application service methods (invoke business logic on message arrival).
- **Shared Database (47)** - Integration style where applications read and write one governed data store.
- **Shared file** - File used as the hand-off between applications in **File Transfer**.
- **SFTP** - Common file-exchange transport; see **Drop zone**.
- **Smart Proxy (558)** - Intercepts request-reply traffic to add management, testing, or cross-cutting behaviour transparently.
- **Splitter (259)** - Splits one logical message into many child messages for parallel or per-item processing.
- **Subscriber** - Consumer of a **Publish-Subscribe Channel** or **topic**.

### T

- **Target consumer** - Intended downstream application or process for a file or message (used in file and messaging sequences).
- **Test Message (569)** - Synthetic traffic to verify routes, transformations, or monitoring without affecting production data.
- **Timeout** - Maximum wait before a caller or broker abandons an operation; central to RPC reliability and **Scatter-Gather** partial-result policy.
- **Topic** - Publish-subscribe destination; each message can be delivered to multiple **subscribers**.
- **Transactional Client (484)** - Couples message sends or acknowledgements with application or database transactions for consistency.
- **Typical scenarios** - Short, bullet-listed situations where a pattern is commonly relevant (illustrative, not exhaustive).

### V

- **Validation** - Checking messages for schema, size, or business rules before processing; failures may route to an **Invalid Message Channel (115)**.

### W

- **When not to use** - Bullet-listed situations where a pattern is usually a poor fit or needs strong safeguards.
- **Wire Tap (547)** - Non-invasively copies messages to an audit or monitoring path without altering the main flow.
- **Work queue** - **Point-to-point** **queue** used to distribute commands or tasks to workers.

## 2. Pattern Reference Entries
## 2.1. File Transfer (43)
**Category**  
Integration style

**Overview**  
Applications exchange files at agreed times or in agreed locations instead of calling each other directly.

**Description**  
**File Transfer (43)** is one of the classic integration styles described alongside other high-level approaches in the Enterprise Integration Patterns material: applications coordinate by producing and consuming files in agreed locations or on agreed schedules, rather than calling each other directly. The sender and receiver are loosely coupled in time because processing can happen when each side is ready, as long as naming, format, and lifecycle rules are clear.

The pattern is simple to reason about and works well with batch schedules, SFTP drops, and legacy systems that only speak files. Trade-offs include late discovery of errors, the need for idempotent or reconciled consumption, and operational care around partial files, duplicates, and file locking.

**Use when**  
Use when batch-style data exchange is acceptable and both sides can work with files.

**When not to use**  
- Avoid when the business needs immediate confirmation, sub-second consistency, or continuous online validation against the source.
- Avoid when ownership of naming, schema, idempotency, and error paths cannot be governed, or when frequency and volume make file churn and reconciliation unmanageable.

**Typical scenarios**  
- Legacy transfers
- Scheduled exports
- SFTP drops
- Partner file exchange

**Related patterns**  
**Shared Database (47)**, **Messaging (53)**, **Message Translator (85)**.

**Combinations**  

Production file exchange usually layers integrity, security, semantic alignment, and lifecycle controls on top of the raw transfer.

- **Manifests and checksums** - Publish a manifest (file list, sizes, hashes) with each drop so the consumer can reject incomplete or corrupted batches before load. Align hash algorithms and canonical paths with partners in the integration contract.

:::mermaid
flowchart LR
    subgraph R0S0["Manifests and checksums"]
        direction TB
        r0n0["Publish a manifest (file list, sizes, hashes) with each drop so the consumer can reject incomplet..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Optional scanning** - Run malware or content inspection in the landing zone before files are promoted to processing; quarantine anything suspicious separately from business validation failures.

:::mermaid
flowchart LR
    subgraph R1S0["Optional scanning"]
        direction TB
        r1n0["Run malware or content inspection in the landing zone before files are promoted to processing; qu..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Normalizer (352)** / **Message Translator (85)** - At ingest, map partner-specific layouts into one internal shape or directly into the hub model so downstream jobs do not re-implement N different parsers. Keep mapping tables versioned and owned.

:::mermaid
flowchart LR
    subgraph R2S0["Normalizer"]
        direction TB
        r2n0["At ingest, map partner-specific layouts into one internal shape or directly into the hub model so..."]
    end
    subgraph R2S1["Message Translator"]
        direction TB
        r2n1["At ingest, map partner-specific layouts into one internal shape or directly into the hub model so..."]
    end
    IN[In] --> r2n0
    r2n0 --> r2n1
    r2n1 --> OUT[Out]
:::

- **Canonical Data Model (355)** - Land translated rows or documents against a governed canonical schema (or projection) so reporting and downstream services share one semantic contract rather than many ad hoc extracts.

:::mermaid
flowchart LR
    subgraph R3S0["Canonical Data Model"]
        direction TB
        r3n0["Land translated rows or documents against a governed canonical schema (or projection) so reportin..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

- **Pick up / archive / quarantine** - Use distinct folders or object prefixes for each state (inbound, in progress, success archive, quarantine). Operators can re-drive failed loads without mixing bad files with clean history.

:::mermaid
flowchart LR
    subgraph R4S0["Pick up / archive / quarantine"]
        direction TB
        r4n0["Use distinct folders or object prefixes for each state (inbound, in progress, success archive, qu..."]
    end
    IN[In] --> r4n0
    r4n0 --> OUT[Out]
:::

**Anti-patterns**  
- shared locations without locking or duplicate handling.
- using files to mimic low-latency APIs where **Remote Procedure Invocation (50)** or **Messaging (53)** is appropriate.

---

**Diagrams**

**High-level topology** - two applications sharing a file.

:::mermaid
flowchart LR
    A["Application A"] --> B["Shared file"]
    B --> C["Application B"]
:::

**Drop zone sequence** - write, pickup, validate, and load.

:::mermaid
sequenceDiagram
    participant Src as Source application
    participant Zone as Drop zone / SFTP
    participant Tgt as Target consumer
    Src->>Zone: Write file (+ optional manifest)
    Note over Tgt: Scheduled or triggered pickup
    Tgt->>Zone: Acquire / move to processing
    Tgt->>Tgt: Validate, translate, load
:::

## 2.2. Shared Database (47)
**Category**  
Integration style

**Overview**  
Applications integrate by reading and writing a common data store.

**Description**  
In the **Shared Database (47)** style, multiple applications integrate by reading and writing the same database (or a tightly governed replica). It avoids building explicit message flows because everyone sees the same tables or views, which can speed early delivery when teams share ownership of the schema.

The cost is tight coupling: schema changes ripple across consumers, contention and performance interact unexpectedly, and data ownership can blur. It suits governed reporting stores or reference data where a single owner controls the model; for broader integration, **Messaging (53)** or a **Canonical Data Model (355)** is often healthier.

**Use when**  
Use when tightly shared data is acceptable and governance can control schema and ownership.

**When not to use**  
- Avoid when applications have conflicting lifecycles, release cadences, or data ownership, or when schema drift would paralyse delivery.
- Avoid as a general integration bus between many autonomous systems-**Messaging (53)** and explicit contracts scale better.

**Typical scenarios**  
- Shared reporting stores
- Governed replicas
- Common reference data

**Related patterns**  
**File Transfer (43)**, **Messaging (53)**, **Canonical Data Model (355)**.

**Combinations**  

Shared-database integration scales better when read traffic, write boundaries, and semantic contracts are deliberately constrained.

- **Read-only replicas** - Direct heavy reporting or analytics consumers at a replica so operational workloads stay protected. Define replication lag SLAs so dashboards do not silently read stale data for time-sensitive decisions.

:::mermaid
flowchart LR
    subgraph R0S0["Read-only replicas"]
        direction TB
        r0n0["Direct heavy reporting or analytics consumers at a replica so operational workloads stay protecte..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Views and APIs** - Expose narrow views or stored procedures instead of base tables so consumers cannot accidentally depend on internal columns. Version views when the underlying schema evolves.

:::mermaid
flowchart LR
    subgraph R1S0["Views and APIs"]
        direction TB
        r1n0["Expose narrow views or stored procedures instead of base tables so consumers cannot accidentally ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Canonical Data Model (355)** **projections** - Materialise or synchronise canonical-shaped tables or views for integration consumers, rather than letting each team interpret raw operational tables differently. Impact-analyse changes through a single owner.

:::mermaid
flowchart LR
    subgraph R2S0["Canonical Data Model"]
        direction TB
        r2n0["Materialise or synchronise canonical-shaped tables or views for integration consumers, rather tha..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Direct table sharing** - When unavoidable, document which tables are integration contracts, who may write them, and how idempotency and locking work under concurrent writers.

:::mermaid
flowchart LR
    subgraph R3S0["Direct table sharing"]
        direction TB
        r3n0["When unavoidable, document which tables are integration contracts, who may write them, and how id..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- “integration tables” written by many apps without a single owner.
- using the operational OLTP database as an ungoverned integration hub.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Application A"] --> B["Shared database"]
    C["Application B"] --> B
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    O[Single schema owner] --> G[Governed views / APIs]
    G --> A[Consumer A]
    G --> B[Consumer B]
:::

## 2.3. Remote Procedure Invocation (50)
**Category**  
Integration style

**Overview**  
One application invokes operations exposed by another application as though it were a remote service.

**Description**  
**Remote Procedure Invocation (50)** treats another application as a callable service: the caller issues an operation and typically waits for a synchronous response, much like a local method call. It maps naturally to HTTP APIs, gRPC, and similar request/response protocols, as discussed in the integration-styles material that precedes the messaging pattern catalogue.

It is straightforward when latency is low and both ends are available, but it couples caller and callee in time and often in failure modes. For long-running work, bursts of load, or many downstream dependencies, asynchronous **Messaging (53)** with **Request-Reply (154)** is often a better fit.

**Use when**  
Use when immediate request/response behaviour is required and both sides can tolerate tighter coupling.

**When not to use**  
- Avoid for long-running or multi-step work where the caller cannot hold a connection, or when cascading failures across many synchronous hops dominate reliability.
- Avoid when the callee’s availability or latency is uncertain and you need buffering and decoupling-prefer **Messaging (53)** with **Message Channel (60)**.

**Typical scenarios**  
- Synchronous API calls
- Service operations
- Command-style integration

**Related patterns**  
**Messaging (53)**, **Request-Reply (154)**, **Messaging Gateway (468)**.

**Combinations**  

RPC paths need explicit time bounds, failure containment, and stable contracts so callers do not block forever or amplify outages.

- **Timeouts** - Set connect, read, and end-to-end deadlines per dependency class. Propagate or map timeout errors into your standard error envelope so upstream retries behave predictably.

:::mermaid
flowchart LR
    subgraph R0S0["Timeouts"]
        direction TB
        r0n0["Set connect, read, and end-to-end deadlines per dependency class. Propagate or map timeout errors..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Circuit breakers** - Trip after sustained failures so threads and connection pools are not exhausted on a sick downstream. Half-open probes should use the same budgets as normal calls.

:::mermaid
flowchart LR
    subgraph R1S0["Circuit breakers"]
        direction TB
        r1n0["Trip after sustained failures so threads and connection pools are not exhausted on a sick downstr..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Error contracts** - Publish machine-readable error codes, correlation ids, and retry guidance (idempotent vs not) in API specifications. Align with the organisation’s Standard Error and Status Messages where applicable.

:::mermaid
flowchart LR
    subgraph R2S0["Error contracts"]
        direction TB
        r2n0["Publish machine-readable error codes, correlation ids, and retry guidance (idempotent vs not) in ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Messaging Gateway (468)** - Where legacy or third-party code cannot speak messaging natively, place a gateway that exposes RPC-style APIs while the integration backbone remains asynchronous behind the facade.

:::mermaid
flowchart LR
    subgraph R3S0["Messaging Gateway"]
        direction TB
        r3n0["Where legacy or third-party code cannot speak messaging natively, place a gateway that exposes RP..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- chatty fine-grained RPC that creates latency chains and overload.
- synchronous calls across organisational boundaries without SLAs or bulkheads.

---

**Diagrams**

**Sequence** - message exchange over time.

**Sequence** - variant 2: use when you need failure or timeout paths and caller-side retry, circuit, or fallback (the primary diagram is success-only).

:::mermaid
sequenceDiagram
    participant Caller
    participant Service
    Caller->>Service: Request
    alt Success
        Service-->>Caller: 2xx + body
    else Failure / timeout
        Service-->>Caller: Error or timeout
        Note over Caller: Retry / circuit / fallback policy
    end
:::

## 2.4. Messaging (53)
**Category**  
Integration style

**Overview**  
Applications exchange messages through messaging infrastructure rather than direct synchronous calls.

**Description**  
The **Messaging (53)** integration style exchanges self-contained messages through middleware (queues, topics, buses) instead of direct synchronous calls. Senders and receivers are decoupled in time and location: the messaging system can buffer, retry, and route while applications remain simpler. This is the style assumed by the rest of the messaging pattern catalogue (channels, endpoints, routing, and transformation).

This style underpins event-driven architectures and resilient integration. Designing channels, message shapes, idempotency, and observability still matters; the catalogue patterns (channels, endpoints, routing, transformation) build on this foundation.

**Use when**  
Use when asynchronous exchange, decoupling, buffering, and flexible routing are desirable.

**When not to use**  
- Avoid when simplicity and debuggability of a single synchronous call outweigh decoupling, or when the organisation cannot operate brokers, monitoring, and dead-letter discipline.
- Avoid “messaging for messaging’s sake” for trivial point-to-point cases with no durability need.

**Typical scenarios**  
- Event-driven integration
- Subscribers
- Queues
- Resilient decoupling

**Related patterns**  
**Message Channel (60)**, **Message Endpoint (95)**, **Message Bus (137)**.

**Combinations**  

Treat messaging as a product: name the logical paths, connect applications through well-behaved endpoints, persist where it matters, and always give poison messages an exit route.

- **Message Channel (60)** - Define queues and topics with clear ownership, purpose (command vs event), and environment naming. Document ordering and duplicate-delivery assumptions per channel.

:::mermaid
flowchart LR
    subgraph R0S0["Message Channel"]
        direction TB
        r0n0["Define queues and topics with clear ownership, purpose (command vs event), and environment naming..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message Endpoint (95)** - Standardise how applications connect, serialise, acknowledge, and handle back-pressure. Share libraries for these concerns across services.

:::mermaid
flowchart LR
    subgraph R1S0["Message Endpoint"]
        direction TB
        r1n0["Standardise how applications connect, serialise, acknowledge, and handle back-pressure. Share lib..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Guaranteed Delivery (122)** - Enable broker persistence, durable producers where needed, and consumer acknowledgement rules that match your consistency goals. Pair with idempotent handlers.

:::mermaid
flowchart LR
    subgraph R2S0["Guaranteed Delivery"]
        direction TB
        r2n0["Enable broker persistence, durable producers where needed, and consumer acknowledgement rules tha..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Dead Letter Channel (119)** - After bounded retries, move unprocessable messages to a DLQ with reason codes, original payload pointers, and operational runbooks (replay vs discard).

:::mermaid
flowchart LR
    subgraph R3S0["Dead Letter Channel"]
        direction TB
        r3n0["After bounded retries, move unprocessable messages to a DLQ with reason codes, original payload p..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- no operational story for poison messages, backlogs, or replay.
- using topics for command processing that must be handled exactly once without consumer design.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Sender"] --> B["Messaging system"] --> C["Receiver"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    S[Sender] --> Q[Channel / broker]
    Q --> C[Consumer]
    Q --> DL[Dead letter / quarantine]
    C -->|failure after retry| DL
:::

## 2.5. Message Channel (60)
**Category**  
Messaging system

**Overview**  
A logical pathway that carries messages between senders and receivers.

**Description**  
A **Message Channel (60)** is the logical pathway over which messages travel from sender to receiver. Physical products implement it as queues, topics, subscriptions, or streams, but the pattern is about a named conduit with defined semantics (ordering, durability, fan-out).

Choosing channel granularity affects scalability and clarity: too many channels add operational overhead; too few mixes unrelated traffic. Datatype channels, point-to-point versus publish-subscribe, and dead-letter handling are common specialisations.

**Use when**  
Use when components need a defined route for message exchange.

**When not to use**  
- Avoid over-fragmenting channels so that operations and access control become unmanageable.
- Avoid reusing one channel for unrelated traffic when consumers cannot safely select or filter without contention.

**Typical scenarios**  
- Queues
- Topics
- Subscriptions
- Logical pathways between components

**Related patterns**  
**Point-to-Point Channel (103)**, **Publish-Subscribe Channel (106)**, **Datatype Channel (111)**.

**Combinations**  

Channels should be discoverable in operations tooling and safe for consumers: separate traffic by intent and make contracts explicit on the wire.

- **Naming** - Encode domain, message family, environment, and sometimes region in channel names. Avoid anonymous queues that only one person understands.

:::mermaid
flowchart LR
    subgraph R0S0["Naming"]
        direction TB
        r0n0["Encode domain, message family, environment, and sometimes region in channel names. Avoid anonymou..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Datatype Channel (111)** - When payloads differ materially (JSON vs binary, or incompatible schemas), use separate channels so consumers are not forced to branch on every message.

:::mermaid
flowchart LR
    subgraph R1S0["Datatype Channel"]
        direction TB
        r1n0["When payloads differ materially (JSON vs binary, or incompatible schemas), use separate channels ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Format Indicator (180)** - When one channel carries multiple schema versions during migration, require version metadata in headers or envelope so consumers can parse and reject unknown versions deterministically.

:::mermaid
flowchart LR
    subgraph R2S0["Format Indicator"]
        direction TB
        r2n0["When one channel carries multiple schema versions during migration, require version metadata in h..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Operational pairing** - Document monitoring (depth, age, DLQ rate) per channel and tie access control to the owning team.

:::mermaid
flowchart LR
    subgraph R3S0["Operational pairing"]
        direction TB
        r3n0["Document monitoring (depth, age, DLQ rate) per channel and tie access control to the owning team."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- anonymous or ad hoc queues with no owner.
- one “god queue” mixing commands, documents, and events without dispatch rules.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Sender"] --> B["Message channel"] --> C["Receiver"]
:::

**Flowchart (left-to-right)** - variant 2: use when you need both work-queue (P2P) and topic fan-out in one view (the primary is a single undifferentiated channel).

:::mermaid
flowchart LR
    subgraph Commands["Commands (P2P)"]
        C1[Producer] --> Q1[Work queue]
    end
    subgraph Events["Events (pub/sub)"]
        E1[Producer] --> T[Topic]
        T --> S1[Subscriber]
        T --> S2[Subscriber]
    end
:::

## 2.6. Message (66)
**Category**  
Messaging system

**Overview**  
A discrete package of data sent from one participant to another through a messaging system.

**Description**  
The **Message (66)** pattern is the atomic unit of integration: a discrete payload (and usually headers) sent through a messaging system. It packages intent-command, document, or event-so that producers and consumers can evolve independently within agreed contracts.

Good message design considers versioning (**Format Indicator (180)**), correlation, size (**Claim Check (346)**), and expiry. Messages should be meaningful on their own where possible, so retries and replays remain safe when combined with **Idempotent Receiver (528)**.

**Use when**  
Use when information must be packaged, transmitted, and handled independently of sender and receiver runtime.

**When not to use**  
- Avoid overloading a single message type with incompatible intents (for example mixing imperative commands with passive events without clear handling).
- Avoid enormous self-contained payloads when **Claim Check (346)** or attachment patterns would keep the bus healthy.

**Typical scenarios**  
- Events
- Commands
- Notifications
- Integration payloads

**Related patterns**  
**Command Message (145)**, **Document Message (147)**, **Event Message (151)**.

**Combinations**  

Stable integration depends on classifying intent, isolating incompatible shapes, and shedding noise before expensive handlers run.

- **Command Message (145)**, **Document Message (147)**, **Event Message (151)** - Encode intent in message type and naming conventions so routers and humans can reason about side effects, fan-out, and idempotency expectations.

:::mermaid
flowchart LR
    subgraph R0S0["Command Message"]
        direction TB
        r0n0["Encode intent in message type and naming conventions so routers and humans can reason about side ..."]
    end
    subgraph R0S1["Document Message"]
        direction TB
        r0n1["Encode intent in message type and naming conventions so routers and humans can reason about side ..."]
    end
    subgraph R0S2["Event Message"]
        direction TB
        r0n2["Encode intent in message type and naming conventions so routers and humans can reason about side ..."]
    end
    IN[In] --> r0n0
    r0n0 --> r0n1
    r0n1 --> r0n2
    r0n2 --> OUT[Out]
:::

- **Datatype Channel (111)** - Route each major payload family to its own channel when parsers, SLAs, or security zones differ.

:::mermaid
flowchart LR
    subgraph R1S0["Datatype Channel"]
        direction TB
        r1n0["Route each major payload family to its own channel when parsers, SLAs, or security zones differ."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Format Indicator (180)** - Carry schema name and version on every message during evolution; pair with compatibility tests in CI.

:::mermaid
flowchart LR
    subgraph R2S0["Format Indicator"]
        direction TB
        r2n0["Carry schema name and version on every message during evolution; pair with compatibility tests in..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Message Filter (237)** - Drop or divert irrelevant traffic early (for example old versions or non-matching tenants) so workers stay simple and cheap.

:::mermaid
flowchart LR
    subgraph R3S0["Message Filter"]
        direction TB
        r3n0["Drop or divert irrelevant traffic early (for example old versions or non-matching tenants) so wor..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- “blob” messages with no version or key, making **Correlation Identifier (163)** and safe replay impossible.
- implicit shared mutable state that only makes sense if sender and receiver run together.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Producer"] --> B["Message"] --> C["Consumer"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    M[Message] --> H[Headers / metadata]
    M --> P[Payload]
    H --> C[Correlation id]
    H --> F[Format / version]
    H --> T[Type: command / document / event]
:::

## 2.7. Pipes and Filters (70)
**Category**  
Messaging system

**Overview**  
Processing is decomposed into a sequence of independent filters connected by channels.

**Description**  
**Pipes and Filters (70)** structures processing as a pipeline: independent filters perform steps such as validation, enrichment, or routing, connected by **Message (66)** Channels. Each filter has a narrow responsibility, which improves testability and reuse.

This pattern suits integration chains where the same stages appear in different orders or where you want to add or remove steps without rewriting a monolith. Watch for error handling, ordering, and performance across many hops-often addressed with **Dead Letter Channel (119)**, **Wire Tap (547)**, or **Message History (551)**.

**Use when**  
Use when complex processing is clearer and safer as a set of small transformation or decision steps.

**When not to use**  
- Avoid long linear chains without observability or dead-letter strategy-debugging becomes painful.
- Avoid splitting what is logically one atomic business validation into many hops if failure compensation is unclear.

**Typical scenarios**  
- Validation pipelines
- Enrichment chains
- Staged integration processing

**Related patterns**  
**Message Channel (60)**, **Message Filter (237)**, **Message Translator (85)**.

**Combinations**  

Pipelines need observability without changing business outcomes, and explicit terminals for bad data versus poison processing.

- **Wire Tap (547)** - Tee a copy to audit or metrics paths asynchronously so support can inspect traffic patterns without delaying the main pipeline.

:::mermaid
flowchart LR
    subgraph R0S0["Wire Tap"]
        direction TB
        r0n0["Tee a copy to audit or metrics paths asynchronously so support can inspect traffic patterns witho..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message History (551)** - Append hop metadata when messages traverse many filters so incidents can be reconstructed across stages.

:::mermaid
flowchart LR
    subgraph R1S0["Message History"]
        direction TB
        r1n0["Append hop metadata when messages traverse many filters so incidents can be reconstructed across ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Dead Letter Channel (119)** - Use when the message is structurally valid but processing fails after retries (downstream outage, business rule violation after validation).

:::mermaid
flowchart LR
    subgraph R2S0["Dead Letter Channel"]
        direction TB
        r2n0["Use when the message is structurally valid but processing fails after retries (downstream outage,..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Invalid Message Channel (115)** - Use when validation fails early (schema, signature, mandatory fields) so poison does not block the main consumer partition.

:::mermaid
flowchart LR
    subgraph R3S0["Invalid Message Channel"]
        direction TB
        r3n0["Use when validation fails early (schema, signature, mandatory fields) so poison does not block th..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- filters that silently swallow errors.
- re-ordering filters without considering idempotency and side effects.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Input"] --> B["Filter 1"] --> C["Filter 2"] --> D["Filter 3"] --> E["Output"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant In as Inbound message
    participant F1 as Filter 1
    participant F2 as Filter 2
    participant Out as Outbound
    In->>F1: Message
    alt Invalid
        F1->>F1: Route to invalid / DLQ
    else OK
        F1->>F2: Forward
        F2->>Out: Forward
    end
:::

## 2.8. Message Router (78)
**Category**  
Messaging system

**Overview**  
A component decides where a message should go without changing the message payload.

**Description**  
A **Message Router (78)** consumes from an input channel and forwards to one or more output channels based on rules, without changing the business payload (routing is separate from transformation). It centralises where messages should go so that business components stay unaware of topology.

Specialisations include **Content-Based Router (230)**, **Recipient List (249)**, and **Dynamic Router (243)**. Routers should remain fast and deterministic where possible; heavy payload rewriting belongs to **Message Translator (85)** instead.

**Use when**  
Use when routing logic should be centralised and kept separate from business processing components.

**When not to use**  
- Avoid embedding heavy transformation in the router-use **Message Translator (85)**.
- Avoid ultra-dynamic routing in the router when a **Routing Slip (301)** or **Process Manager (312)** would make the flow auditable.

**Typical scenarios**  
- Routing by message type
- Route key
- Integration destination

**Related patterns**  
**Content-Based Router (230)**, **Recipient List (249)**, **Routing Slip (301)**.

**Combinations**  

The base router pattern is specialised when routing depends on payload, when you must fan out deliberately, or when destinations change at runtime.

- **Content-Based Router (230)** - Centralise business routing rules that inspect headers or body fields. Keep rules testable and versioned; avoid embedding heavy transformation here.

:::mermaid
flowchart LR
    subgraph R0S0["Content-Based Router"]
        direction TB
        r0n0["Centralise business routing rules that inspect headers or body fields. Keep rules testable and ve..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Recipient List (249)** - Send a deliberate copy to each target channel when several parties must process the same logical message (often paired later with aggregation).

:::mermaid
flowchart LR
    subgraph R1S0["Recipient List"]
        direction TB
        r1n0["Send a deliberate copy to each target channel when several parties must process the same logical ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Dynamic Router (243)** - Consult registries, feature flags, or policy services to choose targets, with caching, timeouts, and safe defaults when policy is unavailable.

:::mermaid
flowchart LR
    subgraph R2S0["Dynamic Router"]
        direction TB
        r2n0["Consult registries, feature flags, or policy services to choose targets, with caching, timeouts, ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- router becomes a monolith of business rules unrelated to destination choice.
- router inspects payloads without access control, leaking sensitive data into logs.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Incoming message"] --> B["Message router"]
    B --> C["Route A"]
    B --> D["Route B"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    M[Message arrives] --> R{Router rules}
    R -->|type A| C1[Channel A]
    R -->|type B| C2[Channel B]
    R -->|no match| D[Default / dead letter]
:::

## 2.9. Message Translator (85)
**Category**  
Messaging system

**Overview**  
A component converts a message from one structure or vocabulary into another.

**Description**  
A **Message Translator (85)** converts a message from one structure or semantic model into another so that heterogeneous systems can interoperate. It is the workhorse of schema mapping, field renaming, unit conversion, and envelope handling.

Translators often sit at system boundaries or after a **Messaging Gateway (468)**. For many source formats converging on one internal model, combine with **Normalizer (352)** or **Canonical Data Model (355)** so translation rules stay maintainable.

**Use when**  
Use when sender and receiver represent the same information differently.

**When not to use**  
- Avoid point-to-point ad hoc mapping everywhere-converge on **Normalizer (352)** or **Canonical Data Model (355)** when many sources exist.
- Avoid translators that call many remote systems synchronously inside the hot path without timeout and bulkhead design.

**Typical scenarios**  
- Transforming upstream payloads into local contracts or canonical structures

**Related patterns**  
**Normalizer (352)**, **Canonical Data Model (355)**, **Envelope Wrapper (330)**.

**Combinations**  

Translators belong at domain boundaries; wire-level evolution needs explicit format and envelope policies.

- **Bounded contexts** - Place **Message Translator (85)** at anti-corruption boundaries so each system keeps its native model while integration maps at the edge.

:::mermaid
flowchart LR
    subgraph R0S0["Message Translator"]
        direction TB
        r0n0["Place Message Translator at anti-corruption boundaries so each system keeps its native model whil..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Format Indicator (180)** - Require consumers to read version metadata before parsing so mixed-version traffic can coexist during rollout.

:::mermaid
flowchart LR
    subgraph R1S0["Format Indicator"]
        direction TB
        r1n0["Require consumers to read version metadata before parsing so mixed-version traffic can coexist du..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Envelope Wrapper (330)** - Separate transport metadata (routing, trace, security) from business payload versioning so each can evolve independently.

:::mermaid
flowchart LR
    subgraph R2S0["Envelope Wrapper"]
        direction TB
        r2n0["Separate transport metadata (routing, trace, security) from business payload versioning so each c..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- duplicated mapping logic in every consumer.
- lossy translation that drops audit-critical fields.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Source message"] --> B["Translator"] --> C["Target message"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant Src as Source format
    participant T as Message translator
    participant Can as Canonical / target
    Src->>T: External message
    T->>T: Map + validate
    T->>Can: Internal message
:::

## 2.10. Message Endpoint (95)
**Category**  
Messaging system

**Overview**  
An application-facing component sends or receives messages on behalf of the application.

**Description**  
A **Message Endpoint (95)** is the application-side adapter that connects domain code to the messaging infrastructure-sending, receiving, or both-while hiding wire protocol details. It is the bridge between how your application thinks and how the bus works.

Endpoints work with Channel Adapters, **Messaging Gateway (468)** facades, and Service Activators. Well-designed endpoints manage acknowledgements, threading, and back-pressure so application services stay focused on business logic.

**Use when**  
Use when the application should be insulated from messaging infrastructure details.

**When not to use**  
- Avoid duplicating endpoint code per queue without shared libraries-extract common acknowledge, retry, and serialisation policy.
- Avoid hiding so much that failures and observability become opaque.

**Typical scenarios**  
- Subscriber handlers
- Publisher adapters
- Endpoint wrappers

**Related patterns**  
**Channel Adapter (127)**, **Messaging Gateway (468)**, **Service Activator (532)**.

**Combinations**  

Endpoints unify how the application talks to the bus: connection handling, acknowledgements, serialisation, and how work is triggered.

- **Lifecycle** - Manage connect/reconnect, credential rotation, and heartbeat behaviour in one place so domain code stays free of broker quirks.

:::mermaid
flowchart LR
    subgraph R0S0["Lifecycle"]
        direction TB
        r0n0["Manage connect/reconnect, credential rotation, and heartbeat behaviour in one place so domain cod..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Acknowledgement policy** - Define when to ack, negative-ack, or dead-letter; never leave stuck locks on poison messages.

:::mermaid
flowchart LR
    subgraph R1S0["Acknowledgement policy"]
        direction TB
        r1n0["Define when to ack, negative-ack, or dead-letter; never leave stuck locks on poison messages."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Polling Consumer (494)** vs **Event-Driven Consumer (498)** - Choose pull semantics (batching, rate control) versus push/callback semantics (lower latency) per workload; document thread and batch sizing.

:::mermaid
flowchart LR
    subgraph R2S0["Polling Consumer"]
        direction TB
        r2n0["Choose pull semantics (batching, rate control) versus push/callback semantics (lower latency) per..."]
    end
    subgraph R2S1["Event-Driven Consumer"]
        direction TB
        r2n1["Choose pull semantics (batching, rate control) versus push/callback semantics (lower latency) per..."]
    end
    IN[In] --> r2n0
    r2n0 --> r2n1
    r2n1 --> OUT[Out]
:::

- **Transactional Client (484)** - When database and message publication must align, use outbox or transactional send patterns so you do not commit one side without the other.

:::mermaid
flowchart LR
    subgraph R3S0["Transactional Client"]
        direction TB
        r3n0["When database and message publication must align, use outbox or transactional send patterns so yo..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- endpoint that never negative-acknowledges or dead-letters, blocking partitions forever.
- unbounded internal queues inside the endpoint.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Application"] <--> B["Message endpoint"] <--> C["Messaging system"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant App as Application code
    participant EP as Message endpoint
    participant Bus as Messaging system
    App->>EP: Send domain command
    EP->>Bus: Publish / send
    Bus->>EP: Deliver message
    EP->>App: Invoke handler
:::

## 2.11. Point-to-Point Channel (103)
**Category**  
Messaging channel

**Overview**  
A channel where one message is intended for one consuming path.

**Description**  
A **Point-to-Point Channel (103)** ensures that each message is consumed by exactly one receiver (or one competing consumer path), which matches command processing and work queues. The channel acts as a load-balancing or task distribution mechanism.

This pattern pairs with **Competing Consumers (502)** for scale and with **Dead Letter Channel (119)** for poison messages. It contrasts with **Publish-Subscribe Channel (106)**, where each subscriber receives its own copy.

**Use when**  
Use when one unit of work should be processed once by one consumer path.

**When not to use**  
- Avoid for broadcast notifications where every subscriber must receive a copy-use **Publish-Subscribe Channel (106)**.
- Avoid a single queue when competing consumers would starve different priority classes-split channels or use **Message Dispatcher (508)** with policy.

**Typical scenarios**  
- Work queues
- Command handling
- Single-owner processing

**Related patterns**  
**Message Channel (60)**, **Competing Consumers (502)**, **Dead Letter Channel (119)**.

**Combinations**  

Point-to-point queues scale horizontally when multiple workers share the same backlog, provided duplicates and poison messages are designed for.

- **Competing Consumers (502)** - Add instances that read from the same queue so throughput rises with load. Ensure fair dispatch and monitor per-instance lag.

:::mermaid
flowchart LR
    subgraph R0S0["Competing Consumers"]
        direction TB
        r0n0["Add instances that read from the same queue so throughput rises with load. Ensure fair dispatch a..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Dead Letter Channel (119)** - Move messages that fail after max deliveries so one bad message does not block the whole partition indefinitely.

:::mermaid
flowchart LR
    subgraph R1S0["Dead Letter Channel"]
        direction TB
        r1n0["Move messages that fail after max deliveries so one bad message does not block the whole partitio..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Idempotent Receiver (528)** - Because redelivery and at-least-once semantics are normal, handlers must tolerate duplicate delivery using business keys or dedupe stores.

:::mermaid
flowchart LR
    subgraph R2S0["Idempotent Receiver"]
        direction TB
        r2n0["Because redelivery and at-least-once semantics are normal, handlers must tolerate duplicate deliv..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- multiple logical owners for the same message type on one queue.
- assuming “once only” delivery without idempotent handling.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Sender"] --> B["Point-to-point channel"] --> C["Single consuming path"]
:::

**Flowchart (left-to-right)** - variant 2: use when scaling out with competing consumers on one queue (the primary shows a single consuming path only).

:::mermaid
flowchart LR
    P[Producer] --> Q[Queue]
    Q --> C1[Consumer instance 1]
    Q --> C2[Consumer instance 2]
:::

## 2.12. Publish-Subscribe Channel (106)
**Category**  
Messaging channel

**Overview**  
A channel that delivers a copy of a message to multiple interested subscribers.

**Description**  
A **Publish-Subscribe Channel (106)** delivers a copy of each message to every interested subscriber, enabling one-to-many notification without the publisher knowing the audience. It fits domain events, cache invalidation, and parallel downstream processing.

Subscribers should be independent; slow consumers are managed with back-pressure, scaling, or **Durable Subscriber (522)** semantics. Combine with **Message Filter (237)** or **Selective Consumer (515)** when subscribers only care about subsets of traffic.

**Use when**  
Use when one event should fan out to multiple independent consumers.

**When not to use**  
- Avoid for work that must be processed exactly once by one worker-use **Point-to-Point Channel (103)**.
- Avoid when subscribers are not isolated and one slow consumer can block others unless the product supports per-subscriber dispatch or you partition topics.

**Typical scenarios**  
- Event publication
- Monitoring taps
- Multi-subscriber business events

**Related patterns**  
**Message Channel (60)**, **Durable Subscriber (522)**, **Wire Tap (547)**.

**Combinations**  

Pub/sub suits many interested parties, but production subscribers need durability and selective processing so they are not overwhelmed by irrelevant events.

- **Durable Subscriber (522)** - For consumers that must not miss events during maintenance, use durable subscriptions or equivalent offset storage; define retention and replay policies.

:::mermaid
flowchart LR
    subgraph R0S0["Durable Subscriber"]
        direction TB
        r0n0["For consumers that must not miss events during maintenance, use durable subscriptions or equivale..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message Filter (237)** - Drop or divert events upstream of expensive handlers when large topics carry heterogeneous event types.

:::mermaid
flowchart LR
    subgraph R1S0["Message Filter"]
        direction TB
        r1n0["Drop or divert events upstream of expensive handlers when large topics carry heterogeneous event ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Selective Consumer (515)** - When sharing a subscription group or queue, skip messages that do not match local responsibility while still advancing offsets according to product semantics.

:::mermaid
flowchart LR
    subgraph R2S0["Selective Consumer"]
        direction TB
        r2n0["When sharing a subscription group or queue, skip messages that do not match local responsibility ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- command-style “do this” messages on a topic with no idempotency.
- publishing huge payloads without **Claim Check (346)**.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Publisher"] --> B["Publish-subscribe channel"]
    B --> C["Subscriber 1"]
    B --> D["Subscriber 2"]
    B --> E["Subscriber 3"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant P as Publisher
    participant T as Topic
    participant S1 as Subscriber 1
    participant S2 as Subscriber 2
    P->>T: Event
    T->>S1: Copy
    T->>S2: Copy
:::

## 2.13. Datatype Channel (111)
**Category**  
Messaging channel

**Overview**  
A channel dedicated to a particular message type or contract.

**Description**  
A **Datatype Channel (111)** dedicates a channel to a specific message type or contract so that consumers can rely on a single schema or processing path. It reduces ambiguous polymorphic queues and makes monitoring and access control clearer.

The trade-off is more channels to operate. Where many types must share infrastructure, use explicit type identifiers (**Format Indicator (180)**) and robust validation, possibly routing invalid messages to an **Invalid Message Channel (115)**.

**Use when**  
Use when channel purpose should communicate message expectations explicitly.

**When not to use**  
- Avoid proliferating channels for every minor schema revision-use **Format Indicator (180)** and versioned contracts on a smaller set of channels when operations cannot support the fan-out.

**Typical scenarios**  
- Separate routes for distinct message families or strongly governed contracts

**Related patterns**  
**Message Channel (60)**, **Message Translator (85)**, **Invalid Message Channel (115)**.

**Combinations**  

A datatype channel is only as clean as its ingress validation and any ordering guarantees you add around rejected or out-of-order fragments.

- **Strict validation** - Reject messages that do not match the declared contract at the channel boundary so downstream code assumes one shape.

:::mermaid
flowchart LR
    subgraph R0S0["Strict validation"]
        direction TB
        r0n0["Reject messages that do not match the declared contract at the channel boundary so downstream cod..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Resequencer (283)** - When fragments or related messages must arrive in order, resequence before business logic; define timeout if parts never arrive.

:::mermaid
flowchart LR
    subgraph R1S0["Resequencer"]
        direction TB
        r1n0["When fragments or related messages must arrive in order, resequence before business logic; define..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Monitoring** - Alert on reject rates and unexpected payload fingerprints per datatype channel.

:::mermaid
flowchart LR
    subgraph R2S0["Monitoring"]
        direction TB
        r2n0["Alert on reject rates and unexpected payload fingerprints per datatype channel."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- datatype channel that still accepts multiple unrelated shapes “temporarily.”
- no monitoring on unexpected volume spikes per channel.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Typed message"] --> B["Datatype channel"] --> C["Typed consumer"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    V[Validate envelope + type] -->|match| B[Business processing]
    V -->|mismatch| I[Invalid message channel]
:::

## 2.14. Invalid Message Channel (115)
**Category**  
Messaging channel

**Overview**  
A dedicated place for messages that are invalid and should not continue normal processing.

**Description**  
The **Invalid Message Channel (115)** is a separate destination for messages that fail structural or business validation and must not be retried blindly on the main path. It preserves throughput for good messages while quarantining bad ones for inspection or correction.

Operational playbooks should define who triages invalid traffic, how messages are repaired or replayed, and when to discard. It complements **Dead Letter Channel (119)**, which typically handles processing failures rather than schema violations.

**Use when**  
Use when invalid input must be isolated instead of blindly retried.

**When not to use**  
- Do not route transient infrastructure errors here if they belong on the main retry path-**Dead Letter Channel (119)** is for exhausted processing or delivery, while invalid structure belongs here.
- Avoid using it as a permanent graveyard without triage and replay procedures.

**Typical scenarios**  
- Schema failures
- Required-field failures
- Structurally invalid messages

**Related patterns**  
**Dead Letter Channel (119)**, **Message Filter (237)**, **Message History (551)**.

**Combinations**  

Invalid traffic should be observable and diagnosable: catch it early, retain enough context to fix upstream, and optionally sample for inspection.

- **Message Filter (237)** **or validator** - Prefer rejecting before expensive steps; record structured error codes and raw payload pointers (not always full bodies) for privacy.

:::mermaid
flowchart LR
    subgraph R0S0["Message Filter"]
        direction TB
        r0n0["Prefer rejecting before expensive steps; record structured error codes and raw payload pointers (..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Downstream tooling** - Dashboards, alerts, and ticketing integration on invalid-channel depth and top failure reasons.

:::mermaid
flowchart LR
    subgraph R1S0["Downstream tooling"]
        direction TB
        r1n0["Dashboards, alerts, and ticketing integration on invalid-channel depth and top failure reasons."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Wire Tap (547)** - For difficult incidents, tap a sample of invalid messages to a secure inspection store with strict retention and access controls.

:::mermaid
flowchart LR
    subgraph R2S0["Wire Tap"]
        direction TB
        r2n0["For difficult incidents, tap a sample of invalid messages to a secure inspection store with stric..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- invalid messages deleted with no diagnostic payload.
- same queue used for invalid and poison without distinction.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Incoming message"] --> B{"Valid?"}
    B -- Yes --> C["Normal channel"]
    B -- No --> D["Invalid message channel"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant R as Receiver
    participant V as Validator
    participant I as Invalid channel
    R->>V: Message
    alt Schema OK
        V->>R: Process
    else Schema fail
        V->>I: Move with reason code
    end
:::

## 2.15. Dead Letter Channel (119)
**Category**  
Messaging channel

**Overview**  
A terminal holding place for messages that could not be processed or delivered successfully.

**Description**  
The **Dead Letter Channel (119)** holds messages that could not be processed successfully after retries, or that cannot be delivered to a valid consumer. It prevents a poison message from blocking the whole queue and gives support teams a forensic trail.

Clear policies are needed for retention, reprocessing, and alerting. Pair with **Guaranteed Delivery (122)** and **Idempotent Receiver (528)** so retries and replays do not duplicate business effects.

**Use when**  
Use when retries are exhausted or the message must be quarantined for later investigation.

**When not to use**  
- Avoid sending every transient glitch to DLQ without categorisation-tune retry, **Message Expiration (176)**, and idempotency first.
- Avoid DLQ as a substitute for fixing the root bug in the consumer.

**Typical scenarios**  
- Poison messages
- Retry exhaustion
- Terminal delivery failure

**Related patterns**  
**Guaranteed Delivery (122)**, **Invalid Message Channel (115)**, **Message History (551)**.

**Combinations**  

Dead-letter handling is operational glue: persist enough to triage, measure systemic issues, and replay safely without conflating bad schema with bad processing.

- **Message Store (555)** - Optionally persist DLQ payloads and metadata for long-term audit and bulk replay tooling.

:::mermaid
flowchart LR
    subgraph R0S0["Message Store"]
        direction TB
        r0n0["Optionally persist DLQ payloads and metadata for long-term audit and bulk replay tooling."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Monitoring** - Track age, count, and rate of DLQ ingress; correlate spikes with deploys or partner changes.

:::mermaid
flowchart LR
    subgraph R1S0["Monitoring"]
        direction TB
        r1n0["Track age, count, and rate of DLQ ingress; correlate spikes with deploys or partner changes."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Replay runbooks** - Define who may requeue messages, after which code fixes, and how **Idempotent Receiver (528)** prevents double effect.

:::mermaid
flowchart LR
    subgraph R2S0["Idempotent Receiver"]
        direction TB
        r2n0["Define who may requeue messages, after which code fixes, and how Idempotent Receiver prevents dou..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Invalid Message Channel (115)** - Keep structurally invalid messages separate from “valid but failed” poison so triage queues stay meaningful.

:::mermaid
flowchart LR
    subgraph R3S0["Invalid Message Channel"]
        direction TB
        r3n0["Keep structurally invalid messages separate from “valid but failed” poison so triage queues stay ..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- unbounded DLQ with no owner.
- automatic requeue without fixing poison semantics.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Processing / delivery attempts"] --> B{"Succeeded?"}
    B -- Yes --> C["Continue"]
    B -- No after retries --> D["Dead letter channel"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant C as Consumer
    participant Q as Work queue
    participant DL as Dead letter
    loop Retries with backoff
        Q->>C: Deliver
        C-->>Q: Fail / nack
    end
    Q->>DL: Move after max attempts
:::

## 2.16. Guaranteed Delivery (122)
**Category**  
Messaging channel

**Overview**  
The messaging infrastructure persists messages so they are not lost during failures.

**Description**  
**Guaranteed Delivery (122)** means the messaging infrastructure persists messages so they survive sender, broker, or network failures until they are safely handed off or acknowledged. It turns at-most-once risks into durable hand-off semantics the business can trust.

Implementation varies by product (durable queues, transactions, replication). It does not remove the need for consumer idempotency or dead-letter handling when processing itself fails.

**Use when**  
Use when messages must survive process, broker, or network interruptions.

**When not to use**  
- Avoid assuming durability equals exactly-once end-to-end semantics-consumers still need **Transactional Client (484)** and clear acknowledgement rules.
- Avoid durable publication without retention and disk governance on the broker.

**Typical scenarios**  
- Reliable queueing
- Critical work dispatch
- Durable event publication

**Related patterns**  
**Dead Letter Channel (119)**, **Transactional Client (484)**, **Message Store (555)**.

**Combinations**  

Guaranteed delivery spans broker persistence, producer behaviour, and consumer acknowledgement-often coordinated with versioning and transactional handoff.

- **Format Indicator (180)** - Ensures replayed or redelivered messages can still be parsed after consumer upgrades.

:::mermaid
flowchart LR
    subgraph R0S0["Format Indicator"]
        direction TB
        r0n0["Ensures replayed or redelivered messages can still be parsed after consumer upgrades."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Outbox / transactional patterns** - When publishing must align with database commits, write an outbox row in the same transaction as business data, then have a relay publish to the bus.

:::mermaid
flowchart LR
    subgraph R1S0["Outbox / transactional patterns"]
        direction TB
        r1n0["When publishing must align with database commits, write an outbox row in the same transaction as ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message Endpoint (95)** - Centralise ack/nack and DLQ behaviour so guaranteed delivery does not deadlock on application bugs.

:::mermaid
flowchart LR
    subgraph R2S0["Message Endpoint"]
        direction TB
        r2n0["Centralise ack/nack and DLQ behaviour so guaranteed delivery does not deadlock on application bugs."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- durable queue with no TTL on test environments filling disks.
- infinite redelivery without DLQ.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Sender"] --> B["Durable store"] --> C["Channel"] --> D["Receiver"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant S as Sender
    participant B as Broker (durable)
    participant R as Receiver
    S->>B: Send (persisted)
    Note over B: Survives broker restart
    B->>R: Deliver
    R-->>B: Ack
:::

## 2.17. Channel Adapter (127)
**Category**  
Messaging channel

**Overview**  
A boundary component connects an application or external system to a messaging channel.

**Description**  
A **Channel Adapter (127)** connects an application-or a non-messaging technology such as files or databases-to a **Message Channel (60)**. One side speaks the application’s native interface; the other produces or consumes messages.

Adapters are often the edge of the integration solution: polling a table, watching a directory, or wrapping an API. They differ from **Messaging Gateway (468)**, which typically presents a messaging-free facade inside the application boundary.

**Use when**  
Use when an application interface must be bridged into messaging.

**When not to use**  
- Avoid tight polling loops without backoff when bridging file or API sources-protect the source system.
- Avoid embedding business rules in the adapter; keep it transport-focused.

**Typical scenarios**  
- API-to-message
- File-to-message
- System ingress or egress connectors

**Related patterns**  
**Message Endpoint (95)**, **Messaging Gateway (468)**, **Messaging Bridge (133)**.

**Combinations**  

Adapters bring external signals onto the bus; mapping and domain objects should sit just inside the boundary so the adapter stays thin and testable.

- **Message Translator (85)** - Normalise wire format into bus contracts (or canonical messages) immediately after ingress so downstream consumers never parse proprietary payloads.

:::mermaid
flowchart LR
    subgraph R0S0["Message Translator"]
        direction TB
        r0n0["Normalise wire format into bus contracts (or canonical messages) immediately after ingress so dow..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Messaging Mapper (477)** - Map messages to or from application entities at the service edge, keeping domain models free of broker-specific annotations.

:::mermaid
flowchart LR
    subgraph R1S0["Messaging Mapper"]
        direction TB
        r1n0["Map messages to or from application entities at the service edge, keeping domain models free of b..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Correlation** - Stamp a trace or correlation id on every message produced by the adapter for cross-system support.

:::mermaid
flowchart LR
    subgraph R2S0["Correlation"]
        direction TB
        r2n0["Stamp a trace or correlation id on every message produced by the adapter for cross-system support."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- adapter that drops errors silently.
- no correlation or trace id when entering the bus.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["External system / app"] --> B["Channel adapter"] --> C["Message channel"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant Ext as External API / file
    participant CA as Channel adapter
    participant Ch as Message channel
    Ext->>CA: Poll / webhook / file event
    CA->>Ch: Normalised message
:::

## 2.18. Messaging Bridge (133)
**Category**  
Messaging channel

**Overview**  
A connector forwards messages from one messaging system or channel space to another.

**Description**  
A **Messaging Bridge (133)** connects two messaging systems (or namespaces) so messages can flow between them, translating protocols or addressing as needed. It helps federate environments, migrate brokers, or link partner-managed buses.

Bridges must preserve semantics: ordering, deduplication, security, and transformation rules should be explicit. **Message Translator (85)** logic is common at the bridge boundary.

**Use when**  
Use when separate messaging domains must be linked without changing the applications.

**When not to use**  
- Avoid bridging without mapping security, identity, and ordering guarantees-silent semantic drift is common.
- Avoid infinite forwarding loops between buses.

**Typical scenarios**  
- Cross-bus forwarding
- Environment bridging
- Broker federation

**Related patterns**  
**Channel Adapter (127)**, **Message Bus (137)**, **Message Translator (85)**.

**Combinations**  

Bridges are high-risk change surfaces: map semantics explicitly, observe traffic during migration, and detect echo loops early.

- **Message Translator (85)** - Apply at each boundary so addressing, headers, and body formats match the target bus conventions without leaking source quirks.

:::mermaid
flowchart LR
    subgraph R0S0["Message Translator"]
        direction TB
        r0n0["Apply at each boundary so addressing, headers, and body formats match the target bus conventions ..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Wire Tap (547)** - Mirror a sample of bridged messages to a secure store during cutover for diffing and volume validation.

:::mermaid
flowchart LR
    subgraph R1S0["Wire Tap"]
        direction TB
        r1n0["Mirror a sample of bridged messages to a secure store during cutover for diffing and volume valid..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message History (551)** - Append hop metadata so operators can prove path and diagnose duplicate delivery across buses.

:::mermaid
flowchart LR
    subgraph R2S0["Message History"]
        direction TB
        r2n0["Append hop metadata so operators can prove path and diagnose duplicate delivery across buses."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- duplicate detection missing so messages echo.
- bridging prod and non-prod without hard gates.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Messaging system A"] --> B["Messaging bridge"] --> C["Messaging system B"]
:::

**Flowchart (left-to-right)** - variant 2: use when naming concrete source/target shapes (for example topic to queue) and dead-letter or alert paths on mapping failure (the primary is a single bridge hop only).

:::mermaid
flowchart LR
    A[Bus A topic] --> B[Bridge]
    B --> C[Bus B queue]
    B --> D[DLQ / alert on map fail]
:::

## 2.19. Message Bus (137)
**Category**  
Messaging channel

**Overview**  
A shared messaging backbone provides common channels and conventions for many participants.

**Description**  
The **Message Bus (137)** pattern describes a shared messaging backbone-a set of common channels, conventions, and infrastructure-that many applications use to integrate in a standard way. It encourages reuse of connectivity and cross-cutting concerns such as monitoring.

A bus is not a single magic product; it is an architectural stance. Successful buses define naming, versioning, security, and operational standards so that Point-to-Point and Publish-Subscribe channels stay governable at scale.

**Use when**  
Use when multiple systems need standardised, reusable integration pathways.

**When not to use**  
- Avoid a bus without governance (naming, schemas, ownership, monitoring)-it becomes a fragile spaghetti network.
- Avoid forcing every integration through a central broker when point-to-point with clear contracts is simpler and safer.

**Typical scenarios**  
- Enterprise event backbone
- Shared integration infrastructure
- Common routing conventions

**Related patterns**  
**Messaging (53)**, **Publish-Subscribe Channel (106)**, **Messaging Bridge (133)**.

**Combinations**  

A governed bus is defined by how types, versions, and failure paths behave across all producers and consumers.

- **Datatype Channel (111)** - Prefer separate channels (or subscriptions) per major contract family so incompatible parsers never share one consumer group accidentally.

:::mermaid
flowchart LR
    subgraph R0S0["Datatype Channel"]
        direction TB
        r0n0["Prefer separate channels (or subscriptions) per major contract family so incompatible parsers nev..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Format Indicator (180)** - When multiple versions share a channel during migration, require explicit version metadata and consumer behaviour for unknown versions.

:::mermaid
flowchart LR
    subgraph R1S0["Format Indicator"]
        direction TB
        r1n0["When multiple versions share a channel during migration, require explicit version metadata and co..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Dead Letter Channel (119)** and **Invalid Message Channel (115)** - Run both as first-class routes with dashboards, owners, and distinct triage: schema failure versus processing failure.

:::mermaid
flowchart LR
    subgraph R2S0["Dead Letter Channel"]
        direction TB
        r2n0["Run both as first-class routes with dashboards, owners, and distinct triage: schema failure versu..."]
    end
    subgraph R2S1["Invalid Message Channel"]
        direction TB
        r2n1["Run both as first-class routes with dashboards, owners, and distinct triage: schema failure versu..."]
    end
    IN[In] --> r2n0
    r2n0 --> r2n1
    r2n1 --> OUT[Out]
:::

**Anti-patterns**  
- bus as the only backup path with no DR story.
- unowned topics and wildcard subscriptions in production.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Producer A"] --> B["Message bus"]
    C["Producer B"] --> B
    B --> D["Consumer A"]
    B --> E["Consumer B"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    subgraph Bus["Governed message bus"]
        T1[Topic / queue standards]
        T2[Schema registry / contracts]
        T3[Observability + DLQ]
    end
:::

## 2.20. Command Message (145)
**Category**  
Message construction

**Overview**  
A message tells the receiver to perform a specific action.

**Description**  
A **Command Message (145)** expresses an instruction: the receiver should perform an operation, often with parameters in the payload. It implies intent and usually expects success, failure, or a follow-up outcome (sometimes via **Request-Reply (154)**).

Design commands to be explicit, versioned, and idempotent where retries occur. They typically travel on Point-to-Point Channels so exactly one handler owns the work.

**Use when**  
Use when the sender wants work to be carried out rather than merely shared as information.

**When not to use**  
- Avoid commands on pub/sub topics unless every subscriber is idempotent and the business accepts duplicate side effects.
- Avoid command names that hide multiple unrelated operations (“Process”).

**Typical scenarios**  
- Process this request
- Create this record
- Start this task

**Related patterns**  
**Document Message (147)**, **Event Message (151)**, **Service Activator (532)**.

**Combinations**  

Commands imply a single owning handler, safe retries, and traceable outcomes across async steps.

- **Point-to-Point Channel (103)** - Send commands to a work queue or dedicated handler path so competing consumers still yield exactly-once business effect through idempotency.

:::mermaid
flowchart LR
    subgraph R0S0["Point-to-Point Channel"]
        direction TB
        r0n0["Send commands to a work queue or dedicated handler path so competing consumers still yield exactl..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Idempotent Receiver (528)** - Use business keys or dedupe stores so redelivered commands do not duplicate side effects.

:::mermaid
flowchart LR
    subgraph R1S0["Idempotent Receiver"]
        direction TB
        r1n0["Use business keys or dedupe stores so redelivered commands do not duplicate side effects."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Format Indicator (180)** - Version command schemas explicitly; reject unknown versions at the edge.

:::mermaid
flowchart LR
    subgraph R2S0["Format Indicator"]
        direction TB
        r2n0["Version command schemas explicitly; reject unknown versions at the edge."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Correlation Identifier (163)** - Tie commands to follow-up events, replies, or saga steps.

:::mermaid
flowchart LR
    subgraph R3S0["Correlation Identifier"]
        direction TB
        r3n0["Tie commands to follow-up events, replies, or saga steps."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- command without stable business key.
- command payload so large it should use **Claim Check (346)**.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Sender"] --> B["Command message"] --> C["Receiver performs action"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant S as Sender
    participant Q as Command queue
    participant H as Handler
    S->>Q: Command + key
    Q->>H: Deliver once (conceptually)
    H->>H: Idempotent execute
:::

## 2.21. Document Message (147)
**Category**  
Message construction

**Overview**  
A message carries a self-contained business document or state representation.

**Description**  
A **Document Message (147)** carries a structured snapshot of data-a document-that the receiver can interpret and process without calling back for the main facts. Examples include orders, HR records, or reference data bundles.

Documents may be large; consider **Claim Check (346)** for references to external storage. Document messages pair well with state transfer and batch-style updates, distinct from fine-grained Command or notification-style Event messages.

**Use when**  
Use when the receiver needs a structured body of information to process.

**When not to use**  
- Avoid sending full document snapshots on every micro-change when **Correlation Identifier (163)** with identifiers plus lazy load would scale better.
- Avoid documents so large they belong in **Message Store (555)**.

**Typical scenarios**  
- Person records
- Leave documents
- Transfer payloads

**Related patterns**  
**Command Message (145)**, **Event Message (151)**, **Message Translator (85)**.

**Combinations**  

Documents often pass through validation, enrichment, and routing stages; history and dynamic policy help long-running document migrations.

- **Pipes and Filters (70)** - Chain validate → transform → authorise → persist as discrete filters with clear failure exits.

:::mermaid
flowchart LR
    subgraph R0S0["Pipes and Filters"]
        direction TB
        r0n0["Chain validate → transform → authorise → persist as discrete filters with clear failure exits."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message History (551)** - Record which stages processed a document for audit and replay when a mid-pipeline defect is fixed.

:::mermaid
flowchart LR
    subgraph R1S0["Message History"]
        direction TB
        r1n0["Record which stages processed a document for audit and replay when a mid-pipeline defect is fixed."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Dynamic Router (243)** - Route document types or versions to different target systems from configuration rather than hard-coded switches.

:::mermaid
flowchart LR
    subgraph R2S0["Dynamic Router"]
        direction TB
        r2n0["Route document types or versions to different target systems from configuration rather than hard-..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- document is not self-describing (missing type/version).
- using document messages for high-frequency telemetry.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Business document"] --> B["Document message"] --> C["Receiver"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    D[Document message] --> M[Metadata: type, version]
    D --> B[Business body]
    M --> V[Validate then process]
    B --> V
:::

## 2.22. Event Message (151)
**Category**  
Message construction

**Overview**  
A message announces that something has happened.

**Description**  
An **Event Message (151)** announces that something significant has occurred in the past tense (for example, “Order placed”). It does not tell subscribers how to react; multiple consumers can interpret the same event independently.

Events suit Publish-Subscribe topologies and drive loose coupling. Include enough context and identifiers for consumers to act idempotently; combine with **Correlation Identifier (163)** when events belong to a larger saga.

**Use when**  
Use when the sender should notify interested parties without directing how they respond.

**When not to use**  
- Avoid when the sender must enforce a single deterministic outcome path-consider **Command Message (145)** to a dedicated handler.
- Avoid events so minimal that every consumer must synchronously call back for context (chatty coupling).

**Typical scenarios**  
- Created
- Changed
- Deleted
- Approved
- Status-changed events

**Related patterns**  
**Publish-Subscribe Channel (106)**, **Message History (551)**, **Idempotent Receiver (528)**.

**Combinations**  

Events are broadcast-friendly but still need identity, versioning, staleness policy, and duplicate safety.

- **Correlation Identifier (163)** - Group related events (for example saga or aggregate stream) for tracing and downstream joins.

:::mermaid
flowchart LR
    subgraph R0S0["Correlation Identifier"]
        direction TB
        r0n0["Group related events (for example saga or aggregate stream) for tracing and downstream joins."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Format Indicator (180)** - Carry event type and schema version so subscribers can evolve at different speeds.

:::mermaid
flowchart LR
    subgraph R1S0["Format Indicator"]
        direction TB
        r1n0["Carry event type and schema version so subscribers can evolve at different speeds."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message Expiration (176)** - Drop or divert stale events where timeliness matters more than completeness (for example cache refresh).

:::mermaid
flowchart LR
    subgraph R2S0["Message Expiration"]
        direction TB
        r2n0["Drop or divert stale events where timeliness matters more than completeness (for example cache re..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Idempotent Receiver (528)** - Treat duplicate delivery as normal, especially under consumer scaling or at-least-once brokers.

:::mermaid
flowchart LR
    subgraph R3S0["Idempotent Receiver"]
        direction TB
        r3n0["Treat duplicate delivery as normal, especially under consumer scaling or at-least-once brokers."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- event as disguised command (“Subscriber must do X”).
- missing event version so consumers cannot evolve safely.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Business event occurs"] --> B["Event message"] --> C["Interested consumers"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant Src as Source
    participant T as Topic
    participant C1 as Consumer A
    participant C2 as Consumer B
    Src->>T: EmployeeUpdated (id, version)
    T->>C1: Deliver
    T->>C2: Deliver
:::

## 2.23. Request-Reply (154)
**Category**  
Message construction

**Overview**  
A sender issues a request and expects a corresponding reply or later response that can be associated back to the original request.

**Description**  
**Request-Reply (154)** models asynchronous two-way conversation: a request is sent on one channel and a reply arrives later, often on a different channel, without blocking the sender’s thread indefinitely. It mirrors RPC semantics over messaging, as described in the message construction section of the catalogue.

**Correlation Identifier (163)** and **Return Address (159)** are the usual companions so replies match requests. Timeouts, **Message Expiration (176)**, and **Process Manager (312)**–style orchestration handle slow or multi-step outcomes.

**Use when**  
Use when submission alone is not enough and the caller must eventually know the outcome.

**When not to use**  
- Avoid masquerading long synchronous workflows as simple request-reply without **Aggregator (268)** state and timeouts.
- Avoid when the caller cannot handle duplicate replies unless **Message Router (78)** semantics exist on both sides.

**Typical scenarios**  
- Accepted
- Pending
- Dependency timeout
- Delayed response
- Asynchronous completion flows

**Related patterns**  
**Correlation Identifier (163)**, **Return Address (159)**, **Process Manager (312)**.

**Combinations**  

Request-reply over messaging often splits work, observes in-flight traffic, pipelines processing, and caps how long waiters may block.

- **Splitter (259)** - Decompose a large request into child tasks with shared correlation when parallel processing is required before replying.

:::mermaid
flowchart LR
    subgraph R0S0["Splitter"]
        direction TB
        r0n0["Decompose a large request into child tasks with shared correlation when parallel processing is re..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Wire Tap (547)** - Observe request and reply traffic for support without altering business flow (redact sensitive fields).

:::mermaid
flowchart LR
    subgraph R1S0["Wire Tap"]
        direction TB
        r1n0["Observe request and reply traffic for support without altering business flow (redact sensitive fi..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Pipes and Filters (70)** - Structure validation, authorisation, and execution stages before the reply is composed.

:::mermaid
flowchart LR
    subgraph R2S0["Pipes and Filters"]
        direction TB
        r2n0["Structure validation, authorisation, and execution stages before the reply is composed."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Message Expiration (176)** - Expire orphaned requests and late replies so reply queues and correlation tables do not grow without bound.

:::mermaid
flowchart LR
    subgraph R3S0["Message Expiration"]
        direction TB
        r3n0["Expire orphaned requests and late replies so reply queues and correlation tables do not grow with..."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- reply queue shared across tenants without isolation.
- no timeout so pending requests accumulate forever.

---

**Diagrams**

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant Sender
    participant RequestChannel as Request Channel
    participant Receiver
    participant ReplyChannel as Reply Channel
    Sender->>RequestChannel: Send request
    RequestChannel->>Receiver: Deliver request
    Receiver->>ReplyChannel: Send reply
    ReplyChannel->>Sender: Deliver correlated reply
:::

**Sequence** - variant 2: use when emphasising correlation and reply-to on the messages themselves (the primary names explicit request and reply channels).

## 2.24. Return Address (159)
**Category**  
Message construction

**Overview**  
A request carries information telling the responder where to send the reply.

**Description**  
**Return Address (159)** embeds in the request (or its context) where the reply should be sent, so the responder does not hard-code reply destinations. That supports dynamic consumers, temporary reply queues, and shared services.

It is essential for flexible **Request-Reply (154)** in multi-tenant or cloud environments where reply routes vary per caller or per transaction.

**Use when**  
Use when the reply destination is not fixed or must be provided dynamically.

**When not to use**  
- Avoid omitting return addressing when multiple service instances or dynamic infrastructure make fixed reply queues wrong.
- Avoid embedding credentials or secrets in return paths.

**Typical scenarios**  
- Reply queues
- Callback routes
- Temporary response paths

**Related patterns**  
**Request-Reply (154)**, **Correlation Identifier (163)**, **Smart Proxy (558)**.

**Combinations**  

Return addressing must be safe for multi-tenant buses and multi-part replies, without letting callers inject arbitrary destinations.

- **Message Sequence (170)** - When replies are fragmented, carry sequence metadata alongside the return address so the requester can reassemble in order.

:::mermaid
flowchart LR
    subgraph R0S0["Message Sequence"]
        direction TB
        r0n0["When replies are fragmented, carry sequence metadata alongside the return address so the requeste..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Target validation** - Allow-list reply queues or topics per tenant or client credential; reject or sanitise return addresses that point outside governed namespaces.

:::mermaid
flowchart LR
    subgraph R1S0["Target validation"]
        direction TB
        r1n0["Allow-list reply queues or topics per tenant or client credential; reject or sanitise return addr..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Temporary reply resources** - If using per-request reply queues, automate creation and TTL cleanup to avoid orphan queues and leaks.

:::mermaid
flowchart LR
    subgraph R2S0["Temporary reply resources"]
        direction TB
        r2n0["If using per-request reply queues, automate creation and TTL cleanup to avoid orphan queues and l..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- hard-coded reply queue in a multi-tenant integration.
- temporary reply queues that are never deleted.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Request + return address"] --> B["Receiver"] --> C["Reply to supplied address"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    Req[Request] --> RA[Return address metadata]
    Req --> CID[Correlation id]
    RA --> R[Reply routing]
    CID --> M[Match reply to caller]
:::

## 2.25. Correlation Identifier (163)
**Category**  
Message construction

**Overview**  
A stable identifier carried across messages and related outcomes so that a later response, event, or log entry can be matched to the original request or processing attempt.

**Description**  
A **Correlation Identifier (163)** is a stable token carried on related messages so asynchronous responses, events, and logs can be tied back to the originating request or business transaction. It enables tracing through **Splitter (259)**/**Aggregator (268)** flows and long-running processes.

Use consistent propagation across services and infrastructure. It underpins observability, support diagnostics, and safe matching of out-of-order replies.

**Use when**  
Use when work is asynchronous, delayed, multi-step, or distributed across several components.

**When not to use**  
- Avoid reusing the same correlation id for unrelated business transactions (“correlation collision”).
- Avoid propagating only opaque internal ids when partners need a stable business key.

**Typical scenarios**  
- Accepted and pending outcomes
- Retries
- Message replay
- Telemetry
- End-to-end tracing

**Related patterns**  
**Request-Reply (154)**, **Message History (551)**, **Process Manager (312)**.

**Combinations**  

End-to-end tracing requires the same correlation (or trace) context on messages, in envelopes, in structured logs, and in replay tooling.

- **Envelope Wrapper (330)** - Carry correlation and parent span ids in a stable header area so every hop can forward without parsing business bodies.

:::mermaid
flowchart LR
    subgraph R0S0["Envelope Wrapper"]
        direction TB
        r0n0["Carry correlation and parent span ids in a stable header area so every hop can forward without pa..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Structured logs** - Log the same correlation id as the bus for support searches; avoid PII in log keys.

:::mermaid
flowchart LR
    subgraph R1S0["Structured logs"]
        direction TB
        r1n0["Log the same correlation id as the bus for support searches; avoid PII in log keys."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message Store (555)** - Persist messages with correlation for forensic replay after incidents; align retention with privacy policy.

:::mermaid
flowchart LR
    subgraph R2S0["Message Store"]
        direction TB
        r2n0["Persist messages with correlation for forensic replay after incidents; align retention with priva..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Distributed tracing** - Map correlation to OpenTelemetry (or equivalent) so APM and bus telemetry align.

:::mermaid
flowchart LR
    subgraph R3S0["Distributed tracing"]
        direction TB
        r3n0["Map correlation to OpenTelemetry (or equivalent) so APM and bus telemetry align."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- correlation only in logs, not on messages.
- new correlation id per hop with no parent link.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Request created"] --> B["Assign correlation identifier"]
    B --> C["Send request"]
    C --> D["Receive later response / event / log"]
    D --> E["Match using same correlation identifier"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant A as Service A
    participant B as Service B
    participant C as Service C
    A->>B: Msg (cid=123)
    B->>C: Msg (cid=123, parent span)
    C-->>B: Reply (cid=123)
    B-->>A: Reply (cid=123)
:::

## 2.26. Message Sequence (170)
**Category**  
Message construction

**Overview**  
A set of related messages carries sequence information so the receiver can recognise grouping and order.

**Description**  
**Message Sequence (170)** splits one logical unit across multiple physical messages while preserving grouping and order metadata (for example, sequence number and total count). Receivers use that metadata to detect completeness and ordering.

It interacts closely with **Splitter (259)**, **Aggregator (268)**, and **Resequencer (283)**. Expiry and partial-loss handling should be defined so incomplete sequences do not block processing forever.

**Use when**  
Use when one logical payload is split into multiple related messages.

**When not to use**  
- Avoid sequences without a defined completion rule (count, timeout, or end marker)-**Idempotent Receiver (528)** and **Message Sequence (170)** will stall.
- Avoid reusing sequence numbers across unrelated logical groups.

**Typical scenarios**  
- Batch decomposition
- Multi-part payloads
- Resequencing needs

**Related patterns**  
**Splitter (259)**, **Aggregator (268)**, **Resequencer (283)**.

**Combinations**  

Sequences need a shared group id, explicit position metadata, and a policy for missing or duplicate parts.

- **Correlation Identifier (163)** - Identifies the logical group (all parts share one correlation or group id).

:::mermaid
flowchart LR
    subgraph R0S0["Correlation Identifier"]
        direction TB
        r0n0["Identifies the logical group (all parts share one correlation or group id)."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Index and size (or end marker)** - Receivers know when the group is complete; define whether size is fixed or inferred from an end message.

:::mermaid
flowchart LR
    subgraph R1S0["Index and size (or end marker)"]
        direction TB
        r1n0["Receivers know when the group is complete; define whether size is fixed or inferred from an end m..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Gap policy** - Time out incomplete groups, emit alerts, and optionally partial-complete with business approval; pair with **Resequencer (283)** when order matters.

:::mermaid
flowchart LR
    subgraph R2S0["Resequencer"]
        direction TB
        r2n0["Time out incomplete groups, emit alerts, and optionally partial-complete with business approval; ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- partial sequences retried without idempotency, duplicating fragments.
- unbounded buffer waiting for “missing” parts.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Logical message"] --> B["Sequence 1 of N"]
    A --> C["Sequence 2 of N"]
    A --> D["Sequence N of N"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    S[Splitter] --> M1[Part 1 of 3 + cid + idx]
    S --> M2[Part 2 of 3 + cid + idx]
    S --> M3[Part 3 of 3 + cid + idx]
    M1 --> R[Resequencer / aggregator]
    M2 --> R
    M3 --> R
:::

## 2.27. Message Expiration (176)
**Category**  
Message construction

**Overview**  
A message carries a time limit after which it should no longer be processed normally.

**Description**  
**Message Expiration (176)** attaches a time limit or cutoff after which a message should not be processed as normal business work-because quotes lapse, tokens age, or retries would now be harmful. Expired messages may be discarded or routed aside.

This pattern reduces stale work and complements timeout policies in **Request-Reply (154)**. Brokers and consumers must agree how expiry is represented and enforced.

**Use when**  
Use when stale work is worse than dropped work.

**When not to use**  
- Avoid aggressive expiry without metrics-you may hide systemic slowdowns.
- Avoid conflicting expiry between broker TTL and application semantics without documentation.

**Typical scenarios**  
- Short-lived requests
- Timeout windows
- Freshness-sensitive events

**Related patterns**  
**Dead Letter Channel (119)**, **Invalid Message Channel (115)**, **Guaranteed Delivery (122)**.

**Combinations**  

Expiry must be consistent across brokers, aggregators, and gather phases so stale work does not block healthy traffic.

- **Correlation Identifier (163)** **SLAs** - Tie message TTL to business deadlines (for example “quote valid for 15 minutes”) and log when work completes after soft expiry.

:::mermaid
flowchart LR
    subgraph R0S0["Correlation Identifier"]
        direction TB
        r0n0["Tie message TTL to business deadlines (for example “quote valid for 15 minutes”) and log when wor..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Scatter-Gather (297)** **timers** - Use gather timeouts that match or exceed the slowest downstream SLA; expire partial gathers explicitly.

:::mermaid
flowchart LR
    subgraph R1S0["Scatter-Gather"]
        direction TB
        r1n0["Use gather timeouts that match or exceed the slowest downstream SLA; expire partial gathers expli..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Guaranteed Delivery (122)** - Document whether expired messages are deleted, dead-lettered, or counted as discarded for reconciliation.

:::mermaid
flowchart LR
    subgraph R2S0["Guaranteed Delivery"]
        direction TB
        r2n0["Document whether expired messages are deleted, dead-lettered, or counted as discarded for reconci..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- expired messages retried forever from **Aggregator (268)** without policy.
- no dead-letter path for expired poison messages.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Message created"] --> B{"Expired?"}
    B -- No --> C["Process normally"]
    B -- Yes --> D["Discard / route aside"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant P as Producer
    participant Q as Queue
    participant C as Consumer
    P->>Q: Message (expiresAt)
    Note over Q: TTL elapsed
    Q->>C: Skip or route to discard
:::

## 2.28. Format Indicator (180)
**Category**  
Message construction

**Overview**  
A message indicates which payload format or schema variant it uses.

**Description**  
A **Format Indicator (180)** tells the receiver which schema, version, or encoding applies to the payload, so multiple representations can coexist during migration. It enables gradual rollout and selective translation.

Pair with **Message Translator (85)** or **Normalizer (352)** at the boundary. Clear registration of format identifiers avoids ambiguous parsing and silent data loss.

**Use when**  
Use when receivers must distinguish among multiple valid message formats.

**When not to use**  
- Avoid ad hoc string markers without a registry-teams will diverge.
- Avoid infinite format proliferation; sunset old formats with policy.

**Typical scenarios**  
- Versioned payloads
- Alternate encodings
- Mixed source formats

**Related patterns**  
**Message Translator (85)**, **Envelope Wrapper (330)**, **Normalizer (352)**.

**Combinations**  

Formats should be registered, discoverable, and traceable when something unknown arrives.

- **Registry** - Maintain the authoritative list of format ids (and sunset dates) in documentation or a schema registry; enforce in CI for producers.

:::mermaid
flowchart LR
    subgraph R0S0["Registry"]
        direction TB
        r0n0["Maintain the authoritative list of format ids (and sunset dates) in documentation or a schema reg..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Unknown handling** - Route unknown formats to quarantine or **Invalid Message Channel (115)** with the **Correlation Identifier (163)** preserved for partner notification.

:::mermaid
flowchart LR
    subgraph R1S0["Invalid Message Channel"]
        direction TB
        r1n0["Route unknown formats to quarantine or Invalid Message Channel with the Correlation Identifier pr..."]
    end
    subgraph R1S1["Correlation Identifier"]
        direction TB
        r1n1["Route unknown formats to quarantine or Invalid Message Channel with the Correlation Identifier pr..."]
    end
    IN[In] --> r1n0
    r1n0 --> r1n1
    r1n1 --> OUT[Out]
:::

- **Message Translator (85)** - Map registered formats to internal versions at the edge; reject before deep parsing when possible.

:::mermaid
flowchart LR
    subgraph R2S0["Message Translator"]
        direction TB
        r2n0["Map registered formats to internal versions at the edge; reject before deep parsing when possible."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- version only in URL or side channel, not on the message.
- default parser that silently misreads data.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Message + format indicator"] --> B["Receiver / translator"] --> C["Correct parsing logic"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    M[Inbound] --> F{Format v1 / v2 / v3?}
    F -->|v1| T1[Translator v1]
    F -->|v2| T2[Translator v2]
    F -->|unknown| I[Invalid / quarantine]
:::

## 2.29. Content-Based Router (230)
**Category**  
Message routing

**Overview**  
A router examines message content and chooses the destination based on what it finds.

**Description**  
A **Content-Based Router (230)** inspects message fields or payload to choose the next destination, implementing business routing rules in one place. It is a specialisation of **Message Router (78)** driven by message content rather than static rules only.

Keep rules maintainable and testable; consider performance when payloads are large. Very dynamic rule sets may move toward **Dynamic Router (243)** or externalised policy stores.

**Use when**  
Use when destination depends on business data inside the message.

**When not to use**  
- Avoid deep payload inspection on hot paths without caching or streaming safeguards.
- Avoid content routing that embeds PII in routing logs.

**Typical scenarios**  
- Route by type
- Region
- Business rule
- Target system

**Related patterns**  
**Message Router (78)**, **Recipient List (249)**, **Dynamic Router (243)**.

**Combinations**  

Content routers benefit from external rule packs, shadow testing on pub/sub traffic, and safe defaults when rules fail to load.

- **External rules** - Store routing tables in configuration services or Git-backed pipelines so changes are reviewed and versioned.

:::mermaid
flowchart LR
    subgraph R0S0["External rules"]
        direction TB
        r0n0["Store routing tables in configuration services or Git-backed pipelines so changes are reviewed an..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Publish-Subscribe Channel (106)** **testing** - Replay captured production-shaped events to a test topic to validate routing before promoting rule changes.

:::mermaid
flowchart LR
    subgraph R1S0["Publish-Subscribe Channel"]
        direction TB
        r1n0["Replay captured production-shaped events to a test topic to validate routing before promoting rul..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Default route** - Define an explicit default to DLQ or audit rather than silent drop when no rule matches.

:::mermaid
flowchart LR
    subgraph R2S0["Default route"]
        direction TB
        r2n0["Define an explicit default to DLQ or audit rather than silent drop when no rule matches."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- undeployed default route black-holes messages.
- router rules duplicated in consumers.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Incoming message"] --> B{"Inspect content"}
    B -- Rule A --> C["Destination A"]
    B -- Rule B --> D["Destination B"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant R as Content-based router
    participant A as System A
    participant B as System B
    R->>R: Read business fields
    alt region = WA
        R->>A: Forward
    else region = SA
        R->>B: Forward
    end
:::

## 2.30. Message Filter (237)
**Category**  
Message routing

**Overview**  
A component passes through only messages that match stated criteria and drops or diverts the rest.

**Description**  
A **Message Filter (237)** drops or diverts messages that do not meet criteria, allowing downstream consumers to see only relevant traffic. Unlike **Content-Based Router (230)**, it usually continues on the same logical path or discards, rather than selecting among many business destinations.

Document what happens to filtered messages-silent drop versus side channel-to avoid hiding useful signals. Often combined with **Invalid Message Channel (115)** for bad data.

**Use when**  
Use when only certain messages should proceed to a given consumer or route.

**When not to use**  
- Avoid silent drops for compliance-critical traffic-route to an audit or **Message Store (555)** instead.
- Avoid filters that implement business transactions-use a proper consumer.

**Typical scenarios**  
- Ignore unsupported events
- Forward only relevant work
- Isolate noise

**Related patterns**  
**Content-Based Router (230)**, **Selective Consumer (515)**, **Invalid Message Channel (115)**.

**Combinations**  

Filters should be observable: every drop or divert should be measurable and intentional, not a silent black hole.

- **Divert paths** - Send filtered messages to a low-volume audit topic, **Message Store (555)**, or **Invalid Message Channel (115)** when compliance requires proof of receipt.

:::mermaid
flowchart LR
    subgraph R0S0["Message Store"]
        direction TB
        r0n0["Send filtered messages to a low-volume audit topic, Message Store, or Invalid Message Channel whe..."]
    end
    subgraph R0S1["Invalid Message Channel"]
        direction TB
        r0n1["Send filtered messages to a low-volume audit topic, Message Store, or Invalid Message Channel whe..."]
    end
    IN[In] --> r0n0
    r0n0 --> r0n1
    r0n1 --> OUT[Out]
:::

- **Metrics** - Count pass, drop, and divert rates; alert on sudden shifts (upstream schema change).

:::mermaid
flowchart LR
    subgraph R1S0["Metrics"]
        direction TB
        r1n0["Count pass, drop, and divert rates; alert on sudden shifts (upstream schema change)."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **DLQ policy** - Document when filtered messages become dead letters (usually only if they are malformed, not merely “not for this consumer”).

:::mermaid
flowchart LR
    subgraph R2S0["DLQ policy"]
        direction TB
        r2n0["Document when filtered messages become dead letters (usually only if they are malformed, not mere..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- filter logic duplicated upstream and downstream with drift.
- filtering after partial side effects.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Incoming messages"] --> B{"Matches filter?"}
    B -- Yes --> C["Forward"]
    B -- No --> D["Discard / divert"]
:::

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    In[Topic] --> F[Message filter]
    F -->|pass| OK[Core consumer]
    F -->|drop| T[Telemetry only]
:::

## 2.31. Dynamic Router (243)
**Category**  
Message routing

**Overview**  
Routing decisions are made step by step at runtime, often influenced by participants or state.

**Description**  
A **Dynamic Router (243)** determines the next hop at runtime, sometimes iteratively, using state, external configuration, or participant feedback. It supports evolving paths that a static **Content-Based Router (230)** cannot capture in one rule set.

Complexity and observability are the main risks; **Routing Slip (301)** or **Process Manager (312)** may be clearer when the route is knowable in advance or staged.

**Use when**  
Use when the next destination cannot be fully determined up front.

**When not to use**  
- Avoid when a fixed **Routing Slip (301)** or explicit **Recipient List (249)** would be clearer for auditors.
- Avoid unbounded loops or non-deterministic routing without safeguards.

**Typical scenarios**  
- Adaptive workflows
- Evolving route decisions
- Conversational flows

**Related patterns**  
**Routing Slip (301)**, **Recipient List (249)**, **Process Manager (312)**.

**Combinations**  

Dynamic routers need durable or sampled decision traces, bounded iteration, and resilient policy lookups.

- **Wire Tap (547)** - Record chosen hops and policy version for post-incident analysis (avoid logging full payloads).

:::mermaid
flowchart LR
    subgraph R0S0["Wire Tap"]
        direction TB
        r0n0["Record chosen hops and policy version for post-incident analysis (avoid logging full payloads)."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Caps** - Enforce maximum hops, wall-clock time, and loop detection so runaway conversations cannot exhaust the broker.

:::mermaid
flowchart LR
    subgraph R1S0["Caps"]
        direction TB
        r1n0["Enforce maximum hops, wall-clock time, and loop detection so runaway conversations cannot exhaust..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Policy access** - Cache policy responses with TTL; on failure, fail closed to DLQ or safe default per risk appetite.

:::mermaid
flowchart LR
    subgraph R2S0["Policy access"]
        direction TB
        r2n0["Cache policy responses with TTL; on failure, fail closed to DLQ or safe default per risk appetite."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- router calls remote policy service synchronously without timeout.
- hidden routing state only in volatile memory.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Incoming message"] --> B["Determine next destination"] --> C["Send"] --> D{"More routing?"}
    D -- Yes --> B
    D -- No --> E["Complete"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant DR as Dynamic router
    participant P as Policy service
    DR->>P: Next hop?
    P-->>DR: Route hint
    DR->>DR: Forward message
:::

## 2.32. Recipient List (249)
**Category**  
Message routing

**Overview**  
A message is delivered to a computed list of recipients rather than one fixed destination.

**Description**  
**Recipient List (249)** computes a set of destinations for a single message and forwards to each-like a targeted multicast. It differs from publish-subscribe in that membership is message-specific rather than subscription-based.

It is useful for quote-aggregation precursors (**Scatter-Gather (297)**) and per-message fan-out. Combine with **Correlation Identifier (163)** when collecting replies.

**Use when**  
Use when the set of destinations varies per message.

**When not to use**  
- Avoid when stable subscription sets are enough-**Test Message (569)** is simpler.
- Avoid recipient lists computed from untrusted input without authorisation checks.

**Typical scenarios**  
- Fan-out to selected targets
- Notifications
- Quote-style requests

**Related patterns**  
**Content-Based Router (230)**, **Publish-Subscribe Channel (106)**, **Scatter-Gather (297)**.

**Combinations**  

Recipient lists combine fan-out with failure handling: bad addresses should not poison the whole batch, and late replies need expiry.

- **Invalid Message Channel (115)** - When list building fails (bad recipient id), route the original message with diagnostics before any partial send.

:::mermaid
flowchart LR
    subgraph R0S0["Invalid Message Channel"]
        direction TB
        r0n0["When list building fails (bad recipient id), route the original message with diagnostics before a..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Guaranteed Delivery (122)** - Each fan-out leg should use durable channels if downstream processing must not lose work mid-flight.

:::mermaid
flowchart LR
    subgraph R1S0["Guaranteed Delivery"]
        direction TB
        r1n0["Each fan-out leg should use durable channels if downstream processing must not lose work mid-flight."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message Expiration (176)** - Align per-leg TTL with gather timeouts so slow recipients do not block aggregation indefinitely.

:::mermaid
flowchart LR
    subgraph R2S0["Message Expiration"]
        direction TB
        r2n0["Align per-leg TTL with gather timeouts so slow recipients do not block aggregation indefinitely."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Partial failure** - Define whether to continue sending to other recipients when one leg fails.

:::mermaid
flowchart LR
    subgraph R3S0["Partial failure"]
        direction TB
        r3n0["Define whether to continue sending to other recipients when one leg fails."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- fan-out without timeout-gather waits forever.
- duplicate recipients causing duplicate side effects without idempotency.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Incoming message"] --> B["Recipient list"]
    B --> C["Recipient 1"]
    B --> D["Recipient 2"]
    B --> E["Recipient 3"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant R as Router
    participant L as Recipient list builder
    participant T1 as Target 1
    participant T2 as Target 2
    R->>L: Message
    L->>T1: Copy
    L->>T2: Copy
:::

## 2.33. Splitter (259)
**Category**  
Message routing

**Overview**  
A component breaks one composite message into multiple smaller messages for separate handling.

**Description**  
A **Splitter (259)** breaks one composite message into many child messages, each processed independently. It enables parallel handling, per-line processing, or downstream scaling.

You must define correlation, ordering (**Message Sequence (170)**, **Resequencer (283)**), and aggregation if a single logical result is required-often via **Aggregator (268)** or **Composed Message Processor (294)**.

**Use when**  
Use when a bulk or grouped input must be processed item by item or step by step.

**When not to use**  
- Avoid splitting without **Request-Reply (154)** and completion semantics when results must reunite.
- Avoid per-item fan-out that overwhelms downstream rate limits.

**Typical scenarios**  
- Batch decomposition
- Item-level handling
- Later aggregation of individual results

**Related patterns**  
**Aggregator (268)**, **Composed Message Processor (294)**, **Message Sequence (170)**.

**Combinations**  

Splitting is only half the story: children need sequence metadata, optional reordering, durable join state, and idempotent handlers.

- **Message Sequence (170)** - Stamp index/total (or stream position) on every child so downstream can detect completeness.

:::mermaid
flowchart LR
    subgraph R0S0["Message Sequence"]
        direction TB
        r0n0["Stamp index/total (or stream position) on every child so downstream can detect completeness."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Resequencer (283)** - Reorder children when the bus does not guarantee global order.

:::mermaid
flowchart LR
    subgraph R1S0["Resequencer"]
        direction TB
        r1n0["Reorder children when the bus does not guarantee global order."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Aggregator (268)** - Rejoin outcomes when a single parent result is required after parallel child processing.

:::mermaid
flowchart LR
    subgraph R2S0["Aggregator"]
        direction TB
        r2n0["Rejoin outcomes when a single parent result is required after parallel child processing."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Message Store (555)** - Persist partial progress for long-running splits so restarts do not lose correlation state.

:::mermaid
flowchart LR
    subgraph R3S0["Message Store"]
        direction TB
        r3n0["Persist partial progress for long-running splits so restarts do not lose correlation state."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- children processed twice on retry, duplicating business effects.
- splitter holds large payloads in memory without streaming.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Composite message"] --> B["Splitter"]
    B --> C["Item 1"]
    B --> D["Item 2"]
    B --> E["Item 3"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant S as Splitter
    participant Q as Work queue
    S->>Q: Item 1 + cid
    S->>Q: Item 2 + cid
    S->>Q: Item 3 + cid
:::

## 2.34. Aggregator (268)
**Category**  
Message routing

**Overview**  
A component collects related messages and combines them into one outcome once enough information has arrived.

**Description**  
An **Aggregator (268)** collects related messages until a completion condition is met, then emits a combined result. It is the fan-in counterpart to **Splitter (259)** or **Scatter-Gather (297)**.

Completion rules (count, timeout, correlation) must be precise to avoid deadlocks or premature release. State may be held in memory or a **Message Store (555)** for durability.

**Use when**  
Use when multiple partial results must be joined into a single logical result.

**When not to use**  
- Avoid ambiguous completion conditions (missing timeout or count).
- Avoid aggregating unrelated correlations into one result.

**Typical scenarios**  
- Multi-step result collection
- Fan-in after split processing
- Partial completion handling

**Related patterns**  
**Splitter (259)**, **Correlation Identifier (163)**, **Resequencer (283)**.

**Combinations**  

Aggregators must not lose state on restart and must close correlations cleanly under timeout or partial failure.

- **Message Store (555)** - Persist partial aggregates and completion rules for crash recovery; compact state when groups complete.

:::mermaid
flowchart LR
    subgraph R0S0["Message Store"]
        direction TB
        r0n0["Persist partial aggregates and completion rules for crash recovery; compact state when groups com..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Scatter-Gather (297)** **timeout policy** - Publish a timeout result, notify callers, and free buffers when not all parts arrive.

:::mermaid
flowchart LR
    subgraph R1S0["Scatter-Gather"]
        direction TB
        r1n0["Publish a timeout result, notify callers, and free buffers when not all parts arrive."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Partial failure** - Decide whether to emit best-effort aggregates, errors, or compensating events; document for API clients.

:::mermaid
flowchart LR
    subgraph R2S0["Partial failure"]
        direction TB
        r2n0["Decide whether to emit best-effort aggregates, errors, or compensating events; document for API c..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- aggregator memory leak on abandoned correlations.
- publishing incomplete aggregates under pressure.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Message 1"] --> D["Aggregator"]
    B["Message 2"] --> D
    C["Message 3"] --> D
    D --> E["Combined result"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant A as Aggregator
    participant O as Output
    A->>A: Part 1 (cid)
    A->>A: Part 2 (cid)
    A->>A: Part 3 (cid) completes group
    A->>O: Emit combined result
:::

## 2.35. Resequencer (283)
**Category**  
Message routing

**Overview**  
A component restores messages to the required order before further processing.

**Description**  
A **Resequencer (283)** buffers out-of-order messages and releases them in the required sequence to downstream processing that depends on order. It addresses network reordering or competing producers.

It requires sequence metadata and policies for gaps and timeouts. Pair with **Message Sequence (170)** when the sender deliberately splits ordered streams.

**Use when**  
Use when messages can arrive out of order but must be consumed in sequence.

**When not to use**  
- Avoid without gap detection and maximum wait-buffers grow without bound.
- Avoid when strict global order across partitions is impossible-redesign partitioning.

**Typical scenarios**  
- Out-of-order event handling and ordered downstream processing

**Related patterns**  
**Message Sequence (170)**, **Aggregator (268)**, **Message Store (555)**.

**Combinations**  

Resequencers trade latency for order: sequence metadata defines the expected stream, and expiry prevents buffers growing forever when messages are lost.

- **Message Sequence (170)** - Every buffered message carries group id and position so the component knows what “next” means.

:::mermaid
flowchart LR
    subgraph R0S0["Message Sequence"]
        direction TB
        r0n0["Every buffered message carries group id and position so the component knows what “next” means."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message Expiration (176)** - If the next expected index never arrives, expire the group and escalate (DLQ, alert, or partial emit per policy).

:::mermaid
flowchart LR
    subgraph R1S0["Message Expiration"]
        direction TB
        r1n0["If the next expected index never arrives, expire the group and escalate (DLQ, alert, or partial e..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Sharding** - Scale by correlation key so one entity’s ordering does not block unrelated traffic.

:::mermaid
flowchart LR
    subgraph R2S0["Sharding"]
        direction TB
        r2n0["Scale by correlation key so one entity’s ordering does not block unrelated traffic."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- resequencer single-threaded bottleneck without scaling plan.
- reordering across different business entities.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["3"] --> D["Resequencer"]
    B["1"] --> D
    C["2"] --> D
    D --> E["1,2,3"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    I[Out-of-order input] --> B[Buffer by correlation]
    B --> W{Next seq ready?}
    W -->|yes| O[Emit in order]
    W -->|timeout| DL[Escalate / DLQ]
:::

## 2.36. Composed Message Processor (294)
**Category**  
Message routing

**Overview**  
A higher-level flow splits a composite message, routes the parts for processing, and recombines the results.

**Description**  
**Composed Message Processor (294)** orchestrates **Splitter (259)**, routing, and **Aggregator (268)** (and optionally translators) as one pattern: a large message is decomposed, parts are processed, and results are composed again.

It is a template for complex but structured workflows over messaging, often implemented with a **Process Manager (312)** when state and timers matter.

**Use when**  
Use when one logical request naturally decomposes into coordinated sub-requests.

**When not to use**  
- Avoid when a single synchronous call or **Message Expiration (176)** chain is clearer and reliable enough.
- Avoid deep nesting of composed processors without **Correlation Identifier (163)** visibility.

**Typical scenarios**  
- Multi-step validation
- Distributed enrichment
- Item-level downstream calls

**Related patterns**  
**Splitter (259)**, **Aggregator (268)**, **Process Manager (312)**.

**Combinations**  

The composed processor is a template: split, route parts, aggregate, and answer the original caller-usually with explicit reply routing and durable orchestration for failures.

- **Return Address (159)** - Preserve reply-to and correlation on every sub-request so parallel workers can respond through one completion path.

:::mermaid
flowchart LR
    subgraph R0S0["Return Address"]
        direction TB
        r0n0["Preserve reply-to and correlation on every sub-request so parallel workers can respond through on..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Routing** - Use **Message Router (78)** or **Content-Based Router (230)** to steer parts without embedding topology in each worker.

:::mermaid
flowchart LR
    subgraph R1S0["Message Router"]
        direction TB
        r1n0["Use Message Router or Content-Based Router to steer parts without embedding topology in each worker."]
    end
    subgraph R1S1["Content-Based Router"]
        direction TB
        r1n1["Use Message Router or Content-Based Router to steer parts without embedding topology in each worker."]
    end
    IN[In] --> r1n0
    r1n0 --> r1n1
    r1n1 --> OUT[Out]
:::

- **Idempotent Receiver (528)** - Sub-tasks and aggregates must tolerate retries when any leg redelivers.

:::mermaid
flowchart LR
    subgraph R2S0["Idempotent Receiver"]
        direction TB
        r2n0["Sub-tasks and aggregates must tolerate retries when any leg redelivers."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Process Manager (312)** - Own timers, compensations, and state when the composed flow spans long periods or many systems.

:::mermaid
flowchart LR
    subgraph R3S0["Process Manager"]
        direction TB
        r3n0["Own timers, compensations, and state when the composed flow spans long periods or many systems."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- partial failures leave no compensation.
- aggregate timeout shorter than slowest downstream SLA.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Composite request"] --> B["Split"]
    B --> C["Process part A"]
    B --> D["Process part B"]
    C --> E["Aggregate"]
    D --> E
    E --> F["Composed result"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant CMP as Composed processor
    participant A as Worker A
    participant B as Worker B
    CMP->>A: Sub-request 1
    CMP->>B: Sub-request 2
    A-->>CMP: Result 1
    B-->>CMP: Result 2
    CMP->>CMP: Compose response
:::

## 2.37. Scatter-Gather (297)
**Category**  
Message routing

**Overview**  
A request is sent to multiple recipients and their responses are gathered into one overall result.

**Description**  
**Scatter-Gather (297)** broadcasts a request to multiple recipients (scatter) and aggregates their replies into one response (gather). It implements parallel consultation-pricing, eligibility, or data fusion-under a single logical operation.

Timeouts, partial failures, and correlation are central design points. **Recipient List (249)** plus **Aggregator (268)** is the usual implementation sketch.

**Use when**  
Use when multiple parties should contribute to one combined response.

**When not to use**  
- Avoid without **Message Expiration (176)** and partial-result policy when any target may be slow or down.
- Avoid when targets are not idempotent for duplicate scatter retries.

**Typical scenarios**  
- Parallel lookups
- Quote collection
- Multi-source evaluation

**Related patterns**  
**Recipient List (249)**, **Aggregator (268)**, **Correlation Identifier (163)**.

**Combinations**  

Scatter-gather is typically implemented as a computed recipient list, parallel sends, and an aggregator keyed by correlation, with strict timeout semantics.

- **Recipient List (249)** - Build the set of targets per request (policy, eligibility, or static configuration) and fan out copies with the same correlation id.

:::mermaid
flowchart LR
    subgraph R0S0["Recipient List"]
        direction TB
        r0n0["Build the set of targets per request (policy, eligibility, or static configuration) and fan out c..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Aggregator (268)** - Collect replies until count, completeness marker, or timeout; emit merged response or structured partial result.

:::mermaid
flowchart LR
    subgraph R1S0["Aggregator"]
        direction TB
        r1n0["Collect replies until count, completeness marker, or timeout; emit merged response or structured ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Correlation Identifier (163)** - Shared across scatter and all replies so gather logic never mixes conversations.

:::mermaid
flowchart LR
    subgraph R2S0["Correlation Identifier"]
        direction TB
        r2n0["Shared across scatter and all replies so gather logic never mixes conversations."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Message Translator (85)** - Normalise heterogeneous reply shapes before merge.

:::mermaid
flowchart LR
    subgraph R3S0["Message Translator"]
        direction TB
        r3n0["Normalise heterogeneous reply shapes before merge."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- gather waits indefinitely.
- combining incompatible result schemas without **Message Translator (85)**.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Request"] --> B["Scatter"]
    B --> C["Target 1"]
    B --> D["Target 2"]
    B --> E["Target 3"]
    C --> F["Gather"]
    D --> F
    E --> F
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant C as Caller
    participant SG as Scatter-gather
    participant T1 as Target 1
    participant T2 as Target 2
    C->>SG: Request (cid)
    SG->>T1: Parallel ask
    SG->>T2: Parallel ask
    T1-->>SG: Reply (cid)
    T2-->>SG: Reply (cid)
    SG-->>C: Merged response
:::

## 2.38. Routing Slip (301)
**Category**  
Message routing

**Overview**  
A message carries the route it should follow through a series of processing steps.

**Description**  
A **Routing Slip (301)** attaches the list-or recipe-of processing steps to the message itself, so each hop consumes the slip and forwards to the next. It decentralises routing logic while keeping the path explicit and auditable.

It suits configurable pipelines where the route is known when the message is created. Contrast with **Dynamic Router (243)** when the next hop is decided only after each step.

**Use when**  
Use when route steps should travel with the message rather than live in a central router.

**When not to use**  
- Avoid when steps or order must change based on deep runtime state-**Canonical Data Model (355)** may fit better.
- Avoid slips that grow unbounded or carry sensitive control data without protection.

**Typical scenarios**  
- Configurable multi-step processing and staged handling

**Related patterns**  
**Dynamic Router (243)**, **Process Manager (312)**, **Message Router (78)**.

**Combinations**  

Routing slips travel with the message: each hop should translate if needed, verify the slip has not been altered, and enforce maximum steps.

- **Message Translator (85)** - At each step, map to local format while preserving slip metadata and remaining route list in a tamper-evident way (sign or HMAC where appropriate).

:::mermaid
flowchart LR
    subgraph R0S0["Message Translator"]
        direction TB
        r0n0["At each step, map to local format while preserving slip metadata and remaining route list in a ta..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Integrity** - Validate slip on entry to each hop; reject or DLQ if steps were removed or reordered unexpectedly.

:::mermaid
flowchart LR
    subgraph R1S0["Integrity"]
        direction TB
        r1n0["Validate slip on entry to each hop; reject or DLQ if steps were removed or reordered unexpectedly."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Audit** - Log slip version and remaining steps to **Message History (551)** for compliance-heavy pipelines.

:::mermaid
flowchart LR
    subgraph R2S0["Message History"]
        direction TB
        r2n0["Log slip version and remaining steps to Message History for compliance-heavy pipelines."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- slip tampering between hops.
- circular slips without max hops.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Message + routing slip"] --> B["Step 1"] --> C["Step 2"] --> D["Step 3"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant M as Message + slip
    participant S1 as Step 1
    participant S2 as Step 2
    M->>S1: Process + pop slip
    S1->>S2: Forward remaining slip
    S2->>S2: Final step
:::

## 2.39. Process Manager (312)
**Category**  
Message routing

**Overview**  
A coordinating component keeps track of a long-running or multi-step process and decides what should happen next.

**Description**  
A **Process Manager (312)** (or orchestrator) maintains state for a long-running business process spanning many messages and systems. It decides what to do next when events arrive, timers fire, or errors occur.

It differs from a simple **Message Router (78)** by owning process state and lifecycles. Persistence, idempotency, and correlation are critical; **Message Store (555)** often backs recovery.

**Use when**  
Use when a workflow spans multiple messages, systems, and decision points.

**When not to use**  
- Avoid for short linear flows that a single service or **Format Indicator (180)** handles cleanly.
- Avoid “mega orchestrators” that own every domain rule-push decisions to services.

**Typical scenarios**  
- Pending business processes
- Approval flows
- Orchestrated retry or compensation

**Related patterns**  
**Request-Reply (154)**, **Correlation Identifier (163)**, **Message Store (555)**.

**Combinations**  

Process managers combine durable state, domain events, and often large payload indirection so orchestration survives restarts and communicates clearly with other systems.

- **Claim Check (346)** - Store bulky state or attachments externally; the manager tracks references and retrieval policy.

:::mermaid
flowchart LR
    subgraph R0S0["Claim Check"]
        direction TB
        r0n0["Store bulky state or attachments externally; the manager tracks references and retrieval policy."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Event Message (151)** - Emit state transitions as events so read models and other services react without polling the orchestrator database.

:::mermaid
flowchart LR
    subgraph R1S0["Event Message"]
        direction TB
        r1n0["Emit state transitions as events so read models and other services react without polling the orch..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Sagas / compensation** - Define explicit compensating commands or events for each forward step; document failure matrices and idempotency.

:::mermaid
flowchart LR
    subgraph R2S0["Sagas / compensation"]
        direction TB
        r2n0["Define explicit compensating commands or events for each forward step; document failure matrices ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- lost state on restart.
- duplicate events advancing state twice without idempotency keys.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Start process"] --> B["Process manager"]
    B --> C["Step / message / decision"]
    C --> B
    B --> D["Process complete"]
:::

**State diagram** - lifecycle or state transitions.

:::mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> AwaitingApproval: event
    AwaitingApproval --> Completed: approved
    AwaitingApproval --> Rejected: rejected
    Completed --> [*]
    Rejected --> [*]
:::

## 2.40. Message Broker (322)
**Category**  
Message routing

**Overview**  
A central intermediary combines routing, transformation, and possibly other integration responsibilities.

**Description**  
A **Message Broker (322)** is a central architectural role (often a product) that routes messages from senders to receivers, may transform or enrich, and provides cross-cutting services such as naming, security, and monitoring.

Brokers simplify many-to-many connectivity but can become bottlenecks or operational single points unless clustered and governed. The pattern names the hub style seen in enterprise service buses and advanced brokers.

**Use when**  
Use when a hub must mediate among many participants and contracts.

**When not to use**  
- Avoid when a thin **Claim Check (346)** plus endpoints is enough-brokers that transform everything can become bottlenecks.
- Avoid centralising domain logic that belongs in bounded contexts.

**Typical scenarios**  
- Central integration hubs and brokered exchange between multiple systems

**Related patterns**  
**Message Router (78)**, **Message Translator (85)**, **Message Bus (137)**.

**Combinations**  

Brokers are operational hubs: high availability, back-pressure, observability, and tested mapping at the edge keep the hub from becoming a fragile bottleneck.

- **HA and rate limits** - Cluster brokers; apply per-tenant or per-route quotas so one noisy neighbour cannot starve others.

:::mermaid
flowchart LR
    subgraph R0S0["HA and rate limits"]
        direction TB
        r0n0["Cluster brokers; apply per-tenant or per-route quotas so one noisy neighbour cannot starve others."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Correlation Identifier (163)** - Propagate through broker transforms and dead-letter metadata for end-to-end tracing.

:::mermaid
flowchart LR
    subgraph R1S0["Correlation Identifier"]
        direction TB
        r1n0["Propagate through broker transforms and dead-letter metadata for end-to-end tracing."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message Endpoint (95)** - Prefer transformation and validation in application endpoints (with automated contract tests) rather than opaque broker-only rules that hide consumer gaps.

:::mermaid
flowchart LR
    subgraph R2S0["Message Endpoint"]
        direction TB
        r2n0["Prefer transformation and validation in application endpoints (with automated contract tests) rat..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- broker as the only place schemas are validated, hiding consumer bugs.
- broker downtime stops all traffic with no degradation path.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Source A"] --> B["Message broker"] --> D["Target A"]
    C["Source B"] --> B --> E["Target B"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    S[Sources] --> B[Broker: route + transform + monitor]
    B --> T[Targets]
    B --> DL[DLQ / alert]
:::

## 2.41. Envelope Wrapper (330)
**Category**  
Message transformation

**Overview**  
Extra wrapper information is added around a message so it can travel through an intermediary or standard channel.

**Description**  
**Envelope Wrapper (330)** wraps domain payload in a standard envelope carrying routing, security, or metadata fields. Applications work with the payload; infrastructure works with the envelope.

It enables consistent cross-cutting concerns without polluting every business schema. Version the envelope separately from payload formats when both evolve.

**Use when**  
Use when transport or infrastructure metadata must be carried separately from business payload.

**When not to use**  
- Avoid double-wrapping envelopes without a clear versioning story.
- Avoid putting large bodies in the envelope-use **Idempotent Receiver (528)**.

**Typical scenarios**  
- Headers
- Transport metadata
- Interoperability wrappers

**Related patterns**  
**Format Indicator (180)**, **Content Enricher (336)**, **Message Translator (85)**.

**Combinations**  

Envelopes evolve on a different cadence than payloads: version both, authenticate the envelope, and keep parsing rules centralised.

- **Dual versioning** - Maintain envelope schema version independently from payload **Format Indicator (180)** so infrastructure can upgrade without forcing business payload changes.

:::mermaid
flowchart LR
    subgraph R0S0["Format Indicator"]
        direction TB
        r0n0["Maintain envelope schema version independently from payload Format Indicator so infrastructure ca..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Signing / authentication** - Use signatures or tokens on the envelope to detect tampering in transit through intermediaries.

:::mermaid
flowchart LR
    subgraph R1S0["Signing / authentication"]
        direction TB
        r1n0["Use signatures or tokens on the envelope to detect tampering in transit through intermediaries."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message Translator (85)** - Strip or validate envelope at trust boundaries before domain code runs.

:::mermaid
flowchart LR
    subgraph R2S0["Message Translator"]
        direction TB
        r2n0["Strip or validate envelope at trust boundaries before domain code runs."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- consumers parsing envelope fields as business facts.
- envelope so large it duplicates the payload.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Business payload"] --> B["Envelope wrapper"] --> C["Wrapped message"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    E[Envelope: routing, security, trace] --> W[Wrapped message]
    P[Payload: business body] --> W
:::

## 2.42. Content Enricher (336)
**Category**  
Message transformation

**Overview**  
A component adds missing information to a message by looking it up elsewhere.

**Description**  
**Content Enricher (336)** adds missing data to a message by looking up reference data or calling other services before forwarding. It centralises fetch-and-merge logic so consumers receive a fuller view.

Mind latency, failure modes, and caching. Over-enrichment can create oversized messages-sometimes addressed with **Claim Check (346)** for heavy blobs.

**Use when**  
Use when downstream processing needs information the original sender did not provide.

**When not to use**  
- Avoid synchronous enrichment chains that exceed latency SLOs or call unstable systems without circuit breakers.
- Avoid enriching with regulated data the downstream is not authorised to receive.

**Typical scenarios**  
- Reference data lookups
- Contextual enrichment
- Filling gaps

**Related patterns**  
**Content Filter (342)**, **Message Translator (85)**, **Claim Check (346)**.

**Combinations**  

Enrichment should be fast, bounded, and observable: cache reference data, offload large additions to dedicated queues, and quarantine bad joins early.

- **Caching** - Cache lookups with TTL and stampede protection; degrade gracefully when the reference API is down.

:::mermaid
flowchart LR
    subgraph R0S0["Caching"]
        direction TB
        r0n0["Cache lookups with TTL and stampede protection; degrade gracefully when the reference API is down."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Point-to-Point Channel (103)** - For very large enrichments, post a follow-up command on a dedicated queue rather than blocking the hot path indefinitely.

:::mermaid
flowchart LR
    subgraph R1S0["Point-to-Point Channel"]
        direction TB
        r1n0["For very large enrichments, post a follow-up command on a dedicated queue rather than blocking th..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Invalid Message Channel (115)** - When lookup fails in a way that makes the message unprocessable, divert with reason codes instead of silent drop.

:::mermaid
flowchart LR
    subgraph R2S0["Invalid Message Channel"]
        direction TB
        r2n0["When lookup fails in a way that makes the message unprocessable, divert with reason codes instead..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- N+1 remote calls per message.
- enrichment hides missing upstream data quality issues.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Original message"] --> B["Content enricher"]
    C["Lookup source"] --> B
    B --> D["Enriched message"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant M as Message
    participant E as Enricher
    participant L as Lookup API
    M->>E: Arrives
    E->>L: Fetch context
    L-->>E: Reference data
    E->>E: Merge
    E->>E: Forward enriched
:::

## 2.43. Content Filter (342)
**Category**  
Message transformation

**Overview**  
A component removes unnecessary parts of a message while retaining what the receiver needs.

**Description**  
**Content Filter (342)** removes fields or sections that the next recipient does not need, reducing coupling and payload size. It is the inverse of **Content Enricher (336)** in intent.

Ensure filtering does not discard data required for auditing or later hops; sometimes a separate tap (**Wire Tap (547)**) preserves the full message for observability.

**Use when**  
Use when the receiver only needs a subset of the original content.

**When not to use**  
- Avoid filtering out fields required for audit, replay, or later **Dead Letter Channel (119)** steps unless **Format Indicator (180)** captures the full message.
- Avoid destructive filtering before authorisation checks complete.

**Typical scenarios**  
- Reducing payload size
- Hiding irrelevant fields
- Simplifying target contracts

**Related patterns**  
**Content Enricher (336)**, **Message Translator (85)**, **Claim Check (346)**.

**Combinations**  

Filtered streams are easier to operate when filtered traffic has its own channel or subscription for monitoring, and when privacy policies define what may be removed.

- **Datatype Channel (111)** - Route “full” versus “reduced” representations on different channels so support can compare volumes without mixing contracts.

:::mermaid
flowchart LR
    subgraph R0S0["Datatype Channel"]
        direction TB
        r0n0["Route “full” versus “reduced” representations on different channels so support can compare volume..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Privacy minimisation** - Document which fields are stripped for each partner class; pair with **Wire Tap (547)** of full payloads only in secured stores if audit requires it.

:::mermaid
flowchart LR
    subgraph R1S0["Wire Tap"]
        direction TB
        r1n0["Document which fields are stripped for each partner class; pair with Wire Tap of full payloads on..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Format Indicator (180)** - Bump version when filter shape changes so consumers know which keys to expect.

:::mermaid
flowchart LR
    subgraph R2S0["Format Indicator"]
        direction TB
        r2n0["Bump version when filter shape changes so consumers know which keys to expect."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- receiver must call back because filter removed needed keys.
- inconsistent filters per consumer duplicating logic.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Large message"] --> B["Content filter"] --> C["Reduced message"]
:::

**Flowchart (left-to-right)** - variant 2: use when a reduced payload goes to the partner but a parallel tap keeps the full message for audit or observability (the primary is filter-only).

:::mermaid
flowchart LR
    Full[Full message] --> F[Content filter]
    Full -.-> T[Wire tap / audit store]
    F --> R[Reduced to partner]
:::

## 2.44. Claim Check (346)
**Category**  
Message transformation

**Overview**  
Large or sensitive message content is stored separately and replaced in the message with a reference.

**Description**  
**Claim Check (346)** stores a large payload in external storage and passes a reference (claim check) through the messaging system instead of the heavy body. It keeps messages small and brokers efficient.

Security, retention, and consistency between the store and the message must be managed. Receivers retrieve the payload when needed using the reference.

**Use when**  
Use when the full payload should not travel with every message hop.

**When not to use**  
- Avoid when receivers cannot access the store (network, credentials, or sovereignty).
- Avoid tiny payloads where indirection only adds latency and failure modes.

**Typical scenarios**  
- Large attachments
- Deferred retrieval
- Sensitive bulk content

**Related patterns**  
**Content Filter (342)**, **Content Enricher (336)**, **Message Store (555)**.

**Combinations**  

Claim checks introduce a second consistency surface: the blob store must be as well governed as the bus, with aligned retention and traceability.

- **Secure store** - Enforce authentication, authorisation, encryption at rest, and network boundaries; never embed long-lived secrets in messages.

:::mermaid
flowchart LR
    subgraph R0S0["Secure store"]
        direction TB
        r0n0["Enforce authentication, authorisation, encryption at rest, and network boundaries; never embed lo..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message History (551)** - Record claim issuance and retrieval events (metadata only) so support can trace orphaned blobs.

:::mermaid
flowchart LR
    subgraph R1S0["Message History"]
        direction TB
        r1n0["Record claim issuance and retrieval events (metadata only) so support can trace orphaned blobs."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Signed URLs** - Prefer time-limited URLs or tokens for fetch; align TTL with **Message Expiration (176)** on the reference message.

:::mermaid
flowchart LR
    subgraph R2S0["Message Expiration"]
        direction TB
        r2n0["Prefer time-limited URLs or tokens for fetch; align TTL with Message Expiration on the reference ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- claim check without access control on the store.
- orphaned blobs after message processing fails.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Large message"] --> B["Store body"] --> C["Message with claim check"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant P as Producer
    participant S as Blob store
    participant B as Bus
    participant C as Consumer
    P->>S: Upload payload
    S-->>P: Token / URI
    P->>B: Message + claim check
    B->>C: Deliver reference
    C->>S: Retrieve when needed
:::

## 2.45. Normalizer (352)
**Category**  
Message transformation

**Overview**  
A component converts different source message variants into one normalised form.

**Description**  
A **Normalizer (352)** converts different message formats from multiple sources into a common structure so downstream logic can be single-path. It reduces N×M translations when many senders feed one process.

It often sits upstream of a **Canonical Data Model (355)**. Maintain clear mapping ownership per source format.

**Use when**  
Use when many sources express similar facts differently.

**When not to use**  
- Avoid forcing diverse domains into one premature model-bounded contexts may need separate models.
- Avoid normalisers that discard source-specific nuance required by compliance.

**Typical scenarios**  
- Upstream source harmonisation and simplified downstream contracts

**Related patterns**  
**Canonical Data Model (355)**, **Message Translator (85)**, **Format Indicator (180)**.

**Combinations**  

Normalisers should sit before core logic, emit observability for each variant, and be covered by fixture tests per upstream format.

- **Placement** - Run normalisation immediately after ingress validation so downstream services see one canonical shape.

:::mermaid
flowchart LR
    subgraph R0S0["Placement"]
        direction TB
        r0n0["Run normalisation immediately after ingress validation so downstream services see one canonical s..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Wire Tap (547)** - Sample normalised outputs per source for regression detection when partners change schemas silently.

:::mermaid
flowchart LR
    subgraph R1S0["Wire Tap"]
        direction TB
        r1n0["Sample normalised outputs per source for regression detection when partners change schemas silently."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Testing** - Maintain golden-message fixtures per source variant in CI; fail builds when mappings drift.

:::mermaid
flowchart LR
    subgraph R2S0["Testing"]
        direction TB
        r2n0["Maintain golden-message fixtures per source variant in CI; fail builds when mappings drift."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- normaliser becomes a second database of business rules.
- unversioned mapping tables.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Source variant A"] --> C["Normalizer"]
    B["Source variant B"] --> C
    C --> D["Normalised message"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    SA[Source A format] --> N[Normalizer]
    SB[Source B format] --> N
    N --> CDM[Canonical shape]
    CDM --> Down[Single downstream path]
:::

## 2.46. Canonical Data Model (355)
**Category**  
Message transformation

**Overview**  
A common enterprise representation reduces point-to-point translations between many systems.

**Description**  
A **Canonical Data Model (355)** defines a standard internal representation of key business entities and events, independent of any one system’s native schema. Integrations map in and out of the canonical form.

It lowers long-term mapping complexity but requires governance: the canonical model must evolve carefully with versioning and impact analysis.

**Use when**  
Use when many systems exchange similar business information in different structures.

**When not to use**  
- Avoid “canonical everything” when sub-domains are stable and pairwise mapping is cheaper.
- Avoid freezing a canonical model without a governance board and deprecation policy.

**Typical scenarios**  
- CoreData-style common structures and shared integration vocabulary

**Related patterns**  
**Normalizer (352)**, **Message Translator (85)**, **Message Bus (137)**.

**Combinations**  

A canonical model is only valuable with governed evolution: translators at every edge, semantic versioning, and change impact analysis before rollout.

- **Message Translator (85)** - Every system maps to and from the canonical representation at its boundary; forbid “reach into” foreign schemas from mid-tier services.

:::mermaid
flowchart LR
    subgraph R0S0["Message Translator"]
        direction TB
        r0n0["Every system maps to and from the canonical representation at its boundary; forbid “reach into” f..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Versioning** - Version canonical entities and publish deprecation timelines; use straddle periods with dual-write or dual-read where needed.

:::mermaid
flowchart LR
    subgraph R1S0["Versioning"]
        direction TB
        r1n0["Version canonical entities and publish deprecation timelines; use straddle periods with dual-writ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Impact analysis** - Run consumer contract tests and partner notifications before breaking changes; track downstream mapping matrices.

:::mermaid
flowchart LR
    subgraph R2S0["Impact analysis"]
        direction TB
        r2n0["Run consumer contract tests and partner notifications before breaking changes; track downstream m..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- canonical model is just one vendor’s schema renamed.
- breaking changes without dual-write or straddle period.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["System A"] --> B["Canonical model"] --> C["System B"]
    D["System C"] --> B
:::

**Flowchart (left-to-right)** - variant 2: use when showing explicit to-canonical and from-canonical translations (the primary is a simpler hub without those edges).

:::mermaid
flowchart LR
    A[Sys A] --> TA[To canonical]
    B[Sys B] --> TB[To canonical]
    TA --> C[Canonical hub]
    TB --> C
    C --> FA[From canonical to A]
    C --> FB[From canonical to B]
:::

## 2.47. Messaging Gateway (468)
**Category**  
Messaging endpoint

**Overview**  
An application-facing façade exposes messaging interaction in an application-friendly form.

**Description**  
A **Messaging Gateway (468)** exposes messaging to the application as a simple API (or hides messaging behind one), so domain code does not depend on queue APIs directly. It encapsulates connection management and sometimes mapping.

It complements **Message Endpoint (95)** and **Messaging Mapper (477)**-gateway for API shape, mapper for object-to-message conversion.

**Use when**  
Use when callers should use methods or service operations without dealing with message mechanics directly.

**When not to use**  
- Avoid hiding so much that back-pressure, timeouts, and poison handling are invisible to callers.
- Avoid gateways that become god-objects routing all enterprise traffic.

**Typical scenarios**  
- Publisher and subscriber facades
- Gateway services
- Integration APIs

**Related patterns**  
**Message Endpoint (95)**, **Messaging Mapper (477)**, **Service Activator (532)**.

**Combinations**  

Gateways should map exceptions clearly, document async versus sync semantics, and use well-named channels behind a stable API.

- **Message Channel (60)** - Configure destination channels, timeouts, and retry policy in one place rather than scattering queue names through callers.

:::mermaid
flowchart LR
    subgraph R0S0["Message Channel"]
        direction TB
        r0n0["Configure destination channels, timeouts, and retry policy in one place rather than scattering qu..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Exception mapping** - Map broker errors to application exceptions that align with your standard error format; include correlation ids in fault messages.

:::mermaid
flowchart LR
    subgraph R1S0["Exception mapping"]
        direction TB
        r1n0["Map broker errors to application exceptions that align with your standard error format; include c..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Async semantics** - Document when methods return after publish versus after acknowledgement or reply (**Request-Reply (154)**).

:::mermaid
flowchart LR
    subgraph R2S0["Request-Reply"]
        direction TB
        r2n0["Document when methods return after publish versus after acknowledgement or reply (Request-Reply)."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- gateway methods that block unbounded time.
- gateway reimplements broker features poorly.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Application"] --> B["Messaging gateway"] --> C["Messaging system"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant App as Application
    participant GW as Messaging gateway
    participant Bus as Bus
    App->>GW: sendOrder(cmd)
    GW->>Bus: Publish mapped message
:::

## 2.48. Messaging Mapper (477)
**Category**  
Messaging endpoint

**Overview**  
A component maps between the application’s internal object model and message structures.

**Description**  
**Messaging Mapper (477)** translates between domain objects and message representations (serialization, headers, keys). It keeps mapping rules out of business services and close to the boundary.

It is especially helpful when the same domain model is projected into several message types or versions; pair with **Format Indicator (180)** for evolution.

**Use when**  
Use when application classes and message contracts should remain independent.

**When not to use**  
- Avoid duplicating mapping in every service-extract shared mappers or codegen.
- Avoid mapping so complex it should be a dedicated **Message Translator (85)** service.

**Typical scenarios**  
- Application-to-message mapping and DTO conversion

**Related patterns**  
**Messaging Gateway (468)**, **Message Translator (85)**, **Canonical Data Model (355)**.

**Combinations**  

Mappers belong beside endpoints so serialisation rules stay cohesive; wire versions ride on format indicators, not implicit class names.

- **Message Endpoint (95)** - Co-locate mapper with connection and ack logic so every send/receive path uses the same DTO rules.

:::mermaid
flowchart LR
    subgraph R0S0["Message Endpoint"]
        direction TB
        r0n0["Co-locate mapper with connection and ack logic so every send/receive path uses the same DTO rules."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Format Indicator (180)** - Encode schema/version on outgoing messages; branch parsers on receive for rolling upgrades.

:::mermaid
flowchart LR
    subgraph R1S0["Format Indicator"]
        direction TB
        r1n0["Encode schema/version on outgoing messages; branch parsers on receive for rolling upgrades."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **No hidden IO** - Mappers should not call databases or remote APIs-use **Content Enricher (336)** instead.

:::mermaid
flowchart LR
    subgraph R2S0["Content Enricher"]
        direction TB
        r2n0["Mappers should not call databases or remote APIs-use Content Enricher instead."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- domain entity serialized directly as wire format with no evolution path.
- mapper performs hidden IO.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Application model"] --> B["Messaging mapper"] --> C["Message model"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    DTO[Domain object] --> M[Mapper layer]
    M --> JSON[Wire DTO + headers]
    JSON --> V[Validate against contract]
:::

## 2.49. Transactional Client (484)
**Category**  
Messaging endpoint

**Overview**  
**Message (66)** send or receive work participates in a transaction so changes and message handling stay consistent.

**Description**  
A **Transactional Client (484)** coordinates message sends or receives with local resource updates in a single transactional unit where the platform supports it, so you do not commit database state without the message (or vice versa).

Exact semantics depend on the broker and database; distributed transactions are not always available, so outbox patterns or idempotent processing may still be required.

**Use when**  
Use when message handling and local state changes must succeed or fail together.

**When not to use**  
- Avoid assuming two-phase commit is available across cloud databases and brokers-prefer **Idempotent Receiver (528)** only where supported, otherwise use outbox/inbox.
- Avoid long transactions holding message locks.

**Typical scenarios**  
- Reliable command handling
- Database updates with messaging
- Exactly-once-style coordination

**Related patterns**  
**Guaranteed Delivery (122)**, **Idempotent Receiver (528)**, **Service Activator (532)**.

**Combinations**  

Transactional messaging usually means outbox/inbox or broker-native transactions: document ordering, pair invalid handling, and test failure modes.

- **Invalid Message Channel (115)** - Messages that fail validation after receive should not retry forever; route aside with diagnostics.

:::mermaid
flowchart LR
    subgraph R0S0["Invalid Message Channel"]
        direction TB
        r0n0["Messages that fail validation after receive should not retry forever; route aside with diagnostics."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Commit order** - Prefer transactional outbox (DB + outbox row in one commit, then async publisher) over “publish then DB” unless the platform offers true XA and you accept its limits.

:::mermaid
flowchart LR
    subgraph R1S0["Commit order"]
        direction TB
        r1n0["Prefer transactional outbox (DB + outbox row in one commit, then async publisher) over “publish t..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Idempotent Receiver (528)** - Even with transactions, duplicates happen; handlers must still be safe.

:::mermaid
flowchart LR
    subgraph R2S0["Idempotent Receiver"]
        direction TB
        r2n0["Even with transactions, duplicates happen; handlers must still be safe."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- message published then DB rolls back.
- distributed transaction spanning unreliable third parties.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Receive / send message"] --> B["Transactional work"] --> C["Commit or roll back"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant C as Consumer
    participant DB as Database
    participant B as Broker
    C->>DB: Begin
    C->>DB: Persist + outbox row
    C->>B: Publish (same tx or follow-up worker)
    C->>DB: Commit
:::

## 2.50. Polling Consumer (494)
**Category**  
Messaging endpoint

**Overview**  
A consumer actively checks for messages at intervals.

**Description**  
A **Polling Consumer (494)** actively requests messages on a schedule or loop, controlling when work is accepted. It suits environments where push callbacks are awkward or rate limiting is consumer-driven.

Trade-offs include latency versus empty polls and the need to size batch and timeout correctly. **Event-Driven Consumer (498)** is the alternative when immediate delivery is preferred.

**Use when**  
Use when the consumer controls its own receive timing or cannot be driven by events.

**When not to use**  
- Avoid tight polling loops that hammer the broker or API; use long polling or backoff.
- Avoid polling when near-real-time processing is required-prefer **Message Store (555)**.

**Typical scenarios**  
- Scheduled polling
- Controlled batch receive
- Pull-based consumption

**Related patterns**  
**Event-Driven Consumer (498)**, **Selective Consumer (515)**, **Transactional Client (484)**.

**Combinations**  

Polling should batch intelligently, back off on errors, and respect message TTL so stale work is not processed late.

- **Batch receive** - Use max message count and max wait to balance latency and efficiency; scale batch size with backlog depth where safe.

:::mermaid
flowchart LR
    subgraph R0S0["Batch receive"]
        direction TB
        r0n0["Use max message count and max wait to balance latency and efficiency; scale batch size with backl..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message Expiration (176)** - Skip or dead-letter expired messages during receive so pollers do not waste cycles on stale payloads.

:::mermaid
flowchart LR
    subgraph R1S0["Message Expiration"]
        direction TB
        r1n0["Skip or dead-letter expired messages during receive so pollers do not waste cycles on stale paylo..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Backoff** - Exponential backoff when the broker or downstream errors; avoid hot loops hammering a failing system.

:::mermaid
flowchart LR
    subgraph R2S0["Backoff"]
        direction TB
        r2n0["Exponential backoff when the broker or downstream errors; avoid hot loops hammering a failing sys..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- poll with zero sleep under errors.
- polling at fixed rate regardless of backlog (missed scaling signal).

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Consumer poll"] --> B["Channel"] --> C["Message if available"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant C as Consumer
    participant Q as Queue
    loop Every interval / long poll
        C->>Q: Receive batch
        alt Messages
            C->>C: Process
        else Empty
            C->>C: Sleep / backoff
        end
    end
:::

## 2.51. Event-Driven Consumer (498)
**Category**  
Messaging endpoint

**Overview**  
A consumer is invoked when a message arrives instead of polling for it.

**Description**  
An **Event-Driven Consumer (498)** is notified or invoked when a message arrives, enabling low-latency, push-style processing. It maps well to callbacks, hosted listeners, and serverless triggers.

Ensure back-pressure, concurrency limits, and error handling so bursts do not overwhelm the consumer or starve others.

**Use when**  
Use when low-latency, push-style handling is preferred.

**When not to use**  
- Avoid unbounded concurrency-configure max parallel handlers and queue depth alerts.
- Avoid push handlers that perform blocking IO without isolation from the callback thread.

**Typical scenarios**  
- Subscriber callbacks
- Queue triggers
- Immediate processing

**Related patterns**  
**Polling Consumer (494)**, **Service Activator (532)**, **Competing Consumers (502)**.

**Combinations**  

Push handlers need taps for supportability, filters to shed noise early, and DLQ discipline when exceptions escape.

- **Wire Tap (547)** - Capture a sample of inbound messages (metadata-first) for debugging bursts without blocking the callback path.

:::mermaid
flowchart LR
    subgraph R0S0["Wire Tap"]
        direction TB
        r0n0["Capture a sample of inbound messages (metadata-first) for debugging bursts without blocking the c..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message Filter (237)** - Drop or divert irrelevant messages before they occupy worker threads during spikes.

:::mermaid
flowchart LR
    subgraph R1S0["Message Filter"]
        direction TB
        r1n0["Drop or divert irrelevant messages before they occupy worker threads during spikes."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Dead Letter Channel (119)** - After bounded retries, move poison messages with failure reason; never infinite nack loops.

:::mermaid
flowchart LR
    subgraph R2S0["Dead Letter Channel"]
        direction TB
        r2n0["After bounded retries, move poison messages with failure reason; never infinite nack loops."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- handler swallows exceptions without nack/dead-letter.
- autoscale on CPU while message age grows (wrong signal).

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Channel receives message"] --> B["Event-driven consumer invoked"] --> C["Handle message"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant B as Broker
    participant H as Handler
    B->>H: Push deliver
    H->>H: Process
    alt OK
        H-->>B: Ack
    else Fail
        H-->>B: Nack / retry policy
    end
:::

## 2.52. Competing Consumers (502)
**Category**  
Messaging endpoint

**Overview**  
Multiple consumers compete for messages from the same point-to-point channel to increase throughput.

**Description**  
**Competing Consumers (502)** run multiple instances reading from the same **Point-to-Point Channel (103)** so messages are distributed for horizontal scale. Each message is still handled by one instance.

Requires idempotent or partition-friendly processing when failures cause redelivery. Monitor consumer lag and poison messages routed to **Dead Letter Channel (119)**.

**Use when**  
Use when work should scale horizontally while each message is still handled once.

**When not to use**  
- Avoid competing consumers on ordered streams that require single-threaded sequence-use partition keys or **Invalid Message Channel (115)**.
- Avoid scaling without **Format Indicator (180)** when redelivery is possible.

**Typical scenarios**  
- Queue-based work distribution and scaled subscriber processing

**Related patterns**  
**Point-to-Point Channel (103)**, **Event-Driven Consumer (498)**, **Dead Letter Channel (119)**.

**Combinations**  

Competing consumers scale only when observability matches: lag, poison rate, and autoscale signals should reflect message age-not just CPU.

- **Lag metrics** - Track oldest message age and processing latency percentiles per queue and consumer group.

:::mermaid
flowchart LR
    subgraph R0S0["Lag metrics"]
        direction TB
        r0n0["Track oldest message age and processing latency percentiles per queue and consumer group."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Poison and DLQ** - Alert when DLQ depth spikes or poison rate exceeds baseline; tie alerts to deploy windows.

:::mermaid
flowchart LR
    subgraph R1S0["Poison and DLQ"]
        direction TB
        r1n0["Alert when DLQ depth spikes or poison rate exceeds baseline; tie alerts to deploy windows."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Autoscale** - Prefer scaling on backlog depth or age in addition to CPU to avoid under-scaling during IO-bound stalls.

:::mermaid
flowchart LR
    subgraph R2S0["Autoscale"]
        direction TB
        r2n0["Prefer scaling on backlog depth or age in addition to CPU to avoid under-scaling during IO-bound ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- all instances share one DB sequence bottleneck.
- autoscale too fast causing thundering herd on downstream APIs.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Work queue"] --> B["Consumer 1"]
    A --> C["Consumer 2"]
    A --> D["Consumer 3"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    Q[Queue] --> L{Load balance}
    L --> C1[Instance 1]
    L --> C2[Instance 2]
    L --> C3[Instance 3]
    C1 --> DL[Shared DLQ policy]
    C2 --> DL
    C3 --> DL
:::

## 2.53. Message Dispatcher (508)
**Category**  
Messaging endpoint

**Overview**  
A dispatcher selects the appropriate handler for an incoming message.

**Description**  
A **Message Dispatcher (508)** sits on the consuming side and dispatches incoming messages to the correct handler based on type or action, similar to polymorphic dispatch in application code.

It keeps a single endpoint while supporting many message varieties; combine with **Selective Consumer (515)** or **Message Filter (237)** when selection rules are complex.

**Use when**  
Use when one endpoint receives different message types or actions.

**When not to use**  
- Avoid a single giant switch with no registration model-use conventions or **Claim Check (346)** upstream.
- Avoid dispatch based on unvalidated user-controlled type strings.

**Typical scenarios**  
- Subscriber dispatch
- Command handler selection
- Endpoint demultiplexing

**Related patterns**  
**Selective Consumer (515)**, **Service Activator (532)**, **Message Router (78)**.

**Combinations**  

Dispatchers multiplex one endpoint: combine with selective receive patterns and centralise unknown-type handling upstream of business code.

- **Selective Consumer (515)** - When products support filters, reduce wasted deliveries so the dispatcher only sees relevant types.

:::mermaid
flowchart LR
    subgraph R0S0["Selective Consumer"]
        direction TB
        r0n0["When products support filters, reduce wasted deliveries so the dispatcher only sees relevant types."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message Filter (237)** - Route unknown or deprecated types to a dedicated diagnostics path instead of a silent default handler.

:::mermaid
flowchart LR
    subgraph R1S0["Message Filter"]
        direction TB
        r1n0["Route unknown or deprecated types to a dedicated diagnostics path instead of a silent default han..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Registration model** - Use explicit handler registry (convention or DI) so new types fail fast in tests if unhandled.

:::mermaid
flowchart LR
    subgraph R2S0["Registration model"]
        direction TB
        r2n0["Use explicit handler registry (convention or DI) so new types fail fast in tests if unhandled."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- default handler silently ignores types.
- dispatcher does heavy validation that belongs in **Durable Subscriber (522)**.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Incoming message"] --> B["Message dispatcher"]
    B --> C["Handler A"]
    B --> D["Handler B"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    M[Message] --> D{Type?}
    D -->|Create| H1[Create handler]
    D -->|Update| H2[Update handler]
    D -->|else| U[Unknown handler]
:::

## 2.54. Selective Consumer (515)
**Category**  
Messaging endpoint

**Overview**  
A consumer deliberately takes only messages that match stated selection rules.

**Description**  
A **Selective Consumer (515)** receives from a shared channel but processes only messages matching its criteria, leaving others for different consumers. It differs from **Message Filter (237)** in that selection is consumer-side and cooperative.

Messaging products vary in how selection is implemented (browser filters, subscriptions); avoid head-of-line blocking where possible.

**Use when**  
Use when a consumer shares a channel but should process only certain messages.

**When not to use**  
- Avoid when head-of-line blocking makes shared channels unusable-split channels or use server-side **Point-to-Point Channel (103)**.
- Avoid complex selection in clients when the broker can enforce subscriptions.

**Typical scenarios**  
- Filtered subscriptions and targeted processing on shared channels

**Related patterns**  
**Message Filter (237)**, **Publish-Subscribe Channel (106)**, **Polling Consumer (494)**.

**Combinations**  

Selective consumption must be documented per broker: skipping messages can still advance offsets, and duplicates still require idempotent handlers.

- **Idempotent Receiver (528)** - Even selective consumers receive duplicates; business keys remain mandatory.

:::mermaid
flowchart LR
    subgraph R0S0["Idempotent Receiver"]
        direction TB
        r0n0["Even selective consumers receive duplicates; business keys remain mandatory."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Broker semantics** - Document whether skipped messages remain available to other consumers or block head-of-line; choose channel topology accordingly.

:::mermaid
flowchart LR
    subgraph R1S0["Broker semantics"]
        direction TB
        r1n0["Document whether skipped messages remain available to other consumers or block head-of-line; choo..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Runbooks** - Publish selection predicates and test fixtures so predicate drift is caught in CI.

:::mermaid
flowchart LR
    subgraph R2S0["Runbooks"]
        direction TB
        r2n0["Publish selection predicates and test fixtures so predicate drift is caught in CI."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- consumer skips messages it should take, starving processing.
- selection rules drift from documentation.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Shared channel"] --> B{"Selected by consumer?"}
    B -- Yes --> C["Consume"]
    B -- No --> D["Leave for others / ignore"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant Q as Shared queue
    participant C1 as Consumer A (filter=WA)
    participant C2 as Consumer B (filter=SA)
    Q->>C1: Offer WA message
    C1->>Q: Accept
    Q->>C2: Offer WA message
    C2->>Q: Skip (not SA)
:::

## 2.55. Durable Subscriber (522)
**Category**  
Messaging endpoint

**Overview**  
A subscriber receives messages published while it is temporarily offline because the subscription is durable.

**Description**  
A **Durable Subscriber (522)** retains subscription state so that messages published while it is offline are delivered when it returns-essential for critical event processing over Publish-Subscribe.

Operational concerns include backlog growth, retention limits, and duplicate delivery on failure; **Idempotent Receiver (528)** remains important.

**Use when**  
Use when subscribers must not miss relevant published messages.

**When not to use**  
- Avoid durable subs for fire-and-forget telemetry that can drop data.
- Avoid unbounded backlog without retention policy-disk and cost explode.

**Typical scenarios**  
- Critical event subscribers and restart-tolerant subscriptions

**Related patterns**  
**Publish-Subscribe Channel (106)**, **Guaranteed Delivery (122)**, **Message Store (555)**.

**Combinations**  

Durable subscribers need backlog governance: retention limits, idempotent processing, and a replay plan after extended outage.

- **Idempotent Receiver (528)** - Backlog replay will duplicate; handlers must dedupe by event id or business key.

:::mermaid
flowchart LR
    subgraph R0S0["Idempotent Receiver"]
        direction TB
        r0n0["Backlog replay will duplicate; handlers must dedupe by event id or business key."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Backlog policy** - Define max age, max count, or compaction rules; alert before retention drops messages unexpectedly.

:::mermaid
flowchart LR
    subgraph R1S0["Backlog policy"]
        direction TB
        r1n0["Define max age, max count, or compaction rules; alert before retention drops messages unexpectedly."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Replay runbook** - After long outage, document whether to fast-forward, snapshot, or bulk-replay from **Message Store (555)**.

:::mermaid
flowchart LR
    subgraph R2S0["Message Store"]
        direction TB
        r2n0["After long outage, document whether to fast-forward, snapshot, or bulk-replay from Message Store."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- durable subscription per ephemeral environment without cleanup.
- expecting strict ordering across all events on a topic.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Publisher"] --> B["Durable subscription"] --> C["Subscriber online later receives backlog"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant P as Publisher
    participant T as Topic
    participant S as Subscriber (offline)
    P->>T: Events while down
    Note over S: Restart
    S->>T: Resume durable sub
    T->>S: Deliver backlog
:::

## 2.56. Idempotent Receiver (528)
**Category**  
Messaging endpoint

**Overview**  
A receiver can safely process duplicate deliveries without causing incorrect repeated effects.

**Description**  
An **Idempotent Receiver (528)** ensures that processing the same logical message more than once does not corrupt state-using deduplication keys, natural keys, or safe upserts. It is essential under at-least-once delivery and retries.

Design commands and events with business keys and clear already-processed semantics. Pair with **Message Store (555)** or outbox logs when dedupe state must survive restarts.

**Use when**  
Use when messages may be retried or delivered more than once.

**When not to use**  
- Avoid “idempotency” that only checks message id without business meaning when ids are regenerated on retry.
- Avoid storing unbounded dedupe keys without TTL.

**Typical scenarios**  
- Duplicate event handling
- Safe retries
- Resilient command processing

**Related patterns**  
**Guaranteed Delivery (122)**, **Message Store (555)**, **Transactional Client (484)**.

**Combinations**  

Idempotency stores should use meaningful business keys, bounded retention, and transactional ordering so effects never run before the dedupe gate.

- **Natural keys** - Prefer stable business identifiers over transport message ids when brokers may assign new ids on redelivery.

:::mermaid
flowchart LR
    subgraph R0S0["Natural keys"]
        direction TB
        r0n0["Prefer stable business identifiers over transport message ids when brokers may assign new ids on ..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Outbox / inbox** - Persist processed-key tables in the same transaction as domain writes when strong consistency is required.

:::mermaid
flowchart LR
    subgraph R1S0["Outbox / inbox"]
        direction TB
        r1n0["Persist processed-key tables in the same transaction as domain writes when strong consistency is ..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Distributed cache** - Use TTL aligned to maximum redelivery window; handle cache loss as “unknown” and re-check authoritative store.

:::mermaid
flowchart LR
    subgraph R2S0["Distributed cache"]
        direction TB
        r2n0["Use TTL aligned to maximum redelivery window; handle cache loss as “unknown” and re-check authori..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- non-idempotent side effects before dedupe check.
- assuming broker delivers exactly-once without design.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Receive message"] --> B{"Already processed?"}
    B -- Yes --> C["Ignore safely"]
    B -- No --> D["Process and record"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant R as Receiver
    participant S as Idempotency store
    R->>S: Check business key
    alt New
        S-->>R: Proceed
        R->>R: Execute side effects
        R->>S: Record done
    else Duplicate
        S-->>R: Skip
    end
:::

## 2.57. Service Activator (532)
**Category**  
Messaging endpoint

**Overview**  
A messaging component invokes application logic when a message arrives.

**Description**  
A **Service Activator (532)** connects messaging to application services: when a message arrives, it invokes the appropriate service method and may send a reply. It is the messaging analogue of a controller entry point.

Often used with **Transactional Client (484)** and **Message Dispatcher (508)**. Keep activators thin so business rules stay in the domain layer.

**Use when**  
Use when messages should trigger business services or domain operations.

**When not to use**  
- Avoid embedding orchestration and sagas inside the activator-use **Process Manager (312)**.
- Avoid activators that call blocking remote APIs without timeouts.

**Typical scenarios**  
- Subscriber handlers
- Queue-triggered actions
- Command execution

**Related patterns**  
**Message Endpoint (95)**, **Event-Driven Consumer (498)**, **Transactional Client (484)**.

**Combinations**  

Activators should be thin entry points: parse, dispatch to the right handler, optionally join a transaction, and delegate all rules to domain services.

- **Message Dispatcher (508)** - Route by type or action so the activator does not grow a monolithic switch.

:::mermaid
flowchart LR
    subgraph R0S0["Message Dispatcher"]
        direction TB
        r0n0["Route by type or action so the activator does not grow a monolithic switch."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Thin boundary** - Keep SQL, HTTP calls, and business rules inside domain services, not in the messaging adapter.

:::mermaid
flowchart LR
    subgraph R1S0["Thin boundary"]
        direction TB
        r1n0["Keep SQL, HTTP calls, and business rules inside domain services, not in the messaging adapter."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Transactional Client (484)** - When handling must align with DB commits, use outbox/inbox or broker-coordinated transactions as supported.

:::mermaid
flowchart LR
    subgraph R2S0["Transactional Client"]
        direction TB
        r2n0["When handling must align with DB commits, use outbox/inbox or broker-coordinated transactions as ..."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- activator contains SQL and business rules.
- no correlation logging on failure.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Incoming message"] --> B["Service activator"] --> C["Application service"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant M as Message
    participant A as Service activator
    participant S as Domain service
    M->>A: Deliver
    A->>S: Invoke use case
    S-->>A: Result / exception
    A-->>M: Ack / nack policy
:::

## 2.58. Control Bus (540)
**Category**  
System management

**Overview**  
Operational commands and monitoring messages travel through a dedicated control path.

**Description**  
The **Control Bus (540)** uses messaging channels dedicated to management-configuration changes, test probes, metrics collection, or lifecycle commands-separate from business traffic.

It lets operators interact with integration components consistently. Combine with **Test Message (569)** and **Wire Tap (547)** for observability without disturbing primary flows.

**Use when**  
Use when runtime control and observation should use the messaging infrastructure itself.

**When not to use**  
- Avoid mixing control traffic on business topics without strict ACLs.
- Avoid control commands without authentication-risk of remote shutdown or data exfiltration paths.

**Typical scenarios**  
- Operational toggles
- Health commands
- Management instructions

**Related patterns**  
**Wire Tap (547)**, **Message Store (555)**, **Detour (545)**.

**Combinations**  

Control buses require network isolation, strong authentication, audit trails, and synthetic probes to prove liveness.

- **Isolation** - Separate control topics/queues from business traffic; restrict with network ACLs and RBAC.

:::mermaid
flowchart LR
    subgraph R0S0["Isolation"]
        direction TB
        r0n0["Separate control topics/queues from business traffic; restrict with network ACLs and RBAC."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Audit** - Log who issued each control command, source IP, and outcome; retain per security policy.

:::mermaid
flowchart LR
    subgraph R1S0["Audit"]
        direction TB
        r1n0["Log who issued each control command, source IP, and outcome; retain per security policy."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Test Message (569)** - Schedule synthetic control and data-plane probes; alert when probes fail across regions.

:::mermaid
flowchart LR
    subgraph R2S0["Test Message"]
        direction TB
        r2n0["Schedule synthetic control and data-plane probes; alert when probes fail across regions."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- control bus reachable from untrusted networks.
- no versioning on control message schema.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Control command / event"] --> B["Control bus"] --> C["Managed component"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant Ops as Operator / automation
    participant CB as Control bus
    participant N as Integration node
    Ops->>CB: Config / pause / resume
    CB->>N: Deliver command
    N-->>CB: Status event
:::

## 2.59. Detour (545)
**Category**  
System management

**Overview**  
Messages are temporarily diverted through an alternate route for inspection, testing, or other purposes.

**Description**  
**Detour (545)** temporarily reroutes messages through an alternate path-for example inspection, shadow processing, or migration-then returns them to the normal route. It supports change with reduced risk.

Clearly define entry and exit criteria so detours do not become permanent hidden paths. Related to **Smart Proxy (558)** and **Wire Tap (547)** patterns.

**Use when**  
Use when a route should be changed without permanently redesigning the flow.

**When not to use**  
- Avoid permanent “temporary” detours-track expiry and ownership.
- Avoid detours that change payload semantics without **Test Message (569)** or contract tests.

**Typical scenarios**  
- Temporary bypass
- Inspection routes
- Controlled diagnostics

**Related patterns**  
**Wire Tap (547)**, **Control Bus (540)**, **Smart Proxy (558)**.

**Combinations**  

Detours should be operator-driven, observable, and safe: toggle via control bus, validate shadow outputs with taps, and always rejoin or fail closed.

- **Control Bus (540)** - Enable, disable, and scope detours through authenticated control messages with audit trails.

:::mermaid
flowchart LR
    subgraph R0S0["Control Bus"]
        direction TB
        r0n0["Enable, disable, and scope detours through authenticated control messages with audit trails."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Wire Tap (547)** - Compare shadow path outputs to main path samples for regression testing during migrations.

:::mermaid
flowchart LR
    subgraph R1S0["Wire Tap"]
        direction TB
        r1n0["Compare shadow path outputs to main path samples for regression testing during migrations."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Rejoin policy** - On shadow failure, either revert to main path only or halt with alert-never silently drop.

:::mermaid
flowchart LR
    subgraph R2S0["Rejoin policy"]
        direction TB
        r2n0["On shadow failure, either revert to main path only or halt with alert-never silently drop."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- detour drops messages on error.
- detour bypasses security controls present on main path.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Normal route"] --> B["Detour"] --> C["Alternate path"] --> D["Return / continue"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    M[Message] --> D{Detour flag?}
    D -->|on| X[Inspection / shadow]
    X --> R[Rejoin main path]
    D -->|off| R
:::

## 2.60. Wire Tap (547)
**Category**  
System management

**Overview**  
A copy of a message is sent to a secondary channel without disturbing the main flow.

**Description**  
**Wire Tap (547)** sends a non-intrusive copy of a message to a secondary channel while the original proceeds unchanged. It supports auditing, debugging, analytics, and **Message History (551)**.

Ensure the tap cannot overwhelm downstream systems and that sensitive data in copies is governed like production traffic.

**Use when**  
Use when monitoring, audit, or side analysis is needed without affecting primary processing.

**When not to use**  
- Avoid tapping highly sensitive payloads into unsecured sinks.
- Avoid taps that double write volume without throttling-overwhelms observability stack.

**Typical scenarios**  
- Audit trails
- Telemetry feeds
- Message inspection

**Related patterns**  
**Message History (551)**, **Message Store (555)**, **Control Bus (540)**.

**Combinations**  

Wire taps should land in governed stores or SIEM pipelines with redaction, sampling, and asynchronous hand-off so observability never blocks production latency.

- **Message Store (555)** - Persist tap stream for replay or compliance when legal retention allows; index by **Correlation Identifier (163)**.

:::mermaid
flowchart LR
    subgraph R0S0["Message Store"]
        direction TB
        r0n0["Persist tap stream for replay or compliance when legal retention allows; index by Correlation Ide..."]
    end
    subgraph R0S1["Correlation Identifier"]
        direction TB
        r0n1["Persist tap stream for replay or compliance when legal retention allows; index by Correlation Ide..."]
    end
    IN[In] --> r0n0
    r0n0 --> r0n1
    r0n1 --> OUT[Out]
:::

- **SIEM / analytics** - Forward structured tap events with field-level redaction; align with privacy assessments.

:::mermaid
flowchart LR
    subgraph R1S0["SIEM / analytics"]
        direction TB
        r1n0["Forward structured tap events with field-level redaction; align with privacy assessments."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Sampling** - Use head-based or tail-based sampling under load to protect downstream systems.

:::mermaid
flowchart LR
    subgraph R2S0["Sampling"]
        direction TB
        r2n0["Use head-based or tail-based sampling under load to protect downstream systems."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- synchronous tap blocking main path.
- tap store without retention policy (privacy incident).

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Main flow"] --> B["Primary destination"]
    A -. copy .-> C["Wire tap channel"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant M as Main path
    participant P as Primary consumer
    participant T as Tap sink
    M->>P: Deliver
    M->>T: Async copy (non-blocking)
:::

## 2.61. Message History (551)
**Category**  
System management

**Overview**  
Metadata is collected about where a message has been and what happened to it.

**Description**  
**Message History (551)** accumulates metadata about components and steps a message has passed through, building a trail for support and analysis. It can be implemented by appending headers or external correlation stores.

Balance detail against message size and privacy. Often implemented via **Wire Tap (547)** into a logging or tracing pipeline.

**Use when**  
Use when support or observability needs to reconstruct the path of a message.

**When not to use**  
- Avoid unbounded header growth-use external trace store for large histories.
- Avoid logging full payloads in history when PII is present.

**Typical scenarios**  
- Troubleshooting
- Audit
- End-to-end flow analysis

**Related patterns**  
**Wire Tap (547)**, **Message Store (555)**, **Correlation Identifier (163)**.

**Combinations**  

Message history is often implemented as a tap into a tracing backend: each hop appends structured metadata keyed by correlation.

- **Wire Tap (547)** - Emit hop events to a trace collector asynchronously; avoid growing unbounded headers on the business message.

:::mermaid
flowchart LR
    subgraph R0S0["Wire Tap"]
        direction TB
        r0n0["Emit hop events to a trace collector asynchronously; avoid growing unbounded headers on the busin..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Correlation Identifier (163)** - Use the same id across hops and in the trace store for single-pane support views.

:::mermaid
flowchart LR
    subgraph R1S0["Correlation Identifier"]
        direction TB
        r1n0["Use the same id across hops and in the trace store for single-pane support views."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Integrity** - Sign hop records or use trusted internal services only to append history.

:::mermaid
flowchart LR
    subgraph R2S0["Integrity"]
        direction TB
        r2n0["Sign hop records or use trusted internal services only to append history."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- history only in unstructured logs.
- forged hop headers without integrity checks.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Message"] --> B["Hop 1"] --> C["Hop 2"] --> D["Hop 3"]
    D --> E["History available for inspection"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    H1[Hop adds component + timestamp] --> H2[Next hop appends]
    H2 --> T[Trace backend query by correlation id]
:::

## 2.62. Message Store (555)
**Category**  
System management

**Overview**  
A persistent store keeps messages or related state for replay, inspection, or later continuation.

**Description**  
**Message Store (555)** persists messages or process state so they can be replayed, inspected, or resumed after failure or delay. It backs recovery, audit, and sometimes legal retention.

Define retention, encryption, and replay semantics. Frequently used with **Process Manager (312)** and **Guaranteed Delivery (122)** discussions.

**Use when**  
Use when messages or process state must survive restarts, delays, or long-running workflows.

**When not to use**  
- Avoid storing full payloads indefinitely without legal retention policy.
- Avoid message store as the system of record for business data-use domain databases.

**Typical scenarios**  
- Deferred work
- Pending workflows
- Retry handling
- Replay support
- Long-running process state

**Related patterns**  
**Process Manager (312)**, **Request-Reply (154)**, **Message History (551)**.

**Combinations**  

Message stores must be encrypted, queryable for support, and TTL-aligned with business expiry; replay tooling must never double-apply side effects.

- **Encrypt at rest** - Use platform KMS or equivalent; separate keys per environment.

:::mermaid
flowchart LR
    subgraph R0S0["Encrypt at rest"]
        direction TB
        r0n0["Use platform KMS or equivalent; separate keys per environment."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Correlation Identifier (163)** - Index persisted messages or state snapshots by cid for incident response.

:::mermaid
flowchart LR
    subgraph R1S0["Correlation Identifier"]
        direction TB
        r1n0["Index persisted messages or state snapshots by cid for incident response."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message Expiration (176)** - Apply retention/TTL policies that match legal and operational needs; purge expired rows on schedule.

:::mermaid
flowchart LR
    subgraph R2S0["Message Expiration"]
        direction TB
        r2n0["Apply retention/TTL policies that match legal and operational needs; purge expired rows on schedule."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

- **Idempotent Receiver (528)** - Replays into business systems must use the same dedupe rules as live traffic.

:::mermaid
flowchart LR
    subgraph R3S0["Idempotent Receiver"]
        direction TB
        r3n0["Replays into business systems must use the same dedupe rules as live traffic."]
    end
    IN[In] --> r3n0
    r3n0 --> OUT[Out]
:::

**Anti-patterns**  
- replay replays side effects without **Idempotent Receiver (528)** discipline.
- store holds secrets in clear text.

---

**Diagrams**

**Layered or staged view.**

:::mermaid
flowchart TD
    A["Message arrives"] --> B["Persist message or state"]
    B --> C["Resume later processing"]
    C --> D["Re-read / replay / inspect as needed"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant W as Worker
    participant S as Message store
    participant B as Bus
    B->>W: Deliver
    W->>S: Checkpoint state
    W->>B: Ack
    Note over W,S: After crash, reload from store
:::

## 2.63. Smart Proxy (558)
**Category**  
System management

**Overview**  
An intermediary stands in the communication path and adds value while preserving the apparent conversation.

**Description**  
**Smart Proxy (558)** intercepts messaging between parties to add behaviour-correlation repair, timeout handling, caching, or routing-while preserving the apparent conversation between endpoints.

It suits situations where legacy endpoints cannot be changed but cross-cutting behaviour is required, in contrast to simple pass-through proxies.

**Use when**  
Use when replies or interactions must be adjusted without changing the endpoints.

**When not to use**  
- Avoid proxies that mutate payloads without contract tests.
- Avoid hiding latency and failure modes from callers without documentation.

**Typical scenarios**  
- Reply address handling
- Correlation repair
- Mediation of legacy endpoints

**Related patterns**  
**Return Address (159)**, **Correlation Identifier (163)**, **Detour (545)**.

**Combinations**  

Smart proxies repair legacy reply and correlation behaviour: rewrite return addresses, normalise correlation headers, and run highly available.

- **Return Address (159)** - Map incorrect or static reply destinations to per-caller dynamic queues when legacy services cannot be changed.

:::mermaid
flowchart LR
    subgraph R0S0["Return Address"]
        direction TB
        r0n0["Map incorrect or static reply destinations to per-caller dynamic queues when legacy services cann..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Correlation Identifier (163)** - Inject or repair missing correlation ids so downstream observability and matching work.

:::mermaid
flowchart LR
    subgraph R1S0["Correlation Identifier"]
        direction TB
        r1n0["Inject or repair missing correlation ids so downstream observability and matching work."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **HA deployment** - Run active/active or active/passive pairs with health checks; document added latency budget.

:::mermaid
flowchart LR
    subgraph R2S0["HA deployment"]
        direction TB
        r2n0["Run active/active or active/passive pairs with health checks; document added latency budget."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- proxy becomes single point without HA.
- proxy logs sensitive payloads.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Caller"] --> B["Smart proxy"] --> C["Service"]
    C --> B --> A
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant C as Caller
    participant P as Smart proxy
    participant S as Service
    C->>P: Request
    P->>S: Adjusted request
    S-->>P: Reply (wrong queue)
    P-->>C: Correlation / reply fix
:::

## 2.64. Test Message (569)
**Category**  
System management

**Overview**  
A special message exercises the integration path to confirm that routing and processing still work.

**Description**  
**Test Message (569)** sends synthetic traffic through integration paths to verify routing, transformation, and availability without relying on real business payloads. It supports continuous validation and deployment checks.

Mark test messages clearly, route them safely, and ensure they do not pollute production analytics or downstream systems.

**Use when**  
Use when health or connectivity should be validated end to end.

**When not to use**  
- Avoid test traffic that mutates production master data or triggers irreversible side effects.
- Avoid tests that look like fraud or attack traffic to security monitoring.

**Typical scenarios**  
- Synthetic monitoring
- Deployment validation
- Operational checks

**Related patterns**  
**Control Bus (540)**, **Message History (551)**, **Channel Purger (572)**.

**Combinations**  

Synthetic traffic must be unambiguous on the wire, injected through controlled paths, and asserted via history or metrics-not by mutating production entities.

- **Clear markers** - Use dedicated headers or envelope flags (`test=true`) enforced by filters on business consumers.

:::mermaid
flowchart LR
    subgraph R0S0["Clear markers"]
        direction TB
        r0n0["Use dedicated headers or envelope flags (`test=true`) enforced by filters on business consumers."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Control Bus (540)** - Trigger or schedule probes through the same secured control plane as other operational commands.

:::mermaid
flowchart LR
    subgraph R1S0["Control Bus"]
        direction TB
        r1n0["Trigger or schedule probes through the same secured control plane as other operational commands."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Message History (551)** / observability - Assert expected hops, latency, and success counters; fail the probe if traces do not appear.

:::mermaid
flowchart LR
    subgraph R2S0["Message History"]
        direction TB
        r2n0["Assert expected hops, latency, and success counters; fail the probe if traces do not appear."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- test messages consumed by business processors as real work.
- no cleanup of test artefacts.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Test message"] --> B["Integration path"] --> C["Expected observation / result"]
:::

**Sequence** - message exchange over time.

:::mermaid
sequenceDiagram
    participant Mon as Monitor
    participant P as Integration path
    participant Obs as Observability
    Mon->>P: Synthetic message (tagged)
    P-->>Obs: Metrics / trace
    Obs-->>Mon: Pass / fail
:::

## 2.65. Channel Purger (572)
**Category**  
System management

**Overview**  
A utility removes unwanted or stale messages from a channel.

**Description**  
**Channel Purger (572)** removes messages from a channel deliberately-after tests, during environment reset, or to clear a poison backlog under controlled conditions.

It is powerful and dangerous: require authorisation, logging, and clear scope (environment, channel, correlation) so production data is never purged accidentally.

**Use when**  
Use when a channel must be cleaned in a controlled manner.

**When not to use**  
- Never use ad hoc purge against production without change control, backup, and scope limits.
- Do not purge when messages are the only evidence for compliance without archival.

**Typical scenarios**  
- Test cleanup
- Stale backlog removal
- Environment reset

**Related patterns**  
**Test Message (569)**, **Message Store (555)**, **Dead Letter Channel (119)**.

**Combinations**  

Purges are destructive: require dual control, export evidence first, and log exact counts and correlation scopes.

- **Dual approval** - Tie purge jobs to change tickets with two-person rule for non-production shared environments and stricter rules for production.

:::mermaid
flowchart LR
    subgraph R0S0["Dual approval"]
        direction TB
        r0n0["Tie purge jobs to change tickets with two-person rule for non-production shared environments and ..."]
    end
    IN[In] --> r0n0
    r0n0 --> OUT[Out]
:::

- **Message Store (555)** **export** - Archive messages or metadata to durable storage before deletion when compliance or triage may need them.

:::mermaid
flowchart LR
    subgraph R1S0["Message Store"]
        direction TB
        r1n0["Archive messages or metadata to durable storage before deletion when compliance or triage may nee..."]
    end
    IN[In] --> r1n0
    r1n0 --> OUT[Out]
:::

- **Telemetry** - Emit before/after depth metrics and purge volume to auditing systems.

:::mermaid
flowchart LR
    subgraph R2S0["Telemetry"]
        direction TB
        r2n0["Emit before/after depth metrics and purge volume to auditing systems."]
    end
    IN[In] --> r2n0
    r2n0 --> OUT[Out]
:::

**Anti-patterns**  
- wildcard purge in shared environment.
- purging DLQ without triage, losing diagnostic data.

---

**Diagrams**

**Primary topology.**

--------------------

:::mermaid
flowchart LR
    A["Channel backlog"] --> B["Channel purger"] --> C["Selected messages removed"]
:::

**Layered or staged view.**

:::mermaid
flowchart TD
    P[Purge request] --> V{Authorised + scoped?}
    V -->|no| X[Reject / audit alert]
    V -->|yes| E[Export optional archive]
    E --> R[Remove messages]
    R --> L[Log counts + ticket ref]
:::

## 3. Cross-cutting combinations and anti-patterns

This section complements the per-pattern **Combinations** and **Anti-patterns** sections in §2.

**Common compositions**

- : **Guaranteed Delivery (122)** + **Dead Letter Channel (119)** + **Idempotent Receiver (528)** + observability keyed by **Correlation Identifier (163)**.
- : **Request-Reply (154)** + **Return Address (159)** + **Correlation Identifier (163)** + **Dead Letter Channel (119)** (or **Message Expiration (176)**) for abandoned requests.
- : **Publish-Subscribe Channel (106)** or **Recipient List (249)** (for targeted broadcast) + **Aggregator (268)** + **Scatter-Gather (297)** timeouts and partial-result policy.
- : **Format Indicator (180)** + **Message Translator (85)** + **Invalid Message Channel (115)** for unknown versions.
- : **Claim Check (346)** + **Content Filter (342)** (if needed) + secured blob store.
- : **Process Manager (312)** + **Message Store (555)** + compensating actions; avoid hiding state only in memory.

**Recurring anti-patterns**

- : unrelated message types on one channel with no **Datatype Channel (111)** or **Message Dispatcher (508)** discipline.
- : synchronous **Remote Procedure Invocation (50)** chains masquerading as microservices, with no bulkheads.
- : brokers deployed without DLQ, metrics, or replay runbooks.
- : assuming no duplicates without **Idempotent Receiver (528)** or outbox.
- : **Wire Tap (547)** or **Message History (551)** logging sensitive payload fields.
- : **Detour (545)** or **Wire Tap (547)** left enabled after go-live, doubling cost and risk.

**Diagram vocabulary**

- Use **flowchart** for topology (channels, routers, stores).
- Use **sequenceDiagram** for request-reply, scatter-gather, transactional commit, and tap semantics.
- Use **stateDiagram** sparingly for **Process Manager (312)** lifecycles.

## 4. Future expansion

This document may later add:

- relationship notes between patterns and specific status or error code catalogues
- worked examples mapped to DFES integration references (for example INT00x)
- deeper product-specific notes (for example Azure Service Bus, Kafka) where standards require it

---

## Appendix A. Renderer parity fixtures (Mermaid → Naiad → DOCX)

These diagrams exist purely to lock in renderer behaviours that are easy to regress.

### A.1. Flowchart rounded curve (`flowchart.curve = "rounded"`)

:::mermaid
%%{init: {"flowchart": {"curve": "rounded"}}}%%
flowchart LR
    A[Start] --> B[Step 1] --> C[Done]
    A --> D[Side path] --> C
:::

### A.2. Flowchart neo look markers + dash shaping (`look = "neo"`)

:::mermaid
%%{init: {"look": "neo"}}%%
flowchart LR
    A[Alpha] -.-> B[Bravo]
    B --> C[Charlie]
    C --> D[Delta]
    A -->|label| D
:::

### A.3. Theme variables override: distinguish `lineColor` vs `arrowheadColor`

:::mermaid
%%{init: {"themeVariables": {"lineColor": "#ff0000", "arrowheadColor": "#0000ff"}}}%%
flowchart LR
    A[Red line] --> B[Blue arrowhead]
:::

### A.3.1. Flowchart symmetric fork columns (varying widths + labels)

Paste this block into Mermaid Live Editor unchanged and compare with Naiad output. It stresses:
- asymmetric node widths (due to label length)
- two-way fan-out with one long route
- labelled edge into a shared sink
- symmetry expectation: two “columns” with vertical early and late segments

:::mermaid
flowchart TD
    S[Sender] --> Q[Channel / broker]
    Q --> C["Consumer (short)"]
    Q --> DL["Dead letter / quarantine (very long label to force a wide node)"]
    C -->|failure after retry| DL
:::

### A.3.2. Flowchart fork/join stress (1→2, 2→1, 1→3, 4→1, multi-level)

Paste this block into Mermaid Live Editor unchanged and compare with Naiad output. It stresses:
- multiple vertical paths and levels
- fan-out (1→2, 1→3) and fan-in (2→1, 4→1)
- varied node widths and multi-line labels
- labelled edges on both fan-out and fan-in

:::mermaid
flowchart TD 
    %% Level 0 (core hub + sink) 
    S[Sender] --> Q[Channel / broker] 
    Q --> C["Consumer (short)"] 
    Q --> DL["Dead letter / quarantine (very long label to force a wide node)"] 
    C -->|failure after retry| DL


    %% Level 1: 1→3 fan-out from the hub (expect symmetric columns)
    Q -->|A| A1["Path A (short)"]
    Q -->|B| B1["Path B (a bit longer label)"]
    Q -->|C| C1["Path C (very long label to force a wide node on this branch)"]

    %% Level 2: 1→2 fan-out on one branch
    B1 -->|B-left| B2L["B2 left"]
    B1 -->|B-right| B2R["B2 right (longer)"]
    
    %% Level 3: 4→1 fan-in (join) into the shared sink
    A1 -->|join| DL
    B2L -->|join| DL
    B2R -->|join| DL
    C1 -->|join| DL
    
    %% Level 4: 1→4 fan-out from the shared sink (stress 4-way columns)
    DL -->|D1| D1["Downstream 1"]
    DL -->|D2| D2["Downstream 2 (long label)"]
    DL -->|D3| D3["Downstream 3"]
    DL -->|D4| D4["Downstream 4"]
    
    %% Level 5: 4→1 fan-in (final join)
    D1 -->|done| OUT["Egress / sink (wide label)\n(two lines)"]
    D2 -->|done| OUT
    D3 -->|done| OUT
    D4 -->|done| OUT
:::

### A.4. Gantt theme variables override (task + grid colours)

:::mermaid
%%{init: {"themeVariables": {"taskBorderColor": "#005a9c", "taskBkgColor": "#b3d9ff", "gridColor": "#00aa00"}}}%%
gantt
    title Themed Gantt
    dateFormat  YYYY-MM-DD
    section Demo
    Task A :a1, 2026-04-01, 3d
:::

### A.5. Sequence theme variables override (arrowhead colour)

:::mermaid
%%{init: {"themeVariables": {"arrowheadColor": "#ff00ff"}}}%%
sequenceDiagram
    participant A as Alpha
    participant B as Bravo
    A->>B: Message
:::

