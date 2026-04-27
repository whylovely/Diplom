using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Client.ViewModels
{
    /// <summary>
    /// Страница «Курсы валют»: просмотр актуальных курсов и управление списком избранного.
    /// Курсы пуш-обновляются <see cref="CurrencyRateService"/> при старте приложения.
    /// </summary>
    public partial class CurrenciesViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly SettingsService _settings;
        
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        public int[] DummyItems { get; } = new int[4];

        public ObservableCollection<CurrencyItemViewModel> Currencies { get; } = new();

        private CancellationTokenSource? _searchCts;

        public CurrenciesViewModel(IDataService data, SettingsService settings)
        {
            _data = data;
            _settings = settings;

            LoadFavorites();
        }

        partial void OnSearchTextChanged(string value)
        {
            _searchCts?.Cancel();

            if (string.IsNullOrWhiteSpace(value))
            {
                IsSearching = false;
                LoadFavorites();
                return;
            }

            _searchCts = new CancellationTokenSource();
            _ = PerformSearchAsync(value, _searchCts.Token);
        }

        private async Task PerformSearchAsync(string query, CancellationToken token)
        {
            try
            {
                Currencies.Clear();
                IsSearching = true;

                // Debounce
                await Task.Delay(400, token);
                token.ThrowIfCancellationRequested();

                var q = query.ToLower();
                var favorites = _settings.Settings.FavoriteCurrencies ?? new List<string>();
                var baseCur = _settings.BaseCurrency;

                var allCodes = _data.CurrencyRates.Select(r => r.CurrencyCode).ToList();
                if (!allCodes.Contains("RUB")) allCodes.Add("RUB");
                foreach (var c in CurrencyHelper.AvailableCurrencies)
                {
                    if (!allCodes.Contains(c)) allCodes.Add(c);
                }

                var results = allCodes.Distinct()
                    .Where(c => c.ToLower().Contains(q) || CurrencyHelper.GetCurrencyName(c).ToLower().Contains(q))
                    .ToList();

                token.ThrowIfCancellationRequested();

                Currencies.Clear();
                foreach (var code in results)
                {
                    var isFav = favorites.Contains(code);
                    var rate = _data.GetRate(code, baseCur);
                    Currencies.Add(new CurrencyItemViewModel(code, CurrencyHelper.GetCurrencyName(code), rate, isFav, baseCur, this));
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsSearching = false;
                }
            }
        }

        private void LoadFavorites()
        {
            Currencies.Clear();
            var favorites = _settings.Settings.FavoriteCurrencies ?? new List<string>();
            var baseCur = _settings.BaseCurrency;
            
            var list = favorites.Count > 0 ? favorites : new List<string> { "RUB", "USD", "EUR", "BTC" };

            foreach (var code in list)
            {
                var rate = _data.GetRate(code, baseCur);
                Currencies.Add(new CurrencyItemViewModel(
                    code, 
                    CurrencyHelper.GetCurrencyName(code), 
                    rate,
                    favorites.Contains(code), 
                    baseCur,
                    this));
            }
        }

        public void ToggleFavorite(string code)
        {
            var favorites = _settings.Settings.FavoriteCurrencies ?? new List<string>();
            if (favorites.Contains(code))
                favorites.Remove(code);
            else
                favorites.Add(code);

            _settings.Settings.FavoriteCurrencies = favorites;
            _settings.Save();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                LoadFavorites();
            }
            else
            {
                var item = Currencies.FirstOrDefault(c => c.Code == code);
                if (item != null)
                {
                    item.IsFavorite = favorites.Contains(code);
                }
            }
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }
    }

    public partial class CurrencyItemViewModel : ObservableObject
    {
        private readonly CurrenciesViewModel _parent;
        
        public string Code { get; }
        public string FullName { get; }
        public decimal Rate { get; }
        public string BaseCurrency { get; }
        
        public string FormattedRate => $"1 {Code} = {Rate:0.####} {BaseCurrency}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FavoriteIconData))]
        [NotifyPropertyChangedFor(nameof(FavoriteColor))]
        private bool _isFavorite;

        public string FavoriteIconData => IsFavorite 
            ? "M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" 
            : "M16.5 3c-1.74 0-3.41.81-4.5 2.09C10.91 3.81 9.24 3 7.5 3 4.42 3 2 5.42 2 8.5c0 3.78 3.4 6.86 8.55 11.54L12 21.35l1.45-1.32C18.6 15.36 22 12.28 22 8.5 22 5.42 19.58 3 16.5 3zm-4.4 15.55l-.1.1-.1-.1C7.14 14.24 4 11.39 4 8.5 4 6.5 5.5 5 7.5 5c1.54 0 3.04.99 3.57 2.36h1.87C13.46 5.99 14.96 5 16.5 5c2 0 3.5 1.5 3.5 3.5 0 2.89-3.14 5.74-7.9 10.05z";

        public string FavoriteColor => IsFavorite ? "#FFD54F" : "#424242";

        public CurrencyItemViewModel(string code, string fullName, decimal rate, bool isFavorite, string baseCurrency, CurrenciesViewModel parent)
        {
            Code = code;
            FullName = fullName;
            Rate = rate;
            IsFavorite = isFavorite;
            BaseCurrency = baseCurrency;
            _parent = parent;
        }

        [RelayCommand]
        private void ToggleFavorite()
        {
            IsFavorite = !IsFavorite;
            _parent.ToggleFavorite(Code);
        }
    }
}