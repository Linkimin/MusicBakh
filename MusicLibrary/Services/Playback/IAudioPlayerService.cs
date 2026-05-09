using MusicLibrary.Models;

namespace MusicLibrary.Services.Playback;

public interface IAudioPlayerService : IDisposable
{
    event EventHandler<string>? MediaOpened;
    event EventHandler? MediaEnded;
    event EventHandler<string>? MediaFailed;

    bool IsPlaying { get; }
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; }

    OperationResult Open(string filePath);
    OperationResult Play();
    void Pause();
    void Stop();
}
