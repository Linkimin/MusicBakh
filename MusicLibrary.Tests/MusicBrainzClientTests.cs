using System.Net;
using System.Net.Http;
using MusicBakh.Infrastructure.Metadata;

namespace MusicLibrary.Tests;

public sealed class MusicBrainzClientTests
{
    [Fact]
    public async Task Search_EscapesQuotesOnceAndKeepsSharedClientUnchanged()
    {
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new MusicBrainzClient(http);
        await client.SearchAsync("A", "Say \"Hello\"", CancellationToken.None);
        Assert.Equal("musicbrainz.org", handler.Uri!.Host);
        Assert.Contains("recording:\"Say \\\"Hello\\\"\"", Uri.UnescapeDataString(handler.Uri.Query));
        Assert.Equal("example.test", http.BaseAddress.Host);
        Assert.Empty(http.DefaultRequestHeaders.UserAgent);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"recordings\":[]}") });
        }
    }
}
