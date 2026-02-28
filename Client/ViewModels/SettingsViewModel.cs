using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace Client.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        private string _selectedCurrency;

        public ObservableCollection<string> AvailableCurrencies { get; } = new()
        {
            "RUB", "USD", "EUR", "GBP", "CNY", "KZT", "BYN"
        };

        public event Action? OnLogoutRequested;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _selectedCurrency = _settingsService.BaseCurrency;
        }

        partial void OnSelectedCurrencyChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _settingsService.BaseCurrency = value;
            }
        }

        [RelayCommand]
        private void Logout()
        {
            _settingsService.Logout();
            OnLogoutRequested?.Invoke();
        }
    }
}

