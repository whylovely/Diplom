using Client.Models;
using Client.Services;
using Client.ViewModels.OperationWithReport;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public partial class BalanceReport  // класс-помощник для отчетов баланса
    {
        public static decimal RefreshBalanceRows(
            IDataService _data,
            DateTimeOffset BalanceDate,
            ObservableCollection<AccountBalanceRow> BalanceRows)
        {
            BalanceRows.Clear();

            var assetAccounts = _data.Accounts
                .Where(a => a.Type == AccountType.Assets)
                .ToList();

            // Транзакции до или в день даты
            var txUpToDate = _data.Transactions
                .Where(t => t.Date <= BalanceDate)
                .ToList();

            foreach (var acc in assetAccounts)
            {
                var d = txUpToDate
                    .SelectMany(t => t.Entries)
                    .Where(e => e.AccountId == acc.Id)
                    .Sum(e => e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount);

                var bal = acc.InitialBalance + d;

                BalanceRows.Add(new AccountBalanceRow
                {
                    AccountName = acc.Name,
                    CurrencyCode = acc.CurrencyCode,
                    Balance = bal
                });
            }

            return BalanceRows.Sum(r => r.Balance);
        }
    }
}
