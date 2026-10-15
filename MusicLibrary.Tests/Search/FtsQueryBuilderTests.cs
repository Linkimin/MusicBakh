using MusicBakh.Infrastructure.Search;
using Xunit;

namespace MusicLibrary.Tests.Search;

public sealed class FtsQueryBuilderTests
{
    [Fact]
    public void Build_BoundsVeryLongInput()
    {
        Assert.Null(FtsQueryBuilder.Build(new string('a', 100_000)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Build_Returns_Null_For_Empty_Or_Whitespace(string? input)
    {
        Assert.Null(FtsQueryBuilder.Build(input));
    }

    [Theory]
    [InlineData("\"\"\"")]
    [InlineData("***")]
    [InlineData("(:)")]
    public void Build_Returns_Null_When_Only_Reserved_Chars(string input)
    {
        Assert.Null(FtsQueryBuilder.Build(input));
    }

    [Fact]
    public void Build_Single_Word_Wraps_And_Appends_Prefix_Star()
    {
        Assert.Equal("\"queen\"*", FtsQueryBuilder.Build("queen"));
    }

    [Fact]
    public void Build_Multiword_Joins_With_Space_Implicit_AND()
    {
        Assert.Equal("\"queen\"* \"radio\"*", FtsQueryBuilder.Build("queen radio"));
    }

    [Fact]
    public void Build_Strips_Reserved_Characters_From_Tokens()
    {
        Assert.Equal("\"foo\"*", FtsQueryBuilder.Build("foo*"));
        Assert.Equal("\"foo\"*", FtsQueryBuilder.Build("\"foo\""));
        Assert.Equal("\"bar\"*", FtsQueryBuilder.Build("(bar)"));
    }

    [Fact]
    public void Build_Handles_Cyrillic()
    {
        Assert.Equal("\"цой\"* \"звезда\"*", FtsQueryBuilder.Build("цой звезда"));
    }

    [Fact]
    public void Build_Caps_At_Ten_Tokens()
    {
        string raw = string.Join(' ', Enumerable.Range(1, 20).Select(i => $"w{i}"));
        string? result = FtsQueryBuilder.Build(raw);

        Assert.NotNull(result);
        Assert.Equal(10, result!.Split(' ').Length);
    }

    [Fact]
    public void Build_Does_Not_Throw_On_Injection_Attempts()
    {
        // Зарезервированные ключевые слова FTS5 (AND/OR/NOT) пользователю не запрещены —
        // они становятся обычными prefix-токенами и не дают пользователю составить
        // boolean-выражение, которое бы изменило семантику запроса.
        string? result = FtsQueryBuilder.Build("foo OR (1=1)");

        Assert.NotNull(result);
        Assert.Contains("\"foo\"*", result);
        Assert.Contains("\"OR\"*", result);
        Assert.Contains("\"1=1\"*", result);
    }
}
