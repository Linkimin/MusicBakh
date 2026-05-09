namespace MusicLibrary.Services.Metadata;

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
            string genre = file.Tag.FirstGenre ?? string.Empty;
            TimeSpan duration = file.Properties?.Duration ?? TimeSpan.Zero;

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
                Genre = genre,
                Duration = duration,
                CoverBytes = coverBytes,
                CoverMimeType = mime
            };
        }
        catch (Exception)
        {
            // TagLib бросает разнообразные исключения на битых/нестандартных файлах.
            // Импорт продолжается, поля просто остаются пустыми.
            return new LocalTagInfo();
        }
    }
}
