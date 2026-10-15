using System.IO;
using System.Net;
using System.Net.Http;
using MusicBakh.Application.Abstractions;
using MusicBakh.Application.Contracts;
using MusicBakh.Infrastructure.Import;

namespace MusicLibrary.Tests;

public sealed class TrackImporterTests : IDisposable
{
    private readonly TestPaths _paths = new();

    [Fact]
    public void TagReader_RejectsHtmlDisguisedAsMp3()
    {
        string file = Path.Combine(_paths.RootDirectory, "bad.mp3");
        File.WriteAllText(file, "<html>Download unavailable</html>");
        Assert.Throws<IOException>(() => new MusicBakh.Infrastructure.Metadata.TagLibSharpTagReader().Read(file));
    }

    [Fact]
    public async Task MetadataFailure_RemovesImportedCopy_PreservesSource()
    {
        string source = Path.Combine(_paths.RootDirectory, "source.mp3");
        await File.WriteAllTextAsync(source, "original");
        using var http = new HttpClient();
        var importer = new TrackImporter(_paths, new Metadata(fail: true), new Cover(), http);
        var result = await importer.ImportAsync(new LocalFileImportRequest(source), null, default);
        Assert.False(result.IsSuccess);
        Assert.Empty(Directory.GetFiles(_paths.MusicDirectory));
        Assert.Equal("original", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task UnsupportedAudioType_IsRejected()
    {
        using var http = new HttpClient(new ResponseHandler("audio/ogg"));
        var importer = new TrackImporter(_paths, new Metadata(), new Cover(), http);
        var result = await importer.ImportAsync(new UrlImportRequest("https://example.test/audio"), null, default);
        Assert.False(result.IsSuccess);
        Assert.Empty(Directory.GetFiles(_paths.MusicDirectory));
    }

    [Fact]
    public async Task CancellationAfterCopy_RemovesImportedFiles()
    {
        string source = Path.Combine(_paths.RootDirectory, "source.mp3");
        await File.WriteAllTextAsync(source, "original");
        using var cts = new CancellationTokenSource();
        using var http = new HttpClient();
        var importer = new TrackImporter(_paths, new Metadata(cancel: cts), new Cover(), http);
        var result = await importer.ImportAsync(new LocalFileImportRequest(source), null, cts.Token);
        Assert.False(result.IsSuccess);
        Assert.Empty(Directory.GetFiles(_paths.MusicDirectory));
        Assert.Empty(Directory.GetFiles(_paths.CoversDirectory));
    }

    private sealed class Metadata(bool fail = false, CancellationTokenSource? cancel = null) : IMetadataResolver
    {
        public Task<ResolvedMetadata> ResolveAsync(string filePath, string? filenameHint, CancellationToken cancellationToken)
        {
            if (fail) throw new IOException("metadata failure");
            cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ResolvedMetadata { Title = "Song", Duration = TimeSpan.FromSeconds(10) });
        }
    }

    private sealed class Cover : ICoverResolver
    {
        public Task<ResolvedCover> ResolveAsync(ResolvedMetadata metadata, CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedCover { Bytes = new byte[] { 1 }, Extension = "png" });
    }

    private sealed class ResponseHandler(string type) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
            content.Headers.ContentType = new(type);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class TestPaths : IMusicStoragePaths
    {
        public string RootDirectory { get; } = Path.Combine(Path.GetTempPath(), "musicbakh-import-tests", Guid.NewGuid().ToString("N"));
        public string MusicDirectory => Path.Combine(RootDirectory, "Music");
        public string CoversDirectory => Path.Combine(RootDirectory, "Covers");
        public TestPaths() { Directory.CreateDirectory(MusicDirectory); Directory.CreateDirectory(CoversDirectory); }
    }

    public void Dispose() => Directory.Delete(_paths.RootDirectory, true);
}
