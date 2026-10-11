using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Xunit;

namespace LusDiscordCompanion.Tests
{
    public class DiscordClientTests
    {
        private const string Webhook = "https://discord.com/api/webhooks/1/token";

        private readonly List<TimeSpan> _delays = new List<TimeSpan>();

        private Task RecordDelay(TimeSpan span, CancellationToken ct)
        {
            _delays.Add(span);
            return Task.FromResult(0);
        }

        private static Dictionary<string, object> ParseJson(string json) =>
            new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);

        [Fact]
        public async Task MultipartBodyHasPayloadAndFile()
        {
            var handler = new FakeHandler(() => FakeHandler.Status(HttpStatusCode.OK));
            var client = new DiscordClient(handler, RecordDelay);
            var image = new byte[] { 1, 2, 3, 4 };

            await client.SendFileAsync(Webhook, "MyBot", "WoWScrnShot_1.png", image);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(Webhook + "?wait=true", request.Uri.ToString());
            Assert.Equal("multipart/form-data", request.ContentType);

            var payload = Assert.Single(request.Parts, p => p.Name == "payload_json");
            var json = ParseJson(Encoding.UTF8.GetString(payload.Body));
            Assert.Equal("MyBot", json["username"]);
            Assert.Equal(DiscordClient.LevelUpMessage, json["content"]);

            var file = Assert.Single(request.Parts, p => p.Name == "files[0]");
            Assert.Equal("WoWScrnShot_1.png", file.FileName);
            Assert.Equal("image/png", file.ContentType);
            Assert.Equal(image, file.Body);
        }

        [Fact]
        public async Task TestMessageUsesBotName()
        {
            var handler = new FakeHandler(() => FakeHandler.Status(HttpStatusCode.OK));
            var client = new DiscordClient(handler, RecordDelay);

            await client.SendTextAsync(Webhook, "MyBot", DiscordClient.TestMessage);

            var json = ParseJson(Assert.Single(handler.Requests).Text);
            Assert.Equal("MyBot", json["username"]);
            Assert.Equal("LevelUpScreenshot test ✅", json["content"]);
        }

        [Fact]
        public async Task RateLimitIsRetriedAfterRetryAfter()
        {
            var handler = new FakeHandler(
                () => FakeHandler.Status((HttpStatusCode)429, "{\"message\":\"rate limited\",\"retry_after\":1.5,\"global\":false}"),
                () => FakeHandler.Status(HttpStatusCode.OK));
            var client = new DiscordClient(handler, RecordDelay);

            await client.SendFileAsync(Webhook, "Bot", "a.png", new byte[] { 1 });

            Assert.Equal(2, handler.Requests.Count);
            Assert.Equal(new[] { TimeSpan.FromSeconds(1.5) }, _delays);
        }

        [Fact]
        public async Task ServerErrorsBackOffThenFail()
        {
            var handler = new FakeHandler(() => FakeHandler.Status(HttpStatusCode.BadGateway));
            var client = new DiscordClient(handler, RecordDelay);

            await Assert.ThrowsAsync<DiscordException>(() => client.SendFileAsync(Webhook, "Bot", "a.png", new byte[] { 1 }));

            Assert.Equal(5, handler.Requests.Count);
            Assert.Equal(new double[] { 2, 5, 15, 60 }, _delays.Select(d => d.TotalSeconds));
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task InvalidWebhookThrows(HttpStatusCode code)
        {
            var handler = new FakeHandler(() => FakeHandler.Status(code));
            var client = new DiscordClient(handler, RecordDelay);

            await Assert.ThrowsAsync<WebhookInvalidException>(() => client.SendFileAsync(Webhook, "Bot", "a.png", new byte[] { 1 }));
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task NotFoundStopsWatcher()
        {
            using (var temp = new TempFolder())
            {
                var handler = new FakeHandler(() => FakeHandler.Status(HttpStatusCode.NotFound));
                var client = new DiscordClient(handler, RecordDelay);
                using (var watcher = new ScreenshotWatcher(temp.Screenshots, "png", PostedLedger.Load(temp.LedgerPath),
                    (path, ct) => client.SendFileAsync(Webhook, "Bot", Path.GetFileName(path), File.ReadAllBytes(path), ct),
                    TempFolder.FastTimings))
                {
                    var invalid = new TaskCompletionSource<bool>();
                    watcher.WebhookInvalid += () => invalid.TrySetResult(true);

                    watcher.Start();
                    File.WriteAllBytes(Path.Combine(temp.Screenshots, "WoWScrnShot_1.png"), new byte[] { 1, 2, 3 });

                    var finished = await Task.WhenAny(invalid.Task, Task.Delay(TimeSpan.FromSeconds(10)));
                    Assert.Same(invalid.Task, finished);
                    Assert.False(watcher.IsRunning);
                }
            }
        }
    }
}
