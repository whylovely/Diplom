using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Client.Views;

public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        DataContext = new DialogVm(this, title, message);
    }

    private sealed class DialogVm
    {
        private readonly Window _wnd;

        public string Title { get; }
        public string Message { get; }

        public IRelayCommand CloseCommand { get; }

        public DialogVm(Window wnd, string title, string message)
        {
            _wnd = wnd;
            Title = title;
            Message = message;
            CloseCommand = new RelayCommand(() => _wnd.Close());
        }
    }
}
