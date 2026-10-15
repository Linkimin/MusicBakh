using MusicBakh.Application.Contracts;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Views;

/// <summary>
/// Абстракция над модальным окном добавления трека. Через неё MainViewModel
/// открывает окно, не зная про WPF Window напрямую — что облегчает юнит-тесты.
/// </summary>
public interface IAddTrackDialogService
{
    /// <summary>
    /// Сохраняет кандидат до закрытия окна; при ошибке окно остаётся открытым для повтора или отмены.
    /// </summary>
    Track? Show(Func<TrackImportCandidate, Track> save);
}
