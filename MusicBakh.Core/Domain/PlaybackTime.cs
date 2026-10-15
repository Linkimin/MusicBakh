namespace MusicBakh.Core.Domain;

public static class PlaybackTime
{
    public static string Format(TimeSpan duration)
    {
        long seconds = Math.Max(0, duration.Ticks / TimeSpan.TicksPerSecond);
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}
