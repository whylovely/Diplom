using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Models
{
    /// <summary>
    /// Счёт пользователя. Делится на три типа (см. <see cref="AccountType"/>):
    /// <list type="bullet">
    ///   <item>Assets — реальные активы (наличные, карты, депозиты)</item>
    ///   <item>Income — технические счета доходов (по одному на категорию)</item>
    ///   <item>Expense — технические счета расходов (по одному на категорию)</item>
    /// </list>
    /// Технические счета нужны как противоположная сторона проводки в двойной записи.
    /// Пользователь их в UI не видит — они скрыты в <c>AccountsViewModel</c> через фильтр.
    /// </summary>
    public sealed partial class Account : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        [ObservableProperty] private string _name = "";

        [ObservableProperty] private Guid? _groupId;

        public string CurrencyCode { get; set; } = "RUB";   // Основная валюта
        public string? SecondaryCurrencyCode { get; set; }

        public decimal InitialBalance { get; set; } 
        [ObservableProperty] private decimal _balance;

        public AccountType Type { get; set; } = AccountType.Assets;
        public MultiCurrencyType AccountMultiType { get; set; } = MultiCurrencyType.Standard;
        public bool IsMultiCurrency
        {
            get => AccountMultiType == MultiCurrencyType.MultiCurrency;
            set => AccountMultiType = value ? MultiCurrencyType.MultiCurrency : MultiCurrencyType.Standard;
        }

        public decimal? ExchangeRate { get; set; }   // курс валют
        [ObservableProperty] private decimal _secondaryBalance;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
        public bool IsDeleted { get; set; }
    }

    public enum AccountType
    {
        Assets  = 0,   // активы
        Income  = 1,   // доход
        Expense = 2    // расход
    }

    public enum MultiCurrencyType
    {
        Standard      = 0,
        MultiCurrency = 1
    }

    /// <summary>Сумма в одной валюте — для виджета «Балансы по валютам» на дашборде.</summary>
    public sealed class CurrencyBalance
    {
        public string CurrencyCode { get; set; } = "";
        public decimal Balance { get; set; }
    }

    /// <summary>
    /// Элемент списка «Последние операции» на главном экране.
    /// Уже отформатирован для отображения: знак суммы, иконка направления, цвет.
    /// </summary>
    public sealed class RecentTransactionItem
    {
        public string Date         { get; set; } = "";
        public decimal Amount      { get; set; }
        public string Currency     { get; set; } = "";
        public bool IsIncome       { get; set; }
        public bool IsTransfer     { get; set; }
        public string CategoryName { get; set; } = "";
        public string AccountName  { get; set; } = "";
        public string Description  { get; set; } = "";

        public string FormattedAmount => IsTransfer
            ? $"{Amount:N2} {Currency}"
            : (IsIncome ? $"+{Amount:N2}" : $"−{Amount:N2}") + $" {Currency}";

        public string AmountColor    => IsTransfer ? "#29B6F6" : (IsIncome ? "#00E676" : "#FF5252");
        public string DirectionIcon  => IsTransfer ? "↔" : (IsIncome ? "↗" : "↙");
    }

    /// <summary>Строка отчёта «Баланс на дату» — снимок остатка по одному счёту.</summary>
    public sealed class AccountBalanceRow
    {
        public string AccountName { get; set; } = "";
        public string CurrencyCode { get; set; } = "";
        public decimal Balance { get; set; }
    }

    /// <summary>
    /// Строка отчёта «Обороты по счетам»: остатки на начало/конец периода
    /// и суммарные дебетовые/кредитовые обороты внутри периода.
    /// </summary>
    public sealed class AccountTurnoverRow
    {
        public string AccountName { get; set; } = "";
        public string CurrencyCode { get; set; } = "";

        public decimal DebitTurnOver { get; set; }
        public decimal CreditTurnOver { get; set; }
        public decimal NetChange => DebitTurnOver - CreditTurnOver;

        public decimal Opening { get; set; }   // Остаток на начало
        public decimal Closing { get; set; }    // Остаток на конец
    }
}