using Avalonia.Controls;
using Client.ViewModels;

namespace Client.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    public ConfirmDialog(string title, string message)
    {
        InitializeComponent();
        DataContext = new ConfirmDialogViewModel(this, title, message);
    }
}
