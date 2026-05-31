namespace MermaidRecognition.Tests.Er;

/// <summary>
/// Regenerates ER diagram verified JSON fixtures (§11.1.14.5).
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
            "relation-cardinality",
            """
            erDiagram
                A ||--|| B : "one to one"
                C |o--o| D : "zero or one"
                E ||..o{ F : "non identifying"
                G ||--|{ H : "one or more"
                I }o--|| J : "zero or more left"
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

        KindFixtureFiles.Write(
            BaseDir,
            "direction",
            """
            erDiagram
                direction LR
                A ||--|| B : links
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "attribute-keys",
            """
            erDiagram
                CUSTOMER {
                    int id PK
                    string email UK
                }
                ORDER {
                    int id PK
                    int customer_id FK
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "classDef-style",
            """
            erDiagram
                CUSTOMER ||--o{ ORDER : places
                classDef highlight fill:#f9f
                class CUSTOMER highlight
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "inline-style",
            """
            erDiagram
                CUSTOMER {
                    string name
                }
                style CUSTOMER fill:#bbf
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "comment",
            """
            erDiagram
                %% Domain: sales
                CUSTOMER ||--o{ ORDER : places
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accessibility",
            """
            erDiagram
                accTitle: Sales schema
                accDescr: Customer and order entities
                CUSTOMER ||--o{ ORDER : places
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "one-to-one",
            """
            erDiagram
                PERSON ||--|| PASSPORT : has
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "non-identifying",
            """
            erDiagram
                CUSTOMER ||..o{ ORDER : places
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "associative-entity",
            """
            erDiagram
                STUDENT }o--o{ ENROLLMENT : has
                COURSE ||--o{ ENROLLMENT : includes
                STUDENT {
                    int id PK
                    string name
                }
                COURSE {
                    int id PK
                    string title
                }
                ENROLLMENT {
                    int student_id FK
                    int course_id FK
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "quoted-relationship-label",
            """
            erDiagram
                USER ||--o{ SESSION : "logs in"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "multiple-relationships",
            """
            erDiagram
                CUSTOMER ||--o{ ORDER : places
                ORDER ||--|{ LINE-ITEM : contains
                PRODUCT ||--o{ LINE-ITEM : includes
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "attribute-comments",
            """
            erDiagram
                CUSTOMER {
                    int id PK "Primary key"
                    string name "Customer name"
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "entity-only",
            """
            erDiagram
                CUSTOMER {
                    string name
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accDescr-block",
            """
            erDiagram
                accTitle: Sales schema
                accDescr {
                    Entity relationship model
                    for the sales domain
                }
                CUSTOMER ||--o{ ORDER : places
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "combined-schema",
            """
            erDiagram
                CUSTOMER {
                    int customer_id PK
                    string email UK
                }
                ORDER {
                    int order_id PK
                    int customer_id FK
                    date order_date
                }
                CUSTOMER ||--o{ ORDER : places
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "recursive-relationship",
            """
            erDiagram
                NODE ||--o{ NODE : "leads to"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "duplicate-role-labels",
            """
            erDiagram
                CAR ||--o{ PERSON : "insured for"
                CAR ||--o{ PERSON : "owns"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "base"}}%%
            erDiagram
                A ||--|| B : relates
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "hyphenated-entities",
            """
            erDiagram
                LINE-ITEM ||--o{ WAREHOUSE-STOCK : stores
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "class-apply-only",
            """
            erDiagram
                CUSTOMER {
                    string name
                }
                class CUSTOMER important
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relationship-before-entity",
            """
            erDiagram
                CAR ||--o{ DRIVER : "insured for"
                CAR {
                    string model
                }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "catalogue-remarked-entity-alias",
            """
            %% erDiagram
            %%     CUSTOMER [Customer Account] {
            %%         string name
            %%     }
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "complex-smoke",
            """
            erDiagram
                CUSTOMER {
                    int customer_id PK "Primary key"
                    string first_name "Customer first name"
                    string last_name "Customer last name"
                    string email UK "Unique email address"
                    date date_of_birth
                    string phone
                    boolean is_active
                }
                ADDRESS {
                    int address_id PK
                    int customer_id FK
                    string street
                    string city
                    string state
                    string postal_code
                    string country
                    string address_type "billing or shipping"
                }
                ORDER {
                    int order_id PK
                    int customer_id FK
                    int shipping_address_id FK
                    int billing_address_id FK
                    datetime order_date
                    datetime shipped_date
                    string status
                    decimal total_amount
                }
                ORDER_ITEM {
                    int item_id PK
                    int order_id FK
                    int product_id FK
                    int quantity
                    decimal unit_price
                    decimal discount
                }
                PRODUCT {
                    int product_id PK
                    int category_id FK
                    string name
                    string description
                    decimal price
                    int stock_quantity
                    string sku UK
                }
                CATEGORY {
                    int category_id PK
                    int parent_id FK "Self-referencing"
                    string name
                    string description
                }
                CUSTOMER ||--o{ ORDER : places
                CUSTOMER ||--o{ ADDRESS : has
                ORDER ||--|{ ORDER_ITEM : contains
                ORDER }o--|| ADDRESS : "ships to"
                ORDER }o--|| ADDRESS : "bills to"
                PRODUCT ||--o{ ORDER_ITEM : "included in"
                CATEGORY ||--o{ PRODUCT : categorizes
                CATEGORY |o--o| CATEGORY : "parent of"
            """);
    }
}
