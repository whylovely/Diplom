using System;

namespace Client.Models
{
    public record class Money
    {
        public override string ToString()
        {
            return $"{Amount:0.##} {CurrencyCode}";
        }
    }
}