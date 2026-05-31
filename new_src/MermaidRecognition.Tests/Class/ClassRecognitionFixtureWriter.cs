namespace MermaidRecognition.Tests.Class;

/// <summary>
/// Regenerates class diagram verified JSON fixtures (§11.1.14.3).
/// </summary>
[TestFixture]
public sealed class ClassRecognitionFixtureWriter
{
    [Test]
    public void Write_All_Class_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetClassFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            classDiagram
                class Animal
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "members",
            """
            classDiagram
                class Animal {
                    +String name
                    +int age
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "multi-level-inheritance",
            """
            classDiagram
                Animal <|-- Duck
                Duck <|-- Mallard
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "classDef-style",
            """
            classDiagram
                class Animal
                classDef highlight fill:#f9f,stroke:#333
                class Animal highlight
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-composition",
            """
            classDiagram
                Car *-- Engine
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-aggregation",
            """
            classDiagram
                Library o-- Book
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-association",
            """
            classDiagram
                Student --> Course : enrolls
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-dependency",
            """
            classDiagram
                Client ..> Service
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-realization",
            """
            classDiagram
                Interface <|.. Implementation
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-reverse-association",
            """
            classDiagram
                Server <-- Client
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-solid-link",
            """
            classDiagram
                A -- B
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-dotted-link",
            """
            classDiagram
                A .. B
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "multiple-classes",
            """
            classDiagram
                class Animal
                class Plant
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "inline-style",
            """
            classDiagram
                class Animal
                style Animal fill:#bbf,stroke:#333
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "direction",
            """
            classDiagram
                direction LR
                Animal <|-- Dog
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "namespace",
            """
            classDiagram
                namespace animals {
                    class Animal
                    class Dog
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accessibility",
            """
            classDiagram
                accTitle: Domain model
                accDescr {
                    Describes core
                    domain entities
                }
                class Animal
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "note",
            """
            classDiagram
                class Animal
                note for Animal "Base type"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "click-href",
            """
            classDiagram
                class Animal
                click Animal href "https://example.com" "Docs"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "interface-annotation",
            """
            classDiagram
                class IFlyable {
                    <<interface>>
                    +fly() void
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "abstract-stereotype",
            """
            classDiagram
                class Shape {
                    <<abstract>>
                    +area() double
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "enum-service-stereotype",
            """
            classDiagram
                class Status {
                    <<enumeration>>
                    ACTIVE
                    INACTIVE
                }
                class UserService {
                    <<service>>
                    +findUser(id: int) User
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relation-cardinality",
            """
            classDiagram
                User "1" --> "1..*" Address : has
                User "1" o-- "*" Order : places
                Order "1" *-- "1..*" Item : contains
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "base"}}%%
            classDiagram
                class Animal
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "header-v2",
            """
            classDiagram-v2
                class Animal
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "generic-class",
            """
            classDiagram
                class Repository~T~ {
                    +get(id: int) T
                    +save(entity: T) void
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "methods-only",
            """
            classDiagram
                class Animal {
                    +makeSound()
                    +move() void
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "members-and-methods",
            """
            classDiagram
                class Animal {
                    +String name
                    +int age
                    +makeSound() void
                    +move(int distance) void
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "visibility-modifiers",
            """
            classDiagram
                class Example {
                    +publicField
                    -privateField
                    #protectedField
                    ~packageField
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "inheritance",
            """
            classDiagram
                Animal <|-- Dog
                Animal <|-- Cat
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accessibility-inline",
            """
            classDiagram
                accTitle: Domain model
                accDescr: Core entities
                class Animal
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "comment",
            """
            classDiagram
                %% domain entity
                class Animal
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "static-members",
            """
            classDiagram
                class Util {
                    +hash(input: String)$ String
                    +validate()$ bool
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "complex-smoke",
            """
            classDiagram
                class IRepository~T~ {
                    <<interface>>
                    +get(id: int) T
                    +save(entity: T) void
                    +delete(id: int) void
                }
                class UserRepository {
                    +get(id: int) User
                }
                class AbstractEntity {
                    <<abstract>>
                    #int id
                    +getId() int
                }
                class UserService {
                    <<service>>
                    -UserRepository repository
                    +createUser(name: String) User
                }
                class Status {
                    <<enumeration>>
                    ACTIVE
                    INACTIVE
                }
                class User {
                    +String name
                    +validate()$ bool
                }
                class Address {
                    +String city
                }
                class Order {
                    +int orderId
                }
                class Item {
                    +String name
                }
                IRepository~T~ <|.. UserRepository : implements
                AbstractEntity <|-- User : extends
                UserService ..> UserRepository : uses
                User "1" --> "1..*" Address : has
                User "1" o-- "*" Order : places
                Order "1" *-- "1..*" Item : contains
            """);
    }
}
