using Client.Models;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Client.ViewModels
{
    public sealed partial class AccountsViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private readonly ICategoryDialogService _catDialog;

        private readonly IInputDialogService _input;
        private readonly Action<Account, TxKindChoice> _onQuickTx;
        private readonly Func<Avalonia.Controls.Window> _getWindow;

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
            Action<Account, TxKindChoice> onQuickTx,
            Func<Avalonia.Controls.Window> getWindow
            )
        {
            _data = data;
            _notify = notify;
            _input = input;
            _catDialog = catDialog;
            _onQuickTx = onQuickTx;
            _getWindow = getWindow;

            Accounts = new ObservableCollection<Account>(_data.Accounts.Where(a => a.Type == AccountType.Assets));
        }

        [RelayCommand]
        private async Task AddAccount()
        {
            var dlg = new AddAccountDialog();
            var acc = await dlg.ShowDialogAsync(_getWindow());

            if (acc is null) return; // пользователь отменил

            if (Accounts.Any(a => a.Name.Equals(acc.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await _notify.ShowErrorAsync("Счет с таким названием уже существует.");
                return;
            }

            _data.AddAccount(acc);
            Accounts.Insert(0, acc);

            await _notify.ShowInfoAsync($"Счет \"{acc.Name}\" добавлен.");
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private async Task RenameAccountAsync() // Переименование счета
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

            await _notify.ShowInfoAsync("Счёт переименован.");
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private async Task DeleteAccountAsync() // Удаление счета
        {
            var acc = SelectedAccount;

            if (acc is null) return;

            if (_data.IsAccountUsed(acc.Id))
            {
                await _notify.ShowErrorAsync("Нельзя удалить счёт: он используется в операциях.");
                return;
            }

            var confirmed = await _notify.ShowConfirmAsync(
                $"Вы уверены, что хотите удалить счёт «{acc.Name}»?",
                "Удаление счёта");

            if (!confirmed) return;

            _data.RemoveAccount(acc.Id); 
            Accounts.Remove(acc);
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private void QuickExpense() // кнопка "Быстрый расход"
        {
            if (_onQuickTx is null) return;
            _onQuickTx(SelectedAccount!, TxKindChoice.Expense);
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private void QuickIncome()  // кнопка "Быстрый доход"
        {
            if (_onQuickTx is null) return;
            _onQuickTx(SelectedAccount!, TxKindChoice.Income);
        }
    }
}