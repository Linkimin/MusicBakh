using MusicLibrary.ViewModels;
using System.Windows;

namespace MusicLibrary.Views;

public partial class AddTrackWindow : Window
{
    public AddTrackWindow(AddTrackViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, confirmed) =>
        {
            DialogResult = confirmed;
            Close();
        };
    }
}
