using System;
using System.Drawing;
using System.Windows.Forms;

namespace LusDiscordCompanion
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components;
        private TableLayoutPanel layout;
        private TextBox txtWebhook;
        private Button btnShow;
        private TextBox txtFolder;
        private Button btnBrowse;
        private Button btnDetect;
        private TextBox txtBotName;
        private ComboBox cmbFormat;
        private CheckBox chkStartup;
        private CheckBox chkTray;
        private Button btnSave;
        private Button btnTest;
        private Button btnWatch;
        private ListBox lstLog;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayOpen;
        private ToolStripMenuItem trayPause;
        private ToolStripMenuItem trayExit;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 6, 12, 6),
            };
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layout = new TableLayoutPanel();
            txtWebhook = new TextBox();
            btnShow = new Button();
            txtFolder = new TextBox();
            btnBrowse = new Button();
            btnDetect = new Button();
            txtBotName = new TextBox();
            cmbFormat = new ComboBox();
            chkStartup = new CheckBox();
            chkTray = new CheckBox();
            btnSave = new Button();
            btnTest = new Button();
            btnWatch = new Button();
            lstLog = new ListBox();
            trayIcon = new NotifyIcon(components);
            trayMenu = new ContextMenuStrip(components);
            trayOpen = new ToolStripMenuItem();
            trayPause = new ToolStripMenuItem();
            trayExit = new ToolStripMenuItem();

            SuspendLayout();
            layout.SuspendLayout();

            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(10);
            layout.ColumnCount = 4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowCount = 8;
            for (int i = 0; i < 7; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            txtWebhook.Dock = DockStyle.Fill;
            txtWebhook.UseSystemPasswordChar = true;
            btnShow.Text = "Show";
            btnShow.AutoSize = true;
            btnShow.Click += BtnShow_Click;
            layout.Controls.Add(FieldLabel("Discord webhook URL"), 0, 0);
            layout.Controls.Add(txtWebhook, 1, 0);
            layout.SetColumnSpan(txtWebhook, 2);
            layout.Controls.Add(btnShow, 3, 0);

            txtFolder.Dock = DockStyle.Fill;
            btnBrowse.Text = "Browse\u2026";
            btnBrowse.AutoSize = true;
            btnBrowse.Click += BtnBrowse_Click;
            btnDetect.Text = "Auto-detect";
            btnDetect.AutoSize = true;
            btnDetect.Click += BtnDetect_Click;
            layout.Controls.Add(FieldLabel("Screenshots folder"), 0, 1);
            layout.Controls.Add(txtFolder, 1, 1);
            layout.Controls.Add(btnBrowse, 2, 1);
            layout.Controls.Add(btnDetect, 3, 1);

            txtBotName.Dock = DockStyle.Fill;
            txtBotName.MaxLength = 80;
            layout.Controls.Add(FieldLabel("Bot name"), 0, 2);
            layout.Controls.Add(txtBotName, 1, 2);
            layout.SetColumnSpan(txtBotName, 3);

            cmbFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFormat.Items.AddRange(new object[] { "PNG", "JPG", "TGA" });
            cmbFormat.Width = 80;
            var formatRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            formatRow.Controls.Add(cmbFormat);
            formatRow.Controls.Add(new Label
            {
                Text = "(must match /lus format in game)",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(8, 6, 3, 3),
            });
            layout.Controls.Add(FieldLabel("Level-up format"), 0, 3);
            layout.Controls.Add(formatRow, 1, 3);
            layout.SetColumnSpan(formatRow, 3);

            chkStartup.Text = "Start with Windows";
            chkStartup.AutoSize = true;
            chkTray.Text = "Minimize to tray";
            chkTray.AutoSize = true;
            chkTray.Margin = new Padding(24, 3, 3, 3);
            var optionsRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 6) };
            optionsRow.Controls.Add(chkStartup);
            optionsRow.Controls.Add(chkTray);
            layout.Controls.Add(optionsRow, 0, 4);
            layout.SetColumnSpan(optionsRow, 4);

            btnSave.Text = "Save";
            btnSave.AutoSize = true;
            btnSave.Click += BtnSave_Click;
            btnTest.Text = "Send test message";
            btnTest.AutoSize = true;
            btnTest.Click += BtnTest_Click;
            btnWatch.Text = "\u25B6 Start watching";
            btnWatch.AutoSize = true;
            btnWatch.Click += BtnWatch_Click;
            var buttonRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
            buttonRow.Controls.Add(btnSave);
            buttonRow.Controls.Add(btnTest);
            buttonRow.Controls.Add(btnWatch);
            layout.Controls.Add(buttonRow, 0, 5);
            layout.SetColumnSpan(buttonRow, 4);

            var activityLabel = FieldLabel("Activity");
            layout.Controls.Add(activityLabel, 0, 6);
            layout.SetColumnSpan(activityLabel, 4);

            lstLog.Dock = DockStyle.Fill;
            lstLog.IntegralHeight = false;
            lstLog.HorizontalScrollbar = true;
            layout.Controls.Add(lstLog, 0, 7);
            layout.SetColumnSpan(lstLog, 4);

            trayOpen.Text = "Open";
            trayOpen.Click += (s, e) => ShowFromTray();
            trayPause.Text = "Pause";
            trayPause.Click += TrayPause_Click;
            trayExit.Text = "Exit";
            trayExit.Click += TrayExit_Click;
            trayMenu.Items.AddRange(new ToolStripItem[] { trayOpen, trayPause, trayExit });
            trayIcon.Text = "LUS Discord Companion";
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += (s, e) => ShowFromTray();

            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(760, 480);
            MinimumSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "LUS Discord Companion";
            Controls.Add(layout);
            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;

            layout.ResumeLayout(false);
            layout.PerformLayout();
            ResumeLayout(false);
        }
    }
}
