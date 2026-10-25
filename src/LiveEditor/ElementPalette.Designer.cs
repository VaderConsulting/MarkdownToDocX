using System.Drawing;
using System.Windows.Forms;

namespace LiveEditor
{
    partial class ElementPalette
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
            MotivationPage = new TabPage();
            MotivationFlow = new FlowLayoutPanel();
            StrategyPage = new TabPage();
            StrategyFlow = new FlowLayoutPanel();
            BusinessPage = new TabPage();
            BusinessFlow = new FlowLayoutPanel();
            ApplicationPage = new TabPage();
            ApplicationFlow = new FlowLayoutPanel();
            TechnologyPage = new TabPage();
            TechnologyFlow = new FlowLayoutPanel();
            ImplementationPage = new TabPage();
            ImplementationFlow = new FlowLayoutPanel();
            CompositePage = new TabPage();
            CompositeFlow = new FlowLayoutPanel();
            JunctionPage = new TabPage();
            JunctionFlow = new FlowLayoutPanel();
            LayerTabs = new TabControl();
            PromptPanel = new FlowLayoutPanel();
            PromptGlyph = new Panel();
            PromptTypeLabel = new Label();
            PromptLabelCaption = new Label();
            PromptLabelBox = new TextBox();
            PromptIdCaption = new Label();
            PromptIdBox = new TextBox();
            InsertLineButton = new Button();
            CancelPromptButton = new Button();
            PromptErrorLabel = new Label();
            MotivationPage.SuspendLayout();
            StrategyPage.SuspendLayout();
            BusinessPage.SuspendLayout();
            ApplicationPage.SuspendLayout();
            TechnologyPage.SuspendLayout();
            ImplementationPage.SuspendLayout();
            CompositePage.SuspendLayout();
            JunctionPage.SuspendLayout();
            LayerTabs.SuspendLayout();
            PromptPanel.SuspendLayout();
            SuspendLayout();
            // 
            // MotivationPage
            // 
            MotivationPage.Controls.Add(MotivationFlow);
            MotivationPage.Location = new Point(4, 31);
            MotivationPage.Name = "MotivationPage";
            MotivationPage.Size = new Size(792, 74);
            MotivationPage.TabIndex = 0;
            MotivationPage.Text = "Motivation";
            MotivationPage.UseVisualStyleBackColor = true;
            // 
            // MotivationFlow
            // 
            MotivationFlow.BackColor = SystemColors.Control;
            MotivationFlow.Dock = DockStyle.Fill;
            MotivationFlow.Location = new Point(0, 0);
            MotivationFlow.Name = "MotivationFlow";
            MotivationFlow.Resize += Flow_Resize;
            MotivationFlow.Padding = new Padding(4, 2, 4, 2);
            MotivationFlow.Size = new Size(792, 74);
            MotivationFlow.TabIndex = 0;
            // 
            // StrategyPage
            // 
            StrategyPage.Controls.Add(StrategyFlow);
            StrategyPage.Location = new Point(4, 31);
            StrategyPage.Name = "StrategyPage";
            StrategyPage.Size = new Size(792, 74);
            StrategyPage.TabIndex = 1;
            StrategyPage.Text = "Strategy";
            StrategyPage.UseVisualStyleBackColor = true;
            // 
            // StrategyFlow
            // 
            StrategyFlow.BackColor = SystemColors.Control;
            StrategyFlow.Dock = DockStyle.Fill;
            StrategyFlow.Location = new Point(0, 0);
            StrategyFlow.Name = "StrategyFlow";
            StrategyFlow.Resize += Flow_Resize;
            StrategyFlow.Padding = new Padding(4, 2, 4, 2);
            StrategyFlow.Size = new Size(792, 74);
            StrategyFlow.TabIndex = 0;
            // 
            // BusinessPage
            // 
            BusinessPage.Controls.Add(BusinessFlow);
            BusinessPage.Location = new Point(4, 31);
            BusinessPage.Name = "BusinessPage";
            BusinessPage.Size = new Size(792, 74);
            BusinessPage.TabIndex = 2;
            BusinessPage.Text = "Business";
            BusinessPage.UseVisualStyleBackColor = true;
            // 
            // BusinessFlow
            // 
            BusinessFlow.BackColor = SystemColors.Control;
            BusinessFlow.Dock = DockStyle.Fill;
            BusinessFlow.Location = new Point(0, 0);
            BusinessFlow.Name = "BusinessFlow";
            BusinessFlow.Resize += Flow_Resize;
            BusinessFlow.Padding = new Padding(4, 2, 4, 2);
            BusinessFlow.Size = new Size(792, 74);
            BusinessFlow.TabIndex = 0;
            // 
            // ApplicationPage
            // 
            ApplicationPage.Controls.Add(ApplicationFlow);
            ApplicationPage.Location = new Point(4, 31);
            ApplicationPage.Name = "ApplicationPage";
            ApplicationPage.Size = new Size(792, 74);
            ApplicationPage.TabIndex = 3;
            ApplicationPage.Text = "Application";
            ApplicationPage.UseVisualStyleBackColor = true;
            // 
            // ApplicationFlow
            // 
            ApplicationFlow.BackColor = SystemColors.Control;
            ApplicationFlow.Dock = DockStyle.Fill;
            ApplicationFlow.Location = new Point(0, 0);
            ApplicationFlow.Name = "ApplicationFlow";
            ApplicationFlow.Resize += Flow_Resize;
            ApplicationFlow.Padding = new Padding(4, 2, 4, 2);
            ApplicationFlow.Size = new Size(792, 74);
            ApplicationFlow.TabIndex = 0;
            // 
            // TechnologyPage
            // 
            TechnologyPage.Controls.Add(TechnologyFlow);
            TechnologyPage.Location = new Point(4, 31);
            TechnologyPage.Name = "TechnologyPage";
            TechnologyPage.Size = new Size(792, 74);
            TechnologyPage.TabIndex = 4;
            TechnologyPage.Text = "Technology";
            TechnologyPage.UseVisualStyleBackColor = true;
            // 
            // TechnologyFlow
            // 
            TechnologyFlow.BackColor = SystemColors.Control;
            TechnologyFlow.Dock = DockStyle.Fill;
            TechnologyFlow.Location = new Point(0, 0);
            TechnologyFlow.Name = "TechnologyFlow";
            TechnologyFlow.Resize += Flow_Resize;
            TechnologyFlow.Padding = new Padding(4, 2, 4, 2);
            TechnologyFlow.Size = new Size(792, 74);
            TechnologyFlow.TabIndex = 0;
            // 
            // ImplementationPage
            // 
            ImplementationPage.Controls.Add(ImplementationFlow);
            ImplementationPage.Location = new Point(4, 31);
            ImplementationPage.Name = "ImplementationPage";
            ImplementationPage.Size = new Size(792, 74);
            ImplementationPage.TabIndex = 5;
            ImplementationPage.Text = "Implementation";
            ImplementationPage.UseVisualStyleBackColor = true;
            // 
            // ImplementationFlow
            // 
            ImplementationFlow.BackColor = SystemColors.Control;
            ImplementationFlow.Dock = DockStyle.Fill;
            ImplementationFlow.Location = new Point(0, 0);
            ImplementationFlow.Name = "ImplementationFlow";
            ImplementationFlow.Resize += Flow_Resize;
            ImplementationFlow.Padding = new Padding(4, 2, 4, 2);
            ImplementationFlow.Size = new Size(792, 74);
            ImplementationFlow.TabIndex = 0;
            // 
            // CompositePage
            // 
            CompositePage.Controls.Add(CompositeFlow);
            CompositePage.Location = new Point(4, 31);
            CompositePage.Name = "CompositePage";
            CompositePage.Size = new Size(792, 74);
            CompositePage.TabIndex = 6;
            CompositePage.Text = "Composite";
            CompositePage.UseVisualStyleBackColor = true;
            // 
            // CompositeFlow
            // 
            CompositeFlow.BackColor = SystemColors.Control;
            CompositeFlow.Dock = DockStyle.Fill;
            CompositeFlow.Location = new Point(0, 0);
            CompositeFlow.Name = "CompositeFlow";
            CompositeFlow.Resize += Flow_Resize;
            CompositeFlow.Padding = new Padding(4, 2, 4, 2);
            CompositeFlow.Size = new Size(792, 74);
            CompositeFlow.TabIndex = 0;
            // 
            // JunctionPage
            // 
            JunctionPage.Controls.Add(JunctionFlow);
            JunctionPage.Location = new Point(4, 31);
            JunctionPage.Name = "JunctionPage";
            JunctionPage.Size = new Size(792, 74);
            JunctionPage.TabIndex = 7;
            JunctionPage.Text = "Junction";
            JunctionPage.UseVisualStyleBackColor = true;
            // 
            // JunctionFlow
            // 
            JunctionFlow.BackColor = SystemColors.Control;
            JunctionFlow.Dock = DockStyle.Fill;
            JunctionFlow.Location = new Point(0, 0);
            JunctionFlow.Name = "JunctionFlow";
            JunctionFlow.Resize += Flow_Resize;
            JunctionFlow.Padding = new Padding(4, 2, 4, 2);
            JunctionFlow.Size = new Size(792, 74);
            JunctionFlow.TabIndex = 0;
            // 
            // LayerTabs
            // 
            LayerTabs.Controls.Add(MotivationPage);
            LayerTabs.Controls.Add(StrategyPage);
            LayerTabs.Controls.Add(BusinessPage);
            LayerTabs.Controls.Add(ApplicationPage);
            LayerTabs.Controls.Add(TechnologyPage);
            LayerTabs.Controls.Add(ImplementationPage);
            LayerTabs.Controls.Add(CompositePage);
            LayerTabs.Controls.Add(JunctionPage);
            LayerTabs.Dock = DockStyle.Fill;
            LayerTabs.Location = new Point(0, 0);
            LayerTabs.Name = "LayerTabs";
            LayerTabs.SelectedIndexChanged += LayerTabs_SelectedIndexChanged;
            LayerTabs.Padding = new Point(6, 4);
            LayerTabs.SelectedIndex = 3;
            LayerTabs.Size = new Size(800, 109);
            LayerTabs.TabIndex = 0;
            // 
            // PromptPanel
            // 
            PromptPanel.AutoSize = true;
            PromptPanel.Controls.Add(PromptGlyph);
            PromptPanel.Controls.Add(PromptTypeLabel);
            PromptPanel.Controls.Add(PromptLabelCaption);
            PromptPanel.Controls.Add(PromptLabelBox);
            PromptPanel.Controls.Add(PromptIdCaption);
            PromptPanel.Controls.Add(PromptIdBox);
            PromptPanel.Controls.Add(InsertLineButton);
            PromptPanel.Controls.Add(CancelPromptButton);
            PromptPanel.Controls.Add(PromptErrorLabel);
            PromptPanel.Dock = DockStyle.Bottom;
            PromptPanel.Location = new Point(0, 109);
            PromptPanel.Name = "PromptPanel";
            PromptPanel.Padding = new Padding(4, 4, 4, 2);
            PromptPanel.Size = new Size(800, 39);
            PromptPanel.TabIndex = 1;
            PromptPanel.Visible = false;
            // 
            // PromptGlyph
            // 
            PromptGlyph.Location = new Point(4, 8);
            PromptGlyph.Margin = new Padding(0, 4, 6, 0);
            PromptGlyph.Name = "PromptGlyph";
            PromptGlyph.Paint += PromptGlyph_Paint;
            PromptGlyph.Size = new Size(34, 22);
            PromptGlyph.TabIndex = 0;
            // 
            // PromptTypeLabel
            // 
            PromptTypeLabel.AutoSize = true;
            PromptTypeLabel.Location = new Point(44, 12);
            PromptTypeLabel.Margin = new Padding(0, 8, 8, 0);
            PromptTypeLabel.Name = "PromptTypeLabel";
            PromptTypeLabel.Size = new Size(63, 20);
            PromptTypeLabel.TabIndex = 1;
            PromptTypeLabel.Text = "Element";
            // 
            // PromptLabelCaption
            // 
            PromptLabelCaption.AutoSize = true;
            PromptLabelCaption.Location = new Point(115, 12);
            PromptLabelCaption.Margin = new Padding(0, 8, 4, 0);
            PromptLabelCaption.Name = "PromptLabelCaption";
            PromptLabelCaption.Size = new Size(45, 20);
            PromptLabelCaption.TabIndex = 2;
            PromptLabelCaption.Text = "Label";
            // 
            // PromptLabelBox
            // 
            PromptLabelBox.Location = new Point(164, 8);
            PromptLabelBox.Margin = new Padding(0, 4, 8, 0);
            PromptLabelBox.Name = "PromptLabelBox";
            PromptLabelBox.TextChanged += PromptLabelBox_TextChanged;
            PromptLabelBox.Size = new Size(140, 23);
            PromptLabelBox.TabIndex = 3;
            // 
            // PromptIdCaption
            // 
            PromptIdCaption.AutoSize = true;
            PromptIdCaption.Location = new Point(312, 12);
            PromptIdCaption.Margin = new Padding(0, 8, 4, 0);
            PromptIdCaption.Name = "PromptIdCaption";
            PromptIdCaption.Size = new Size(69, 20);
            PromptIdCaption.TabIndex = 4;
            PromptIdCaption.Text = "Identifier";
            // 
            // PromptIdBox
            // 
            PromptIdBox.Font = new Font("Consolas", 5.76F);
            PromptIdBox.Location = new Point(385, 8);
            PromptIdBox.Margin = new Padding(0, 4, 8, 0);
            PromptIdBox.MaxLength = 8;
            PromptIdBox.Name = "PromptIdBox";
            PromptIdBox.TextChanged += PromptIdBox_TextChanged;
            PromptIdBox.Size = new Size(64, 19);
            PromptIdBox.TabIndex = 5;
            // 
            // InsertLineButton
            // 
            InsertLineButton.AutoSize = true;
            InsertLineButton.Location = new Point(457, 7);
            InsertLineButton.Margin = new Padding(0, 3, 4, 0);
            InsertLineButton.Name = "InsertLineButton";
            InsertLineButton.Click += InsertLineButton_Click;
            InsertLineButton.Size = new Size(83, 30);
            InsertLineButton.TabIndex = 6;
            InsertLineButton.Text = "Insert line";
            // 
            // CancelPromptButton
            // 
            CancelPromptButton.AutoSize = true;
            CancelPromptButton.Location = new Point(544, 7);
            CancelPromptButton.Margin = new Padding(0, 3, 4, 0);
            CancelPromptButton.Name = "CancelPromptButton";
            CancelPromptButton.Click += CancelPromptButton_Click;
            CancelPromptButton.Size = new Size(75, 30);
            CancelPromptButton.TabIndex = 7;
            CancelPromptButton.Text = "Cancel";
            // 
            // PromptErrorLabel
            // 
            PromptErrorLabel.AutoSize = true;
            PromptErrorLabel.ForeColor = Color.DarkRed;
            PromptErrorLabel.Location = new Point(623, 10);
            PromptErrorLabel.Margin = new Padding(0, 6, 0, 0);
            PromptErrorLabel.Name = "PromptErrorLabel";
            PromptErrorLabel.Size = new Size(0, 17);
            PromptErrorLabel.TabIndex = 8;
            PromptErrorLabel.Visible = false;
            // 
            // ElementPalette
            // 
            AutoScaleMode = AutoScaleMode.Inherit;
            Controls.Add(LayerTabs);
            Controls.Add(PromptPanel);
            Name = "ElementPalette";
            Size = new Size(800, 148);
            MotivationPage.ResumeLayout(false);
            StrategyPage.ResumeLayout(false);
            BusinessPage.ResumeLayout(false);
            ApplicationPage.ResumeLayout(false);
            TechnologyPage.ResumeLayout(false);
            ImplementationPage.ResumeLayout(false);
            CompositePage.ResumeLayout(false);
            JunctionPage.ResumeLayout(false);
            LayerTabs.ResumeLayout(false);
            PromptPanel.ResumeLayout(false);
            PromptPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private TabPage MotivationPage;
        private FlowLayoutPanel MotivationFlow;
        private TabPage StrategyPage;
        private FlowLayoutPanel StrategyFlow;
        private TabPage BusinessPage;
        private FlowLayoutPanel BusinessFlow;
        private TabPage ApplicationPage;
        private FlowLayoutPanel ApplicationFlow;
        private TabPage TechnologyPage;
        private FlowLayoutPanel TechnologyFlow;
        private TabPage ImplementationPage;
        private FlowLayoutPanel ImplementationFlow;
        private TabPage CompositePage;
        private FlowLayoutPanel CompositeFlow;
        private TabPage JunctionPage;
        private FlowLayoutPanel JunctionFlow;
        private TabControl LayerTabs;

        private FlowLayoutPanel PromptPanel;

        private Panel PromptGlyph;

        private Label PromptTypeLabel;

        private Label PromptLabelCaption;

        private TextBox PromptLabelBox;

        private Label PromptIdCaption;

        private TextBox PromptIdBox;

        private Button InsertLineButton;

        private Button CancelPromptButton;

        private Label PromptErrorLabel;
    }
}
