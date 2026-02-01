using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.Models;
using Client.ViewModels;
using System.Threading.Tasks;

namespace Client.Views;

public partial class AddCategoryDialog : Window
{
    public AddCategoryDialog()
    {
        InitializeComponent();
    }

    public Task<Category?> ShowDialogAsync(Window owner, string? initialName = null)
    {
        DataContext = new AddCategoryDialogViewModel(initialName);
        return ShowDialog<Category?>(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddCategoryDialogViewModel vm) return;
        if (string.IsNullOrWhiteSpace(vm.Name)) return;

        Close(new Category
        {
            Id = Guid.NewGuid(),
            Name = vm.Name.Trim(),
            Kind = vm.Kind
        });
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}