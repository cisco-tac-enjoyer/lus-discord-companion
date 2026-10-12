using Xunit;

namespace LusDiscordCompanion.Tests
{
    public class ValidationTests
    {
        [Theory]
        [InlineData("https://discord.com/api/webhooks/123456789/abcDEF_-123")]
        [InlineData("https://ptb.discord.com/api/webhooks/1/x")]
        [InlineData("https://canary.discord.com/api/webhooks/1/x")]
        [InlineData("https://discordapp.com/api/webhooks/1/token")]
        public void AcceptsValidWebhooks(string url) => Assert.True(AppSettings.IsValidWebhook(url));

        [Theory]
        [InlineData("")]
        [InlineData("http://discord.com/api/webhooks/1/x")]
        [InlineData("https://evil.com/api/webhooks/1/x")]
        [InlineData("https://discord.com/api/webhooks/abc/x")]
        [InlineData("https://discord.com/api/webhooks/1/")]
        [InlineData("https://discord.com/api/webhooks/1/x?wait=true")]
        [InlineData("https://beta.discord.com/api/webhooks/1/x")]
        public void RejectsInvalidWebhooks(string url) => Assert.False(AppSettings.IsValidWebhook(url));

        [Theory]
        [InlineData("LevelUpScreenshot")]
        [InlineData("A")]
        public void AcceptsValidBotNames(string name) => Assert.Null(AppSettings.ValidateBotName(name));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("My Discord Bot")]
        [InlineData("clyde")]
        [InlineData("ClydeHelper")]
        public void RejectsInvalidBotNames(string name) => Assert.NotNull(AppSettings.ValidateBotName(name));

        [Fact]
        public void RejectsBotNameOver80Characters() =>
            Assert.NotNull(AppSettings.ValidateBotName(new string('a', 81)));

        [Fact]
        public void AcceptsBotNameOf80Characters() =>
            Assert.Null(AppSettings.ValidateBotName(new string('a', 80)));

        [Fact]
        public void RedactsWebhookUrls()
        {
            var text = AppSettings.Redact("Failed POST https://discord.com/api/webhooks/123/secret-token?wait=true now");
            Assert.DoesNotContain("secret-token", text);
            Assert.Contains("[webhook]", text);
        }

        [Fact]
        public void SettingsRoundTripWithGuideKeys()
        {
            using (var temp = new TempFolder())
            {
                var path = System.IO.Path.Combine(temp.Root, "settings.json");
                new AppSettings { WebhookUrl = "https://discord.com/api/webhooks/1/x", BotName = "Bot", LevelUpFormat = "tga" }.Save(path);

                var json = System.IO.File.ReadAllText(path);
                Assert.Contains("\"webhookUrl\"", json);
                Assert.Contains("\"autoStartWatching\"", json);

                var loaded = AppSettings.Load(path);
                Assert.Equal("Bot", loaded.BotName);
                Assert.Equal("tga", loaded.LevelUpFormat);
                Assert.True(loaded.MinimizeToTray);
            }
        }
    }
}
