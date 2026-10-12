using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LusDiscordCompanion
{
    public sealed class WebhookInvalidException : Exception
    {
        public WebhookInvalidException() : base("Webhook is invalid or was deleted") { }
    }

    public sealed class DiscordException : Exception
    {
        public DiscordException(string message) : base(message) { }
    }

    public sealed class DiscordClient
    {
        public const string LevelUpMessage = "\U0001F389 Level up!";
        public const string TestMessage = "LevelUpScreenshot test \u2705";

        static readonly TimeSpan[] Backoff =
        {
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(60),
        };

        const int MaxRateLimitRetries = 10;

        readonly HttpClient _http;
        readonly Func<TimeSpan, CancellationToken, Task> _delay;

        public DiscordClient(HttpMessageHandler handler = null, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(100);
            _delay = delay ?? ((span, ct) => Task.Delay(span, ct));
        }

        public Task SendTextAsync(string webhookUrl, string botName, string content, CancellationToken ct = default(CancellationToken))
        {
            return SendAsync(webhookUrl, () => new StringContent(Payload(botName, content), Encoding.UTF8, "application/json"), ct);
        }

        public Task SendFileAsync(string webhookUrl, string botName, string fileName, byte[] data, CancellationToken ct = default(CancellationToken))
        {
            return SendAsync(webhookUrl, () =>
            {
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(Payload(botName, LevelUpMessage), Encoding.UTF8, "application/json"), "payload_json");
                var file = new ByteArrayContent(data);
                file.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeFor(fileName));
                form.Add(file, "files[0]", fileName);
                return form;
            }, ct);
        }

        static string Payload(string botName, string content)
        {
            return AppSettings.Json.Serialize(new Dictionary<string, string> { { "username", botName.Trim() }, { "content", content } });
        }

        static string ContentTypeFor(string fileName)
        {
            switch (Path.GetExtension(fileName).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                default: return "application/octet-stream";
            }
        }

        async Task SendAsync(string webhookUrl, Func<HttpContent> buildContent, CancellationToken ct)
        {
            var url = webhookUrl.Trim() + "?wait=true";
            int failures = 0, rateLimits = 0;

            while (true)
            {
                HttpResponseMessage response = null;
                Exception networkError = null;
                try
                {
                    using (var content = buildContent())
                        response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    networkError = ex;
                }
                catch (TaskCanceledException ex)
                {
                    if (ct.IsCancellationRequested)
                        throw;
                    networkError = ex;
                }

                if (networkError != null)
                {
                    if (failures >= Backoff.Length)
                        throw new DiscordException("Network error: " + AppSettings.Redact(networkError.Message));
                    await _delay(Backoff[failures++], ct).ConfigureAwait(false);
                    continue;
                }

                using (response)
                {
                    if (response.IsSuccessStatusCode)
                        return;

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var code = (int)response.StatusCode;

                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.NotFound)
                        throw new WebhookInvalidException();

                    if (code == 429 && rateLimits++ < MaxRateLimitRetries)
                    {
                        await _delay(RetryAfter(response, body), ct).ConfigureAwait(false);
                        continue;
                    }

                    if (code >= 500 && failures < Backoff.Length)
                    {
                        await _delay(Backoff[failures++], ct).ConfigureAwait(false);
                        continue;
                    }

                    throw new DiscordException("Discord returned " + code + ": " + AppSettings.Redact(body));
                }
            }
        }

        static TimeSpan RetryAfter(HttpResponseMessage response, string body)
        {
            try
            {
                var values = AppSettings.Json.Deserialize<Dictionary<string, object>>(body);
                object value;
                if (values != null && values.TryGetValue("retry_after", out value) && value != null)
                    return TimeSpan.FromSeconds(Math.Max(Convert.ToDouble(value, CultureInfo.InvariantCulture), 0));
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (FormatException)
            {
            }

            IEnumerable<string> header;
            double seconds;
            if (response.Headers.TryGetValues("Retry-After", out header)
                && double.TryParse(header.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                return TimeSpan.FromSeconds(Math.Max(seconds, 0));

            return TimeSpan.FromSeconds(1);
        }
    }
}
