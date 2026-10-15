using MusicBakh.Application.Abstractions;
using MusicBakh.Application.Contracts;
using MusicBakh.Core.Domain;
using MusicLibrary.ViewModels;
using System.Windows;

namespace MusicLibrary.Views;

public sealed class AddTrackDialogService : IAddTrackDialogService
{
    private readonly IOpenFileDialogService _openFileDialog;
    private readonly ITrackImporter _importer;

    public AddTrackDialogService(IOpenFileDialogService openFileDialog, ITrackImporter importer)
    {
        _openFileDialog = openFileDialog;
        _importer = importer;
    }

    public Track? Show(Func<TrackImportCandidate, Track> save)
    {
        using var viewModel = new AddTrackViewModel(_openFileDialog, _importer, save);
        var window = new AddTrackWindow(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        bool? result = window.ShowDialog();
        return result == true ? viewModel.SavedTrack : null;
    }
}
