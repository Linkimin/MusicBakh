using System.Globalization;

namespace MusicLibrary.Services.Metadata;

/// <summary>
/// Сводит англоязычные жанры из MusicBrainz/iTunes к русским названиям, совместимым
/// с захардкоженной библиотекой. Незнакомые жанры сохраняются «как есть» с заглавной буквы,
/// чтобы фильтр выглядел опрятно.
/// </summary>
public sealed class RussianGenreNormalizer : IGenreNormalizer
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rock"] = "Рок",
        ["hard rock"] = "Рок",
        ["classic rock"] = "Рок",
        ["alternative"] = "Альтернатива",
        ["alternative rock"] = "Альтернатива",
        ["indie"] = "Инди",
        ["indie rock"] = "Инди",
        ["indie pop"] = "Инди",
        ["pop"] = "Поп",
        ["pop rock"] = "Поп",
        ["electronic"] = "Электроника",
        ["electronica"] = "Электроника",
        ["edm"] = "Электроника",
        ["dance"] = "Электроника",
        ["house"] = "Хаус",
        ["techno"] = "Техно",
        ["trance"] = "Транс",
        ["ambient"] = "Эмбиент",
        ["dnb"] = "Драм-н-бэйс",
        ["drum and bass"] = "Драм-н-бэйс",
        ["dubstep"] = "Дабстеп",
        ["metal"] = "Метал",
        ["heavy metal"] = "Метал",
        ["power metal"] = "Метал",
        ["death metal"] = "Метал",
        ["thrash metal"] = "Метал",
        ["punk"] = "Панк",
        ["punk rock"] = "Панк",
        ["post-punk"] = "Постпанк",
        ["post punk"] = "Постпанк",
        ["hip-hop"] = "Хип-хоп",
        ["hip hop"] = "Хип-хоп",
        ["rap"] = "Рэп",
        ["phonk"] = "Фонк",
        ["jazz"] = "Джаз",
        ["blues"] = "Блюз",
        ["classical"] = "Классика",
        ["folk"] = "Фолк",
        ["country"] = "Кантри",
        ["soundtrack"] = "Саундтрек",
        ["ost"] = "Аниме/OST",
        ["anime"] = "Аниме/OST",
        ["j-pop"] = "J-Pop",
        ["k-pop"] = "K-Pop",
        ["reggae"] = "Регги",
        ["ska"] = "Ска",
        ["r&b"] = "R&B",
        ["soul"] = "Соул",
        ["funk"] = "Фанк",
        ["disco"] = "Диско"
    };

    public string Normalize(string rawGenre)
    {
        if (string.IsNullOrWhiteSpace(rawGenre))
        {
            return string.Empty;
        }

        string trimmed = rawGenre.Trim();

        if (Map.TryGetValue(trimmed, out string? mapped))
        {
            return mapped;
        }

        // ID3 нередко возвращает "Rock; Alternative" или "Rock/Alternative" — берем первую часть.
        char[] separators = { ';', '/', ',' };
        int splitIndex = trimmed.IndexOfAny(separators);
        if (splitIndex > 0)
        {
            string firstPart = trimmed[..splitIndex].Trim();
            if (Map.TryGetValue(firstPart, out string? splitMapped))
            {
                return splitMapped;
            }
            trimmed = firstPart;
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
    }
}
