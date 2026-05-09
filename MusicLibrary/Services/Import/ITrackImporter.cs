namespace MusicLibrary.Services.Import;

/// <summary>
/// Полный pipeline добавления нового трека: получить файл (локально или по URL),
/// распознать метаданные, найти/нарисовать обложку, сохранить всё на диск и вернуть
/// заполненный TrackImportCandidate. progress отчитывается о прогрессе скачивания
/// для url-сценария; для локального файла шкала не нужна.
/// </summary>
public interface ITrackImporter
{
    Task<ImportResult> ImportAsync(ImportRequest request, IProgress<double>? progress, CancellationToken cancellationToken);
}
