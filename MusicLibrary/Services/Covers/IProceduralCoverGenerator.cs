namespace MusicLibrary.Services.Covers;

/// <summary>
/// Последняя ступень каскада обложек — рисует заглушку, если онлайн-источники не нашли картинку.
/// Цвета и буква детерминированы по строке "artist|title", чтобы у одного трека всегда
/// была одна и та же заглушка.
/// </summary>
public interface IProceduralCoverGenerator
{
    ResolvedCover Generate(string artist, string title);
}
