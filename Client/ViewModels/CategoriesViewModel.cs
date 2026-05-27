using Avalonia.Styling;
using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Client.ViewModels;

public sealed partial class CategoriesViewModel : ViewModelBase
{
    private readonly IDataService _data;
    private readonly INotificationService _notify;
    private readonly ICategoryDialogService _catDialog;

    public ObservableCollection<Category> Categories { get; } = new();

    [ObservableProperty]
    private Category? _selected;

    public CategoriesViewModel(
        IDataService data, 
        INotificationService notify, 
        ICategoryDialogService catDialog)
    {
        _data = data;
        _notify = notify;
        _catDialog = catDialog;

        _data.DataChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        var selectedId = Selected?.Id;

        Categories.Clear();
        foreach (var c in _data.Categories.OrderBy(c => c.Name))
            Categories.Add(new Category { Id = c.Id, Name = c.Name, Kind = c.Kind });

        Selected = selectedId.HasValue ? Categories.FirstOrDefault(c => c.Id == selectedId) ?? Categories.FirstOrDefault() : Categories.FirstOrDefault();
    }

    private bool hasSelection() => Selected is not null;

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        var created = await _catDialog.ShowAddCategoryDialogAsync();
        if (created is null) return;

        if (_data.Categories.Any(c => c.Name.Equals(created.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await _notify.ShowErrorAsync($"Категория \"{created.Name}\" уже существует.");
            return;
        }

        _data.AddCategory(created);
        Refresh();

        await _notify.ShowInfoAsync($"Категория \"{created.Name}\" добавлена.");
    }

    [RelayCommand(CanExecute = nameof(hasSelection))]
    private async Task EditCategoryAsync()
    {
        if (Selected is null) return;

        var result = await _catDialog.ShowAddCategoryDialogAsync(
            initialName: Selected.Name,
            initialKind: Selected.Kind);
        if (result is null) return;

        var newName = result.Name.Trim();

        if (_data.Categories.Any(c => c.Id != Selected.Id && c.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            await _notify.ShowErrorAsync($"Категория \"{newName}\" уже существует.");
            return;
        }

        _data.RenameCategory(Selected, newName, result.Kind);

        await _notify.ShowInfoAsync("Категория обновлена.");
    }

    [RelayCommand(CanExecute = nameof(hasSelection))]
    private async Task DeleteCategoryAsync()
    {
        if (Selected is null) return;

        var usedCount = _data.Transactions.SelectMany(t => t.Entries).Count(e => e.CategoryId == Selected.Id);

        if (usedCount > 0)
        {
            await _notify.ShowErrorAsync($"Нельзя удалить категорию \"{Selected.Name}\": используется в операциях ({usedCount}).");
            return;
        }

        var confirmed = await _notify.ShowConfirmAsync(
            $"Вы уверены, что хотите удалить категорию «{Selected.Name}»?",
            "Удаление категории");

        if (!confirmed) return;

        _data.RemoveCategory(Selected);
        Refresh();
    }

    partial void OnSelectedChanged(Category? value)
    {
        EditCategoryCommand.NotifyCanExecuteChanged();
        DeleteCategoryCommand.NotifyCanExecuteChanged();
    }
}