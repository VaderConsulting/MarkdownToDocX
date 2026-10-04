namespace MermaidRecognition.Tests.State;

/// <summary>
/// Regenerates state diagram verified JSON fixtures (§11.1.14.4).
/// </summary>
[TestFixture]
public sealed class StateRecognitionFixtureWriter
{
    [Test]
    [Explicit("Regenerates baseline fixtures; select this test explicitly to update them.")]
    public void Write_All_State_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetStateFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            stateDiagram-v2
                [*] --> Still
                Still --> Moving
                Moving --> Still
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "composite",
            """
            stateDiagram-v2
                [*] --> First
                state First {
                    [*] --> Second
                    Second --> [*]
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "note",
            """
            stateDiagram-v2
                State1 --> State2
                note right of State2 : Important
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "fork-join",
            """
            stateDiagram-v2
                state fork_state <<fork>>
                state join_state <<join>>
                [*] --> fork_state
                fork_state --> State2
                fork_state --> State3
                State2 --> join_state
                State3 --> join_state
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "choice",
            """
            stateDiagram-v2
                state choice_state <<choice>>
                [*] --> IsPositive
                IsPositive --> choice_state
                choice_state --> Positive : if n > 0
                choice_state --> Negative : if n < 0
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "direction",
            """
            stateDiagram-v2
                direction LR
                [*] --> Still
                Still --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "transition-labels",
            """
            stateDiagram-v2
                [*] --> Active
                Active --> Inactive : timeout
                Inactive --> Active : reset
                Active --> [*] : shutdown
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "transition-colons",
            """
            stateDiagram-v2
                locked --> pending : recoverable error (ex: timeout)
                A --> B : info: key: value
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "classDef-style",
            """
            stateDiagram-v2
                [*] --> Active
                classDef highlight fill:#f9f,stroke:#333
                class Active highlight
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accessibility",
            """
            stateDiagram-v2
                accTitle: Process lifecycle
                accDescr: Core state transitions
                [*] --> Idle
                Idle --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "click-href",
            """
            stateDiagram-v2
                [*] --> State1
                click State1 href "https://example.com" "Docs"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "base"}}%%
            stateDiagram-v2
                [*] --> Active
                Active --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "state-description",
            """
            stateDiagram-v2
                state "Processing" as P
                [*] --> P
                P --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "inline-style",
            """
            stateDiagram-v2
                [*] --> State1
                State1 --> State2
                style State2 fill:#bbf,stroke:#333
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "struct-open",
            """
            stateDiagram-v2
                [*] --> Outer
                state Outer
                {
                    [*] --> Inner
                    Inner --> [*]
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "note-left",
            """
            stateDiagram-v2
                [*] --> Error
                Error --> [*]
                note left of Error : Error handling
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accDescr-block",
            """
            stateDiagram-v2
                accTitle: Process lifecycle
                accDescr {
                    Describes core
                    state transitions
                }
                [*] --> Idle
                Idle --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "comment",
            """
            stateDiagram-v2
                %% lifecycle diagram
                [*] --> Active
                Active --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "multiple-states",
            """
            stateDiagram-v2
                [*] --> Still
                Still --> Moving
                Moving --> Still
                Moving --> Crash
                Crash --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "header-v1",
            """
            stateDiagram
                [*] --> Still
                Still --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "multiple-terminators",
            """
            stateDiagram-v2
                [*] --> Active
                Active --> Done
                Active --> Failed
                Done --> [*]
                Failed --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "hide-empty-description",
            """
            stateDiagram-v2
                hide empty description
                [*] --> Active
                Active --> [*]
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "complex-smoke",
            """
            stateDiagram-v2
                [*] --> Idle
                state "Processing State" as Processing
                state fork_state <<fork>>
                state join_state <<join>>
                state choice_state <<choice>>
                Idle --> Processing : start
                Processing --> fork_state
                fork_state --> TaskA
                fork_state --> TaskB
                TaskA --> join_state
                TaskB --> join_state
                join_state --> choice_state
                choice_state --> Success : if valid
                choice_state --> Error : if invalid
                Success --> Idle : reset
                Error --> Idle : retry
                Success --> [*] : complete
                note right of Processing : This is a processing note
                note left of Error : Error handling
            """);
    }
}
