using MusicLibrary.Services.Playback;
using System.IO;

namespace MusicLibrary.Tests;

public sealed class MediaPlayerAudioServiceTests
{
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
