namespace MusicLibrary.Models;

/// <summary>
/// Режим повтора при завершении трека.
/// Off    — после текущего трека воспроизведение останавливается.
/// Current — текущий трек запускается заново.
/// Library — играет следующий трек из видимого списка, в конце возвращается к первому.
/// </summary>
public enum RepeatMode
{
    Off,
    Current,
    Library
}
