using MusicLibrary.Models;

namespace MusicLibrary.Services.Storage;

/// <summary>
/// Снимок настроек плеера, сохраняемый между запусками приложения.
/// </summary>
public sealed record PlayerSettings(double Volume, bool IsMuted, RepeatMode RepeatMode)
{
    public static PlayerSettings Default { get; } =
        new(Volume: 1.0, IsMuted: false, RepeatMode: RepeatMode.Off);
}
