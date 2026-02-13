using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Client.ViewModels
{
    public sealed partial class JournalViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private List<JournalRow> _allRows = new();

        public ObservableCollection<JournalRow> FilteredRows { get; } = new();

        [ObservableProperty]
        private string _searchText = "";

        public JournalViewModel(IDataService data)
        {
            _data = data;
            RebuildRows();
            _data.DataChanged += Refresh;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        public void Refresh()
        {
            RebuildRows();
        }

        private void RebuildRows()
        {
            _allRows = _data.Transactions
                .SelectMany(tx => ConvertToRows(tx))
                .OrderByDescending(r => r.Date)
                .ToList();

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            FilteredRows.Clear();

            var query = (SearchText ?? "").Trim();

            foreach (var row in _allRows)
            {
                if (string.IsNullOrEmpty(query) || MatchesFilter(row, query))
                    FilteredRows.Add(row);
            }
        }

        private static bool MatchesFilter(JournalRow row, string query)
        {
            return row.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.AccountName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (row.CategoryName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || row.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (row.ToAccountName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private IEnumerable<JournalRow> ConvertToRows(Transaction tx)
        {
            // Находим проводку по активному (пользовательскому) счёту
            var assetEntries = tx.Entries
                .Where(e => FindAccount(e.AccountId)?.Type == AccountType.Assets)
                .ToList();

            if (assetEntries.Count == 0)
            {
                // Нет проводок по активным счетам — показываем как есть
                var first = tx.Entries.FirstOrDefault();
                if (first is null) yield break;

                yield return new JournalRow
                {
                    TransactionId = tx.Id,
                    Date = tx.Date,
                    Description = tx.Description,
                    TypeLabel = "Операция",
                    AccountName = FindAccount(first.AccountId)?.Name ?? "?",
                    Amount = first.Amount.Amount,
                    CurrencyCode = first.Amount.CurrencyCode
                };
                yield break;
            }

            // Два Assets-счёта → перевод
            if (assetEntries.Count >= 2)
            {
                var debitEntry = assetEntries.FirstOrDefault(e => e.Direction == EntryDirection.Debit);
                var creditEntry = assetEntries.FirstOrDefault(e => e.Direction == EntryDirection.Credit);

                var fromAcc = creditEntry is not null ? FindAccount(creditEntry.AccountId) : null;
                var toAcc = debitEntry is not null ? FindAccount(debitEntry.AccountId) : null;
                var entry = creditEntry ?? debitEntry ?? assetEntries[0];

                yield return new JournalRow
                {
                    TransactionId = tx.Id,
                    Date = tx.Date,
                    Description = tx.Description,
                    TypeLabel = "Перевод",
                    AccountName = fromAcc?.Name ?? "?",
                    ToAccountName = toAcc?.Name ?? "?",
                    Amount = entry.Amount.Amount,
                    CurrencyCode = entry.Amount.CurrencyCode,
                    IsTransfer = true
                };
                yield break;
            }

            // Один Assets-счёт: расход или доход
            foreach (var entry in assetEntries)
            {
                var acc = FindAccount(entry.AccountId);
                var isExpense = entry.Direction == EntryDirection.Credit;
                var category = entry.CategoryId.HasValue ? FindCategory(entry.CategoryId.Value) : null;

                yield return new JournalRow
                {
                    TransactionId = tx.Id,
                    Date = tx.Date,
                    Description = tx.Description,
                    TypeLabel = isExpense ? "Расход" : "Доход",
                    AccountName = acc?.Name ?? "?",
                    CategoryName = category?.Name,
                    Amount = entry.Amount.Amount,
                    CurrencyCode = entry.Amount.CurrencyCode,
                    IsExpense = isExpense,
                    IsIncome = !isExpense
                };
            }
        }

        private Account? FindAccount(Guid id) =>
            _data.Accounts.FirstOrDefault(a => a.Id == id);

        private Category? FindCategory(Guid id) =>
            _data.Categories.FirstOrDefault(c => c.Id == id);
    }
}