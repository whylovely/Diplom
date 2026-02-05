using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
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

        private readonly IInputDialogService _input;
        private readonly Action<Account, TxKindChoice> _onQuickTx;

        public ObservableCollection<Account> Accounts { get; }

        [NotifyCanExecuteChangedFor(nameof(RenameAccountCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteAccountCommand))]
        [NotifyCanExecuteChangedFor(nameof(QuickExpenseCommand))]
        [NotifyCanExecuteChangedFor(nameof(QuickIncomeCommand))]
        [ObservableProperty] 
        private Account? _selectedAccount;

        private bool hasSelectedAccount() => SelectedAccount is not null;

        public AccountsViewModel(
            IDataService data, 
            INotificationService notify, 
            ICategoryDialogService catDialog,
            IInputDialogService input,
            Action onCatAdded,
            Action<Account, TxKindChoice> onQuickTx
            )
        {
            _data = data;
            _notify = notify;
            _input = input;
            _catDialog = catDialog;
            _onCatAdded = onCatAdded;
            _onQuickTx = onQuickTx;

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

        [RelayCommand]  // Убрать в другое vm
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

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private async Task RenameAccountAsync()
        {
            var acc = SelectedAccount;

            if (acc is null) return;

            var newName = await _input.PromptAsync(
                title: "Переименовать счет",
                message: "Введите новое название",
                initialText: acc.Name);
            if (newName is null) return;

            newName = newName.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                await _notify.ShowErrorAsync("Название не может быть пустым.");
                return;
            }

            if (Accounts.Any(a => a.Id != acc.Id && a.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                await _notify.ShowErrorAsync("Счёт с таким названием уже существует.");
                return;
            }

            _data.RenameAccount(acc.Id, newName);
            acc.Name = newName;

            var idx = Accounts.ToList().FindIndex(a => a.Id == acc.Id); // Подумать почему не обновляется на экране (обновляется только если уйти в другой экран и вернуться)
            if (idx >= 0)
            {
                await _notify.ShowInfoAsync($"idx={idx}, count={Accounts.Count}");
                Accounts.RemoveAt(idx);
                Accounts.Insert(idx, acc);
            }

            await _notify.ShowInfoAsync("Счёт переименован.");
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private async Task DeleteAccountAsync()
        {
            var acc = SelectedAccount;

            if (acc is null) return;

            if (_data.IsAccountUsed(acc.Id))
            {
                await _notify.ShowErrorAsync("Нельзя удалить счёт: он используется в операциях.");
                return;
            }

            _data.RemoveAccount(acc.Id); 
            Accounts.Remove(acc);

            await _notify.ShowInfoAsync("Счёт удалён.");
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private void QuickExpense()
        {
            if (_onQuickTx is null) return;
            _onQuickTx(SelectedAccount!, TxKindChoice.Expense);
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private void QuickIncome()
        {
            if (_onQuickTx is null) return;
            _onQuickTx(SelectedAccount!, TxKindChoice.Income);
        }
    }
}