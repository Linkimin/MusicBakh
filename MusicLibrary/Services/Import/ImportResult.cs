using MusicLibrary.Models;

namespace MusicLibrary.Services.Import;

public sealed class ImportResult
{
    private ImportResult(bool isSuccess, string message, TrackImportCandidate? candidate)
    {
        IsSuccess = isSuccess;
        Message = message;
        Candidate = candidate;
    }

    public bool IsSuccess { get; }
    public string Message { get; }
    public TrackImportCandidate? Candidate { get; }

    public static ImportResult Success(TrackImportCandidate candidate) => new(true, "Импорт завершен.", candidate);
    public static ImportResult Error(string message) => new(false, message, null);
}
