using System;

namespace Client.Models
{
    /// <summary>
    /// Сумма + код валюты. Record — value-семантика и иммутабельность важны:
    /// мы передаём Money между слоями и не должны случайно его модифицировать.
    /// </summary>
    public record Money(decimal Amount, string CurrencyCode)
    {
        public override string ToString() => $"{Amount:0.##} {CurrencyCode}";
    }
}