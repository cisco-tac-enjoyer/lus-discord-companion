using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LusDiscordCompanion.Tests
{
    public class ScreenshotWatcherTests
    {
        private readonly ConcurrentQueue<string> _uploaded = new ConcurrentQueue<string>();

        private Task Upload(string path, CancellationToken ct)
        {
            _uploaded.Enqueue(Path.GetFileName(path));
            return Task.FromResult(0);
        }

        private ScreenshotWatcher Create(TempFolder temp, string format = "png") =>
            new ScreenshotWatcher(temp.Screenshots, format, PostedLedger.Load(temp.LedgerPath), Upload, TempFolder.FastTimings);

        private async Task WaitForUploads(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (_uploaded.Count < count && DateTime.UtcNow < deadline)
                await Task.Delay(50);
        }

        [Fact]
        public async Task SlowlyWrittenPngIsUploadedExactlyOnce()
        {
            using (var temp = new TempFolder())
            using (var watcher = Create(temp))
            {
                watcher.Start();

                var path = Path.Combine(temp.Screenshots, "WoWScrnShot_slow.png");
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        await stream.WriteAsync(new byte[1024], 0, 1024);
                        await stream.FlushAsync();
                        await Task.Delay(100);
                    }
                }

                await WaitForUploads(1, TimeSpan.FromSeconds(10));
                await Task.Delay(1500);

                Assert.Equal(new[] { "WoWScrnShot_slow.png" }, _uploaded);
            }
        }

        [Fact]
        public async Task JpgIsIgnoredWhenFormatIsPng()
        {
            using (var temp = new TempFolder())
            using (var watcher = Create(temp))
            {
                watcher.Start();

                File.WriteAllBytes(Path.Combine(temp.Screenshots, "WoWScrnShot_normal.jpg"), new byte[] { 1, 2, 3 });
                await Task.Delay(1500);

                Assert.Empty(_uploaded);
            }
        }

        [Fact]
        public async Task PostedFilesAreNotPostedAgainAfterRestart()
        {
            using (var temp = new TempFolder())
            {
                using (var first = Create(temp))
                {
                    first.Start();
                    File.WriteAllBytes(Path.Combine(temp.Screenshots, "WoWScrnShot_1.png"), new byte[] { 1, 2, 3 });
                    await WaitForUploads(1, TimeSpan.FromSeconds(10));
                }
                Assert.Single(_uploaded);

                using (var second = Create(temp))
                {
                    second.Start();
                    await Task.Delay(1500);
                }

                Assert.Single(_uploaded);
            }
        }

        [Fact]
        public async Task FilesOlderThanBaselineAreSkipped()
        {
            using (var temp = new TempFolder())
            {
                var old = Path.Combine(temp.Screenshots, "WoWScrnShot_old.png");
                File.WriteAllBytes(old, new byte[] { 1, 2, 3 });
                File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));

                using (var watcher = Create(temp))
                {
                    watcher.Start();
                    await Task.Delay(1500);
                }

                Assert.Empty(_uploaded);
            }
        }

        [Fact]
        public async Task FilesCreatedWhileClosedArePostedOnStart()
        {
            using (var temp = new TempFolder())
            {
                PostedLedger.Load(temp.LedgerPath).EnsureBaseline(temp.Screenshots, "png", DateTimeOffset.UtcNow.AddHours(-1));
                File.WriteAllBytes(Path.Combine(temp.Screenshots, "WoWScrnShot_a.png"), new byte[] { 1 });
                File.SetLastWriteTimeUtc(Path.Combine(temp.Screenshots, "WoWScrnShot_a.png"), DateTime.UtcNow.AddMinutes(-2));
                File.WriteAllBytes(Path.Combine(temp.Screenshots, "WoWScrnShot_b.png"), new byte[] { 2 });

                using (var watcher = Create(temp))
                {
                    watcher.Start();
                    await WaitForUploads(2, TimeSpan.FromSeconds(10));
                }

                Assert.Equal(new[] { "WoWScrnShot_a.png", "WoWScrnShot_b.png" }, _uploaded);
            }
        }

        [Fact]
        public void ChangingFormatResetsBaseline()
        {
            using (var temp = new TempFolder())
            {
                var ledger = PostedLedger.Load(temp.LedgerPath);
                var first = DateTimeOffset.UtcNow.AddHours(-2);
                ledger.EnsureBaseline(temp.Screenshots, "png", first);
                ledger.EnsureBaseline(temp.Screenshots, "png", DateTimeOffset.UtcNow);
                Assert.Equal(first, ledger.BaselineAt);

                var later = DateTimeOffset.UtcNow;
                ledger.EnsureBaseline(temp.Screenshots, "tga", later);
                Assert.Equal(later, PostedLedger.Load(temp.LedgerPath).BaselineAt);
            }
        }
    }
}
