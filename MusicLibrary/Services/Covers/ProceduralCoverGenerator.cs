using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicLibrary.Services.Covers;

/// <summary>
/// Генерирует 512x512 PNG с градиентом и крупной буквой исполнителя.
/// Цвета выбираются по хешу строки, чтобы у одного трека всегда была одна и та же обложка.
/// </summary>
public sealed class ProceduralCoverGenerator : IProceduralCoverGenerator
{
    private const int Size = 512;

    public ResolvedCover Generate(string artist, string title)
    {
        // WPF Visual API требует STA-поток. ConfigureAwait(false) увёл код на пул-поток,
        // поэтому переключаемся на UI-диспетчер для отрисовки.
        Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        return dispatcher.Invoke(() => RenderCover(artist, title));
    }

    private static ResolvedCover RenderCover(string artist, string title)
    {
        string seed = string.IsNullOrWhiteSpace(artist) ? title : $"{artist}|{title}";
        (Color a, Color b) = PickColors(seed);
        char letter = PickLetter(string.IsNullOrWhiteSpace(artist) ? title : artist);

        var grid = new Grid
        {
            Width = Size,
            Height = Size,
            Background = new LinearGradientBrush(a, b, new Point(0, 0), new Point(1, 1))
        };

        var letterText = new TextBlock
        {
            Text = letter.ToString(),
            FontSize = 280,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Playfair Display, Georgia"),
            Foreground = new SolidColorBrush(Color.FromArgb(220, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(letterText);

        grid.Measure(new Size(Size, Size));
        grid.Arrange(new Rect(0, 0, Size, Size));
        grid.UpdateLayout();

        var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(grid);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return new ResolvedCover { Bytes = stream.ToArray(), Extension = "png" };
    }

    private static (Color, Color) PickColors(string seed)
    {
        int hash = StableHash(seed);
        double hue1 = (hash & 0xFFFF) / 65535.0 * 360.0;
        double hue2 = (hue1 + 35.0) % 360.0;
        return (HsvToRgb(hue1, 0.55, 0.5), HsvToRgb(hue2, 0.7, 0.25));
    }

    private static char PickLetter(string source)
    {
        foreach (char ch in source)
        {
            if (char.IsLetterOrDigit(ch))
            {
                return char.ToUpperInvariant(ch);
            }
        }
        return '♪';
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            foreach (char ch in value)
            {
                hash = (hash * 31) ^ ch;
            }
            return Math.Abs(hash);
        }
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double hh = h / 60.0;
        double x = c * (1 - Math.Abs(hh % 2 - 1));
        double r = 0, g = 0, b = 0;

        switch ((int)hh)
        {
            case 0: r = c; g = x; break;
            case 1: r = x; g = c; break;
            case 2: g = c; b = x; break;
            case 3: g = x; b = c; break;
            case 4: r = x; b = c; break;
            default: r = c; b = x; break;
        }

        double m = v - c;
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
