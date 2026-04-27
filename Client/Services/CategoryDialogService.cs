using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Client.Models;
using Client.Views;

namespace Client.Services;

/// <summary>
/// Показывает диалог добавления категории. Используется из NewTransactionViewModel,
/// когда пользователь начал вводить категорию, которой ещё нет — можно создать тут же.
/// </summary>
public interface ICategoryDialogService
{
    Task<Category?> ShowAddCategoryDialogAsync(string? initialName = null);
}

public sealed class CategoryDialogService : ICategoryDialogService
{
    public async Task<Category?> ShowAddCategoryDialogAsync(string? initialName = null)
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;

        if (owner is null)
            return null;

        var dlg = new AddCategoryDialog();
        return await dlg.ShowDialogAsync(owner, initialName);
    }
}