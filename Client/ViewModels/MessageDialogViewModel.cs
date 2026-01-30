using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels;

public sealed class MessageDialogViewModel
{
    private readonly Window _wnd;

    public string Title { get; }
    public string Message { get; }

    public IRelayCommand CloseCommand { get; }

    public MessageDialogViewModel(Window wnd, string title, string message)
    {
        _wnd = wnd;
        Title = title;
        Message = message;
        CloseCommand = new RelayCommand(() => _wnd.Close());
    }
}