using System;

namespace Client.Models
{
    public sealed class Account
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public string Name { get; set; } = "";
        public string CurrencyCode { get; set; } = "RUB";

        public decimal Balance { get; set; } // Пока хранится в памяти, позже изменить на серверное хранении

        public AccountType Type { get; set; } = AccountType.Assets;
    }
}