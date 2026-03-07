using Client.Models;
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

        [ObservableProperty] private string _selectedCurrency;
        
        public event Action? OnLogoutRequested;

        public string[] AvailableCurrencies => CurrencyHelper.AvailableCurrencies;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _selectedCurrency = _settingsService.BaseCurrency;
        }

        [RelayCommand]
        private void Save()
        {
            if (!string.IsNullOrEmpty(SelectedCurrency))
            {
                _settingsService.BaseCurrency = SelectedCurrency;
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

