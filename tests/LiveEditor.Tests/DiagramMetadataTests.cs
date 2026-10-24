using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using LiveEditor.Editor;
using NUnit.Framework;

namespace LiveEditor.Tests;

/// <summary>
/// The diagram in the editor carries metadata: its type, its nodes and relationships, and how far its render has got.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class DiagramMetadataTests
{
    private const string SOURCE = "enterprise\r\n"
        + "  group shopFront \"Shop front\"\r\n"
        + "  application-component shop \"Web Shop\" in shopFront\r\n"
        + "  application-service orders \"Orders\"\r\n"
        + "  note explanation \"Read this note\" right-of shop\r\n"
        + "  orders -serving-> shop\r\n";

    [Test]
    public void EnterpriseMetadataListsEveryNodeInSourceOrder()
    {
        DiagramMetadata Metadata = DiagramMetadata.Build(SOURCE, 7);

        Assert.That(Metadata.Generation, Is.EqualTo(7));
        Assert.That(Metadata.DiagramType, Is.EqualTo("enterprise"));
        Assert.That(Metadata.NodeCount, Is.EqualTo(4));
        Assert.That(Metadata.Nodes[0].Id, Is.EqualTo("shopFront"));
        Assert.That(Metadata.Nodes[0].Kind, Is.EqualTo(DiagramNodeKind.Group));
        Assert.That(Metadata.Nodes[1].Id, Is.EqualTo("shop"));
        Assert.That(Metadata.Nodes[1].TypeName, Is.EqualTo("application-component"));
        Assert.That(Metadata.Nodes[1].Label, Is.EqualTo("Web Shop"));
        Assert.That(Metadata.Nodes[1].ParentId, Is.EqualTo("shopFront"));
        Assert.That(Metadata.Nodes[1].LineNumber, Is.EqualTo(3));
        Assert.That(Metadata.Nodes[2].Id, Is.EqualTo("orders"));
        Assert.That(Metadata.Nodes[3].Kind, Is.EqualTo(DiagramNodeKind.Note));
        Assert.That(Metadata.RelationshipCount, Is.EqualTo(1));
        Assert.That(Metadata.Relationships[0].SourceId, Is.EqualTo("orders"));
        Assert.That(Metadata.Relationships[0].TargetId, Is.EqualTo("shop"));
        Assert.That(Metadata.Relationships[0].KindName, Does.Contain("erving"));
        Assert.That(Metadata.ErrorCount, Is.EqualTo(0));
        Assert.That(Metadata.PercentRendered, Is.EqualTo(0));
    }

    [Test]
    public void EmptySourceHasNoNodesAndAnUnknownType()
    {
        DiagramMetadata Metadata = DiagramMetadata.Build("   ", 1);

        Assert.That(Metadata.DiagramType, Is.EqualTo(DiagramMetadata.UNKNOWN_TYPE));
        Assert.That(Metadata.NodeCount, Is.EqualTo(0));
        Assert.That(Metadata.RelationshipCount, Is.EqualTo(0));
    }

    [Test]
    public void ParseErrorsAreCounted()
    {
        DiagramMetadata Metadata = DiagramMetadata.Build("enterprise\r\n  not-a-statement\r\n", 1);

        Assert.That(Metadata.DiagramType, Is.EqualTo("enterprise"));
        Assert.That(Metadata.ErrorCount, Is.GreaterThan(0));
    }

    [Test]
    public void ProgressIsClampedAndRaisesAnEvent()
    {
        DiagramMetadata Metadata = DiagramMetadata.Build(SOURCE, 1);
        int Raised = 0;
        Metadata.ProgressChanged += (Sender, E) => Raised++;

        Metadata.ReportProgress(140, "Done");
        Assert.That(Metadata.PercentRendered, Is.EqualTo(100));
        Assert.That(Metadata.RenderStage, Is.EqualTo("Done"));

        Metadata.ReportFailure("Broken");
        Assert.That(Metadata.RenderFailed, Is.True);
        Assert.That(Raised, Is.EqualTo(2));
    }

    [Test]
    public void TheEditorAttachesMetadataToItsDiagramAndFinishesAtOneHundredPercent()
    {
        SessionSettings Settings = new SessionSettings();
        Settings.HighlightDelayMilliseconds = 40;
        Settings.RenderDelayMilliseconds = 40;
        using MainForm Form = new MainForm(Settings);
        Form.ShowInTaskbar = false;
        Form.StartPosition = FormStartPosition.Manual;
        Form.Location = new Point(40, 40);
        Form.ClientSize = new Size(1200, 800);
        Form.Show();
        Application.DoEvents();
        RichTextBox Editor = (RichTextBox)Form.Controls.Find("SourceEditor", true)[0];
        Editor.Text = SOURCE;
        Application.DoEvents();

        DateTime End = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < End && Form.DiagramMetadata.PercentRendered < 100)
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }

        DiagramMetadata Metadata = Form.DiagramMetadata;
        Assert.That(Metadata.DiagramType, Is.EqualTo("enterprise"));
        Assert.That(Metadata.NodeCount, Is.EqualTo(4));
        Assert.That(Metadata.PercentRendered, Is.EqualTo(100));
        Assert.That(Metadata.RenderStage, Is.EqualTo("Done"));
    }
}