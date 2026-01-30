using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Client.ViewModels
{
    public sealed partial class AccountsViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private readonly ICategoryDialogService _catDialog;
        private readonly Action _onCatAdded;

        public ObservableCollection<Account> Accounts { get; }

        public AccountsViewModel(IDataService data, INotificationService notify, ICategoryDialogService catDialog, Action onCatAdded)
        {
            _data = data;
            _notify = notify;
            _catDialog = catDialog;
            _onCatAdded = onCatAdded;

            Accounts = new ObservableCollection<Account>(_data.Accounts.Where(a => a.Type == AccountType.Assets));
        }

        [RelayCommand]
        private async Task AddAccount()
        {
            var name = "Новый счет";
            var currency = "RUB";

            if (string.IsNullOrWhiteSpace(name))
            {
                await _notify.ShowErrorAsync("Название счета не может быть пустым.");
                return;
            }

            name = name.Trim();

            if (Accounts.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                await _notify.ShowErrorAsync("Счет с таким названием уже существует.");
                return;
            }

            var acc = new Account
            {
                Name = name,
                CurrencyCode = currency,
                Type = AccountType.Assets,
                Balance = 0,
                InitialBalance = 0
            };

            _data.AddAccount(acc);
            Accounts.Insert(0, acc);

            await _notify.ShowInfoAsync($"Счет \"{name}\" добавлен.");
        }

        [RelayCommand]
        private async Task AddCategoryAsync()
        {
            var created = await _catDialog.ShowAddCategoryDialogAsync();
            if (created is null) return;

            if (_data.Categories.Any(c => c.Name.Equals(created.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await _notify.ShowErrorAsync("Категория с таким названием уже существует.");
                return;
            }

            _data.AddCategory(created);
            _onCatAdded();

            await _notify.ShowInfoAsync("Категория добавлена");
        }
    }
}