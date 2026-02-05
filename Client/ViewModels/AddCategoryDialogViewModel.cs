using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Client.Models;

namespace Client.ViewModels;

public sealed partial class AddCategoryDialogViewModel : ViewModelBase
{
    public sealed record KindItem(CategoryKind Kind, string Title);

    public KindItem[] KindItems { get; } =
    {
        new(CategoryKind.Expense, "Расход"),
        new(CategoryKind.Income,  "Доход"),
    };

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private KindItem _selectedKind;

    public bool CanOk => !string.IsNullOrWhiteSpace(Name);

    public Action<bool>? Close { get; set; }

    public AddCategoryDialogViewModel(string? initialName = null)
    {
        _name = initialName ?? "";
        _selectedKind = KindItems[0];
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanOk));

    public CategoryKind Kind => SelectedKind.Kind;
}