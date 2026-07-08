using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicBakh.Application.Abstractions;
using MusicBakh.Core.Abstractions;
using MusicBakh.Infrastructure.Covers;
using MusicBakh.Infrastructure.FileSystem;
using MusicBakh.Infrastructure.Import;
using MusicBakh.Infrastructure.Metadata;
using MusicBakh.Infrastructure.Migration;
using MusicBakh.Infrastructure.Persistence;
using MusicBakh.Infrastructure.Persistence.Repositories;
using MusicBakh.Infrastructure.Search;
using MusicBakh.Infrastructure.Seeding;
using MusicBakh.Infrastructure.Time;

namespace MusicBakh.Infrastructure.DependencyInjection;

/// <summary>
/// Регистрация SQLite-репозиториев, HTTP-клиентов, провайдеров метаданных и обложек,
/// импортёра, сервиса миграции и seed-сервиса в DI-контейнере.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMusicBakhInfrastructure(
        this IServiceCollection services,
        LibraryDbContextOptions options)
    {
        // Фабрика DbContext: каждый репозиторий получает короткоживущий контекст на вызов.
        services.AddDbContextFactory<LibraryDbContext>(opt => opt.UseSqlite(options.ConnectionString));
        services.AddSingleton<Func<LibraryDbContext>>(sp =>
            () => sp.GetRequiredService<IDbContextFactory<LibraryDbContext>>().CreateDbContext());

        // Доменные репозитории.
        services.AddSingleton<ITrackRepository, SqliteTrackRepository>();
        services.AddSingleton<IListeningHistoryRepository, SqliteListeningHistoryRepository>();
        services.AddSingleton<IPlayerSettingsRepository, SqlitePlayerSettingsRepository>();
        services.AddSingleton<ITagRepository, SqliteTagRepository>();
        services.AddSingleton<IPlaylistRepository, SqlitePlaylistRepository>();
        services.AddSingleton<ISearchService, SqliteFtsSearchService>();
        services.AddSingleton<IClock, SystemClock>();

        // HTTP-клиент и зависимые от него сервисы.
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IMusicBrainzClient>(sp => new MusicBrainzClient(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<IItunesCoverClient>(sp => new ItunesCoverClient(sp.GetRequiredService<HttpClient>()));

        // Метаданные и обложки.
        services.AddSingleton<ITagReader, TagLibSharpTagReader>();
        services.AddSingleton<IGenreNormalizer, RussianGenreNormalizer>();
        services.AddSingleton<IMetadataResolver, DefaultMetadataResolver>();
        services.AddSingleton<ICoverResolver, CompositeCoverResolver>();

        // Файловая система и пути хранения.
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IMusicStoragePaths, LocalAppDataMusicStoragePaths>();

        // Импортёр треков.
        services.AddSingleton<ITrackImporter, TrackImporter>();

        // Миграция данных и seeding. Сервис миграции получает Func<LibraryDbContext>
        // напрямую (а не ITrackRepository), чтобы выполнять перенос в одной транзакции.
        services.AddSingleton<JsonToSqliteMigrationService>(sp => new JsonToSqliteMigrationService(
            sp.GetRequiredService<IMusicStoragePaths>().RootDirectory,
            sp.GetRequiredService<Func<LibraryDbContext>>()));
        services.AddSingleton<BuiltInTrackSeeder>(sp => new BuiltInTrackSeeder(
            sp.GetRequiredService<ITrackRepository>(),
            BuiltInTracksProvider.GetDefaults));

        return services;
    }
}
