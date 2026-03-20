using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;

        public event Action? OnLogoutRequested;
        public event Action? OnNavigateToCurrenciesRequested;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasFavorites))]
        private ObservableCollection<string> _favoriteCurrencies = new();

        private string? _selectedCurrency;
        public string? SelectedCurrency
        {
            get => _selectedCurrency;
            set
            {
                if (SetProperty(ref _selectedCurrency, value) && value != null)
                {
                    _settingsService.BaseCurrency = value;
                }
            }
        }

        public bool HasFavorites => FavoriteCurrencies.Count > 0;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadFavorites();
            
            _settingsService.SettingsChanged += LoadFavorites;
        }

        private void LoadFavorites()
        {
            var favs = _settingsService.Settings.FavoriteCurrencies ?? new System.Collections.Generic.List<string>();
            var currentBase = _settingsService.BaseCurrency;

            if (!favs.Contains(currentBase))
            {
                favs.Add(currentBase);
            }

            FavoriteCurrencies.Clear();
            foreach (var code in favs)
            {
                FavoriteCurrencies.Add(code);
            }

            OnPropertyChanged(nameof(HasFavorites));

            SelectedCurrency = currentBase;
        }

        [RelayCommand]
        private void Logout()
        {
            _settingsService.Logout();
            OnLogoutRequested?.Invoke();
        }

        [RelayCommand]
        private void GoToCurrencies()
        {
            OnNavigateToCurrenciesRequested?.Invoke();
        }
    }
}

