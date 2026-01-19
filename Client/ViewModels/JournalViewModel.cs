using Client.Services;
using System.Collections.ObjectModel;
using System.Transactions;

namespace Client.ViewModels
{
    public sealed partial class JournalViewModel
    {
        private readonly IDataService _data;

        public ObservableCollection<Transaction> Transactions { get; }

        public JournalViewModel(IDataService data)
        {
            _data = data;
            Transactions = new ObservableCollection<Transaction>(_data.Transactions);
        }

        public void Refresh()
        {
            Transactions.Clear();
            foreach (var tx in _data.Transactions)
            {
                Transactions.Add(tx);
            }
        }
    }
}