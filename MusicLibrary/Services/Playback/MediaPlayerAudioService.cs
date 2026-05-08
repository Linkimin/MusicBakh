using MusicLibrary.Models;
using System.Windows.Media;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Встроенный плеер приложения. Это осознанное отличие от работы:
/// вместо внешнего проигрывателя используется MediaPlayer, но функция прослушивания сохраняется.
/// </summary>
public sealed class MediaPlayerAudioService : IAudioPlayerService
{
    private readonly MediaPlayer _player = new();
    private bool _isDisposed;

    public MediaPlayerAudioService()
    {
        _player.MediaOpened += (_, _) => MediaOpened?.Invoke(this, EventArgs.Empty);
        _player.MediaEnded += (_, _) =>
        {
            IsPlaying = false;
            MediaEnded?.Invoke(this, EventArgs.Empty);
        };
        _player.MediaFailed += (_, args) =>
        {
            IsPlaying = false;
            MediaFailed?.Invoke(this, args.ErrorException.Message);
        };
    }

    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler<string>? MediaFailed;

    public bool IsPlaying { get; private set; }

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value;
    }

    public TimeSpan Duration
    {
        get
        {
            if (_player.NaturalDuration.HasTimeSpan)
            {
                return _player.NaturalDuration.TimeSpan;
            }

            return TimeSpan.Zero;
        }
    }

    public OperationResult Open(string filePath)
    {
        try
        {
            _player.Open(new Uri(filePath, UriKind.Absolute));
            return OperationResult.Success("Трек подготовлен к воспроизведению.");
        }
        catch (Exception exception) when (exception is UriFormatException or InvalidOperationException)
        {
            IsPlaying = false;
            return OperationResult.Error($"Не удалось открыть аудиофайл: {exception.Message}");
        }
    }

    public OperationResult Play()
    {
        try
        {
            _player.Play();
            IsPlaying = true;
            return OperationResult.Success("Воспроизведение запущено.");
        }
        catch (InvalidOperationException exception)
        {
            IsPlaying = false;
            return OperationResult.Error($"Не удалось запустить воспроизведение: {exception.Message}");
        }
    }

    public void Pause()
    {
        _player.Pause();
        IsPlaying = false;
    }

    public void Stop()
    {
        _player.Stop();
        _player.Position = TimeSpan.Zero;
        IsPlaying = false;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _player.Close();
        _isDisposed = true;
    }
}
