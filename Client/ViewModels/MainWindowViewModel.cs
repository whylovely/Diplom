using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Client.ViewModels
{
    public sealed partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private readonly SettingsService _settings;

        [ObservableProperty]
        private ViewModelBase _current;

        public AccountsViewModel AccountsVm { get; }
        public JournalViewModel JournalVm { get; }
        public NewTransactionViewModel NewTxVm { get; }
        public ReportViewModel ReportVm { get; }
        public CategoriesViewModel CategoriesVm { get; }
        public ObligationsViewModel ObligationsVm { get; }
        public SettingsViewModel SettingsVm { get; }

        public MainWindowViewModel()
        {
            _data = new MockDS();
            _notify = new NotificationService();
            _settings = new SettingsService();

            var catDialog = new CategoryDialogService();
            var input = new InputDialogService();

            AccountsVm = new AccountsViewModel(
                _data, 
                _notify, 
                catDialog,
                input, 
                _settings,
                onQuickTx: openQuickTx,
                getWindow: () => App.MainWindow!);

            JournalVm = new JournalViewModel(_data);

            NewTxVm = new NewTransactionViewModel(
                _data, 
                _notify, 
                onPosted: () =>
            {
                JournalVm.Refresh();
                Current = JournalVm;
            });

            ReportVm = new ReportViewModel(_data);
            CategoriesVm = new CategoriesViewModel(_data, _notify, catDialog);
            ObligationsVm = new ObligationsViewModel(_data, _notify);
            SettingsVm = new SettingsViewModel(_settings);

            _current = AccountsVm;
        }

        public async Task OnWindowLoaded()
        {
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

        private void openQuickTx(Account account, TxKindChoice choice)
        {
            Current = NewTxVm;
            NewTxVm.PresetForQuickTx(account, choice);
        }

        [RelayCommand] private void NavigateAccounts() => Current = AccountsVm;
        [RelayCommand] private void NavigateJournal() => Current = JournalVm;
        [RelayCommand] private void NavigateNewTransaction() => Current = NewTxVm;
        [RelayCommand] private void NavigateReport() => Current = ReportVm;
        [RelayCommand] private void NavigateCategories() => Current = CategoriesVm;
        [RelayCommand] private void NavigateObligations() => Current = ObligationsVm;
        [RelayCommand] private void NavigateSettings() => Current = SettingsVm;
    }
}