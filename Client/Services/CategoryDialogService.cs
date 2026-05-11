using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Client.Models;
using Client.Views;

namespace Client.Services;

// Показывает диалог добавления категории
public interface ICategoryDialogService
{
    Task<Category?> ShowAddCategoryDialogAsync(string? initialName = null, CategoryKind? initialKind = null);
}

public sealed class CategoryDialogService : ICategoryDialogService
{
    public async Task<Category?> ShowAddCategoryDialogAsync(string? initialName = null, CategoryKind? initialKind = null)
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;

        if (owner is null)
            return null;

        var dlg = new AddCategoryDialog();
        return await dlg.ShowDialogAsync(owner, initialName, initialKind);
    }
}