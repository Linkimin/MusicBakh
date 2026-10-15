using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicLibrary.Tests.TestSupport;
using Xunit;

namespace MusicLibrary.Tests.Persistence;

public sealed class SqlitePlayerSettingsRepositoryTests
{
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-1")]
    [InlineData("2")]
    public void Load_InvalidVolume_UsesDefault(string raw)
    {
        using var factory = new InMemorySqliteDbContextFactory();
        using var ctx = factory.CreateContext();
        ctx.KeyValueStore.Add(new() { Key = "player.volume", Value = raw });
        ctx.KeyValueStore.Add(new() { Key = "player.repeatMode", Value = "999" });
        ctx.SaveChanges();
        Assert.Equal(PlayerSettings.Default, new SqlitePlayerSettingsRepository(factory.CreateContext).Load());
    }

    [Fact]
    public void ConnectionString_PreservesSemicolonInPath()
    {
        const string path = "C:/Music;Library/library.db";
        var options = new MusicBakh.Infrastructure.Persistence.LibraryDbContextOptions(path);
        Assert.Equal(path, new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(options.ConnectionString).DataSource);
    }

    [Fact]
    public void Load_Returns_Default_For_Fresh_Db()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqlitePlayerSettingsRepository(factory.CreateContext);

        Assert.Equal(PlayerSettings.Default, repo.Load());
    }

    [Fact]
    public void Save_Then_Load_Returns_Same_Settings()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqlitePlayerSettingsRepository(factory.CreateContext);

        var settings = new PlayerSettings(Volume: 0.42, IsMuted: true, RepeatMode: RepeatMode.Current);
        repo.Save(settings);

        Assert.Equal(settings, repo.Load());
    }

    [Fact]
    public void Save_Is_Idempotent()
    {
        using var factory = new InMemorySqliteDbContextFactory();
        var repo = new SqlitePlayerSettingsRepository(factory.CreateContext);

        var first = new PlayerSettings(0.5, false, RepeatMode.Off);
        var second = new PlayerSettings(0.7, true, RepeatMode.Library);

        repo.Save(first);
        repo.Save(second);

        Assert.Equal(second, repo.Load());
    }
}
