using System.Windows.Forms;
using Microsoft.Win32;

namespace LusDiscordCompanion
{
    public static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "lus-discord-companion";
        static readonly string[] LegacyValueNames = { "WoWLevelUpDiscord", "LevelUpScreenshot-Companion" };

        public static void Set(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                foreach (var legacy in LegacyValueNames)
                    key.DeleteValue(legacy, false);
                if (enabled)
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --minimized");
                else
                    key.DeleteValue(ValueName, false);
            }
        }
    }
}
