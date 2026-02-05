using Client.Services;
using System.Collections.ObjectModel;
using Client.Models;

namespace Client.ViewModels
{
    public sealed partial class JournalViewModel : ViewModelBase
    {
        private readonly IDataService _data;

        public ObservableCollection<Transaction> Transactions { get; }

        public JournalViewModel(IDataService data)
        {
            _data = data;
            Transactions = new ObservableCollection<Transaction>(_data.Transactions);
            _data.DataChanged += Refresh;
        }

        public void Refresh()
        {
            Transactions.Clear();
            foreach (var tx in _data.Transactions)
                Transactions.Add(tx);
        }
    }
}