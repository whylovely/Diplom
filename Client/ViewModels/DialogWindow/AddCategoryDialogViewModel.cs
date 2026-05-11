using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Client.Models;

namespace Client.ViewModels;

public sealed partial class AddCategoryDialogViewModel : ViewModelBase  // Создание категорий
{
    public sealed record KindItem(CategoryKind Kind, string Title);

    public KindItem[] KindItems { get; } =
    {
        new(CategoryKind.Expense, "Расход"),
        new(CategoryKind.Income,  "Доход")
    };

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private KindItem _selectedKind;

    public bool CanOk => !string.IsNullOrWhiteSpace(Name);  // Серые кнопки

    public Action<bool>? Close { get; set; }

    public string DialogTitle { get; }
    public string OkButtonText { get; }

    public AddCategoryDialogViewModel(string? initialName = null, CategoryKind? initialKind = null)
    {
        _name = initialName ?? "";
        _selectedKind = initialKind.HasValue
            ? KindItems.First(k => k.Kind == initialKind.Value)
            : KindItems[0];
        DialogTitle   = initialName is null ? "Новая категория"  : "Редактировать категорию";
        OkButtonText  = initialName is null ? "✓ Создать"        : "✓ Сохранить";
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanOk));

    public CategoryKind Kind => SelectedKind.Kind;
}