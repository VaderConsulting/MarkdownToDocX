namespace MermaidRecognition.Tests.Er;

/// <summary>
/// Regenerates ER diagram verified JSON fixtures (§11.0.12.6).
/// </summary>
[TestFixture]
public sealed class ErRecognitionFixtureWriter
{
    [Test]
    public void Write_All_Er_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetErFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "relationship",
            """
            erDiagram
                CUSTOMER ||--o{ ORDER : places
                ORDER ||--|{ LINE-ITEM : contains
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "entity-attributes",
            """
            erDiagram
                CUSTOMER {
                    string name
                    string email PK
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-zero-or-one",
            """
            erDiagram
                EMPLOYEE |o--o| PARKING-SPACE : uses
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-zero-or-more-left",
            """
            erDiagram
                ORDER }o--|| ADDRESS : "ships to"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-zero-or-one-dotted",
            """
            erDiagram
                A |o..o{ B : has
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-zero-or-more-both",
            """
            erDiagram
                TEACHER }o--o{ STUDENT : teaches
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-one-or-more-dotted",
            """
            erDiagram
                A }|..o{ B : has
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-zero-or-more-dotted",
            """
            erDiagram
                A }o..o{ B : links
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-optional-zero-or-more",
            """
            erDiagram
                PARENT o|--o{ CHILD : has
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "cardinality-zero-or-more-zero-or-one-dotted",
            """
            erDiagram
                A }o..|| B : optional
            """);
    }
}
