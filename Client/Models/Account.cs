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
        private decimal _balance;

        public AccountType Type { get; set; } = AccountType.Assets;

        // Многовалютность (выровнено с Shared.Accounts.MultiCurrencyType)
        public MultiCurrencyType AccountMultiType { get; set; } = MultiCurrencyType.Standard;

        /// <summary>Вычисляемое свойство для совместимости с XAML-биндингами.</summary>
        public bool IsMultiCurrency
        {
            get => AccountMultiType == MultiCurrencyType.MultiCurrency;
            set => AccountMultiType = value ? MultiCurrencyType.MultiCurrency : MultiCurrencyType.Standard;
        }

        public string? SecondaryCurrencyCode { get; set; }
        public decimal? ExchangeRate { get; set; }   // курс конвертации (из Shared)

        [ObservableProperty]
        private decimal _secondaryBalance;

        // Метки времени для синхронизации
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    }

    /// <summary>Выровнено с Shared.Accounts.AccountKind.</summary>
    public enum AccountType
    {
        Assets  = 0,   // активы
        Income  = 1,   // доход
        Expense = 2    // расход
    }

    /// <summary>Выровнено с Shared.Accounts.MultiCurrencyType.</summary>
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