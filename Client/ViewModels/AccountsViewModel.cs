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
    public sealed partial class AccountsViewModel : ViewModelBase   // упраление страницей "Счета"
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private readonly ICategoryDialogService _catDialog;

        private readonly IInputDialogService _input;
        private readonly Action<Account, TxKindChoice> _onQuickTx;  // Быстрые действия
        private readonly Func<Avalonia.Controls.Window> _getWindow;
        private readonly SettingsService _settings;
        private readonly SyncService? _syncService;

        public ObservableCollection<Account> Accounts { get; }

        // доступная/недоступная кнопка
        [NotifyCanExecuteChangedFor(nameof(RenameAccountCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteAccountCommand))]
        [NotifyCanExecuteChangedFor(nameof(QuickExpenseCommand))]
        [NotifyCanExecuteChangedFor(nameof(QuickIncomeCommand))]
        [ObservableProperty] 
        private Account? _selectedAccount;

        public decimal TotalBalance // Общий баланс
        {
            get
            {
                if (Accounts == null) return 0;
                return Accounts.Sum(a => a.Balance);
            }
        }

        private bool hasSelectedAccount() => SelectedAccount is not null;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _syncStatusText = "Синхронизировано";

        [ObservableProperty]
        private string _syncIconColor = "#00E676"; // Green

        [ObservableProperty]
        private string _syncIconData = "M12,18A6,6 0 0,1 6,12C6,11 6.25,10.03 6.7,9.2L5.24,7.74C4.46,8.97 4,10.43 4,12A8,8 0 0,0 12,20V23L16,19L12,15M12,4V1L8,5L12,9V6A6,6 0 0,1 18,12C18,13 17.75,13.97 17.3,14.8L18.76,16.26C19.54,15.03 20,13.57 20,12A8,8 0 0,0 12,4Z";

        [ObservableProperty]
        private bool _isSyncing = false;

        public AccountsViewModel(
            IDataService data, 
            INotificationService notify, 
            ICategoryDialogService catDialog,
            IInputDialogService input,
            SettingsService settings,
            Action<Account, TxKindChoice> onQuickTx,
            Func<Avalonia.Controls.Window> getWindow,
            SyncService? syncService = null
            )
        {
            _data = data;
            _notify = notify;
            _input = input;
            _catDialog = catDialog;
            _settings = settings;
            _onQuickTx = onQuickTx;
            _getWindow = getWindow;
            _syncService = syncService;

            Accounts = new ObservableCollection<Account>();
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            await Task.Delay(2000); // Имитация долгой загрузки
            
            var loadedAccounts = _data.Accounts.Where(a => a.Type == AccountType.Assets);
            foreach (var acc in loadedAccounts)
            {
                Accounts.Add(acc);
            }
            OnPropertyChanged(nameof(TotalBalance));

            IsLoading = false;
        }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            var dlg = new AddAccountDialog();
            var acc = await dlg.ShowDialogAsync(_getWindow(), _settings.BaseCurrency);

            if (acc is null) return;

            if (Accounts.Any(a => a.Name.Equals(acc.Name, StringComparison.OrdinalIgnoreCase)))
            {
                await _notify.ShowErrorAsync("Счет с таким названием уже существует.");
                return;
            }

            _data.AddAccount(acc);
            Accounts.Insert(0, acc);
            OnPropertyChanged(nameof(TotalBalance));

            await _notify.ShowInfoAsync($"Счет \"{acc.Name}\" добавлен.");
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
            OnPropertyChanged(nameof(TotalBalance));

            await _notify.ShowInfoAsync("Счёт переименован.");
        }

        [RelayCommand(CanExecute = nameof(hasSelectedAccount))]
        private async Task DeleteAccountAsync()
        {
            var acc = SelectedAccount;

            if (acc is null) return;

            // Запрет удаления счёта с ненулевым балансом
            if (acc.Balance != 0)
            {
                await _notify.ShowErrorAsync($"Нельзя удалить счёт: баланс = {acc.Balance:N2} {acc.CurrencyCode}. Сначала обнулите баланс.");
                return;
            }

            // Предупреждение если счёт использовался в операциях
            if (_data.IsAccountUsed(acc.Id))
            {
                var confirmed = await _notify.ShowConfirmAsync(
                    $"Счёт «{acc.Name}» используется в операциях. Баланс = 0.\n\nУдаление пометит счёт как удалённый, но операции сохранятся в истории.\n\nПродолжить?",
                    "Удаление счёта");
                if (!confirmed) return;
            }
            else
            {
                var confirmed = await _notify.ShowConfirmAsync(
                    $"Вы уверены, что хотите удалить счёт «{acc.Name}»?",
                    "Удаление счёта");
                if (!confirmed) return;
            }

            try
            {
                _data.RemoveAccount(acc.Id); 
                Accounts.Remove(acc);
                OnPropertyChanged(nameof(TotalBalance));
            }
            catch (Exception ex)
            {
                await _notify.ShowErrorAsync(ex.Message);
            }
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

        [RelayCommand]
        private async Task SyncAsync()
        {
            if (_syncService is null || IsSyncing) return;

            IsSyncing = true;
            SyncStatusText = "Синхронизация...";
            SyncIconColor = "#29B6F6";

            var result = await Task.Run(() => _syncService.SyncAsync());

            if (result.Success)
            {
                Accounts.Clear();
                foreach (var acc in _data.Accounts.Where(a => a.Type == AccountType.Assets))
                    Accounts.Add(acc);
                OnPropertyChanged(nameof(TotalBalance));

                SyncStatusText = $"Синхронизировано";
                SyncIconColor = "#00E676";
            }
            else
            {
                var err = result.ErrorMessage ?? "Неизвестная ошибка";
                if (err.Length > 60) err = err[..60] + "…";
                SyncStatusText = $"Ошибка: {err}";
                SyncIconColor = "#FF5252";
            }

            IsSyncing = false;
        }
    }
}