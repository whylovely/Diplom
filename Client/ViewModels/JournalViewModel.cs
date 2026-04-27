using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Client.ViewModels
{
    /// <summary>
    /// Страница «Журнал»: таблица всех транзакций с поиском, фильтром по типу и
    /// подсветкой возможных дублей (эвристика — одинаковая дата, сумма и счёт).
    /// Поддерживает сторнирование выбранной транзакции через <c>StornoTransactionCommand</c>.
    /// </summary>
    public sealed partial class JournalViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;
        private List<JournalRow> _allRows = new();

        public ObservableCollection<JournalRow> FilteredRows { get; } = new();
        [ObservableProperty] private string _searchText = "";   // Строка поиска

        [NotifyCanExecuteChangedFor(nameof(StornoTransactionCommand))]
        [ObservableProperty]
        private JournalRow? _selectedRow;
        private bool hasSelectedRow() => SelectedRow is not null;

        public JournalViewModel(IDataService data, INotificationService notify)
        {
            _data = data;
            _notify = notify;
            RebuildRows();
            _data.DataChanged += Refresh;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();    // ввели один символ - меняем

        public void Refresh() => RebuildRows();

        private void RebuildRows()
        {
            _allRows = _data.Transactions
                .SelectMany(tx => ConvertToRows(tx))
                .OrderByDescending(r => r.Date)
                .ToList();

            // Выявление дубликатов транзакций
            var duplicates = _allRows.GroupBy(r => new { r.Date.Date, r.Amount, r.TypeLabel })
                                     .Where(g => g.Count() > 1)
                                     .SelectMany(g => g);
            foreach (var dup in duplicates)
            {
                dup.IsDuplicate = true;
            }

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

        [RelayCommand(CanExecute = nameof(hasSelectedRow))]
        private async System.Threading.Tasks.Task StornoTransactionAsync()
        {
            var row = SelectedRow;
            if (row is null) return;
            var confirmed = await _notify.ShowConfirmAsync(
                "Вы уверены, что хотите сторнировать выбранную операцию? Будет создана корректирующая проводка (Сторно).",
                "Сторнирование операции");
            if (!confirmed) return;

            try
            {
                await _data.StornoTransactionAsync(row.TransactionId);
                await _notify.ShowInfoAsync("Операция успешно сторнирована.");
                Refresh();
            }
            catch (Exception ex)
            {
                await _notify.ShowErrorAsync(ex.Message);
            }
        }

        private static bool MatchesFilter(JournalRow row, string query) // Мега-удобный поиск
        {
            return row.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.AccountName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (row.CategoryName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || row.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (row.ToAccountName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private IEnumerable<JournalRow> ConvertToRows(Transaction tx)
        {
            var assetEntries = tx.Entries
                .Where(e => FindAccount(e.AccountId)?.Type == AccountType.Assets)
                .ToList();

            if (assetEntries.Count == 0)
            {
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