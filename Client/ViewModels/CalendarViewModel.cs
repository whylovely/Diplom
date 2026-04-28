using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Client.ViewModels
{
    // Страница «Календарь операций»: 7×6 сетка дней месяца с агрегатами доходов/расходов.
    public sealed partial class CalendarViewModel : ViewModelBase
    {
        private readonly IDataService _data;
        private readonly INotificationService _notify;

        [ObservableProperty] private int _displayMonth;
        [ObservableProperty] private int _displayYear;
        [ObservableProperty] private string _monthYearLabel = "";

        [ObservableProperty] private CalendarDay? _selectedDay;
        [ObservableProperty] private decimal _monthIncome;
        [ObservableProperty] private decimal _monthExpense;
        [ObservableProperty] private decimal _monthNet;

        public ObservableCollection<CalendarDay> Days { get; } = new();
        public ObservableCollection<JournalRow> SelectedDayTransactions { get; } = new();

        public CalendarViewModel(IDataService data, INotificationService notify)
        {
            _data = data;
            _notify = notify;

            var today = DateTime.Today;
            _displayMonth = today.Month;
            _displayYear = today.Year;

            _data.DataChanged += () => Refresh();
            Refresh();
        }

        partial void OnSelectedDayChanged(CalendarDay? value)
        {
            SelectedDayTransactions.Clear();
            if (value is null) return;
            foreach (var row in value.Transactions)
                SelectedDayTransactions.Add(row);
        }

        [RelayCommand]
        public void PreviousMonth()
        {
            if (DisplayMonth == 1)
            {
                DisplayMonth = 12;
                DisplayYear--;
            }
            else
            {
                DisplayMonth--;
            }
            Refresh();
        }

        [RelayCommand]
        public void NextMonth()
        {
            if (DisplayMonth == 12)
            {
                DisplayMonth = 1;
                DisplayYear++;
            }
            else
            {
                DisplayMonth++;
            }
            Refresh();
        }

        [RelayCommand]
        public void GoToToday()
        {
            var today = DateTime.Today;
            DisplayMonth = today.Month;
            DisplayYear = today.Year;
            Refresh();
        }

        [RelayCommand]
        public void SelectDay(CalendarDay? day)
        {
            SelectedDay = day;
        }

        [RelayCommand]
        public void Refresh()
        {
            var culture = new CultureInfo("ru-RU");
            var monthName = culture.DateTimeFormat.GetMonthName(DisplayMonth);
            MonthYearLabel = char.ToUpper(monthName[0]) + monthName.Substring(1) + " " + DisplayYear;

            BuildGrid();
            UpdateMonthTotals();

            if (SelectedDay is not null)
            {
                var prev = SelectedDay.Date;
                SelectedDay = Days.FirstOrDefault(d => d.Date == prev);
            }
        }

        private void BuildGrid()
        {
            Days.Clear();

            var firstOfMonth = new DateTime(DisplayYear, DisplayMonth, 1);
            var daysInMonth = DateTime.DaysInMonth(DisplayYear, DisplayMonth);

            int startDayOfWeek = ((int)firstOfMonth.DayOfWeek + 6) % 7;

            var prevMonth = firstOfMonth.AddDays(-startDayOfWeek);
            for (int i = 0; i < startDayOfWeek; i++)
            {
                Days.Add(BuildDay(prevMonth.AddDays(i), isCurrentMonth: false));
            }

            // Текущий месяц
            for (int d = 1; d <= daysInMonth; d++)
            {
                Days.Add(BuildDay(new DateTime(DisplayYear, DisplayMonth, d), isCurrentMonth: true));
            }

            var nextDay = new DateTime(DisplayYear, DisplayMonth, daysInMonth).AddDays(1);
            while (Days.Count < 42)
            {
                Days.Add(BuildDay(nextDay, isCurrentMonth: false));
                nextDay = nextDay.AddDays(1);
            }
        }

        private CalendarDay BuildDay(DateTime date, bool isCurrentMonth)
        {
            var day = new CalendarDay
            {
                Date = date,
                IsCurrentMonth = isCurrentMonth,
                IsToday = date == DateTime.Today
            };

            var dayRows = new List<JournalRow>();

            foreach (var tx in _data.Transactions)
            {
                if (tx.Date.Date != date) continue;

                foreach (var entry in tx.Entries)
                {
                    var acc = _data.Accounts.FirstOrDefault(a => a.Id == entry.AccountId);
                    if (acc is null || acc.Type != AccountType.Assets) continue;

                    var isExpense = entry.Direction == EntryDirection.Credit;
                    var category = entry.CategoryId.HasValue
                        ? _data.Categories.FirstOrDefault(c => c.Id == entry.CategoryId.Value)
                        : null;

                    var assetEntries = tx.Entries
                        .Where(e => _data.Accounts.FirstOrDefault(a => a.Id == e.AccountId)?.Type == AccountType.Assets)
                        .ToList();

                    bool isTransfer = assetEntries.Count >= 2;

                    if (isTransfer)
                    {
                        if (entry.Direction == EntryDirection.Credit)
                        {
                            var toEntry = assetEntries.FirstOrDefault(e => e.Direction == EntryDirection.Debit);
                            var toAcc = toEntry is not null
                                ? _data.Accounts.FirstOrDefault(a => a.Id == toEntry.AccountId)
                                : null;

                            dayRows.Add(new JournalRow
                            {
                                TransactionId = tx.Id,
                                Date = tx.Date,
                                Description = tx.Description,
                                TypeLabel = "Перевод",
                                AccountName = acc.Name,
                                ToAccountName = toAcc?.Name ?? "?",
                                Amount = entry.Amount.Amount,
                                CurrencyCode = entry.Amount.CurrencyCode,
                                IsTransfer = true
                            });
                        }
                    }
                    else
                    {
                        if (isExpense)
                            day.TotalExpense += entry.Amount.Amount;
                        else
                            day.TotalIncome += entry.Amount.Amount;

                        dayRows.Add(new JournalRow
                        {
                            TransactionId = tx.Id,
                            Date = tx.Date,
                            Description = tx.Description,
                            TypeLabel = isExpense ? "Расход" : "Доход",
                            AccountName = acc.Name,
                            CategoryName = category?.Name,
                            Amount = entry.Amount.Amount,
                            CurrencyCode = entry.Amount.CurrencyCode,
                            IsExpense = isExpense,
                            IsIncome = !isExpense
                        });
                    }
                }
            }

            day.Transactions = dayRows;
            return day;
        }

        private void UpdateMonthTotals()
        {
            MonthIncome = Days.Where(d => d.IsCurrentMonth).Sum(d => d.TotalIncome);
            MonthExpense = Days.Where(d => d.IsCurrentMonth).Sum(d => d.TotalExpense);
            MonthNet = MonthIncome - MonthExpense;
        }

        private Account? FindAccount(Guid id) =>
            _data.Accounts.FirstOrDefault(a => a.Id == id);
    }
}