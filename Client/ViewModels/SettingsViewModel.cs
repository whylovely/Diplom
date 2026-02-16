using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
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
    }
}
