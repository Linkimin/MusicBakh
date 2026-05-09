using MusicLibrary.Models;

namespace MusicLibrary.Views;

/// <summary>
/// Абстракция над модальным окном добавления трека. Через неё MainViewModel
/// открывает окно, не зная про WPF Window напрямую — что облегчает юнит-тесты.
/// </summary>
public interface IAddTrackDialogService
{
    /// <summary>
    /// Открывает модальное окно добавления и возвращает заполненный кандидат, если пользователь подтвердил.
    /// </summary>
    TrackImportCandidate? Show();
}
