using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LusDiscordCompanion
{
    public sealed class AppSettings
    {
        public static readonly string[] Formats = { "png", "jpg", "tga" };

        static readonly Regex WebhookRegex = new Regex(
            @"^https://(?:(?:ptb|canary)\.)?discord(?:app)?\.com/api/webhooks/\d+/[\w-]+$",
            RegexOptions.Compiled);

        static readonly Regex WebhookInTextRegex = new Regex(
            @"https?://\S*discord(?:app)?\.com/api/webhooks/\S+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string WebhookUrl { get; set; }
        public string ScreenshotsFolder { get; set; }
        public string BotName { get; set; }
        public string LevelUpFormat { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool AutoStartWatching { get; set; }

        // File the values were read from (settings.json or the legacy file), or null when defaults were used.
        public string SourcePath { get; private set; }

        public AppSettings()
        {
            WebhookUrl = "";
            ScreenshotsFolder = "";
            BotName = "LevelUpScreenshot";
            LevelUpFormat = "png";
            MinimizeToTray = true;
            AutoStartWatching = true;
        }

        public static string DataDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lus-discord-companion");
            }
        }

        static string PreviousDataDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LevelUpScreenshot-Companion");
            }
        }

        public static string DefaultPath
        {
            get { return Path.Combine(DataDir, "settings.json"); }
        }

        static string LegacyPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WoWLevelUpDiscord", "settings.json");
            }
        }

        public static AppSettings Load(string path = null)
        {
            if (path == null)
                MigratePreviousDataDir();
            var settings = new AppSettings();
            var values = ReadJson(path ?? DefaultPath);
            if (values != null)
            {
                settings.SourcePath = path ?? DefaultPath;
                settings.WebhookUrl = GetString(values, "webhookUrl", settings.WebhookUrl);
                settings.ScreenshotsFolder = GetString(values, "screenshotsFolder", settings.ScreenshotsFolder);
                settings.BotName = GetString(values, "botName", settings.BotName);
                settings.LevelUpFormat = GetString(values, "levelUpFormat", settings.LevelUpFormat).ToLowerInvariant();
                settings.StartWithWindows = GetBool(values, "startWithWindows", settings.StartWithWindows);
                settings.MinimizeToTray = GetBool(values, "minimizeToTray", settings.MinimizeToTray);
                settings.AutoStartWatching = GetBool(values, "autoStartWatching", settings.AutoStartWatching);
            }
            else if (path == null)
            {
                var legacy = ReadJson(LegacyPath);
                if (legacy != null)
                {
                    settings.SourcePath = LegacyPath;
                    settings.WebhookUrl = Regex.Replace(GetString(legacy, "WebhookUrl", ""), @"\s", "");
                    settings.ScreenshotsFolder = GetString(legacy, "ScreenshotsFolder", "");
                }
            }
            return settings;
        }

        public void Save(string path = null)
        {
            var values = new Dictionary<string, object>
            {
                { "webhookUrl", WebhookUrl },
                { "screenshotsFolder", ScreenshotsFolder },
                { "botName", BotName },
                { "levelUpFormat", LevelUpFormat },
                { "startWithWindows", StartWithWindows },
                { "minimizeToTray", MinimizeToTray },
                { "autoStartWatching", AutoStartWatching },
            };
            AtomicFile.WriteAllText(path ?? DefaultPath, Json.Serialize(values));
        }

        static void MigratePreviousDataDir()
        {
            if (File.Exists(DefaultPath) || !Directory.Exists(PreviousDataDir))
                return;
            try
            {
                Directory.CreateDirectory(DataDir);
                foreach (var name in new[] { "settings.json", "posted.json" })
                {
                    var source = Path.Combine(PreviousDataDir, name);
                    if (File.Exists(source))
                        File.Copy(source, Path.Combine(DataDir, name), true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        static Dictionary<string, object> ReadJson(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                return Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        static string GetString(Dictionary<string, object> values, string key, string fallback)
        {
            object value;
            return values.TryGetValue(key, out value) && value is string ? (string)value : fallback;
        }

        static bool GetBool(Dictionary<string, object> values, string key, bool fallback)
        {
            object value;
            return values.TryGetValue(key, out value) && value is bool ? (bool)value : fallback;
        }

        public static JavaScriptSerializer Json
        {
            get { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }; }
        }

        public static bool IsValidWebhook(string url)
        {
            return url != null && WebhookRegex.IsMatch(url.Trim());
        }

        public static string ValidateBotName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length < 1 || name.Length > 80)
                return "Bot name must be 1\u201380 characters.";
            if (name.IndexOf("discord", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("clyde", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Bot name must not contain \"discord\" or \"clyde\".";
            return null;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (!IsValidWebhook(WebhookUrl))
                errors.Add("Webhook URL is not a valid Discord webhook URL.");
            if (string.IsNullOrWhiteSpace(ScreenshotsFolder) || !Directory.Exists(ScreenshotsFolder))
                errors.Add("Screenshots folder does not exist.");
            var botError = ValidateBotName(BotName);
            if (botError != null)
                errors.Add(botError);
            if (!Formats.Contains(LevelUpFormat))
                errors.Add("Level-up format must be png, jpg or tga.");
            return errors;
        }

        public static string Redact(string text)
        {
            return text == null ? "" : WebhookInTextRegex.Replace(text, "[webhook]");
        }
    }

    internal static class AtomicFile
    {
        public static void WriteAllText(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temp = path + ".tmp";
            File.WriteAllText(temp, contents, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
    }
}
