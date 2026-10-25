using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class NewDiagramForm
    {
        private System.ComponentModel.IContainer _Components = null;

        protected override void Dispose(bool Disposing)
        {
            if (Disposing && _Components != null)
            {
                _Components.Dispose();
            }

            base.Dispose(Disposing);
        }

        private void InitializeComponent()
        {
            GroupModelPage = new TabPage();
            GroupModelFlow = new FlowLayoutPanel();
            Kind_enterprise = new Button();
            Kind_architecture = new Button();
            Kind_c4 = new Button();
            GroupFlowPage = new TabPage();
            GroupFlowFlow = new FlowLayoutPanel();
            Kind_flowchart = new Button();
            GroupInteractionPage = new TabPage();
            GroupInteractionFlow = new FlowLayoutPanel();
            Kind_sequence = new Button();
            GroupStructurePage = new TabPage();
            GroupStructureFlow = new FlowLayoutPanel();
            Kind_class = new Button();
            Kind_state = new Button();
            Kind_er = new Button();
            GroupChartsPage = new TabPage();
            GroupChartsFlow = new FlowLayoutPanel();
            Kind_gantt = new Button();
            Kind_pie = new Button();
            Kind_gitGraph = new Button();
            Kind_quadrantChart = new Button();
            Kind_xychart = new Button();
            Kind_block = new Button();
            Kind_packet = new Button();
            Kind_radar = new Button();
            Kind_treemap = new Button();
            GroupNotesPage = new TabPage();
            GroupNotesFlow = new FlowLayoutPanel();
            Kind_mindmap = new Button();
            Kind_timeline = new Button();
            Kind_journey = new Button();
            KindTabs = new TabControl();
            ButtonRow = new TableLayoutPanel();
            KindDescriptionLabel = new Label();
            DialogButtons = new FlowLayoutPanel();
            CreateDiagramButton = new Button();
            CancelDiagramButton = new Button();
            GroupModelPage.SuspendLayout();
            GroupModelFlow.SuspendLayout();
            GroupFlowPage.SuspendLayout();
            GroupFlowFlow.SuspendLayout();
            GroupInteractionPage.SuspendLayout();
            GroupInteractionFlow.SuspendLayout();
            GroupStructurePage.SuspendLayout();
            GroupStructureFlow.SuspendLayout();
            GroupChartsPage.SuspendLayout();
            GroupChartsFlow.SuspendLayout();
            GroupNotesPage.SuspendLayout();
            GroupNotesFlow.SuspendLayout();
            KindTabs.SuspendLayout();
            ButtonRow.SuspendLayout();
            DialogButtons.SuspendLayout();
            SuspendLayout();
            // 
            // GroupModelPage
            // 
            GroupModelPage.Controls.Add(GroupModelFlow);
            GroupModelPage.Location = new Point(4, 35);
            GroupModelPage.Name = "GroupModelPage";
            GroupModelPage.Size = new Size(716, 345);
            GroupModelPage.TabIndex = 0;
            GroupModelPage.Text = "Model";
            GroupModelPage.UseVisualStyleBackColor = true;
            // 
            // GroupModelFlow
            // 
            GroupModelFlow.Controls.Add(Kind_enterprise);
            GroupModelFlow.Controls.Add(Kind_architecture);
            GroupModelFlow.Controls.Add(Kind_c4);
            GroupModelFlow.Dock = DockStyle.Fill;
            GroupModelFlow.Location = new Point(0, 0);
            GroupModelFlow.Name = "GroupModelFlow";
            GroupModelFlow.Padding = new Padding(8);
            GroupModelFlow.Size = new Size(716, 345);
            GroupModelFlow.TabIndex = 0;
            // 
            // Kind_enterprise
            // 
            Kind_enterprise.AutoSize = true;
            Kind_enterprise.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_enterprise.Location = new Point(12, 12);
            Kind_enterprise.Margin = new Padding(4);
            Kind_enterprise.Name = "Kind_enterprise";
            Kind_enterprise.Click += Choice_Click;
            Kind_enterprise.DoubleClick += Choice_DoubleClick;
            Kind_enterprise.Size = new Size(77, 27);
            Kind_enterprise.TabIndex = 0;
            Kind_enterprise.Tag = "enterprise";
            Kind_enterprise.Text = "Enterprise";
            Kind_enterprise.UseVisualStyleBackColor = false;
            // 
            // Kind_architecture
            // 
            Kind_architecture.AutoSize = true;
            Kind_architecture.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_architecture.Location = new Point(97, 12);
            Kind_architecture.Margin = new Padding(4);
            Kind_architecture.Name = "Kind_architecture";
            Kind_architecture.Click += Choice_Click;
            Kind_architecture.DoubleClick += Choice_DoubleClick;
            Kind_architecture.Size = new Size(87, 27);
            Kind_architecture.TabIndex = 1;
            Kind_architecture.Tag = "architecture";
            Kind_architecture.Text = "Architecture";
            Kind_architecture.UseVisualStyleBackColor = false;
            // 
            // Kind_c4
            // 
            Kind_c4.AutoSize = true;
            Kind_c4.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_c4.Location = new Point(192, 12);
            Kind_c4.Margin = new Padding(4);
            Kind_c4.Name = "Kind_c4";
            Kind_c4.Click += Choice_Click;
            Kind_c4.DoubleClick += Choice_DoubleClick;
            Kind_c4.Size = new Size(33, 27);
            Kind_c4.TabIndex = 2;
            Kind_c4.Tag = "c4";
            Kind_c4.Text = "C4";
            Kind_c4.UseVisualStyleBackColor = false;
            // 
            // GroupFlowPage
            // 
            GroupFlowPage.Controls.Add(GroupFlowFlow);
            GroupFlowPage.Location = new Point(4, 35);
            GroupFlowPage.Name = "GroupFlowPage";
            GroupFlowPage.Size = new Size(714, 337);
            GroupFlowPage.TabIndex = 1;
            GroupFlowPage.Text = "Flow";
            GroupFlowPage.UseVisualStyleBackColor = true;
            // 
            // GroupFlowFlow
            // 
            GroupFlowFlow.Controls.Add(Kind_flowchart);
            GroupFlowFlow.Dock = DockStyle.Fill;
            GroupFlowFlow.Location = new Point(0, 0);
            GroupFlowFlow.Name = "GroupFlowFlow";
            GroupFlowFlow.Padding = new Padding(8);
            GroupFlowFlow.Size = new Size(714, 337);
            GroupFlowFlow.TabIndex = 0;
            // 
            // Kind_flowchart
            // 
            Kind_flowchart.AutoSize = true;
            Kind_flowchart.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_flowchart.Location = new Point(12, 12);
            Kind_flowchart.Margin = new Padding(4);
            Kind_flowchart.Name = "Kind_flowchart";
            Kind_flowchart.Click += Choice_Click;
            Kind_flowchart.DoubleClick += Choice_DoubleClick;
            Kind_flowchart.Size = new Size(83, 30);
            Kind_flowchart.TabIndex = 0;
            Kind_flowchart.Tag = "flowchart";
            Kind_flowchart.Text = "Flowchart";
            Kind_flowchart.UseVisualStyleBackColor = false;
            // 
            // GroupInteractionPage
            // 
            GroupInteractionPage.Controls.Add(GroupInteractionFlow);
            GroupInteractionPage.Location = new Point(4, 35);
            GroupInteractionPage.Name = "GroupInteractionPage";
            GroupInteractionPage.Size = new Size(714, 337);
            GroupInteractionPage.TabIndex = 2;
            GroupInteractionPage.Text = "Interaction";
            GroupInteractionPage.UseVisualStyleBackColor = true;
            // 
            // GroupInteractionFlow
            // 
            GroupInteractionFlow.Controls.Add(Kind_sequence);
            GroupInteractionFlow.Dock = DockStyle.Fill;
            GroupInteractionFlow.Location = new Point(0, 0);
            GroupInteractionFlow.Name = "GroupInteractionFlow";
            GroupInteractionFlow.Padding = new Padding(8);
            GroupInteractionFlow.Size = new Size(714, 337);
            GroupInteractionFlow.TabIndex = 0;
            // 
            // Kind_sequence
            // 
            Kind_sequence.AutoSize = true;
            Kind_sequence.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_sequence.Location = new Point(12, 12);
            Kind_sequence.Margin = new Padding(4);
            Kind_sequence.Name = "Kind_sequence";
            Kind_sequence.Click += Choice_Click;
            Kind_sequence.DoubleClick += Choice_DoubleClick;
            Kind_sequence.Size = new Size(83, 30);
            Kind_sequence.TabIndex = 0;
            Kind_sequence.Tag = "sequence";
            Kind_sequence.Text = "Sequence";
            Kind_sequence.UseVisualStyleBackColor = false;
            // 
            // GroupStructurePage
            // 
            GroupStructurePage.Controls.Add(GroupStructureFlow);
            GroupStructurePage.Location = new Point(4, 35);
            GroupStructurePage.Name = "GroupStructurePage";
            GroupStructurePage.Size = new Size(714, 337);
            GroupStructurePage.TabIndex = 3;
            GroupStructurePage.Text = "Structure";
            GroupStructurePage.UseVisualStyleBackColor = true;
            // 
            // GroupStructureFlow
            // 
            GroupStructureFlow.Controls.Add(Kind_class);
            GroupStructureFlow.Controls.Add(Kind_state);
            GroupStructureFlow.Controls.Add(Kind_er);
            GroupStructureFlow.Dock = DockStyle.Fill;
            GroupStructureFlow.Location = new Point(0, 0);
            GroupStructureFlow.Name = "GroupStructureFlow";
            GroupStructureFlow.Padding = new Padding(8);
            GroupStructureFlow.Size = new Size(714, 337);
            GroupStructureFlow.TabIndex = 0;
            // 
            // Kind_class
            // 
            Kind_class.AutoSize = true;
            Kind_class.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_class.Location = new Point(12, 12);
            Kind_class.Margin = new Padding(4);
            Kind_class.Name = "Kind_class";
            Kind_class.Click += Choice_Click;
            Kind_class.DoubleClick += Choice_DoubleClick;
            Kind_class.Size = new Size(52, 30);
            Kind_class.TabIndex = 0;
            Kind_class.Tag = "class";
            Kind_class.Text = "Class";
            Kind_class.UseVisualStyleBackColor = false;
            // 
            // Kind_state
            // 
            Kind_state.AutoSize = true;
            Kind_state.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_state.Location = new Point(72, 12);
            Kind_state.Margin = new Padding(4);
            Kind_state.Name = "Kind_state";
            Kind_state.Click += Choice_Click;
            Kind_state.DoubleClick += Choice_DoubleClick;
            Kind_state.Size = new Size(53, 30);
            Kind_state.TabIndex = 1;
            Kind_state.Tag = "state";
            Kind_state.Text = "State";
            Kind_state.UseVisualStyleBackColor = false;
            // 
            // Kind_er
            // 
            Kind_er.AutoSize = true;
            Kind_er.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_er.Location = new Point(133, 12);
            Kind_er.Margin = new Padding(4);
            Kind_er.Name = "Kind_er";
            Kind_er.Click += Choice_Click;
            Kind_er.DoubleClick += Choice_DoubleClick;
            Kind_er.Size = new Size(138, 30);
            Kind_er.TabIndex = 2;
            Kind_er.Tag = "er";
            Kind_er.Text = "Entity relationship";
            Kind_er.UseVisualStyleBackColor = false;
            // 
            // GroupChartsPage
            // 
            GroupChartsPage.Controls.Add(GroupChartsFlow);
            GroupChartsPage.Location = new Point(4, 35);
            GroupChartsPage.Name = "GroupChartsPage";
            GroupChartsPage.Size = new Size(714, 337);
            GroupChartsPage.TabIndex = 4;
            GroupChartsPage.Text = "Charts";
            GroupChartsPage.UseVisualStyleBackColor = true;
            // 
            // GroupChartsFlow
            // 
            GroupChartsFlow.Controls.Add(Kind_gantt);
            GroupChartsFlow.Controls.Add(Kind_pie);
            GroupChartsFlow.Controls.Add(Kind_gitGraph);
            GroupChartsFlow.Controls.Add(Kind_quadrantChart);
            GroupChartsFlow.Controls.Add(Kind_xychart);
            GroupChartsFlow.Controls.Add(Kind_block);
            GroupChartsFlow.Controls.Add(Kind_packet);
            GroupChartsFlow.Controls.Add(Kind_radar);
            GroupChartsFlow.Controls.Add(Kind_treemap);
            GroupChartsFlow.Dock = DockStyle.Fill;
            GroupChartsFlow.Location = new Point(0, 0);
            GroupChartsFlow.Name = "GroupChartsFlow";
            GroupChartsFlow.Padding = new Padding(8);
            GroupChartsFlow.Size = new Size(714, 337);
            GroupChartsFlow.TabIndex = 0;
            // 
            // Kind_gantt
            // 
            Kind_gantt.AutoSize = true;
            Kind_gantt.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_gantt.Location = new Point(12, 12);
            Kind_gantt.Margin = new Padding(4);
            Kind_gantt.Name = "Kind_gantt";
            Kind_gantt.Click += Choice_Click;
            Kind_gantt.DoubleClick += Choice_DoubleClick;
            Kind_gantt.Size = new Size(55, 30);
            Kind_gantt.TabIndex = 0;
            Kind_gantt.Tag = "gantt";
            Kind_gantt.Text = "Gantt";
            Kind_gantt.UseVisualStyleBackColor = false;
            // 
            // Kind_pie
            // 
            Kind_pie.AutoSize = true;
            Kind_pie.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_pie.Location = new Point(75, 12);
            Kind_pie.Margin = new Padding(4);
            Kind_pie.Name = "Kind_pie";
            Kind_pie.Click += Choice_Click;
            Kind_pie.DoubleClick += Choice_DoubleClick;
            Kind_pie.Size = new Size(39, 30);
            Kind_pie.TabIndex = 1;
            Kind_pie.Tag = "pie";
            Kind_pie.Text = "Pie";
            Kind_pie.UseVisualStyleBackColor = false;
            // 
            // Kind_gitGraph
            // 
            Kind_gitGraph.AutoSize = true;
            Kind_gitGraph.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_gitGraph.Location = new Point(122, 12);
            Kind_gitGraph.Margin = new Padding(4);
            Kind_gitGraph.Name = "Kind_gitGraph";
            Kind_gitGraph.Click += Choice_Click;
            Kind_gitGraph.DoubleClick += Choice_DoubleClick;
            Kind_gitGraph.Size = new Size(81, 30);
            Kind_gitGraph.TabIndex = 2;
            Kind_gitGraph.Tag = "gitGraph";
            Kind_gitGraph.Text = "Git graph";
            Kind_gitGraph.UseVisualStyleBackColor = false;
            // 
            // Kind_quadrantChart
            // 
            Kind_quadrantChart.AutoSize = true;
            Kind_quadrantChart.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_quadrantChart.Location = new Point(211, 12);
            Kind_quadrantChart.Margin = new Padding(4);
            Kind_quadrantChart.Name = "Kind_quadrantChart";
            Kind_quadrantChart.Click += Choice_Click;
            Kind_quadrantChart.DoubleClick += Choice_DoubleClick;
            Kind_quadrantChart.Size = new Size(118, 30);
            Kind_quadrantChart.TabIndex = 3;
            Kind_quadrantChart.Tag = "quadrantChart";
            Kind_quadrantChart.Text = "Quadrant chart";
            Kind_quadrantChart.UseVisualStyleBackColor = false;
            // 
            // Kind_xychart
            // 
            Kind_xychart.AutoSize = true;
            Kind_xychart.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_xychart.Location = new Point(337, 12);
            Kind_xychart.Margin = new Padding(4);
            Kind_xychart.Name = "Kind_xychart";
            Kind_xychart.Click += Choice_Click;
            Kind_xychart.DoubleClick += Choice_DoubleClick;
            Kind_xychart.Size = new Size(73, 30);
            Kind_xychart.TabIndex = 4;
            Kind_xychart.Tag = "xychart";
            Kind_xychart.Text = "XY chart";
            Kind_xychart.UseVisualStyleBackColor = false;
            // 
            // Kind_block
            // 
            Kind_block.AutoSize = true;
            Kind_block.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_block.Location = new Point(418, 12);
            Kind_block.Margin = new Padding(4);
            Kind_block.Name = "Kind_block";
            Kind_block.Click += Choice_Click;
            Kind_block.DoubleClick += Choice_DoubleClick;
            Kind_block.Size = new Size(55, 30);
            Kind_block.TabIndex = 5;
            Kind_block.Tag = "block";
            Kind_block.Text = "Block";
            Kind_block.UseVisualStyleBackColor = false;
            // 
            // Kind_packet
            // 
            Kind_packet.AutoSize = true;
            Kind_packet.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_packet.Location = new Point(481, 12);
            Kind_packet.Margin = new Padding(4);
            Kind_packet.Name = "Kind_packet";
            Kind_packet.Click += Choice_Click;
            Kind_packet.DoubleClick += Choice_DoubleClick;
            Kind_packet.Size = new Size(61, 30);
            Kind_packet.TabIndex = 6;
            Kind_packet.Tag = "packet";
            Kind_packet.Text = "Packet";
            Kind_packet.UseVisualStyleBackColor = false;
            // 
            // Kind_radar
            // 
            Kind_radar.AutoSize = true;
            Kind_radar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_radar.Location = new Point(550, 12);
            Kind_radar.Margin = new Padding(4);
            Kind_radar.Name = "Kind_radar";
            Kind_radar.Click += Choice_Click;
            Kind_radar.DoubleClick += Choice_DoubleClick;
            Kind_radar.Size = new Size(58, 30);
            Kind_radar.TabIndex = 7;
            Kind_radar.Tag = "radar";
            Kind_radar.Text = "Radar";
            Kind_radar.UseVisualStyleBackColor = false;
            // 
            // Kind_treemap
            // 
            Kind_treemap.AutoSize = true;
            Kind_treemap.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_treemap.Location = new Point(616, 12);
            Kind_treemap.Margin = new Padding(4);
            Kind_treemap.Name = "Kind_treemap";
            Kind_treemap.Click += Choice_Click;
            Kind_treemap.DoubleClick += Choice_DoubleClick;
            Kind_treemap.Size = new Size(77, 30);
            Kind_treemap.TabIndex = 8;
            Kind_treemap.Tag = "treemap";
            Kind_treemap.Text = "Treemap";
            Kind_treemap.UseVisualStyleBackColor = false;
            // 
            // GroupNotesPage
            // 
            GroupNotesPage.Controls.Add(GroupNotesFlow);
            GroupNotesPage.Location = new Point(4, 35);
            GroupNotesPage.Name = "GroupNotesPage";
            GroupNotesPage.Size = new Size(714, 337);
            GroupNotesPage.TabIndex = 5;
            GroupNotesPage.Text = "Notes";
            GroupNotesPage.UseVisualStyleBackColor = true;
            // 
            // GroupNotesFlow
            // 
            GroupNotesFlow.Controls.Add(Kind_mindmap);
            GroupNotesFlow.Controls.Add(Kind_timeline);
            GroupNotesFlow.Controls.Add(Kind_journey);
            GroupNotesFlow.Dock = DockStyle.Fill;
            GroupNotesFlow.Location = new Point(0, 0);
            GroupNotesFlow.Name = "GroupNotesFlow";
            GroupNotesFlow.Padding = new Padding(8);
            GroupNotesFlow.Size = new Size(714, 337);
            GroupNotesFlow.TabIndex = 0;
            // 
            // Kind_mindmap
            // 
            Kind_mindmap.AutoSize = true;
            Kind_mindmap.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_mindmap.Location = new Point(12, 12);
            Kind_mindmap.Margin = new Padding(4);
            Kind_mindmap.Name = "Kind_mindmap";
            Kind_mindmap.Click += Choice_Click;
            Kind_mindmap.DoubleClick += Choice_DoubleClick;
            Kind_mindmap.Size = new Size(83, 30);
            Kind_mindmap.TabIndex = 0;
            Kind_mindmap.Tag = "mindmap";
            Kind_mindmap.Text = "Mindmap";
            Kind_mindmap.UseVisualStyleBackColor = false;
            // 
            // Kind_timeline
            // 
            Kind_timeline.AutoSize = true;
            Kind_timeline.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_timeline.Location = new Point(103, 12);
            Kind_timeline.Margin = new Padding(4);
            Kind_timeline.Name = "Kind_timeline";
            Kind_timeline.Click += Choice_Click;
            Kind_timeline.DoubleClick += Choice_DoubleClick;
            Kind_timeline.Size = new Size(76, 30);
            Kind_timeline.TabIndex = 1;
            Kind_timeline.Tag = "timeline";
            Kind_timeline.Text = "Timeline";
            Kind_timeline.UseVisualStyleBackColor = false;
            // 
            // Kind_journey
            // 
            Kind_journey.AutoSize = true;
            Kind_journey.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Kind_journey.Location = new Point(187, 12);
            Kind_journey.Margin = new Padding(4);
            Kind_journey.Name = "Kind_journey";
            Kind_journey.Click += Choice_Click;
            Kind_journey.DoubleClick += Choice_DoubleClick;
            Kind_journey.Size = new Size(69, 30);
            Kind_journey.TabIndex = 2;
            Kind_journey.Tag = "journey";
            Kind_journey.Text = "Journey";
            Kind_journey.UseVisualStyleBackColor = false;
            // 
            // KindTabs
            // 
            KindTabs.Controls.Add(GroupModelPage);
            KindTabs.Controls.Add(GroupFlowPage);
            KindTabs.Controls.Add(GroupInteractionPage);
            KindTabs.Controls.Add(GroupStructurePage);
            KindTabs.Controls.Add(GroupChartsPage);
            KindTabs.Controls.Add(GroupNotesPage);
            KindTabs.Dock = DockStyle.Fill;
            KindTabs.Location = new Point(0, 0);
            KindTabs.Name = "KindTabs";
            KindTabs.Padding = new Point(8, 6);
            KindTabs.SelectedIndex = 0;
            KindTabs.Size = new Size(724, 384);
            KindTabs.TabIndex = 0;
            // 
            // ButtonRow
            // 
            ButtonRow.ColumnCount = 2;
            ButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            ButtonRow.ColumnStyles.Add(new ColumnStyle());
            ButtonRow.Controls.Add(KindDescriptionLabel, 0, 0);
            ButtonRow.Controls.Add(DialogButtons, 1, 0);
            ButtonRow.Dock = DockStyle.Bottom;
            ButtonRow.Location = new Point(0, 384);
            ButtonRow.Name = "ButtonRow";
            ButtonRow.Padding = new Padding(8);
            ButtonRow.RowCount = 1;
            ButtonRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            ButtonRow.Size = new Size(724, 52);
            ButtonRow.TabIndex = 1;
            // 
            // KindDescriptionLabel
            // 
            KindDescriptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            KindDescriptionLabel.AutoSize = true;
            KindDescriptionLabel.Location = new Point(11, 18);
            KindDescriptionLabel.Name = "KindDescriptionLabel";
            KindDescriptionLabel.Size = new Size(538, 20);
            KindDescriptionLabel.TabIndex = 0;
            KindDescriptionLabel.Text = "Choose a diagram type.";
            KindDescriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // DialogButtons
            // 
            DialogButtons.Anchor = AnchorStyles.Right;
            DialogButtons.AutoSize = true;
            DialogButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            DialogButtons.Controls.Add(CreateDiagramButton);
            DialogButtons.Controls.Add(CancelDiagramButton);
            DialogButtons.Location = new Point(555, 13);
            DialogButtons.Name = "DialogButtons";
            DialogButtons.Size = new Size(158, 30);
            DialogButtons.TabIndex = 1;
            DialogButtons.WrapContents = false;
            // 
            // CreateDiagramButton
            // 
            CreateDiagramButton.AutoSize = true;
            CreateDiagramButton.Location = new Point(0, 0);
            CreateDiagramButton.Margin = new Padding(0, 0, 8, 0);
            CreateDiagramButton.Name = "CreateDiagramButton";
            CreateDiagramButton.Click += CreateDiagramButton_Click;
            CreateDiagramButton.Size = new Size(75, 30);
            CreateDiagramButton.TabIndex = 0;
            CreateDiagramButton.Text = "Create";
            CreateDiagramButton.UseVisualStyleBackColor = true;
            // 
            // CancelDiagramButton
            // 
            CancelDiagramButton.AutoSize = true;
            CancelDiagramButton.Location = new Point(83, 0);
            CancelDiagramButton.Margin = new Padding(0);
            CancelDiagramButton.Name = "CancelDiagramButton";
            CancelDiagramButton.Click += CancelDiagramButton_Click;
            CancelDiagramButton.Size = new Size(75, 30);
            CancelDiagramButton.TabIndex = 1;
            CancelDiagramButton.Text = "Cancel";
            CancelDiagramButton.UseVisualStyleBackColor = true;
            // 
            // NewDiagramForm
            // 
            AcceptButton = CreateDiagramButton;
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = CancelDiagramButton;
            ClientSize = new Size(724, 436);
            Controls.Add(KindTabs);
            Controls.Add(ButtonRow);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "NewDiagramForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "New diagram";
            GroupModelPage.ResumeLayout(false);
            GroupModelFlow.ResumeLayout(false);
            GroupModelFlow.PerformLayout();
            GroupFlowPage.ResumeLayout(false);
            GroupFlowFlow.ResumeLayout(false);
            GroupFlowFlow.PerformLayout();
            GroupInteractionPage.ResumeLayout(false);
            GroupInteractionFlow.ResumeLayout(false);
            GroupInteractionFlow.PerformLayout();
            GroupStructurePage.ResumeLayout(false);
            GroupStructureFlow.ResumeLayout(false);
            GroupStructureFlow.PerformLayout();
            GroupChartsPage.ResumeLayout(false);
            GroupChartsFlow.ResumeLayout(false);
            GroupChartsFlow.PerformLayout();
            GroupNotesPage.ResumeLayout(false);
            GroupNotesFlow.ResumeLayout(false);
            GroupNotesFlow.PerformLayout();
            KindTabs.ResumeLayout(false);
            ButtonRow.ResumeLayout(false);
            ButtonRow.PerformLayout();
            DialogButtons.ResumeLayout(false);
            DialogButtons.PerformLayout();
            ResumeLayout(false);
        }

        private TabPage GroupModelPage;
        private FlowLayoutPanel GroupModelFlow;
        private TabPage GroupFlowPage;
        private FlowLayoutPanel GroupFlowFlow;
        private TabPage GroupInteractionPage;
        private FlowLayoutPanel GroupInteractionFlow;
        private TabPage GroupStructurePage;
        private FlowLayoutPanel GroupStructureFlow;
        private TabPage GroupChartsPage;
        private FlowLayoutPanel GroupChartsFlow;
        private TabPage GroupNotesPage;
        private FlowLayoutPanel GroupNotesFlow;
        private Button Kind_enterprise;
        private Button Kind_flowchart;
        private Button Kind_sequence;
        private Button Kind_class;
        private Button Kind_state;
        private Button Kind_er;
        private Button Kind_gantt;
        private Button Kind_pie;
        private Button Kind_gitGraph;
        private Button Kind_mindmap;
        private Button Kind_timeline;
        private Button Kind_journey;
        private Button Kind_quadrantChart;
        private Button Kind_xychart;
        private Button Kind_architecture;
        private Button Kind_block;
        private Button Kind_packet;
        private Button Kind_radar;
        private Button Kind_treemap;
        private Button Kind_c4;
        private TabControl KindTabs;

        private TableLayoutPanel ButtonRow;

        private FlowLayoutPanel DialogButtons;

        private Label KindDescriptionLabel;

        private Button CreateDiagramButton;

        private Button CancelDiagramButton;
    }
}
