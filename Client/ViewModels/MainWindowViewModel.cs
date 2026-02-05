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

        [ObservableProperty]
        private ViewModelBase _current;

        public AccountsViewModel AccountsVm { get; }
        public JournalViewModel JournalVm { get; }
        public NewTransactionViewModel NewTxVm { get; }
        public ReportViewModel ReportVm { get; }
        public CategoriesViewModel CategoriesVm { get; }

        public MainWindowViewModel()
        {
            _data = new MockDS();
            _notify = new NotificationService();

            var catDialog = new CategoryDialogService();
            var input = new InputDialogService();

            AccountsVm = new AccountsViewModel(
                _data, 
                _notify, 
                catDialog,
                input, 
                onQuickTx: openQuickTx);

            JournalVm = new JournalViewModel(_data);

            NewTxVm = new NewTransactionViewModel(_data, _notify, onPosted: () =>
            {
                JournalVm.Refresh();
                Current = JournalVm;
            });
            ReportVm = new ReportViewModel(_data);
            CategoriesVm = new CategoriesViewModel(_data, _notify, catDialog);

            _current = AccountsVm;
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
    }
}