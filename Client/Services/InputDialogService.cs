using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Client.Views;

namespace Client.Services;

public interface IInputDialogService
{
    Task<string?> PromptAsync(string title, string message, string? initialText = null);
}

public sealed class InputDialogService : IInputDialogService
{
    public async Task<string?> PromptAsync(string title, string message, string? initialText = null)
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        if (owner is null) return null;

        var dlg = new InputDialog();
        return await dlg.ShowDialogAsync(owner, title, message, initialText);
    }
}