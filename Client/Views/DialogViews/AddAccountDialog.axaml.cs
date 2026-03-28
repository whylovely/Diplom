using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.Models;
using Client.Services;
using Client.ViewModels;
using System.Threading.Tasks;

namespace Client.Views;

public partial class AddAccountDialog : Window
{
    public AddAccountDialog() => InitializeComponent();

    private string _baseCurrency = "RUB";

    public Task<Account?> ShowDialogAsync(Window owner, string baseCurrency, SettingsService? settings = null)
    {
        _baseCurrency = baseCurrency;
        DataContext = new AddAccountDialogViewModel(settings) { SelectedCurrency = baseCurrency };
        return ShowDialog<Account?>(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddAccountDialogViewModel vm) return;
        if (string.IsNullOrWhiteSpace(vm.Name)) return;

        var isMulti = vm.SelectedCurrency != _baseCurrency;
        var acc = new Account
        {
            Id = Guid.NewGuid(),
            Name = vm.Name.Trim(),
            CurrencyCode = vm.SelectedCurrency,
            InitialBalance = vm.InitialBalance,
            Balance = vm.InitialBalance,
            Type = AccountType.Assets,
            IsMultiCurrency = isMulti,
            SecondaryCurrencyCode = isMulti ? _baseCurrency : null,
            SecondaryBalance = 0
        };

        Close(acc);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}