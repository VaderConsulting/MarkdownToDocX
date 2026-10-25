using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Completion;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// Drives the real editor popup for each IntelliSense feature.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class EditorIntelliSenseTests
{
    private Form _Host = null!;

    private TestEditor _Editor = null!;

    private EditorIntelliSenseController _Controller = null!;

    [SetUp]
    public void SetUp()
    {
        _Host = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            ClientSize = new Size(900, 700)
        };
        _Editor = new TestEditor
        {
            Dock = DockStyle.Fill
        };
        _Host.Controls.Add(_Editor);
        _Controller = new EditorIntelliSenseController(_Editor);
        _Controller.Attach();
        _Host.Show();
        _Editor.Focus();
        Application.DoEvents();
    }

    [TearDown]
    public void TearDown()
    {
        _Controller.Dispose();
        _Host.Dispose();
        Application.DoEvents();
    }

    [Test]
    public void TypingFiltersTheListAndKeepsTheEditorFocused()
    {
        SetSource("enterprise\r\n  ");
        Type("a");

        IReadOnlyList<string> First = _Controller.Popup.MemberLabels;
        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(_Editor.Focused, Is.True);
        Assert.That(_Controller.Popup.ContainsFocus, Is.False);
        Assert.That(First.Count, Is.GreaterThan(1));
        Assert.That(_Controller.Popup.DetailText, Is.Not.Empty);
        Assert.That(_Controller.Popup.ShowsScrollBar, Is.False);
        AssertEachStartsWith(First, "a");

        Type("p");

        IReadOnlyList<string> Second = _Controller.Popup.MemberLabels;
        Assert.That(_Editor.Focused, Is.True);
        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(string.Join("|", Second), Is.Not.EqualTo(string.Join("|", First)));
        Assert.That(Second.Count, Is.LessThan(First.Count));
        AssertEachStartsWith(Second, "ap");
        Assert.That(_Controller.Popup.ShowsScrollBar, Is.False);
    }

    [Test]
    public void PopupGrowsWithItsText()
    {
        SetSource("enterprise\r\n  direction T");
        Type("B");
        _Editor.RaiseKeyDown(new KeyEventArgs(Keys.Control | Keys.J));
        Application.DoEvents();
        int ShortHeight = _Controller.Popup.WindowSize.Height;

        SetSource("enterprise\r\n  ");
        Type("a");
        int TallHeight = _Controller.Popup.WindowSize.Height;

        Assert.That(ShortHeight, Is.GreaterThan(0));
        Assert.That(TallHeight, Is.GreaterThan(ShortHeight));
        Assert.That(_Controller.Popup.ShowsScrollBar, Is.False);
    }

    [Test]
    public void CompletedWordClosesTheListUntilForced()
    {
        SetSource("enterprise\r\n  direction T");
        Type("B");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Parameter));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("direction"));

        _Editor.RaiseKeyDown(new KeyEventArgs(Keys.Control | Keys.J));
        Application.DoEvents();

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
    }

    [Test]
    public void EscapeClosesThePopup()
    {
        SetSource("enterprise\r\n  ");
        Type("a");
        Assert.That(_Controller.Popup.IsVisible, Is.True);

        _Editor.RaiseKeyDown(new KeyEventArgs(Keys.Escape));
        Application.DoEvents();

        Assert.That(_Controller.Popup.IsVisible, Is.False);
    }

    [Test]
    public void ClickMovesTheCaretAndClosesThePopup()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n  ");
        Type("a");
        Assert.That(_Controller.Popup.IsVisible, Is.True);

        _Editor.SelectionStart = _Editor.Text.IndexOf("webShop", StringComparison.Ordinal);
        _Editor.SelectionLength = 0;
        _Editor.RaiseMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0));
        Application.DoEvents();

        Assert.That(_Editor.SelectionLength, Is.EqualTo(0));
        Assert.That(_Controller.Popup.IsVisible, Is.False);
    }

    [Test]
    public void SelectingOneWordShowsQuickInfo()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n");
        int Start = _Editor.Text.IndexOf("webShop", StringComparison.Ordinal);
        _Editor.Select(Start, "webShop".Length);
        Application.DoEvents();

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.QuickInfo));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("webShop"));
        Assert.That(_Controller.Popup.ShowsScrollBar, Is.False);
    }

    [Test]
    public void QuickInfoForANoteShowsEachPartOnItsOwnLine()
    {
        SetSource("enterprise\r\n  note margin \"A note beside the order\"\r\n");
        int Start = _Editor.Text.IndexOf("margin", StringComparison.Ordinal);
        _Editor.Select(Start, "margin".Length);
        Application.DoEvents();

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.QuickInfo));
        string[] Lines = _Controller.Popup.DetailText.Split("\r\n");
        Assert.That(Lines, Does.Contain("note margin"));
        Assert.That(Lines, Does.Contain("A note beside the order"));
        Assert.That(_Controller.Popup.DetailText.Replace("\r\n", string.Empty, StringComparison.Ordinal), Does.Not.Contain("\n"));
    }
    [Test]
    public void QualifierInsideParenthesesShowsParameterHelp()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n  data-object order \"Order\"\r\n  webShop -access(");

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        bool HasQualifierList = _Controller.Popup.Surface == AssistSurface.Members && _Controller.Popup.MemberLabels.Contains("read");
        bool HasParameter = _Controller.Popup.Surface == AssistSurface.Parameter && _Controller.Popup.DetailText.Length > 0;
        Assert.That(HasQualifierList || HasParameter, Is.True);
        if (_Controller.Popup.Surface == AssistSurface.Members)
        {
            Assert.That(_Controller.Popup.SignatureText, Is.Not.Empty);
        }
    }

    [Test]
    public void TabInsertsTheHighlightedSuggestion()
    {
        SetSource("enterprise\r\n  ");
        Type("ap");
        string Before = _Editor.Text;
        string Selected = _Controller.Popup.SelectedMemberLabel;

        _Editor.RaiseKeyDown(new KeyEventArgs(Keys.Tab));
        Application.DoEvents();

        Assert.That(Selected, Is.Not.Empty);
        Assert.That(_Editor.Text, Does.Contain(Selected));
        Assert.That(_Editor.Text, Is.Not.EqualTo(Before));
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Parameter));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("required identifier"));
    }

    [Test]
    public void UpOnAFinishedRelationshipLeavesTheCaretFree()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n  application-service orderApi \"Order API\"\r\n  data-object order \"Order\"\r\n  webShop -serving------> orderApi\r\n  orderApi -access(write)----> order");
        AssertArrowLeaves("-> order", 3);
        AssertArrowLeaves("-serving", 4);
        AssertArrowLeaves("-access(write)", 9);
    }

    private void AssertArrowLeaves(string Marker, int Offset)
    {
        int Index = _Editor.Text.IndexOf(Marker, StringComparison.Ordinal);
        Assert.That(Index, Is.GreaterThanOrEqualTo(0));
        _Editor.Select(Index + Offset, 0);
        Application.DoEvents();
        _Controller.Refresh(Force: false);
        Application.DoEvents();

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members), Marker);
        int Caret = _Editor.SelectionStart;
        KeyEventArgs Up = new KeyEventArgs(Keys.Up);
        _Editor.RaiseKeyDown(Up);
        Application.DoEvents();

        Assert.That(_Controller.OwnsVerticalArrows(), Is.False, Marker);
        Assert.That(Up.Handled, Is.False, Marker);
        Assert.That(_Editor.SelectionStart, Is.EqualTo(Caret), Marker);
    }

    [Test]
    public void UpOnADeclaredElementLeavesTheCaretFree()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n  application-service orderApi \"Order API\"\r\n");
        int Index = _Editor.Text.IndexOf("webShop", StringComparison.Ordinal);
        _Editor.Select(Index + 2, 0);
        Application.DoEvents();
        _Controller.Refresh(Force: false);
        Application.DoEvents();

        int Caret = _Editor.SelectionStart;
        KeyEventArgs Up = new KeyEventArgs(Keys.Up);
        _Editor.RaiseKeyDown(Up);
        Application.DoEvents();

        Assert.That(_Controller.OwnsVerticalArrows(), Is.False);
        Assert.That(Up.Handled, Is.False);
        Assert.That(_Editor.SelectionStart, Is.EqualTo(Caret));
    }

    [Test]
    public void UpAndDownChangeTheHighlightedRow()
    {
        SetSource("enterprise\r\n  ");
        Type("a");
        int Caret = _Editor.SelectionStart;
        string First = _Controller.Popup.SelectedMemberLabel;

        _Editor.RaiseKeyDown(new KeyEventArgs(Keys.Down));
        Application.DoEvents();

        Assert.That(_Controller.Popup.SelectedMemberLabel, Is.Not.EqualTo(First));
        Assert.That(_Editor.SelectionStart, Is.EqualTo(Caret));
    }

    [Test]
    public void HoverShowsQuickInfoUntilAListIsOpen()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n");
        _Controller.Hide();
        int Index = _Editor.Text.IndexOf("webShop", StringComparison.Ordinal);
        Point Point = _Editor.GetPositionFromCharIndex(Index);
        _Editor.RaiseMouseMove(new MouseEventArgs(MouseButtons.None, 0, Point.X, Point.Y, 0));

        Assert.That(_Controller.HoverPending, Is.True);
        _Controller.ShowPendingHover();
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.QuickInfo));

        _Editor.SelectionStart = _Editor.TextLength;
        Type("a");
        _Editor.RaiseMouseMove(new MouseEventArgs(MouseButtons.None, 0, Point.X + 8, Point.Y, 0));
        _Controller.ShowPendingHover();

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
    }

    [Test]
    public void HoverQuickInfoClosesWhenThePointerLeaves()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n");
        _Controller.Hide();
        int Index = _Editor.Text.IndexOf("webShop", StringComparison.Ordinal);
        Point Point = _Editor.GetPositionFromCharIndex(Index);
        _Editor.RaiseMouseMove(new MouseEventArgs(MouseButtons.None, 0, Point.X, Point.Y, 0));
        _Controller.ShowPendingHover();
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.QuickInfo));

        _Editor.RaiseMouseLeave();

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.None));
    }

    [Test]
    public void HoverQuickInfoFollowsTheWordUnderThePointer()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n");
        _Controller.Hide();
        int Shop = _Editor.Text.IndexOf("webShop", StringComparison.Ordinal);
        Point ShopPoint = _Editor.GetPositionFromCharIndex(Shop);
        _Editor.RaiseMouseMove(new MouseEventArgs(MouseButtons.None, 0, ShopPoint.X, ShopPoint.Y, 0));
        _Controller.ShowPendingHover();
        string First = _Controller.Popup.DetailText;

        int Type = _Editor.Text.IndexOf("application-component", StringComparison.Ordinal);
        Point TypePoint = _Editor.GetPositionFromCharIndex(Type);
        _Editor.RaiseMouseMove(new MouseEventArgs(MouseButtons.None, 0, TypePoint.X, TypePoint.Y, 0));

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.QuickInfo));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("application-component"));
        Assert.That(_Controller.Popup.DetailText, Is.Not.EqualTo(First));
    }

    [Test]
    public void MovingTheWindowMovesThePopup()
    {
        SetSource("enterprise\r\n  ");
        Type("a");
        Point Before = _Controller.Popup.WindowLocation;

        _Host.Location = new Point(_Host.Location.X + 48, _Host.Location.Y + 36);
        Application.DoEvents();

        Assert.That(_Controller.Popup.WindowLocation, Is.Not.EqualTo(Before));
    }

    [TestCase("flowchart TD\r\n  sub", "subgraph")]
    [TestCase("sequenceDiagram\r\n  par", "participant")]
    [TestCase("classDiagram\r\n  not", "note")]
    [TestCase("stateDiagram-v2\r\n  sta", "state")]
    [TestCase("erDiagram\r\n  P", "PK")]
    [TestCase("journey\r\n  sec", "section")]
    [TestCase("gantt\r\n  date", "dateFormat")]
    [TestCase("pie\r\n  show", "showData")]
    [TestCase("mindmap\r\n  acc", "accTitle")]
    [TestCase("timeline\r\n  sec", "section")]
    [TestCase("sankey-beta\r\n  source", "source,target,value")]
    [TestCase("architecture-beta\r\n  ser", "service")]
    public void DiagramKeywordsOpenAList(string Source, string ExpectedLabel)
    {
        SetSource(Source);

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain(ExpectedLabel));
        Assert.That(_Controller.Popup.ShowsScrollBar, Is.False);
    }

    [Test]
    public void NoDiagramYetOffersDiagramOpeners()
    {
        SetSource(string.Empty);
        Type("f");

        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("flowchart TD"));
    }

    [Test]
    public void SpacesAfterAnIdentifierPromptForTheLabel()
    {
        SetSource("enterprise\r\n  application-component comp  ");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Parameter));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("label"));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("quotes"));
        Assert.That(string.Join(" ", _Controller.Popup.MemberLabels), Does.Not.Contain("above"));
        Assert.That(_Editor.Focused, Is.True);
    }

    [Test]
    public void AFinishedLabelListsEveryOptionalClause()
    {
        SetSource("enterprise\r\n  application-service orderApi \"Order API\" ");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("in"));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("above"));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("below"));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("left-of"));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("right-of"));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("push"));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("[in <container>]"));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("[+n]"));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("<N|S|E|W>"));
        Assert.That(_Editor.Focused, Is.True);
    }

    [Test]
    public void AStartedPlacementWordStillListsPlacement()
    {
        SetSource("enterprise\r\n  application-component comp ab");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("above"));
        Assert.That(_Controller.Popup.MemberLabels, Does.Not.Contain("below"));
        Assert.That(_Editor.Focused, Is.True);
    }

    [Test]
    public void HyphenInsideAnElementTypeKeepsTheListOpen()
    {
        SetSource("enterprise\r\n  ");
        Type("application");

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("application-interface"));

        Type("-");

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(_Controller.Popup.MemberLabels, Does.Contain("application-interface"));
        Assert.That(_Controller.Popup.MemberLabels, Does.Not.Contain("business-actor"));
        Assert.That(_Editor.Focused, Is.True);
    }

    [Test]
    public void IdentifierAfterATypeShowsParameterHelp()
    {
        SetSource("enterprise\r\n  application-collaboration ");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Parameter));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("<id>"));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("required identifier"));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("case-sensitive"));
        Assert.That(_Editor.Focused, Is.True);
    }

    [Test]
    public void ARepeatedIdentifierIsExplainedWithoutChangingTheDeclaration()
    {
        SetSource("enterprise\r\n  application-service orderApi \"Order API\"\r\n  application-service orderApi");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Parameter));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("already in use"));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("application-service"));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("Order API"));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("F12"));
    }

    [Test]
    public void MovingBackToTheIdentifierRetargetsParameterHelp()
    {
        SetSource("enterprise\r\n  application-collaboration ac \"");
        int Identifier = _Editor.Text.LastIndexOf("ac", StringComparison.Ordinal);
        _Editor.Select(Identifier + 1, 0);
        Application.DoEvents();
        _Controller.Refresh(Force: false);
        Application.DoEvents();

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Parameter));
        Assert.That(_Controller.Popup.DetailText, Does.Contain("required identifier"));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("<id>"));
    }

    [Test]
    public void AccessQualifierExplainsTheModes()
    {
        SetSource("enterprise\r\n  application-service orderApi \"Order API\"\r\n  data-object order \"Order\"\r\n  orderApi -access(");

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        string Notes = _Controller.Popup.DetailText + "\n" + _Controller.Popup.SignatureText;
        Assert.That(Notes, Does.Contain("read"));
        Assert.That(Notes, Does.Contain("write"));
        Assert.That(Notes, Does.Contain("Default when omitted: write."));
    }

    [Test]
    public void LongerRelationshipArrowStillListsTargets()
    {
        SetSource("enterprise\r\n  application-service orderApi \"Order API\"\r\n  data-object order \"Order\"\r\n  orderApi -access(read)---> ");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(string.Join("\n", _Controller.Popup.MemberLabels), Does.Contain("order"));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("<target>"));
    }

    [Test]
    public void RelationshipTargetListsCompatibleElementsAndExistingConnections()
    {
        SetSource("enterprise\r\n  application-service orderApi \"Order API\"\r\n  data-object order \"Order\"\r\n  orderApi -access(read)-> order\r\n  orderApi -access(read)-> ");

        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(string.Join("\n", _Controller.Popup.MemberLabels), Does.Contain("order"));
        Assert.That(string.Join("\n", _Controller.Popup.MemberLabels), Does.Contain("Already connected: access(read)"));
        Assert.That(_Controller.Popup.SignatureText, Does.Contain("<target>"));
    }

    [Test]
    public void AcceptingAnExistingElementInsertsOnlyItsIdentifier()
    {
        SetSource("enterprise\r\n  application-service orderApi \"Order API\"\r\n  ord");
        for (int Step = 0; Step < 40 && _Controller.Popup.IsVisible && !_Controller.Popup.SelectedMemberLabel.Contains("orderApi", StringComparison.Ordinal); Step++)
        {
            _Editor.RaiseKeyDown(new KeyEventArgs(Keys.Down));
            Application.DoEvents();
        }

        Assert.That(_Controller.Popup.SelectedMemberLabel, Does.Contain("orderApi"));
        _Editor.RaiseKeyDown(new KeyEventArgs(Keys.Tab));
        Application.DoEvents();

        Assert.That(_Editor.Text, Does.EndWith("orderApi"));
        Assert.That(_Editor.Text, Does.Not.Contain("orderApi -"));
    }

    [Test]
    public void RelationshipLineListsLegalKinds()
    {
        SetSource("enterprise\r\n  application-component webShop \"Web Shop\"\r\n  application-service orderApi \"Order API\"\r\n  webShop -");

        Assert.That(_Controller.Popup.IsVisible, Is.True);
        Assert.That(_Controller.Popup.Surface, Is.EqualTo(AssistSurface.Members));
        Assert.That(_Controller.Popup.MemberLabels.Count, Is.GreaterThan(0));
        Assert.That(_Controller.Popup.ShowsScrollBar, Is.False);
    }

    private void SetSource(string Text)
    {
        _Controller.Suppress = true;
        try
        {
            _Editor.Text = Text;
            _Editor.SelectionStart = _Editor.TextLength;
            _Editor.SelectionLength = 0;
        }
        finally
        {
            _Controller.Suppress = false;
        }

        _Editor.Focus();
        Application.DoEvents();
        _Controller.Refresh(Force: false);
        Application.DoEvents();
    }

    private void Type(string Text)
    {
        _Editor.Focus();
        _Editor.Select(_Editor.TextLength, 0);
        _Editor.SelectedText = Text;
        Application.DoEvents();
    }

    private static void AssertEachStartsWith(IReadOnlyList<string> Labels, string Prefix)
    {
        Assert.That(Labels.Count, Is.GreaterThan(0));
        foreach (string Label in Labels)
        {
            Assert.That(Label.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase), Is.True, Label);
        }
    }
}
