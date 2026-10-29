using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

[TestFixture]
public sealed class EnterpriseRenameTests
{
    private const string Script = "enterprise\n  title order flow\n  classDef order fill:#eef\n"
        + "  group shop \"Shop\" {\n    application-component order \"order\" order2 :::order\n  }\n"
        + "  application-component order2 \"Order 2\" right-of order +1\n"
        + "  application-component order3 \"after order\" in shop\n"
        + "  data-object ledger \"Ledger\" NW order +1 +2   %% order is the anchor\n"
        + "  note memo \"order notes\" below order\n"
        + "  order -serving-> order2 \"order\"\n  order2 -access(read)-> ledger\n  memo --- order\n"
        + "  class order,ledger order\n  style order fill:#fee\n";

    [Test]
    public void RenamesEveryReferenceByRoleAndNothingElse()
    {
        string Fixed = Script.Replace("application-component order \"order\" order2 :::order", "application-component order \"order\" :::order");
        var Result = EnterpriseRename.Rename(Fixed, "order", "purchase");
        Assert.That(Result.Failure, Is.Null);
        string Updated = Result.Updated!;
        Assert.That(Updated, Does.Contain("application-component purchase \"order\" :::order"));
        Assert.That(Updated, Does.Contain("order2 \"Order 2\" right-of purchase +1"));
        Assert.That(Updated, Does.Contain("NW purchase +1 +2   %% order is the anchor"));
        Assert.That(Updated, Does.Contain("note memo \"order notes\" below purchase"));
        Assert.That(Updated, Does.Contain("purchase -serving-> order2 \"order\""));
        Assert.That(Updated, Does.Contain("memo --- purchase"));
        Assert.That(Updated, Does.Contain("class purchase,ledger order"));
        Assert.That(Updated, Does.Contain("style purchase fill:#fee"));
        Assert.That(Updated, Does.Contain("title order flow").And.Contain("classDef order fill:#eef"));
        Assert.That(Updated, Does.Contain("order3 \"after order\" in shop"));
        Assert.That(Updated, Does.Contain("application-component order2").And.Contain("-> order2"));
    }

    [Test]
    public void RenamesAParentGroupAndItsMembersReference()
    {
        var Result = EnterpriseRename.Rename(Script.Replace("application-component order \"order\" order2 :::order", "application-component order \"order\""), "shop", "store");
        Assert.That(Result.Failure, Is.Null);
        Assert.That(Result.Updated, Does.Contain("group store \"Shop\" {").And.Contain("order3 \"after order\" in store"));
    }

    [TestCase("ledger", "Already used by another object")]
    [TestCase("group", "keyword")]
    [TestCase("goal", "keyword")]
    [TestCase("1 bad", "must start")]
    [TestCase("order", "same as the old id")]
    public void RefusesUnsafeNewIds(string NewId, string Reason)
    {
        var Result = EnterpriseRename.Rename(Script.Replace(" order2 :::order", " :::order"), "order", NewId);
        Assert.That(Result.Updated, Is.Null);
        Assert.That(Result.Failure, Does.Contain(Reason).IgnoreCase.Or.Contain("already used"));
    }

    [Test]
    public void RefusesWhatIsNotAnObject()
    {
        Assert.That(EnterpriseRename.Rename(Script, "emphasis", "x").Updated, Is.Null);
        Assert.That(EnterpriseRename.Rename(Script, "Shop", "x").Updated, Is.Null);
        Assert.That(EnterpriseRename.IsRenameable(Script, "ledger"), Is.True);
        Assert.That(EnterpriseRename.IsRenameable(Script, "flow"), Is.False);
    }

    [Test]
    public void PreservesLineEndingsAndRenamedScriptDescribesTheSameDiagram()
    {
        string Crlf = "enterprise\r\n  application-component a \"A\"\r\n  application-service b \"B\" below a +1\r\n  a -serving-> b\r\n";
        var Result = EnterpriseRename.Rename(Crlf, "a", "alpha");
        Assert.That(Result.Updated, Is.EqualTo("enterprise\r\n  application-component alpha \"A\"\r\n  application-service b \"B\" below alpha +1\r\n  alpha -serving-> b\r\n"));
        Assert.That(Result.References, Is.EqualTo(3));
    }
}
