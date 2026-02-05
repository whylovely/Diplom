using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;

namespace Client.ViewModels;

public sealed partial class MessageDialogViewModel : ViewModelBase
{
    public string Title { get; }
    public string Message { get; }

    private readonly Window _window;

    public MessageDialogViewModel(Window window, string title, string message)
    {
        _window = window;
        Title = title;
        Message = message;
    }

    [RelayCommand]
    private void Close()
    {
        _window.Close();
    }
}