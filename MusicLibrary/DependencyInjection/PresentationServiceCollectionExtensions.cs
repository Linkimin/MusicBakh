using Microsoft.Extensions.DependencyInjection;
using MusicBakh.Application.Abstractions;
using MusicLibrary.Services.Covers;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Playback;
using MusicLibrary.ViewModels;
using MusicLibrary.Views;

namespace MusicLibrary.DependencyInjection;

/// <summary>
/// Регистрация WPF-зависимых сервисов: аудиоплеер, файловые диалоги, брендированный
/// диалог подтверждения, процедурный генератор обложек (использует WPF), ViewModels
/// и MainWindow.
/// </summary>
public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddMusicBakhPresentation(this IServiceCollection services)
    {
        // WPF-специфичные сервисы.
        services.AddSingleton<IAudioPlayerService, MediaPlayerAudioService>();
        services.AddSingleton<IOpenFileDialogService, OpenFileDialogService>();
        services.AddSingleton<ISaveFileDialogService, SaveFileDialogService>();
        services.AddSingleton<IConfirmationService, ConfirmationDialogService>();
        services.AddSingleton<IProceduralCoverGenerator, ProceduralCoverGenerator>();

        // Диалоги и сервисы окон.
        services.AddSingleton<IAddTrackDialogService, AddTrackDialogService>();
        services.AddSingleton<IStatsWindowService, StatsWindowService>();
        services.AddSingleton<ITagsWindowService, TagsWindowService>();

        // ViewModels (transient — каждое окно получает свой инстанс).
        services.AddTransient<MainViewModel>();
        services.AddTransient<AddTrackViewModel>();
        services.AddTransient<StatsViewModel>();
        services.AddTransient<TagsViewModel>();

        // PlaylistsViewModel — singleton: состояние плейлистов должно жить всю сессию
        // приложения, а не пересоздаваться при каждом новом MainWindow/MainViewModel
        // (MainViewModel сам transient, но плейлисты — общий ресурс, как и IPlaylistRepository).
        services.AddSingleton<PlaylistsViewModel>();

        // MEDI не умеет автоматически генерировать Func<T> для DI — регистрируем явно.
        // Каждый вызов factory резолвит свежий transient ViewModel из корня
        // провайдера, поэтому окно всегда видит актуальные данные.
        services.AddSingleton<Func<StatsViewModel>>(sp =>
            () => sp.GetRequiredService<StatsViewModel>());
        services.AddSingleton<Func<TagsViewModel>>(sp =>
            () => sp.GetRequiredService<TagsViewModel>());

        // Окна.
        services.AddTransient<MainWindow>();

        return services;
    }
}
