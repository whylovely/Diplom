using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Client.Models;
using Client.Services;
using Client.ViewModels;

namespace Client.Views;

public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
    }

    private void AddAccount_Click(object? sender, RoutedEventArgs e)
    {
        var vm = DataContext as AccountsViewModel;

        // ¬ажно: если vm == null Ч DataContext не тот, и биндинги не работают
        vm?.Accounts.Insert(0, new Account { Name = "TEST", CurrencyCode = "RUB", Balance = 0 });
    }
}