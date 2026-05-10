using MusicLibrary.Models;
using MusicLibrary.Services.Storage;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class JsonPlayerSettingsStorageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly JsonPlayerSettingsStorage _storage;

    public JsonPlayerSettingsStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MusicBakhTests-" + Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_tempDir, "player-settings.json");
        _storage = new JsonPlayerSettingsStorage(_settingsPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_FileMissing_ReturnsDefault()
    {
        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var original = new PlayerSettings(Volume: 0.42, IsMuted: true, RepeatMode: RepeatMode.Library);

        _storage.Save(original);
        PlayerSettings loaded = _storage.Load();

        Assert.Equal(original, loaded);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefault()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{not valid json");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Load_MissingFields_ReturnsDefault()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, "{}");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Load_VolumeOutsideRange_ClampedToZeroOne()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, """{"volume":2.5,"isMuted":false,"repeatMode":"Off"}""");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(1.0, settings.Volume);

        File.WriteAllText(_settingsPath, """{"volume":-0.3,"isMuted":false,"repeatMode":"Off"}""");

        settings = _storage.Load();

        Assert.Equal(0.0, settings.Volume);
    }

    [Fact]
    public void Load_UnknownRepeatMode_ReturnsDefault()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, """{"volume":0.5,"isMuted":false,"repeatMode":"Shuffle"}""");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Load_NumericUnknownRepeatMode_ReturnsDefault()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_settingsPath, """{"volume":0.5,"isMuted":false,"repeatMode":123}""");

        PlayerSettings settings = _storage.Load();

        Assert.Equal(PlayerSettings.Default, settings);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        Assert.False(Directory.Exists(_tempDir));

        _storage.Save(new PlayerSettings(Volume: 0.7, IsMuted: false, RepeatMode: RepeatMode.Current));

        Assert.True(File.Exists(_settingsPath));
    }
}
