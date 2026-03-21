using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Models
{
    public sealed partial class Account : ObservableObject  // счет
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

    public sealed class AccountBalanceRow
    {
        public string AccountName { get; set; } = "";
        public string CurrencyCode { get; set; } = "";
        public decimal Balance { get; set; }
    }

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