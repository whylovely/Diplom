using System;

namespace Client.Models
{
    public sealed class Account
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public string Name { get; set; } = "";
        public string CurrencyCode { get; set; } = "RUB";

        public decimal InitialBalance { get; set; } 
        public decimal Balance { get; set; } // Пока хранится в памяти, позже изменить на серверное хранении

        public AccountType Type { get; set; } = AccountType.Assets;
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