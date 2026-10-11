using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LusDiscordCompanion.Tests
{
    internal sealed class CapturedPart
    {
        public string Name;
        public string FileName;
        public string ContentType;
        public byte[] Body;
    }

    internal sealed class CapturedRequest
    {
        public Uri Uri;
        public string ContentType;
        public List<CapturedPart> Parts = new List<CapturedPart>();
        public string Text;
    }

    internal sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage>[] _responses;

        public FakeHandler(params Func<HttpResponseMessage>[] responses)
        {
            _responses = responses;
        }

        public List<CapturedRequest> Requests { get; } = new List<CapturedRequest>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var captured = new CapturedRequest
            {
                Uri = request.RequestUri,
                ContentType = request.Content?.Headers.ContentType?.MediaType,
            };
            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (var part in multipart)
                {
                    var disposition = part.Headers.ContentDisposition;
                    captured.Parts.Add(new CapturedPart
                    {
                        Name = disposition.Name.Trim('"'),
                        FileName = disposition.FileName?.Trim('"'),
                        ContentType = part.Headers.ContentType?.MediaType,
                        Body = await part.ReadAsByteArrayAsync(),
                    });
                }
            }
            else if (request.Content != null)
            {
                captured.Text = await request.Content.ReadAsStringAsync();
            }

            Requests.Add(captured);
            return _responses[Math.Min(Requests.Count - 1, _responses.Length - 1)]();
        }

        public static HttpResponseMessage Status(HttpStatusCode code, string body = "{}")
        {
            return new HttpResponseMessage(code) { Content = new StringContent(body) };
        }
    }
}
