using MusicLibrary.Models;
using System.Windows.Media;

namespace MusicLibrary.Services.Playback;

/// <summary>
/// Встроенный плеер приложения. Это осознанное отличие от работы:
/// вместо внешнего проигрывателя используется MediaPlayer, но функция прослушивания сохраняется.
/// </summary>
public sealed class MediaPlayerAudioService : IAudioPlayerService
{
    private MediaPlayer _player;
    private string? _currentFilePath;
    private bool _isDisposed;

    public MediaPlayerAudioService()
    {
        _player = CreatePlayer(filePath: null);
    }

    private MediaPlayer CreatePlayer(string? filePath)
    {
        var player = new MediaPlayer();

        player.MediaOpened += (_, _) =>
        {
            if (IsCurrentPlayer(player, filePath))
            {
                MediaOpened?.Invoke(this, filePath!);
            }
        };

        player.MediaEnded += (_, _) =>
        {
            if (!ReferenceEquals(player, _player))
            {
                return;
            }

            IsPlaying = false;
            _currentFilePath = null;
            MediaEnded?.Invoke(this, EventArgs.Empty);
        };

        player.MediaFailed += (_, args) =>
        {
            if (!ReferenceEquals(player, _player))
            {
                return;
            }

            IsPlaying = false;
            _currentFilePath = null;
            MediaFailed?.Invoke(this, args.ErrorException.Message);
        };

        return player;
    }

    private bool IsCurrentPlayer(MediaPlayer player, string? filePath)
    {
        return ReferenceEquals(player, _player)
            && filePath is not null
            && string.Equals(_currentFilePath, filePath, StringComparison.OrdinalIgnoreCase);
    }

    public event EventHandler<string>? MediaOpened;
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

    public double Volume
    {
        get => _player.Volume;
        // Защитный clamp на случай, если откуда-то прилетит volume > 1 или < 0.
        set => _player.Volume = Math.Clamp(value, 0.0, 1.0);
    }

    public bool IsMuted
    {
        get => _player.IsMuted;
        set => _player.IsMuted = value;
    }

    public OperationResult Open(string filePath)
    {
        try
        {
            double volume = _player.Volume;
            bool isMuted = _player.IsMuted;

            _player.Close();
            _player = CreatePlayer(filePath);
            _player.Volume = volume;
            _player.IsMuted = isMuted;
            _currentFilePath = filePath;
            _player.Open(new Uri(filePath, UriKind.Absolute));
            return OperationResult.Success("Трек подготовлен к воспроизведению.");
        }
        catch (Exception exception) when (exception is UriFormatException or InvalidOperationException)
        {
            IsPlaying = false;
            _currentFilePath = null;
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
        _currentFilePath = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _player.Close();
        _currentFilePath = null;
        _isDisposed = true;
    }
}
