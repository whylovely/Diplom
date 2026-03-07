using System;
using System.Collections.ObjectModel;

namespace Client.Models;

public sealed class CurrencyRate    // Курсы валют
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal RateToBase { get; set; }
}

public static class CurrencyHelper
{
    public static readonly string[] AvailableCurrencies = 
    { 
        "RUB", "USD", "EUR", "GBP", "CNY", "TRY", "KZT", "BYN"
    };

    public static ObservableCollection<string> GetObservableCurrencies() 
        => new(AvailableCurrencies);
}