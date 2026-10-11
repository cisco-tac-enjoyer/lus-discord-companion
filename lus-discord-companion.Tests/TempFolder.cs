using System;
using System.IO;

namespace LusDiscordCompanion.Tests
{
    internal sealed class TempFolder : IDisposable
    {
        public static WatcherTimings FastTimings
        {
            get
            {
                return new WatcherTimings
                {
                    Rescan = TimeSpan.FromMilliseconds(200),
                    StableFor = TimeSpan.FromMilliseconds(300),
                    GiveUpAfter = TimeSpan.FromSeconds(10),
                    Poll = TimeSpan.FromMilliseconds(50),
                };
            }
        }

        public string Root { get; }
        public string Screenshots { get; }
        public string LedgerPath => Path.Combine(Root, "posted.json");

        public TempFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "lus-test-" + Guid.NewGuid().ToString("N"));
            Screenshots = Directory.CreateDirectory(Path.Combine(Root, "Screenshots")).FullName;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
