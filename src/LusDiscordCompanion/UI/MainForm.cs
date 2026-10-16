using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LusDiscordCompanion
{
    public partial class MainForm : Form
    {
        const int MaxLogLines = 200;
        const string BalloonTitle = "LUS Discord Companion";

        readonly ActivityLog _log = new ActivityLog(Path.Combine(AppSettings.DataDir, "log.txt"));
        readonly DiscordClient _discord = new DiscordClient();
        AppSettings _settings;
        ScreenshotWatcher _watcher;
        bool _allowShow;
        bool _exiting;
        bool _revertingOption;

        public MainForm(bool startMinimized)
        {
            InitializeComponent();

            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            trayIcon.Icon = Icon;
            trayIcon.Visible = true;

            _log.LineAdded += line => RunOnUi(() => AddLogLine(line));

            _settings = AppSettings.Load();
            LoadFields(_settings);
            chkStartup.CheckedChanged += ChkOptions_CheckedChanged;
            chkTray.CheckedChanged += ChkOptions_CheckedChanged;
            ShowConfigPath(_settings.SourcePath);
            txtExePath.Text = Application.ExecutablePath;
            if (_settings.StartWithWindows)
            {
                try
                {
                    Autostart.Set(true);
                }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException))
                        throw;
                }
            }

            _allowShow = !(startMinimized && _settings.MinimizeToTray);
            if (startMinimized && !_settings.MinimizeToTray)
                WindowState = FormWindowState.Minimized;
            if (!IsHandleCreated)
                CreateHandle();

            if (_settings.AutoStartWatching && _settings.Validate().Count == 0)
                StartWatching();
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(_allowShow && value);
        }

        public void ShowFromTray()
        {
            _allowShow = true;
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        void AddLogLine(string line)
        {
            lstLog.Items.Add(line);
            while (lstLog.Items.Count > MaxLogLines)
                lstLog.Items.RemoveAt(0);
            lstLog.TopIndex = lstLog.Items.Count - 1;
        }

        void ShowBalloon(string text, ToolTipIcon icon)
        {
            RunOnUi(() => trayIcon.ShowBalloonTip(icon == ToolTipIcon.Error ? 5000 : 3000, BalloonTitle, text, icon));
        }

        void LoadFields(AppSettings s)
        {
            txtWebhook.Text = s.WebhookUrl;
            txtFolder.Text = s.ScreenshotsFolder;
            txtBotName.Text = s.BotName;
            var index = Array.IndexOf(AppSettings.Formats, s.LevelUpFormat);
            cmbFormat.SelectedIndex = index < 0 ? 0 : index;
            chkStartup.Checked = s.StartWithWindows;
            chkTray.Checked = s.MinimizeToTray;
        }

        void ShowConfigPath(string loadedFrom)
        {
            if (loadedFrom == null)
                txtConfigPath.Text = AppSettings.DefaultPath + "  (not saved yet)";
            else if (loadedFrom != AppSettings.DefaultPath)
                txtConfigPath.Text = loadedFrom + "  (old version, press Save to move it to " + AppSettings.DefaultPath + ")";
            else
                txtConfigPath.Text = loadedFrom;
        }

        void BtnOpenConfig_Click(object sender, EventArgs e)
        {
            var path = File.Exists(AppSettings.DefaultPath) ? AppSettings.DefaultPath : _settings.SourcePath;
            try
            {
                if (path != null && File.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + Directory.CreateDirectory(AppSettings.DataDir).FullName + "\"");
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.ComponentModel.Win32Exception))
                    throw;
                MessageBox.Show(this, "Could not open folder: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void BtnOpenExe_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Application.ExecutablePath + "\"");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(this, "Could not open folder: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // The checkboxes stay usable while watching (when Save is disabled), so they are saved on change.
        void ChkOptions_CheckedChanged(object sender, EventArgs e)
        {
            if (_revertingOption)
                return;

            var candidate = new AppSettings
            {
                WebhookUrl = _settings.WebhookUrl,
                ScreenshotsFolder = _settings.ScreenshotsFolder,
                BotName = _settings.BotName,
                LevelUpFormat = _settings.LevelUpFormat,
                StartWithWindows = chkStartup.Checked,
                MinimizeToTray = chkTray.Checked,
                AutoStartWatching = _settings.AutoStartWatching,
            };

            try
            {
                candidate.Save();
                Autostart.Set(candidate.StartWithWindows);
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException))
                    throw;
                MessageBox.Show(this, "Could not save settings: " + AppSettings.Redact(ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                _revertingOption = true;
                chkStartup.Checked = _settings.StartWithWindows;
                chkTray.Checked = _settings.MinimizeToTray;
                _revertingOption = false;
                return;
            }

            _settings = candidate;
            ShowConfigPath(AppSettings.DefaultPath);
            _log.Write("Settings saved.");
        }

        AppSettings ReadFields()
        {
            return new AppSettings
            {
                WebhookUrl = txtWebhook.Text.Trim(),
                ScreenshotsFolder = txtFolder.Text.Trim().Trim('"'),
                BotName = txtBotName.Text.Trim(),
                LevelUpFormat = AppSettings.Formats[Math.Max(cmbFormat.SelectedIndex, 0)],
                StartWithWindows = chkStartup.Checked,
                MinimizeToTray = chkTray.Checked,
                AutoStartWatching = _settings.AutoStartWatching,
            };
        }

        bool SaveFields()
        {
            var candidate = ReadFields();
            var errors = candidate.Validate();
            if (errors.Count > 0)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, errors), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                candidate.Save();
                Autostart.Set(candidate.StartWithWindows);
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException))
                    throw;
                MessageBox.Show(this, "Could not save settings: " + AppSettings.Redact(ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            _settings = candidate;
            ShowConfigPath(AppSettings.DefaultPath);
            _log.Write("Settings saved.");
            return true;
        }

        void StartWatching()
        {
            if (_watcher != null)
                return;

            var settings = _settings;
            var watcher = new ScreenshotWatcher(settings.ScreenshotsFolder, settings.LevelUpFormat, PostedLedger.Load(),
                async (path, ct) =>
                {
                    var image = await Task.Run(() => ImageTools.Prepare(path), ct).ConfigureAwait(false);
                    await _discord.SendFileAsync(settings.WebhookUrl, settings.BotName, image.FileName, image.Data, ct).ConfigureAwait(false);
                });

            watcher.Posted += name =>
            {
                _log.Write("Posted " + name);
                ShowBalloon("Posted " + name, ToolTipIcon.Info);
            };
            watcher.Failed += (name, ex) =>
            {
                _log.Write("Failed to post " + name + ": " + ex.Message);
                ShowBalloon("Upload failed: " + name, ToolTipIcon.Error);
            };
            watcher.WebhookInvalid += () => RunOnUi(() =>
            {
                StopWatching();
                _log.Write("Webhook is invalid or was deleted. Watching stopped.");
                trayIcon.ShowBalloonTip(5000, BalloonTitle, "Webhook is invalid or was deleted", ToolTipIcon.Error);
                if (Visible)
                    MessageBox.Show(this, "Webhook is invalid or was deleted", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            });

            try
            {
                watcher.Start();
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException))
                    throw;
                watcher.Dispose();
                _log.Write("Could not start watching: " + ex.Message);
                return;
            }

            _watcher = watcher;
            _log.Write("Watching " + settings.ScreenshotsFolder + " for *." + settings.LevelUpFormat);
            UpdateWatchingState();
        }

        void StopWatching()
        {
            if (_watcher == null)
                return;
            _watcher.Dispose();
            _watcher = null;
            _log.Write("Stopped watching.");
            UpdateWatchingState();
        }

        void UpdateWatchingState()
        {
            bool watching = _watcher != null;
            foreach (var c in new Control[] { txtWebhook, txtFolder, btnBrowse, btnDetect, txtBotName, cmbFormat, btnSave })
                c.Enabled = !watching;
            btnWatch.Text = watching ? "\u25A0 Stop watching" : "\u25B6 Start watching";
            trayPause.Text = watching ? "Pause" : "Resume";
        }

        void BtnShow_Click(object sender, EventArgs e)
        {
            txtWebhook.UseSystemPasswordChar = !txtWebhook.UseSystemPasswordChar;
            btnShow.Text = txtWebhook.UseSystemPasswordChar ? "Show" : "Hide";
        }

        void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select the WoW Screenshots folder" })
            {
                if (Directory.Exists(txtFolder.Text))
                    dialog.SelectedPath = txtFolder.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtFolder.Text = dialog.SelectedPath;
            }
        }

        void BtnDetect_Click(object sender, EventArgs e)
        {
            var found = WowFolderDetector.Detect();
            if (found.Count == 0)
            {
                MessageBox.Show(this, "No WoW Screenshots folder found. Take a screenshot in game once, or use Browse\u2026", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var choice = found.Count == 1 ? found[0] : PickFolder(found);
            if (choice != null)
                txtFolder.Text = choice;
        }

        string PickFolder(List<string> folders)
        {
            using (var picker = new Form
            {
                AutoScaleDimensions = new SizeF(96F, 96F),
                AutoScaleMode = AutoScaleMode.Dpi,
                Font = SystemFonts.MessageBoxFont,
                Text = "Choose Screenshots folder",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(560, 260),
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false,
            })
            {
                var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
                list.Items.AddRange(folders.Cast<object>().ToArray());
                list.SelectedIndex = 0;
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
                list.DoubleClick += (s, e) => picker.DialogResult = DialogResult.OK;
                picker.Controls.Add(list);
                picker.Controls.Add(ok);
                picker.AcceptButton = ok;
                return picker.ShowDialog(this) == DialogResult.OK ? list.SelectedItem as string : null;
            }
        }

        void BtnSave_Click(object sender, EventArgs e)
        {
            SaveFields();
        }

        async void BtnTest_Click(object sender, EventArgs e)
        {
            var url = txtWebhook.Text.Trim();
            var botName = txtBotName.Text.Trim();
            if (!AppSettings.IsValidWebhook(url))
            {
                MessageBox.Show(this, "Webhook URL is not a valid Discord webhook URL.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var botError = AppSettings.ValidateBotName(botName);
            if (botError != null)
            {
                MessageBox.Show(this, botError, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTest.Enabled = false;
            string error = null;
            try
            {
                await _discord.SendTextAsync(url, botName, DiscordClient.TestMessage);
            }
            catch (Exception ex)
            {
                error = AppSettings.Redact(ex.Message);
            }
            btnTest.Enabled = true;

            if (error == null)
            {
                _log.Write("Test message sent.");
                MessageBox.Show(this, "Test message sent.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _log.Write("Test message failed: " + error);
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void BtnWatch_Click(object sender, EventArgs e)
        {
            if (_watcher != null)
                StopWatching();
            else if (SaveFields())
                StartWatching();
        }

        void TrayPause_Click(object sender, EventArgs e)
        {
            if (_watcher != null)
            {
                StopWatching();
            }
            else if (_settings.Validate().Count == 0)
            {
                StartWatching();
            }
            else
            {
                ShowFromTray();
                _log.Write("Settings are incomplete. Fill in the fields and press Start watching.");
            }
        }

        void TrayExit_Click(object sender, EventArgs e)
        {
            _exiting = true;
            StopWatching();
            trayIcon.Visible = false;
            Application.Exit();
        }

        void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && chkTray.Checked)
                Hide();
        }

        void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_exiting && e.CloseReason == CloseReason.UserClosing && chkTray.Checked)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            StopWatching();
            trayIcon.Visible = false;
        }
    }
}
