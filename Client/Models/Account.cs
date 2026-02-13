using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Models
{
    public sealed partial class Account : ObservableObject
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        [ObservableProperty]
        private string _name = "";

        public string CurrencyCode { get; set; } = "RUB";

        public decimal InitialBalance { get; set; } 

        [ObservableProperty]
        private decimal _balance; // Пока хранится в памяти, позже изменить на серверное хранение

        public AccountType Type { get; set; } = AccountType.Assets;

        // Многовалютность
        public bool IsMultiCurrency { get; set; }
        public string? SecondaryCurrencyCode { get; set; }

        [ObservableProperty]
        private decimal _secondaryBalance;
    }

    public enum AccountType
    {
        Assets = 1, // активы
        Income = 2, // доход
        Expense = 3,   // расход
        Liability = 4   // долги/обязательства
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