using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Client.Models;
using Client.Views;

namespace Client.Services;

public sealed class CategoryDialogService : ICategoryDialogService
{
    public async Task<Category?> ShowAddCategoryDialogAsync(string? initialName = null)
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;

        if (owner is null)
            return null;

        var dlg = new AddCategoryDialog(initialName);
        return await dlg.ShowDialog<Category?>(owner);
    }
}