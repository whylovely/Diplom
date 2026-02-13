using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Client.Models;

namespace Client.ViewModels;

public sealed partial class ConfirmDialogViewModel : ViewModelBase
{
    public string Title { get; }
    public string Message { get; }

    public Geometry IconPath { get; }
    public IBrush IconColor { get; }

    private readonly Window _window;

    public ConfirmDialogViewModel(Window window, string title, string message)
    {
        _window = window;
        Title = title;
        Message = message;

        IconPath = Geometry.Parse(
            "M1,21 L12,2 L23,21 Z " +
            "M13,18 L11,18 L11,16 L13,16 Z " +
            "M13,14 L11,14 L11,10 L13,10 Z");
        IconColor = new SolidColorBrush(Color.Parse("#FF8C00"));
    }

    [RelayCommand]
    private void Confirm() => _window.Close(true);

    [RelayCommand]
    private void Cancel() => _window.Close(false);
}
