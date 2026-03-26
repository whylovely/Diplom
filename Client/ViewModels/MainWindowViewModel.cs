using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace Client.ViewModels
{
    public sealed partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private readonly SettingsService _settings;
        private readonly AuthService _auth;
        private readonly CurrencyRateService _rateService;

        [ObservableProperty] private ViewModelBase _current;
        [ObservableProperty] private bool _isMenuOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
        [NotifyPropertyChangedFor(nameof(IsAccountsActive))]
        [NotifyPropertyChangedFor(nameof(IsCategoriesActive))]
        [NotifyPropertyChangedFor(nameof(IsJournalActive))]
        [NotifyPropertyChangedFor(nameof(IsReportActive))]
        [NotifyPropertyChangedFor(nameof(IsObligationsActive))]
        [NotifyPropertyChangedFor(nameof(IsNewTransactionActive))]
        [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
        [NotifyPropertyChangedFor(nameof(IsCurrenciesActive))]
        private string _activePage = "Dashboard";

        public bool IsDashboardActive => ActivePage == "Dashboard";
        public bool IsAccountsActive => ActivePage == "Accounts";
        public bool IsCategoriesActive => ActivePage == "Categories";
        public bool IsJournalActive => ActivePage == "Journal";
        public bool IsReportActive => ActivePage == "Report";
        public bool IsObligationsActive => ActivePage == "Obligations";
        public bool IsNewTransactionActive => ActivePage == "NewTransaction";
        public bool IsSettingsActive => ActivePage == "Settings";
        public bool IsCurrenciesActive => ActivePage == "Currencies";

        public DashboardViewModel DashboardVm { get; }
        public AccountsViewModel AccountsVm { get; }
        public JournalViewModel JournalVm { get; }
        public NewTransactionViewModel NewTxVm { get; }
        public ReportViewModel ReportVm { get; }
        public CategoriesViewModel CategoriesVm { get; }
        public ObligationsViewModel ObligationsVm { get; }
        public SettingsViewModel SettingsVm { get; }
        public CurrenciesViewModel CurrenciesVm { get; }

        public MainWindowViewModel()
        {
            _data = new LocalDbService();
            _notify = new NotificationService();
            _settings = new SettingsService();
            _auth = new AuthService(_settings);
            _rateService = new CurrencyRateService(_settings);

            var catDialog = new CategoryDialogService();
            var input = new InputDialogService();
            var apiService = new ApiService(_settings);
            var syncService = new SyncService(apiService, (LocalDbService)_data);

            DashboardVm = new DashboardViewModel(_data, _settings);

            AccountsVm = new AccountsViewModel(
                _data, 
                _notify, 
                catDialog,
                input, 
                _settings,
                onQuickTx: openQuickTx,
                getWindow: () => App.MainWindow!,
                syncService: syncService);

            JournalVm = new JournalViewModel(_data, _notify);

            NewTxVm = new NewTransactionViewModel(
                _data,
                _notify,
                input,
                onPosted: () =>
            {
                JournalVm.Refresh();
                NavigateJournal();
            });
            ReportVm = new ReportViewModel(_data, _notify, _settings);
            CategoriesVm = new CategoriesViewModel(_data, _notify, catDialog);
            ObligationsVm = new ObligationsViewModel(_data, _notify, _settings, openDebtTx);
            SettingsVm = new SettingsViewModel(_data, _settings);
            CurrenciesVm = new CurrenciesViewModel(_data, _settings);

            SettingsVm.OnLogoutRequested += async () =>
            {
                _data.ClearDatabase();
                await ShowLoginDialog();
                if (!_auth.IsLoggedIn)
                {
                    App.MainWindow?.Close();
                }
            };

            SettingsVm.OnNavigateToCurrenciesRequested += () =>
            {
                NavigateCurrencies();
            };

            _current = DashboardVm;
        }

        public async Task OnWindowLoaded()
        {
            if (!_auth.IsLoggedIn)
            {
                await ShowLoginDialog();
                if (!_auth.IsLoggedIn)
                {
                    App.MainWindow?.Close();
                    return;
                }
            }

            if (_settings.IsFirstRun)
            {
                var vm = new Client.ViewModels.DialogWindow.FirstRunDialogViewModel();
                var dialog = new Client.Views.DialogViews.FirstRunDialog
                {
                    DataContext = vm
                };

                vm.OnConfirmed += (currency) =>
                {
                    _settings.BaseCurrency = currency;
                    _data.UpdateAccountsBaseCurrency(currency);
                    _settings.CompleteFirstRun();
                    dialog.Close();
                };

                await dialog.ShowDialog(App.MainWindow!);
            }

            // Подтягиваем актуальные курсы валют из интернета
            await _rateService.UpdateRatesAsync(_data);

            var now = DateTimeOffset.Now.Date;
            var overdue = 0;
            var approaching = 0;
            foreach (var ob in _data.Obligations)
            {
                if (ob.IsPaid || !ob.DueDate.HasValue) continue;
                var due = ob.DueDate.Value.Date;
                if (due < now) overdue++;
                else if ((due - now).TotalDays <= 3) approaching++;
            }

            if (overdue > 0 || approaching > 0)
            {
                var msg = "";
                if (overdue > 0) msg += $"Просроченных долгов: {overdue}.\n";
                if (approaching > 0) msg += $"Подходит срок: {approaching}.";
                await _notify.ShowInfoAsync(msg.Trim(), "Напоминание об обязательствах");
            }
        }

        public async Task ShowLoginDialog()
        {
            var vm = new Client.ViewModels.DialogWindow.LoginDialogViewModel(_auth);
            var dialog = new Client.Views.DialogViews.LoginDialog
            {
                DataContext = vm
            };

            vm.OnSuccess += () =>
            {
                dialog.Close();
            };

            await dialog.ShowDialog(App.MainWindow!);
        }

        private void openQuickTx(Account account, TxKindChoice choice)
        {
            NavigateNewTransaction();
            NewTxVm.PresetForQuickTx(account, choice);
        }

        private void openDebtTx(Obligation obligation)
        {
            NavigateNewTransaction();
            NewTxVm.PresetForDebtTx(obligation);
        }

        [RelayCommand] private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

        [RelayCommand] private void NavigateDashboard() { Current = DashboardVm; ActivePage = "Dashboard"; IsMenuOpen = false; DashboardVm.Refresh(); }
        [RelayCommand] private void NavigateAccounts() { Current = AccountsVm; ActivePage = "Accounts"; IsMenuOpen = false; }
        [RelayCommand] private void NavigateJournal() { Current = JournalVm; ActivePage = "Journal"; IsMenuOpen = false; }
        [RelayCommand] private void NavigateNewTransaction() { Current = NewTxVm; ActivePage = "NewTransaction"; IsMenuOpen = false; }
        [RelayCommand] private void NavigateReport() { Current = ReportVm; ActivePage = "Report"; IsMenuOpen = false; }
        [RelayCommand] private void NavigateCategories() { Current = CategoriesVm; ActivePage = "Categories"; IsMenuOpen = false; }
        [RelayCommand] private void NavigateObligations() { Current = ObligationsVm; ActivePage = "Obligations"; IsMenuOpen = false; }
        [RelayCommand] private void NavigateCurrencies() { Current = CurrenciesVm; ActivePage = "Currencies"; IsMenuOpen = false; }
        [RelayCommand] private void NavigateSettings() { Current = SettingsVm; ActivePage = "Settings"; IsMenuOpen = false; }
    }
}