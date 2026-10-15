using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MusicBakh.Infrastructure.DependencyInjection;
using MusicBakh.Infrastructure.Migration;
using MusicBakh.Infrastructure.Persistence;
using MusicBakh.Infrastructure.Seeding;
using MusicLibrary.DependencyInjection;

namespace MusicLibrary;

/// <summary>
/// Точка входа WPF-приложения. Поднимает DI-host, прогоняет миграции SQLite, мигрирует legacy
/// userTracks.json (если он есть), наполняет встроенные треки и открывает MainWindow.
/// </summary>
public partial class App : Application
{
    private readonly bool _startServices;
    public App() : this(startServices: true) { }
    internal App(bool startServices) => _startServices = startServices;

    private IHost? _host;
    private TextWriterTraceListener? _diagnostics;

    protected override void OnStartup(StartupEventArgs e)
    {
        // WPF queues Startup even without Run(); rendering tests must load resources
        // without starting DI, migrating storage or opening the production window.
        if (!_startServices) return;
        // Стабильный AppUserModelID нужен, чтобы Windows корректно группировал приложение в taskbar.
        _ = SetCurrentProcessExplicitAppUserModelID("MusicBakh.Player");

        base.OnStartup(e);
        InitializeDiagnostics();
        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddMusicBakhInfrastructure(LibraryDbContextOptions.Default);
                    services.AddMusicBakhPresentation();
                })
                .Build();

            InitializeDatabase(_host.Services);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            Trace.TraceError("Ошибка запуска MusicBakh: {0}", exception);
            MessageBox.Show($"Не удалось открыть музыкальную библиотеку.\n\n{exception.Message}\n\n" +
                $"Данные не сброшены. Проверьте доступ к папке:\n{Path.GetDirectoryName(LibraryDbContextOptions.Default.DatabasePath)}\n\n" +
                "Если ошибка возникла после обновления, сохраните эту папку вместе с файлами .backup и журналом musicbakh.log.",
                "MusicBakh — ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void InitializeDiagnostics()
    {
        try
        {
            LibraryDbContextOptions.Default.EnsureDirectory();
            string path = Path.Combine(Path.GetDirectoryName(LibraryDbContextOptions.Default.DatabasePath)!, "musicbakh.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                File.Move(path, path + ".previous", overwrite: true);
            _diagnostics = new TextWriterTraceListener(path);
            Trace.Listeners.Add(_diagnostics);
            Trace.AutoFlush = true;
            Trace.TraceInformation("Запуск MusicBakh {0:O}", DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Недоступный журнал не должен препятствовать показу ошибки основной операции.
            Debug.WriteLine(ex);
        }
    }

    private static void InitializeDatabase(IServiceProvider services)
    {
        // 0) На чистой машине каталог %LocalAppData%\MusicLibrary ещё не существует;
        // SQLite не создаёт его сам — нужно явно перед открытием соединения.
        LibraryDbContextOptions.Default.EnsureDirectory();

        // 1) Создаём БД и применяем миграции.
        DatabaseMaintenance.Migrate(LibraryDbContextOptions.Default);

        // 2) Мигрируем legacy userTracks.json при первом запуске после апгрейда с 1.0.x.
        var migration = services.GetRequiredService<JsonToSqliteMigrationService>();
        migration.Run();

        // 3) Сеем встроенные треки (сравнение по парам Artist/Title): срабатывает
        // и на чистой БД, и сразу после JsonToSqliteMigrationService — когда
        // пользовательские треки уже есть, но эталонных ещё нет.
        var seeder = services.GetRequiredService<BuiltInTrackSeeder>();
        seeder.SeedBuiltIns();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        if (_diagnostics is not null)
        {
            Trace.Listeners.Remove(_diagnostics);
            _diagnostics.Dispose();
        }
        base.OnExit(e);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}
