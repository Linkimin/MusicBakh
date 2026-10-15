using System.Windows;
using System.Windows.Controls;
using MusicLibrary.Services.Library;

namespace MusicLibrary.Selectors;

/// <summary>
/// Выбирает DataTemplate по типу LeftColumnState. Сами шаблоны передаются
/// через свойства; они задаются в XAML рядом с ContentControl.
/// </summary>
public sealed class LeftColumnTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TracksTemplate { get; set; }
    public DataTemplate? AlbumsTemplate { get; set; }
    public DataTemplate? ArtistsTemplate { get; set; }
    public DataTemplate? AlbumDetailTemplate { get; set; }
    public DataTemplate? ArtistDetailTemplate { get; set; }
    public DataTemplate? PlaylistsTemplate { get; set; }
    public DataTemplate? PlaylistDetailTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item switch
    {
        LeftColumnState.TracksRoot   => TracksTemplate,
        LeftColumnState.AlbumsRoot   => AlbumsTemplate,
        LeftColumnState.ArtistsRoot  => ArtistsTemplate,
        LeftColumnState.AlbumDetail  => AlbumDetailTemplate,
        LeftColumnState.ArtistDetail => ArtistDetailTemplate,
        LeftColumnState.PlaylistsRoot   => PlaylistsTemplate,
        LeftColumnState.PlaylistDetail  => PlaylistDetailTemplate,
        _ => null
    };
}
