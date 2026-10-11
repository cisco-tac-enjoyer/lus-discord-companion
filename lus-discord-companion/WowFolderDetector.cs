using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;

namespace LusDiscordCompanion
{
    public static class WowFolderDetector
    {
        static readonly string[] Flavors = { "_retail_", "_classic_", "_classic_era_", "_anniversary_" };

        public static List<string> Detect()
        {
            var roots = new List<string>();

            try
            {
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = hive.OpenSubKey(@"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft"))
                {
                    var installPath = key == null ? null : key.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(installPath))
                    {
                        var dir = new DirectoryInfo(installPath.TrimEnd('\\', '/'));
                        roots.Add(dir.Name.StartsWith("_") && dir.Parent != null ? dir.Parent.FullName : dir.FullName);
                    }
                }
            }
            catch (SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            catch (ArgumentException)
            {
            }

            roots.Add(@"C:\Program Files (x86)\World of Warcraft");
            roots.Add(@"C:\Program Files\World of Warcraft");

            return roots
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .SelectMany(root => Flavors.Select(flavor => Path.Combine(root, flavor, "Screenshots")))
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
