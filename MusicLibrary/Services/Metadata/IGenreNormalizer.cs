namespace MusicLibrary.Services.Metadata;

/// <summary>
/// Сводит англоязычные жанры внешних API к русским, чтобы фильтр библиотеки
/// не показывал смесь "Rock" и "Рок" как два разных пункта.
/// </summary>
public interface IGenreNormalizer
{
    string Normalize(string rawGenre);
}
