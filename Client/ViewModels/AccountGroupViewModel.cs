using System;
using System.Collections.ObjectModel;
using System.Linq;
using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels
{
    public sealed partial class AccountGroupViewModel : ObservableObject
    {
        private readonly IDataService _data;
        private readonly SettingsService _settings;

        public AccountGroup? Group { get; }
        public string Name => Group?.Name ?? "Без группы";
        public Guid? Id => Group?.Id;

        public ObservableCollection<Account> Accounts { get; } = new();

        public decimal TotalBalance
        {
            get
            {
                return Accounts.Sum(a => a.Balance * _data.GetRate(a.CurrencyCode, _settings.BaseCurrency));
            }
        }

        public string BaseCurrency => _settings.BaseCurrency;

        public AccountGroupViewModel(AccountGroup? group, IDataService data, SettingsService settings)
        {
            Group = group;
            _data = data;
            _settings = settings;
        }

        public void Refresh()
        {
            foreach (var a in Accounts)
            {
                if (a.IsMultiCurrency && !string.IsNullOrEmpty(a.SecondaryCurrencyCode))
                {
                    a.SecondaryBalance = a.Balance * _data.GetRate(a.CurrencyCode, a.SecondaryCurrencyCode);
                }
                else
                {
                    a.SecondaryBalance = 0;
                }
            }

            OnPropertyChanged(nameof(TotalBalance));
            OnPropertyChanged(nameof(Name));
        }
    }
}