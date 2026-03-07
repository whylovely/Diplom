using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Client.Models;

namespace Client.ViewModels;

public sealed partial class MessageDialogViewModel : ViewModelBase  // окно уведомлений
{
    public string Title { get; }
    public string Message { get; }
    public MessageLevel Level { get; }

    public Geometry IconPath { get; }
    public IBrush IconColor { get; }

    private readonly Window _window;

    public MessageDialogViewModel(Window window, string title, string message, MessageLevel level = MessageLevel.Info)
    {
        _window = window;
        Title = title;
        Message = message;
        Level = level;

        (IconPath, IconColor) = GetIconData(level);
    }

    [RelayCommand] private void Close() => _window.Close();

    private static (Geometry path, IBrush color) GetIconData(MessageLevel level) => level switch
    {
        MessageLevel.Info => (
            Geometry.Parse(
                "M12,2 C6.48,2 2,6.48 2,12 C2,17.52 6.48,22 12,22 " +
                "C17.52,22 22,17.52 22,12 C22,6.48 17.52,2 12,2 Z " +
                "M13,17 L11,17 L11,11 L13,11 Z " +
                "M13,9 L11,9 L11,7 L13,7 Z"),
            new SolidColorBrush(Color.Parse("#035fa5"))),

        MessageLevel.Warning => (
            Geometry.Parse(
                "M1,21 L12,2 L23,21 Z " +
                "M13,18 L11,18 L11,16 L13,16 Z " +
                "M13,14 L11,14 L11,10 L13,10 Z"),
            new SolidColorBrush(Color.Parse("#ffaa00"))), 

        MessageLevel.Error => (
            Geometry.Parse(
                "M12,2 C6.48,2 2,6.48 2,12 C2,17.52 6.48,22 12,22 " +
                "C17.52,22 22,17.52 22,12 C22,6.48 17.52,2 12,2 Z " +
                "M15.59,7 L12,10.59 L8.41,7 L7,8.41 L10.59,12 L7,15.59 " +
                "L8.41,17 L12,13.41 L15.59,17 L17,15.59 L13.41,12 " +
                "L17,8.41 Z"),
            new SolidColorBrush(Color.Parse("#d4161c"))), 

        _ => (Geometry.Parse(""), Brushes.Transparent)
    };
}