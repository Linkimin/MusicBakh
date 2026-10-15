using MusicBakh.Application.Contracts;

namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Полный pipeline добавления нового трека: получить файл (локально или по URL),
/// распознать метаданные, найти/нарисовать обложку, сохранить всё на диск и вернуть
/// заполненный TrackImportCandidate. progress отчитывается о прогрессе скачивания
/// для url-сценария; для локального файла шкала не нужна.
/// </summary>
public interface ITrackImporter
{
    /// <summary>Удаляет принадлежащие импортёру файлы неподтверждённого кандидата.</summary>
    void Discard(TrackImportCandidate candidate);
    Task<ImportResult> ImportAsync(ImportRequest request, IProgress<double>? progress, CancellationToken cancellationToken);
}
