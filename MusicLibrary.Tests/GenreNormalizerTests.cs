using MusicLibrary.Services.Metadata;

namespace MusicLibrary.Tests;

public sealed class GenreNormalizerTests
{
    private readonly RussianGenreNormalizer _normalizer = new();

    [Theory]
    [InlineData("rock", "Рок")]
    [InlineData("Rock", "Рок")]
    [InlineData("electronic", "Электроника")]
    [InlineData("metal", "Метал")]
    [InlineData("post-punk", "Постпанк")]
    [InlineData("indie", "Инди")]
    [InlineData("phonk", "Фонк")]
    [InlineData("hip-hop", "Хип-хоп")]
    [InlineData("anime", "Аниме/OST")]
    public void MappedGenresAreTranslated(string input, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(input));
    }

    [Fact]
    public void EmptyInputReturnsEmpty()
    {
        Assert.Equal(string.Empty, _normalizer.Normalize(string.Empty));
        Assert.Equal(string.Empty, _normalizer.Normalize("   "));
    }

    [Fact]
    public void UnknownGenreIsTitleCased()
    {
        Assert.Equal("Витчхаус", _normalizer.Normalize("витчхаус"));
    }

    [Fact]
    public void SemicolonSeparatedGenreUsesFirstKnown()
    {
        Assert.Equal("Рок", _normalizer.Normalize("Rock; Alternative"));
    }
}
