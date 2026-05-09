namespace MusicLibrary.Services.Files;

/// <summary>
/// Абстракция диалога подтверждения. Нужна, чтобы ViewModel не вызывал MessageBox напрямую —
/// тогда юнит-тесты могут подсунуть фейковую реализацию и не зависят от WPF UI потока.
/// </summary>
public interface IConfirmationService
{
    bool Confirm(string title, string message);
}
