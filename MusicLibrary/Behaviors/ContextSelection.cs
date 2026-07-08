using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Behaviors;

/// <summary>Определяет набор треков для операции по правому клику: если кликнутая строка входит
/// в мульти-выделение (&gt;1) — берём всё выделение; иначе — только кликнутый трек.</summary>
public static class ContextSelection
{
    public static IReadOnlyList<Track> Resolve(IList? selectedItems, Track clicked)
    {
        if (selectedItems is null) return new[] { clicked };
        var tracks = selectedItems.OfType<Track>().ToList();
        return tracks.Count > 1 && tracks.Contains(clicked) ? tracks : new List<Track> { clicked };
    }
}
