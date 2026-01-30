using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

            AccountsVm = new AccountsViewModel(_data, _notify, catDialog, onCatAdded: () => NewTxVm.ReloadCategories());  
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

        [RelayCommand] private void NavigateAccounts() => Current = AccountsVm;
        [RelayCommand] private void NavigateJournal() => Current = JournalVm;
        [RelayCommand] private void NavigateNewTransaction() => Current = NewTxVm;
        [RelayCommand] private void NavigateReport() => Current = ReportVm;
        [RelayCommand] private void NavigateCategories() => Current = CategoriesVm;
    }
}