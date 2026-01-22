using System;

namespace Client.Models
{
    public record Money(decimal Amount, string CurrencyCode)
    {
        public override string ToString() => $"{Amount:0.##} {CurrencyCode}";
    }
}