using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels.DialogWindow
{
    public partial class FirstRunDialogViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _selectedCurrency = "RUB";

        public ObservableCollection<string> AvailableCurrencies { get; } = new()
        {
            "RUB", "USD", "EUR", "GBP", "CNY", "KZT", "BYN"
        };

        public event Action<string>? OnConfirmed;

        [RelayCommand]
        private void Confirm()
        {
            OnConfirmed?.Invoke(SelectedCurrency);
        }
    }
}
