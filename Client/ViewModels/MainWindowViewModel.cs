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

        [ObservableProperty] private ViewModelBase _current;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAccountsActive))]
        [NotifyPropertyChangedFor(nameof(IsCategoriesActive))]
        [NotifyPropertyChangedFor(nameof(IsJournalActive))]
        [NotifyPropertyChangedFor(nameof(IsReportActive))]
        [NotifyPropertyChangedFor(nameof(IsObligationsActive))]
        [NotifyPropertyChangedFor(nameof(IsNewTransactionActive))]
        [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
        private string _activePage = "Accounts";

        public bool IsAccountsActive => ActivePage == "Accounts";
        public bool IsCategoriesActive => ActivePage == "Categories";
        public bool IsJournalActive => ActivePage == "Journal";
        public bool IsReportActive => ActivePage == "Report";
        public bool IsObligationsActive => ActivePage == "Obligations";
        public bool IsNewTransactionActive => ActivePage == "NewTransaction";
        public bool IsSettingsActive => ActivePage == "Settings";

        public AccountsViewModel AccountsVm { get; }
        public JournalViewModel JournalVm { get; }
        public NewTransactionViewModel NewTxVm { get; }
        public ReportViewModel ReportVm { get; }
        public CategoriesViewModel CategoriesVm { get; }
        public ObligationsViewModel ObligationsVm { get; }
        public SettingsViewModel SettingsVm { get; }

        public MainWindowViewModel()
        {
            _data = new LocalDbService();
            _notify = new NotificationService();
            _settings = new SettingsService();
            _auth = new AuthService(_settings);

            var catDialog = new CategoryDialogService();
            var input = new InputDialogService();
            var apiService = new ApiService(_settings);
            var syncService = new SyncService(apiService, (LocalDbService)_data);

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
                onPosted: () =>
            {
                JournalVm.Refresh();
                NavigateJournal();
            });

            ReportVm = new ReportViewModel(_data, _notify, _settings);
            CategoriesVm = new CategoriesViewModel(_data, _notify, catDialog);
            ObligationsVm = new ObligationsViewModel(_data, _notify, _settings, openDebtTx);
            SettingsVm = new SettingsViewModel(_settings);
            SettingsVm.OnLogoutRequested += async () =>
            {
                await ShowLoginDialog();
                if (!_auth.IsLoggedIn)
                {
                    App.MainWindow?.Close();
                }
            };

            _current = AccountsVm;
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
                    _settings.CompleteFirstRun();
                    dialog.Close();
                };

                await dialog.ShowDialog(App.MainWindow!);
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

        [RelayCommand] private void NavigateAccounts()        { Current = AccountsVm;     ActivePage = "Accounts"; }
        [RelayCommand] private void NavigateJournal()         { Current = JournalVm;      ActivePage = "Journal"; }
        [RelayCommand] private void NavigateNewTransaction()  { Current = NewTxVm;        ActivePage = "NewTransaction"; }
        [RelayCommand] private void NavigateReport()          { Current = ReportVm;       ActivePage = "Report"; }
        [RelayCommand] private void NavigateCategories()      { Current = CategoriesVm;   ActivePage = "Categories"; }
        [RelayCommand] private void NavigateObligations()     { Current = ObligationsVm;  ActivePage = "Obligations"; }
        [RelayCommand] private void NavigateSettings()        { Current = SettingsVm;     ActivePage = "Settings"; }
    }
}