using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.ViewModels;
using System.Threading.Tasks;

namespace Client.Views;

public partial class InputDialog : Window
{
    public InputDialog() => InitializeComponent();

    public Task<string?> ShowDialogAsync(Window owner, string title, string message, string? initialText)
    {
        var vm = new InputDialogViewModel
        {
            Title = title,
            Message = message,
            Text = initialText ?? ""
        };

        DataContext = vm;
        return ShowDialog<string?>(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InputDialogViewModel vm) { Close(null); return; }
        var text = vm.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        Close(text);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}