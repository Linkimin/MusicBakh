namespace MusicLibrary.Services.Import;

/// <summary>
/// Источник трека для импорта. Реализован через discriminated union на record-наследниках,
/// чтобы импортёр единым switch-выражением обрабатывал и локальный файл, и URL.
/// </summary>
public abstract record ImportRequest;

public sealed record LocalFileImportRequest(string FilePath) : ImportRequest;

public sealed record UrlImportRequest(string Url) : ImportRequest;
