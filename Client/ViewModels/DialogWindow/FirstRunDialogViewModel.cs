using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels.DialogWindow
{
    public partial class FirstRunDialogViewModel : ViewModelBase    // первое открытие в жизни
    {
        [ObservableProperty] private string _selectedCurrency = "RUB";

        public string[] AvailableCurrencies => Models.CurrencyHelper.AvailableCurrencies;

        public event Action<string>? OnConfirmed;

        [RelayCommand] private void Confirm() => OnConfirmed?.Invoke(SelectedCurrency);
    }
}