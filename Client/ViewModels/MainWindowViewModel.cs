using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels
{
    public sealed partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IDataService _data;

        [ObservableProperty]
        private ViewModelBase _current;

        public AccountsViewModel AccountsVm { get; }
        public JournalViewModel JournalVm { get; }
        public NewTransactionViewModel NewTxVm { get; }

        public MainWindowViewModel()
        {
            _data = new MockDS();

            AccountsVm = new AccountsViewModel(_data);
            JournalVm = new JournalViewModel(_data);
            NewTxVm = new NewTransactionViewModel(_data);

            _current = AccountsVm;
        }

        [RelayCommand] private void NavigateAccounts() => Current = AccountsVm;
        [RelayCommand] private void NavigateJournal() => Current = JournalVm;
        [RelayCommand] private void NavigateNewTransaction() => Current = NewTxVm;
    }
}
