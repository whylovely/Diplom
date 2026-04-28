using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels.DialogWindow
{
    public partial class CurrencyRatesDialogViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly SettingsService _settings;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        public ObservableCollection<CurrencyRateItem> Items { get; } = new();

        public event Action? OnCloseRequested;

        public CurrencyRatesDialogViewModel(IDataService data, SettingsService settings)
        {
            _data = data;
            _settings = settings;

            LoadRates();
        }

        partial void OnSearchQueryChanged(string value)
        {
            LoadRates();
        }

        private void LoadRates()
        {
            Items.Clear();

            var baseCur = _settings.BaseCurrency;
            var favorites = _settings.Settings.FavoriteCurrencies ?? new List<string>();
            var query = SearchQuery?.Trim().ToLower() ?? "";

            var list = new List<CurrencyRateItem>();

            var allCodes = _data.CurrencyRates.Select(r => r.CurrencyCode).ToList();
            if (!allCodes.Contains("RUB")) allCodes.Add("RUB");
            
            foreach (var code in CurrencyHelper.AvailableCurrencies)
            {
                if (!allCodes.Contains(code))
                    allCodes.Add(code);
            }

            allCodes = allCodes.Distinct().ToList();

            foreach (var code in allCodes)
            {
                if (code == baseCur) continue;

                var name = CurrencyHelper.GetCurrencyName(code);
                
                if (!string.IsNullOrEmpty(query))
                {
                    if (!code.ToLower().Contains(query) && !name.ToLower().Contains(query))
                    {
                        continue;
                    }
                }

                var isFav = favorites.Contains(code);
                var rate = _data.GetRate(code, baseCur);

                list.Add(new CurrencyRateItem(this, code, name, rate, isFav, baseCur));
            }

            var sorted = list.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.Code);

            foreach (var item in sorted)
            {
                Items.Add(item);
            }
        }

        public void ToggleFavorite(string code)
        {
            var favorites = _settings.Settings.FavoriteCurrencies ?? new List<string>();
            if (favorites.Contains(code))
            {
                favorites.Remove(code);
            }
            else
            {
                favorites.Add(code);
            }
            
            _settings.Settings.FavoriteCurrencies = favorites;
            _settings.Save();
            
            LoadRates();
        }

        [RelayCommand]
        private void Close()
        {
            OnCloseRequested?.Invoke();
        }
    }

    public partial class CurrencyRateItem : ObservableObject
    {
        private readonly CurrencyRatesDialogViewModel _parent;

        public string Code { get; }
        public string Name { get; }
        public decimal Rate { get; }
        public bool IsFavorite { get; }
        public string BaseCurrency { get; }

        public string FormattedRate => $"1 {Code} = {Rate:0.####} {BaseCurrency}";
        
        public string FavoriteIconData => IsFavorite 
            ? "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" 
            : "M16.5 3c-1.74 0-3.41.81-4.5 2.09C10.91 3.81 9.24 3 7.5 3 4.42 3 2 5.42 2 8.5c0 3.78 3.4 6.86 8.55 11.54L12 21.35l1.45-1.32C18.6 15.36 22 12.28 22 8.5 22 5.42 19.58 3 16.5 3zm-4.4 15.55l-.1.1-.1-.1C7.14 14.24 4 11.39 4 8.5 4 6.5 5.5 5 7.5 5c1.54 0 3.04.99 3.57 2.36h1.87C13.46 5.99 14.96 5 16.5 5c2 0 3.5 1.5 3.5 3.5 0 2.89-3.14 5.74-7.9 10.05z";

        public string FavoriteColor => IsFavorite ? "#e53935" : "#888888"; // Red for favorite, gray for not

        public CurrencyRateItem(CurrencyRatesDialogViewModel parent, string code, string name, decimal rate, bool isFavorite, string baseCurrency)
        {
            _parent = parent;
            Code = code;
            Name = name;
            Rate = rate;
            IsFavorite = isFavorite;
            BaseCurrency = baseCurrency;
        }

        [RelayCommand]
        private void Toggle()
        {
            _parent.ToggleFavorite(Code);
        }
    }
}