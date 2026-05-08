namespace MusicLibrary.Models;

public enum OperationMessageKind
{
    Info,
    Success,
    Error
}

/// <summary>
/// Единый результат пользовательской операции: успешность и текст для вывода в интерфейс.
/// </summary>
public sealed class OperationResult
{
    private OperationResult(bool isSuccess, string message, OperationMessageKind kind)
    {
        IsSuccess = isSuccess;
        Message = message;
        Kind = kind;
    }

    public bool IsSuccess { get; }
    public string Message { get; }
    public OperationMessageKind Kind { get; }

    public static OperationResult Success(string message) => new(true, message, OperationMessageKind.Success);
    public static OperationResult Info(string message) => new(true, message, OperationMessageKind.Info);
    public static OperationResult Error(string message) => new(false, message, OperationMessageKind.Error);
}
