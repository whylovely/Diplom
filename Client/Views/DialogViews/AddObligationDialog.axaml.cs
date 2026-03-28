using Avalonia.Controls;
using Client.Models;
using Client.Services;
using Client.ViewModels.DialogWindow;

namespace Client.Views;

public partial class AddObligationDialog : Window
{
    public AddObligationDialog(SettingsService? settings = null)
    {
        InitializeComponent();
        DataContext = new AddObligationDialogViewModel(this, settings);
    }

    public AddObligationDialog(Obligation? existing, SettingsService? settings = null)
    {
        InitializeComponent();
        DataContext = new AddObligationDialogViewModel(this, settings, existing);
    }
}