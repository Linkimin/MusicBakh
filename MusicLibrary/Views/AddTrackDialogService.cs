using MusicLibrary.Models;
using MusicLibrary.Services.Files;
using MusicLibrary.Services.Import;
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

    public TrackImportCandidate? Show()
    {
        var viewModel = new AddTrackViewModel(_openFileDialog, _importer);
        var window = new AddTrackWindow(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        bool? result = window.ShowDialog();
        return result == true ? viewModel.Result : null;
    }
}
