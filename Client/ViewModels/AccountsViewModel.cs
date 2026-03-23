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
        public ObservableCollection<AccountGroupViewModel> Groups { get; } = new();

        // доступная/недоступная кнопка
        [NotifyCanExecuteChangedFor(nameof(RenameAccountCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteAccountCommand))]
        [NotifyCanExecuteChangedFor(nameof(QuickExpenseCommand))]
        [NotifyCanExecuteChangedFor(nameof(QuickIncomeCommand))]
        [NotifyCanExecuteChangedFor(nameof(MoveToGroupCommand))]
        [ObservableProperty] 
        private Account? _selectedAccount;

        public string BaseCurrencyCode => _settings.BaseCurrency;

        public decimal TotalBalance // Общий баланс (в базовой валюте)
        {
            get
            {
                if (Groups == null) return 0;
                return Groups.Sum(g => g.TotalBalance);
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
            _data.DataChanged += OnDataChanged;
            _settings.SettingsChanged += OnSettingsChanged;
            _ = LoadDataAsync();
        }

        private void OnDataChanged()
        {
            _ = LoadDataAsync();
        }

        private void OnSettingsChanged()
        {
            OnPropertyChanged(nameof(BaseCurrencyCode));
            OnPropertyChanged(nameof(TotalBalance));
            foreach (var g in Groups) g.Refresh();
        }

        private int _loadCounter = 0;

        private async Task LoadDataAsync()
        {
            var currentLoad = ++_loadCounter;
            
            IsLoading = true;
            await Task.Delay(200); // Небольшая задержка для UI
            
            if (currentLoad != _loadCounter) return; // Была запущена новая загрузка

            Accounts.Clear();
            Groups.Clear();

            var loadedAccounts = _data.Accounts.Where(a => a.Type == AccountType.Assets).ToList();
            var loadedGroups = _data.AccountGroups.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList();

            // Создаем вьюмодели для всех групп
            foreach (var g in loadedGroups)
            {
                var gvm = new AccountGroupViewModel(g, _data, _settings);
                Groups.Add(gvm);
            }

            // Вьюмодель для "Без группы"
            var noGroupVm = new AccountGroupViewModel(null, _data, _settings);
            Groups.Add(noGroupVm);

            foreach (var acc in loadedAccounts)
            {
                Accounts.Add(acc);
                var gvm = Groups.FirstOrDefault(g => g.Id == acc.GroupId) ?? noGroupVm;
                gvm.Accounts.Add(acc);
            }

            // Убираем пустую "Без группы", если нет счетов
            if (noGroupVm.Accounts.Count == 0)
                Groups.Remove(noGroupVm);

            // Обновляем все группы, чтобы рассчитались вторичные балансы
            foreach (var g in Groups) g.Refresh();

            OnPropertyChanged(nameof(TotalBalance));
            IsLoading = false;
        }

        [RelayCommand]
        private async Task AddGroupAsync()
        {
            var name = await _input.PromptAsync("Новая группа", "Введите название группы счетов:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var group = new AccountGroup { Name = name.Trim() };
            await _data.AddAccountGroupAsync(group);
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task RenameGroupAsync(AccountGroupViewModel gvm)
        {
            if (gvm?.Group == null) return;
            var name = await _input.PromptAsync("Переименовать группу", "Введите новое название:", gvm.Group.Name);
            if (string.IsNullOrWhiteSpace(name)) return;

            gvm.Group.Name = name.Trim();
            await _data.UpdateAccountGroupAsync(gvm.Group);
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task DeleteGroupAsync(AccountGroupViewModel gvm)
        {
            if (gvm?.Group == null) return;
            var confirmed = await _notify.ShowConfirmAsync($"Удалить группу «{gvm.Name}»?\nСчета не удалятся, а просто останутся без группы.", "Удаление группы");
            if (!confirmed) return;

            await _data.DeleteAccountGroupAsync(gvm.Group.Id);
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task MoveToGroupAsync(Account? acc)
        {
            if (acc == null) return;
            
            var groups = _data.AccountGroups.ToList();
            var dlg = new Views.DialogViews.GroupSelectionDialog(groups);
            var result = await dlg.ShowDialog<AccountGroup?>(_getWindow());
            
            if (result == null && !dlg.IsCancelled) // Выбрано "Без группы"
            {
                _data.SetAccountGroup(acc.Id, null);
                await LoadDataAsync();
            }
            else if (result != null)
            {
                _data.SetAccountGroup(acc.Id, result.Id);
                await LoadDataAsync();
            }
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
            await LoadDataAsync();

            await _notify.ShowInfoAsync($"Счет \"{acc.Name}\" добавлен.");
        }

        [RelayCommand]
        private async Task RenameAccountAsync(Account? acc)
        {
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
            await LoadDataAsync();

            await _notify.ShowInfoAsync("Счёт переименован.");
        }

        [RelayCommand]
        private async Task DeleteAccountAsync(Account? acc)
        {
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
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await _notify.ShowErrorAsync(ex.Message);
            }
        }

        [RelayCommand]
        private void QuickExpense(Account? acc)
        {
            if (_onQuickTx is null || acc is null) return;
            _onQuickTx(acc, TxKindChoice.Expense);
        }

        [RelayCommand]
        private void QuickIncome(Account? acc)
        {
            if (_onQuickTx is null || acc is null) return;
            _onQuickTx(acc, TxKindChoice.Income);
        }

        [RelayCommand]
        private async Task SyncAsync()
        {
            if (_syncService is null || IsSyncing) return;

            IsSyncing = true;
            SyncStatusText = "Проверка...";
            SyncIconColor = "#29B6F6";

            // 1. Сравнить количество транзакций
            var localCount = _data.GetLocalTransactionCount();
            var serverCount = await Task.Run(() => _syncService.GetServerTransactionCountAsync());

            if (serverCount >= 0 && localCount != serverCount && localCount > 0)
            {
                // Данные различаются — показать диалог
                var localDate = _data.GetLocalLastChangeDate();
                var dialog = new SyncConflictDialog(localDate, localCount, serverCount);
                var choice = await dialog.ShowDialog<string?>(_getWindow());

                if (choice == "client")
                {
                    // Пользователь решил оставить свои данные
                    SyncStatusText = "Отменено пользователем";
                    SyncIconColor = "#FF8C00";
                    IsSyncing = false;
                    return;
                }

                if (choice == "push")
                {
                    var confirmed = await _notify.ShowConfirmAsync(
                        "Вы уверены, что хотите перезаписать данные на сервере вашими локальными? Это действие нельзя отменить.",
                        "Отправка на сервер"
                    );

                    if (!confirmed)
                    {
                        SyncStatusText = "Отменено пользователем";
                        SyncIconColor = "#FF8C00";
                        IsSyncing = false;
                        return;
                    }

                    SyncStatusText = "Отправка на сервер...";
                    var pushResult = await Task.Run(() => _syncService.PushAllDataToServerAsync());
                    
                    if (pushResult.Success)
                    {
                        SyncStatusText = $"Синхронизировано";
                        SyncIconColor = "#00E676";
                    }
                    else
                    {
                        var err = pushResult.ErrorMessage ?? "Неизвестная ошибка";
                        if (err.Length > 60) err = err[..60] + "…";
                        SyncStatusText = $"Ошибка: {err}";
                        SyncIconColor = "#FF5252";
                    }
                    IsSyncing = false;
                    return;
                }

                if (choice is null)
                {
                    // Отмена
                    SyncStatusText = "Синхронизировано";
                    SyncIconColor = "#00E676";
                    IsSyncing = false;
                    return;
                }

                // choice == "server" — продолжаем синхронизацию
            }

            // 2. Выполнить синхронизацию
            SyncStatusText = "Синхронизация...";

            var result = await Task.Run(() => _syncService.SyncAsync());

            if (result.Success)
            {
                await LoadDataAsync();

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