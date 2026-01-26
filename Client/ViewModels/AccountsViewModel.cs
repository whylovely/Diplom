using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public sealed partial class AccountsViewModel : ViewModelBase
    {
        private readonly IDataService _data;

        public ObservableCollection<Account> Accounts { get; }

        public AccountsViewModel(IDataService data)
        {
            _data = data;
            Accounts = new ObservableCollection<Account>(
                _data.Accounts.Where(a => a.Type == AccountType.Assets)
                );
        }

        [RelayCommand]
        private void AddAccount()
        {
            var acc = new Account 
            { 
                Name = "Новый счет", 
                CurrencyCode = "RUB",
                Type = AccountType.Assets,
                Balance = 0 
            };
            _data.AddAccount(acc);
            Accounts.Insert(0, acc);
        }
    }
}