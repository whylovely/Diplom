using Avalonia.Controls;
using Client.Models;
using Client.ViewModels;

namespace Client.Views;

public partial class MessageDialog : Window
{
    public MessageDialog() => InitializeComponent();

    public MessageDialog(string title, string message,
                         MessageLevel level = MessageLevel.Info)
    {
        InitializeComponent();
        DataContext = new MessageDialogViewModel(this, title, message, level);
    }
}
