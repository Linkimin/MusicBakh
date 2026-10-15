using MusicBakh.Application.Abstractions;
using MusicBakh.Application.Contracts;

namespace MusicBakh.Infrastructure.Metadata;

/// <summary>
/// Адаптер библиотеки TagLib# (NuGet TagLibSharp). Достает Title/Artist/Genre/Duration
/// и встроенную обложку из ID3v2 (mp3) или Vorbis-комментариев (другие форматы).
/// </summary>
public sealed class TagLibSharpTagReader : ITagReader
{
    public LocalTagInfo Read(string filePath)
    {
        try
        {
            using TagLib.File file = TagLib.File.Create(filePath);

            string title = file.Tag.Title ?? string.Empty;
            string artist = file.Tag.FirstPerformer ?? file.Tag.JoinedPerformers ?? string.Empty;
            string album = file.Tag.Album ?? string.Empty;
            string genre = file.Tag.FirstGenre ?? string.Empty;
            TimeSpan duration = file.Properties?.Duration ?? TimeSpan.Zero;

            uint year = file.Tag.Year;
            uint trackNumber = file.Tag.Track;
            string? albumArtist = file.Tag.FirstAlbumArtist;
            if (string.IsNullOrWhiteSpace(albumArtist))
            {
                albumArtist = null;
            }

            byte[]? coverBytes = null;
            string? mime = null;
            TagLib.IPicture? picture = file.Tag.Pictures.FirstOrDefault();
            if (picture is not null && picture.Data?.Data is byte[] data && data.Length > 0)
            {
                coverBytes = data;
                mime = picture.MimeType;
            }

            return new LocalTagInfo
            {
                Title = title,
                Artist = artist,
                Album = album,
                Genre = genre,
                Duration = duration,
                CoverBytes = coverBytes,
                CoverMimeType = mime,
                Year = year > 0 ? (int)year : (int?)null,
                TrackNumber = trackNumber > 0 ? (int)trackNumber : (int?)null,
                AlbumArtist = albumArtist
            };
        }
        catch (Exception ex) when (ex is TagLib.CorruptFileException or TagLib.UnsupportedFormatException)
        {
            // Отсутствующие теги допустимы, нечитаемый аудиофайл — нет.
            throw new System.IO.IOException("Аудиофайл повреждён или его формат не поддерживается.", ex);
        }
    }
}
