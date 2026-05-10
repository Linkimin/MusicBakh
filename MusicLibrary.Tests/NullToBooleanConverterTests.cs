using MusicLibrary.Converters;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicLibrary.Tests;

public sealed class NullToBooleanConverterTests
{
    private readonly NullToBooleanConverter _converter = new();

    [Theory]
    [MemberData(nameof(FalseValues))]
    public void Convert_NullOrBindingSentinel_ReturnsFalse(object? value)
    {
        object result = _converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_Object_ReturnsTrue()
    {
        object result = _converter.Convert(new object(), typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }

    public static TheoryData<object?> FalseValues => new()
    {
        null,
        DependencyProperty.UnsetValue,
        Binding.DoNothing
    };
}
