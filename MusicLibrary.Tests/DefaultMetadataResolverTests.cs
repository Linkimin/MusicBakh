using MusicLibrary.Services.Covers;
using MusicLibrary.Services.Metadata;

namespace MusicLibrary.Tests;

public sealed class DefaultMetadataResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenFilenameFallbackContainsAggregatorSuffix_StripsSuffixFromTitle()
    {
        var resolver = CreateResolver(new LocalTagInfo());

        ResolvedMetadata result = await resolver.ResolveAsync(
            @"C:\Temp\track.mp3",
            "Три дня дождя, MONA - Прощание (zaycev.net)",
            CancellationToken.None);

        Assert.Equal("Прощание", result.Title);
        Assert.Equal("Три дня дождя, MONA", result.Artist);
    }

    [Fact]
    public async Task ResolveAsync_WhenFilenameFallbackContainsLegitimateBracketSuffix_KeepsSuffix()
    {
        var resolver = CreateResolver(new LocalTagInfo());

        ResolvedMetadata result = await resolver.ResolveAsync(
            @"C:\Temp\track.mp3",
            "Artist - Song (Remix)",
            CancellationToken.None);

        Assert.Equal("Song (Remix)", result.Title);
        Assert.Equal("Artist", result.Artist);
    }

    private static DefaultMetadataResolver CreateResolver(LocalTagInfo tagInfo)
    {
        return new DefaultMetadataResolver(
            new StubTagReader(tagInfo),
            new StubMusicBrainzClient(),
            new StubItunesCoverClient(),
            new RussianGenreNormalizer());
    }

    private sealed class StubTagReader(LocalTagInfo tagInfo) : ITagReader
    {
        public LocalTagInfo Read(string filePath)
        {
            return tagInfo;
        }
    }

    private sealed class StubMusicBrainzClient : IMusicBrainzClient
    {
        public Task<MusicBrainzMatch?> SearchAsync(string artist, string title, CancellationToken cancellationToken)
        {
            return Task.FromResult<MusicBrainzMatch?>(null);
        }
    }

    private sealed class StubItunesCoverClient : IItunesCoverClient
    {
        public Task<ResolvedCover?> FindAsync(string artist, string title, CancellationToken cancellationToken)
        {
            return Task.FromResult<ResolvedCover?>(null);
        }

        public Task<ItunesSearchHit?> SearchAsync(string artist, string title, CancellationToken cancellationToken)
        {
            return Task.FromResult<ItunesSearchHit?>(null);
        }
    }
}
