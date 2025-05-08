namespace Core.VGV
{
    partial class VGVEngine
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Pn_Background = new Panel();
            TBarCoarse = new TrackBar();
            TBarFine = new TrackBar();
            RTBxLogs = new RichTextBox();
            Pn_RTBxLogs = new Panel();
            Pn_Views = new Panel();
            GLViewMain = new Axone.Engine.GLView();
            PnSidebar = new Panel();
            BtnOnColor = new Button();
            TBxOnColor = new TextBox();
            BtnGenerate = new Button();
            PnSettingBox = new Panel();
            BtnNone06 = new Button();
            BtnNone04 = new Button();
            BtnNone03 = new Button();
            BtnNone05 = new Button();
            BtnNone02 = new Button();
            BtnNone01 = new Button();
            btnMenuG01 = new Button();
            CBxModel = new ComboBox();
            Pn_Title = new PictureBox();
            BtnClose = new Button();
            Pn_Background.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)TBarCoarse).BeginInit();
            ((System.ComponentModel.ISupportInitialize)TBarFine).BeginInit();
            Pn_Views.SuspendLayout();
            PnSidebar.SuspendLayout();
            PnSettingBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)Pn_Title).BeginInit();
            SuspendLayout();
            // 
            // Pn_Background
            // 
            Pn_Background.BackColor = SystemColors.InactiveBorder;
            Pn_Background.Controls.Add(TBarCoarse);
            Pn_Background.Controls.Add(TBarFine);
            Pn_Background.Controls.Add(RTBxLogs);
            Pn_Background.Controls.Add(Pn_RTBxLogs);
            Pn_Background.Controls.Add(Pn_Views);
            Pn_Background.Controls.Add(PnSidebar);
            Pn_Background.Location = new Point(3, 3);
            Pn_Background.Name = "Pn_Background";
            Pn_Background.Size = new Size(980, 660);
            Pn_Background.TabIndex = 1;
            // 
            // TBarCoarse
            // 
            TBarCoarse.Location = new Point(5, 579);
            TBarCoarse.Name = "TBarCoarse";
            TBarCoarse.Size = new Size(655, 45);
            TBarCoarse.TabIndex = 6;
            TBarCoarse.Scroll += bar_Scroll;
            // 
            // TBarFine
            // 
            TBarFine.Location = new Point(5, 539);
            TBarFine.Maximum = 100;
            TBarFine.Name = "TBarFine";
            TBarFine.Size = new Size(655, 45);
            TBarFine.TabIndex = 5;
            TBarFine.Scroll += bar_Scroll;
            // 
            // RTBxLogs
            // 
            RTBxLogs.BorderStyle = BorderStyle.None;
            RTBxLogs.Location = new Point(8, 13);
            RTBxLogs.Name = "RTBxLogs";
            RTBxLogs.Size = new Size(233, 505);
            RTBxLogs.TabIndex = 3;
            RTBxLogs.Text = "";
            // 
            // Pn_RTBxLogs
            // 
            Pn_RTBxLogs.BackColor = Color.FromArgb(40, 45, 50);
            Pn_RTBxLogs.Location = new Point(5, 10);
            Pn_RTBxLogs.Name = "Pn_RTBxLogs";
            Pn_RTBxLogs.Size = new Size(239, 511);
            Pn_RTBxLogs.TabIndex = 4;
            // 
            // Pn_Views
            // 
            Pn_Views.BackColor = SystemColors.ActiveCaption;
            Pn_Views.Controls.Add(GLViewMain);
            Pn_Views.Location = new Point(250, 10);
            Pn_Views.Name = "Pn_Views";
            Pn_Views.Size = new Size(510, 510);
            Pn_Views.TabIndex = 2;
            // 
            // GLViewMain
            // 
            GLViewMain.API = OpenTK.Windowing.Common.ContextAPI.OpenGL;
            GLViewMain.Flags = OpenTK.Windowing.Common.ContextFlags.Default;
            GLViewMain.IsEventDriven = true;
            GLViewMain.Location = new Point(5, 5);
            GLViewMain.Name = "GLViewMain";
            GLViewMain.Profile = OpenTK.Windowing.Common.ContextProfile.Core;
            GLViewMain.SharedContext = null;
            GLViewMain.Size = new Size(500, 500);
            GLViewMain.TabIndex = 0;
            // 
            // PnSidebar
            // 
            PnSidebar.BackColor = Color.FromArgb(40, 45, 50);
            PnSidebar.Controls.Add(BtnOnColor);
            PnSidebar.Controls.Add(TBxOnColor);
            PnSidebar.Controls.Add(BtnGenerate);
            PnSidebar.Controls.Add(PnSettingBox);
            PnSidebar.Controls.Add(CBxModel);
            PnSidebar.Controls.Add(Pn_Title);
            PnSidebar.Controls.Add(BtnClose);
            PnSidebar.Font = new Font("UD Digi Kyokasho N", 10.5F);
            PnSidebar.Location = new Point(770, 0);
            PnSidebar.Name = "PnSidebar";
            PnSidebar.Size = new Size(210, 660);
            PnSidebar.TabIndex = 1;
            // 
            // BtnOnColor
            // 
            BtnOnColor.BackColor = Color.FromArgb(130, 135, 140);
            BtnOnColor.FlatAppearance.BorderSize = 0;
            BtnOnColor.FlatStyle = FlatStyle.Flat;
            BtnOnColor.Font = new Font("UD Digi Kyokasho N", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 128);
            BtnOnColor.ForeColor = SystemColors.HighlightText;
            BtnOnColor.Location = new Point(105, 344);
            BtnOnColor.Name = "BtnOnColor";
            BtnOnColor.Size = new Size(96, 35);
            BtnOnColor.TabIndex = 28;
            BtnOnColor.Text = "Heatmap";
            BtnOnColor.UseVisualStyleBackColor = false;
            // 
            // TBxOnColor
            // 
            TBxOnColor.BorderStyle = BorderStyle.FixedSingle;
            TBxOnColor.Font = new Font("Yu Gothic UI", 15.75F, FontStyle.Regular, GraphicsUnit.Point, 128);
            TBxOnColor.Location = new Point(7, 344);
            TBxOnColor.Name = "TBxOnColor";
            TBxOnColor.Size = new Size(96, 35);
            TBxOnColor.TabIndex = 29;
            // 
            // BtnGenerate
            // 
            BtnGenerate.BackColor = Color.OliveDrab;
            BtnGenerate.FlatAppearance.BorderSize = 0;
            BtnGenerate.FlatStyle = FlatStyle.Flat;
            BtnGenerate.Font = new Font("Microsoft Sans Serif", 12F);
            BtnGenerate.ForeColor = SystemColors.ButtonFace;
            BtnGenerate.Location = new Point(0, 524);
            BtnGenerate.Margin = new Padding(2);
            BtnGenerate.Name = "BtnGenerate";
            BtnGenerate.Size = new Size(210, 41);
            BtnGenerate.TabIndex = 11;
            BtnGenerate.Tag = "C:\\Dataset\\TestDatas\\Mapping-Master_20231225.kzv";
            BtnGenerate.Text = "Generate";
            BtnGenerate.UseVisualStyleBackColor = false;
            BtnGenerate.Click += btnOpen_Click;
            // 
            // PnSettingBox
            // 
            PnSettingBox.Controls.Add(BtnNone06);
            PnSettingBox.Controls.Add(BtnNone04);
            PnSettingBox.Controls.Add(BtnNone03);
            PnSettingBox.Controls.Add(BtnNone05);
            PnSettingBox.Controls.Add(BtnNone02);
            PnSettingBox.Controls.Add(BtnNone01);
            PnSettingBox.Controls.Add(btnMenuG01);
            PnSettingBox.Location = new Point(0, 90);
            PnSettingBox.Margin = new Padding(2);
            PnSettingBox.MaximumSize = new Size(210, 222);
            PnSettingBox.MinimumSize = new Size(210, 40);
            PnSettingBox.Name = "PnSettingBox";
            PnSettingBox.Size = new Size(210, 222);
            PnSettingBox.TabIndex = 10;
            // 
            // BtnNone06
            // 
            BtnNone06.BackColor = Color.FromArgb(50, 55, 60);
            BtnNone06.FlatAppearance.BorderSize = 0;
            BtnNone06.FlatStyle = FlatStyle.Flat;
            BtnNone06.Font = new Font("Microsoft Sans Serif", 12F);
            BtnNone06.ForeColor = SystemColors.ButtonFace;
            BtnNone06.Location = new Point(0, 191);
            BtnNone06.Margin = new Padding(2);
            BtnNone06.Name = "BtnNone06";
            BtnNone06.Size = new Size(210, 30);
            BtnNone06.TabIndex = 12;
            BtnNone06.Text = "none";
            BtnNone06.UseVisualStyleBackColor = false;
            // 
            // BtnNone04
            // 
            BtnNone04.BackColor = Color.FromArgb(50, 55, 60);
            BtnNone04.FlatAppearance.BorderSize = 0;
            BtnNone04.FlatStyle = FlatStyle.Flat;
            BtnNone04.Font = new Font("Microsoft Sans Serif", 12F);
            BtnNone04.ForeColor = SystemColors.ButtonFace;
            BtnNone04.Location = new Point(0, 131);
            BtnNone04.Margin = new Padding(2);
            BtnNone04.Name = "BtnNone04";
            BtnNone04.Size = new Size(210, 30);
            BtnNone04.TabIndex = 11;
            BtnNone04.Text = "none";
            BtnNone04.UseVisualStyleBackColor = false;
            // 
            // BtnNone03
            // 
            BtnNone03.BackColor = Color.FromArgb(50, 55, 60);
            BtnNone03.FlatAppearance.BorderSize = 0;
            BtnNone03.FlatStyle = FlatStyle.Flat;
            BtnNone03.Font = new Font("Microsoft Sans Serif", 12F);
            BtnNone03.ForeColor = SystemColors.ButtonFace;
            BtnNone03.Location = new Point(0, 101);
            BtnNone03.Margin = new Padding(2);
            BtnNone03.Name = "BtnNone03";
            BtnNone03.Size = new Size(210, 30);
            BtnNone03.TabIndex = 10;
            BtnNone03.Text = "none";
            BtnNone03.UseVisualStyleBackColor = false;
            // 
            // BtnNone05
            // 
            BtnNone05.BackColor = Color.FromArgb(50, 55, 60);
            BtnNone05.FlatAppearance.BorderSize = 0;
            BtnNone05.FlatStyle = FlatStyle.Flat;
            BtnNone05.Font = new Font("Microsoft Sans Serif", 12F);
            BtnNone05.ForeColor = SystemColors.ButtonFace;
            BtnNone05.Location = new Point(0, 161);
            BtnNone05.Margin = new Padding(2);
            BtnNone05.Name = "BtnNone05";
            BtnNone05.Size = new Size(210, 30);
            BtnNone05.TabIndex = 9;
            BtnNone05.Text = "none";
            BtnNone05.UseVisualStyleBackColor = false;
            // 
            // BtnNone02
            // 
            BtnNone02.BackColor = Color.FromArgb(50, 55, 60);
            BtnNone02.FlatAppearance.BorderSize = 0;
            BtnNone02.FlatStyle = FlatStyle.Flat;
            BtnNone02.Font = new Font("Microsoft Sans Serif", 12F);
            BtnNone02.ForeColor = SystemColors.ButtonFace;
            BtnNone02.Location = new Point(0, 71);
            BtnNone02.Margin = new Padding(2);
            BtnNone02.Name = "BtnNone02";
            BtnNone02.Size = new Size(210, 30);
            BtnNone02.TabIndex = 7;
            BtnNone02.Text = "none";
            BtnNone02.UseVisualStyleBackColor = false;
            // 
            // BtnNone01
            // 
            BtnNone01.BackColor = Color.FromArgb(50, 55, 60);
            BtnNone01.FlatAppearance.BorderSize = 0;
            BtnNone01.FlatStyle = FlatStyle.Flat;
            BtnNone01.Font = new Font("Microsoft Sans Serif", 12F);
            BtnNone01.ForeColor = SystemColors.ButtonFace;
            BtnNone01.Location = new Point(0, 41);
            BtnNone01.Margin = new Padding(2);
            BtnNone01.Name = "BtnNone01";
            BtnNone01.Size = new Size(210, 30);
            BtnNone01.TabIndex = 2;
            BtnNone01.Text = "CpuCheck";
            BtnNone01.UseVisualStyleBackColor = false;
            BtnNone01.Click += BtnNone01_Click;
            // 
            // btnMenuG01
            // 
            btnMenuG01.BackColor = Color.FromArgb(25, 30, 35);
            btnMenuG01.FlatAppearance.BorderSize = 0;
            btnMenuG01.FlatStyle = FlatStyle.Flat;
            btnMenuG01.Font = new Font("Microsoft Sans Serif", 12F);
            btnMenuG01.ForeColor = SystemColors.ButtonFace;
            btnMenuG01.Location = new Point(0, 0);
            btnMenuG01.Margin = new Padding(2);
            btnMenuG01.Name = "btnMenuG01";
            btnMenuG01.Size = new Size(210, 40);
            btnMenuG01.TabIndex = 0;
            btnMenuG01.Text = "Debugger";
            btnMenuG01.UseVisualStyleBackColor = false;
            btnMenuG01.Click += btnMenuG01_Click;
            // 
            // CBxModel
            // 
            CBxModel.DropDownStyle = ComboBoxStyle.DropDownList;
            CBxModel.FlatStyle = FlatStyle.Flat;
            CBxModel.Font = new Font("UD Digi Kyokasho N", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 128);
            CBxModel.FormattingEnabled = true;
            CBxModel.Items.AddRange(new object[] { "None" });
            CBxModel.Location = new Point(41, 32);
            CBxModel.Margin = new Padding(2);
            CBxModel.Name = "CBxModel";
            CBxModel.Size = new Size(130, 24);
            CBxModel.TabIndex = 9;
            // 
            // Pn_Title
            // 
            Pn_Title.BackColor = Color.FromArgb(206, 206, 206);
            Pn_Title.Location = new Point(0, 20);
            Pn_Title.Margin = new Padding(2);
            Pn_Title.Name = "Pn_Title";
            Pn_Title.Size = new Size(210, 50);
            Pn_Title.TabIndex = 8;
            Pn_Title.TabStop = false;
            // 
            // BtnClose
            // 
            BtnClose.BackColor = Color.DarkRed;
            BtnClose.FlatStyle = FlatStyle.Flat;
            BtnClose.Font = new Font("楷体", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            BtnClose.ForeColor = SystemColors.ButtonHighlight;
            BtnClose.Location = new Point(-3, 590);
            BtnClose.Margin = new Padding(2);
            BtnClose.Name = "BtnClose";
            BtnClose.Size = new Size(216, 50);
            BtnClose.TabIndex = 7;
            BtnClose.Text = "❌";
            BtnClose.UseVisualStyleBackColor = false;
            // 
            // VGVEngine
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(986, 666);
            Controls.Add(Pn_Background);
            FormBorderStyle = FormBorderStyle.None;
            Name = "VGVEngine";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "VGVEngine";
            Load += VGVEngine_Load;
            Pn_Background.ResumeLayout(false);
            Pn_Background.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)TBarCoarse).EndInit();
            ((System.ComponentModel.ISupportInitialize)TBarFine).EndInit();
            Pn_Views.ResumeLayout(false);
            PnSidebar.ResumeLayout(false);
            PnSidebar.PerformLayout();
            PnSettingBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)Pn_Title).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private Panel Pn_Background;
        private Panel PnSidebar;
        private ComboBox CBxModel;
        private PictureBox Pn_Title;
        private Button BtnClose;
        private Panel Pn_Views;
        private Axone.Engine.GLView GLViewMain;
        private Button BtnGenerate;
        private Panel PnSettingBox;
        private Button BtnNone06;
        private Button BtnNone04;
        private Button BtnNone03;
        private Button BtnNone05;
        private Button BtnNone02;
        private Button BtnNone01;
        private Button btnMenuG01;
        private RichTextBox RTBxLogs;
        private Panel Pn_RTBxLogs;
        private TrackBar TBarCoarse;
        private TrackBar TBarFine;
        private Button BtnOnColor;
        private TextBox TBxOnColor;
    }
}