using MusicLibrary.ViewModels;
using System.Windows;

namespace MusicLibrary.Views;

public partial class AddTrackWindow : Window
{
    public AddTrackWindow(AddTrackViewModel viewModel)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeWindowAppearance.Apply(this);
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
        viewModel.CloseRequested += (_, confirmed) =>
        {
            DialogResult = confirmed;
            Close();
        };
    }
}
