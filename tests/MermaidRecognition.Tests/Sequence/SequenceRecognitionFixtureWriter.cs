namespace MermaidRecognition.Tests.Sequence;

/// <summary>
/// Regenerates sequence diagram verified JSON fixtures (§11.1.14.2).
/// </summary>
[TestFixture]
public sealed class SequenceRecognitionFixtureWriter
{
    [Test]
    [Explicit("Regenerates baseline fixtures; select this test explicitly to update them.")]
    public void Write_All_Sequence_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetSequenceFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            sequenceDiagram
                Alice->>Bob: Hello Bob
                Bob-->>Alice: Hi Alice
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "participants",
            """
            sequenceDiagram
                participant A as Alice
                participant B as Bob
                A->>B: Hello
                B->>A: Hi
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "activation",
            """
            sequenceDiagram
                Alice->>+Bob: Hello
                Bob-->>-Alice: Hi
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "notes",
            """
            sequenceDiagram
                Alice->>Bob: Hello
                Note right of Bob: Bob thinks
                Bob-->>Alice: Hi
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "autonumber",
            """
            sequenceDiagram
                autonumber
                Alice->>Bob: Hello
                Bob->>Alice: Hi
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "title",
            """
            sequenceDiagram
                title Authentication Flow
                Client->>Server: Login
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "alt-else-blocks",
            """
            sequenceDiagram
                actor User
                participant API
                User->>API: Request
                alt Data found
                    API-->>User: OK
                else Not found
                    API-->>User: 404
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "activate-deactivate",
            """
            sequenceDiagram
                activate A
                deactivate A
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "loop-control",
            """
            sequenceDiagram
                loop Every minute
                    A->>B: Ping
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "sequence-accessibility",
            """
            sequenceDiagram
                accTitle: Auth
                accDescr: Login flow
                Client->>Server: Login
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "par-block",
            """
            sequenceDiagram
                par Action A
                    Alice->>Bob: A1
                and Action B
                    Bob->>Alice: B1
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "par-over",
            """
            sequenceDiagram
            par_over Parallel overlap
            Alice ->> Bob: Message
            Note left of Alice: Alice note
            Note right of Bob: Bob note
            end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "opt-block",
            """
            sequenceDiagram
                opt Optional step
                    Alice->>Bob: Maybe
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "critical-block",
            """
            sequenceDiagram
                critical Establish connection
                    Alice->>Bob: Connect
                option Timeout
                    Bob-->>Alice: Retry
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "box-rgb",
            """
            sequenceDiagram
                box rgb(200,200,200) Internal
                    participant A
                    participant B
                end
                A->>B: Hi
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "lifecycle-create",
            """
            sequenceDiagram
                participant A
                create participant B
                A->>B: Hello
                destroy B
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "implicit-participants",
            """
            sequenceDiagram
                Client->>Server: Request
                Server-->>Client: Response
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "self-message",
            """
            sequenceDiagram
                participant A
                A->>A: Reflect
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "arrow-variants",
            """
            sequenceDiagram
                A->>B: Solid
                A-->>B: Dotted
                A->C: Solid open
                A-->C: Dotted open
                A-xD: Solid cross
                A--xD: Dotted cross
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accDescr-block",
            """
            sequenceDiagram
                accTitle: Auth
                accDescr {
                    Describes login
                    and session flow
                }
                Client->>Server: Login
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "actor-participant",
            """
            sequenceDiagram
                actor User
                participant Server
                User->>Server: Request
                Server-->>User: Response
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "participant-order",
            """
            sequenceDiagram
                participant API
                actor User
                User->>API: Call
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "multi-hop",
            """
            sequenceDiagram
                A->>B: Forward
                B->>C: Relay
                C-->>A: Reply
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "comment",
            """
            sequenceDiagram
                %% This is a comment
                A->>B: Visible message
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "base"}}%%
            sequenceDiagram
                A->>B: Themed message
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "break-block",
            """
            sequenceDiagram
                A->>B: Start
                break On error
                    B-->>A: Abort
                end
                A->>B: Continue
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "rect-block",
            """
            sequenceDiagram
                rect rgb(240, 240, 240)
                    A->>B: Inside rect
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "closed-arrows",
            """
            sequenceDiagram
                A->>B: Solid
                A-->>B: Dotted
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "nested-blocks",
            """
            sequenceDiagram
                loop Retry up to 3
                    A->>B: Attempt
                    alt Success
                        B-->>A: OK
                    else Failure
                        B-->>A: Retry
                    end
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "else-only",
            """
            sequenceDiagram
                A->>B: Check
                alt OK
                    B-->>A: Proceed
                end
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "long-message-label",
            """
            sequenceDiagram
                A->>B: This message label is intentionally long so wrapping and clearance from activation bars can be verified in Word and SVG output.
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "quoted-message-label",
            """
            sequenceDiagram
                A->>B: "Step 1: validate input"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "open-cross-arrows",
            """
            sequenceDiagram
                A->>B: Solid open
                A-->B: Dotted open
                A-xB: Solid cross
                A--xB: Dotted cross
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "auth-smoke",
            """
            sequenceDiagram
                title Complete Authentication Flow
                autonumber
                actor User
                participant Client as Web Client
                participant Auth as Auth Service
                participant DB as Database
                participant Email as Email Service
                User->>+Client: Enter credentials
                Client->>+Auth: POST /login
                Auth->>+DB: Query user
                DB-->>-Auth: User data
                Note right of Auth: Validate credentials
                Auth->>Auth: Generate JWT
                Note right of Auth: Token expires in 24h
                Auth-->>-Client: 200 OK + Token
                Client->>+Email: Send welcome email
                Email-->>-Client: Email sent
                Client-->>-User: Show dashboard
                Note over User,DB: Session established
            """);
    }
}
