using Client.Models;
using Client.Services;
using Client.ViewModels.OperationWithReport;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public partial class AccountReport
    {
        public static void RefreshAccountsRows(
            IDataService _data,
            DateTimeOffset DateFrom,
            DateTimeOffset DateTo,
            ObservableCollection<AccountTurnoverRow> AccountRows)
        {
            AccountRows.Clear();

            var assetAccounts = _data.Accounts
                .Where(a => a.Type == AccountType.Assets)
                .ToList();

            var allTx = _data.Transactions.ToList();

            foreach (var acc in assetAccounts)
            {
                var deltaBeforeFrom = allTx
                    .Where(t => t.Date < DateFrom)
                    .SelectMany(t => t.Entries)
                    .Where(e => e.AccountId == acc.Id)
                    .Sum(e => e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount);

                var opening = acc.InitialBalance + deltaBeforeFrom;

                var entriesInPeriod = allTx
                    .Where(t => t.Date >= DateFrom && t.Date <= DateTo)
                    .SelectMany(t => t.Entries)
                    .Where(e => e.AccountId == acc.Id)
                    .ToList();

                var debitTurnover = entriesInPeriod
                    .Where(e => e.Direction == EntryDirection.Debit)
                    .Sum(e => e.Amount.Amount);

                var creditTurnover = entriesInPeriod
                    .Where(e => e.Direction == EntryDirection.Credit)
                    .Sum(e => e.Amount.Amount);

                var closing = opening + (debitTurnover - creditTurnover);

                AccountRows.Add(new AccountTurnoverRow
                {
                    AccountName = acc.Name,
                    CurrencyCode = acc.CurrencyCode,
                    Opening = opening,
                    DebitTurnOver = debitTurnover,
                    CreditTurnOver = creditTurnover,
                    Closing = closing
                });
            }
        }
    }
}
