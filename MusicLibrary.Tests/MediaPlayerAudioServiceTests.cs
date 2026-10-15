using MusicLibrary.Services.Playback;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class MediaPlayerAudioServiceTests
{
    [Fact]
    public void InitialVolume_MatchesDefaultSettings()
    {
        using var service = new MediaPlayerAudioService();
        Assert.Equal(MusicBakh.Core.Domain.PlayerSettings.Default.Volume, service.Volume);
    }

    [Fact]
    public void InvalidVolume_DoesNotReachNativePlayer()
    {
        using var service = new MediaPlayerAudioService();
        service.Volume = 0.4;
        service.Volume = double.NaN;
        Assert.Equal(0.4, service.Volume);
    }

    [Fact]
    public void PlayWithoutOpen_DoesNotReportPlaying()
    {
        using var service = new MediaPlayerAudioService();
        Assert.False(service.Play().IsSuccess);
        Assert.False(service.IsPlaying);
    }

    [Fact]
    public void Open_PreservesVolumeAndMuteState()
    {
        using var service = new MediaPlayerAudioService();
        string filePath = Path.Combine(Path.GetTempPath(), "missing-track.mp3");
        service.Volume = 0.37;
        service.IsMuted = true;

        service.Open(filePath);

        Assert.Equal(0.37, service.Volume);
        Assert.True(service.IsMuted);
    }
}
