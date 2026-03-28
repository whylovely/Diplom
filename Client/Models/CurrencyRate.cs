using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
        "RUB", "USD", "EUR", "GBP", "CNY", "TRY", "KZT", "BYN",
        "BTC", "ETH", "SOL", "TON"
    };

    private static readonly Dictionary<string, string> CurrencyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "RUB", "Российский рубль" },
        { "USD", "Доллар США" },
        { "EUR", "Евро" },
        { "GBP", "Британский фунт" },
        { "CNY", "Китайский юань" },
        { "TRY", "Турецкая лира" },
        { "KZT", "Казахстанский тенге" },
        { "BYN", "Белорусский рубль" },
        { "BTC", "Bitcoin" },
        { "ETH", "Ethereum" },
        { "SOL", "Solana" },
        { "TON", "Toncoin" }
    };

    public static string GetCurrencyName(string code)
    {
        if (CurrencyNames.TryGetValue(code, out var name))
            return name;
        return code; // Fallback to code if name not found
    }

    /// <summary>
    /// Возвращает валюты, отфильтрованные по списку избранных.
    /// Если избранных нет — возвращает все доступные (fallback).
    /// </summary>
    public static string[] GetFilteredCurrencies(List<string>? favorites)
    {
        if (favorites is null || favorites.Count == 0)
            return AvailableCurrencies;

        // Сохраняем порядок из AvailableCurrencies, добавляя в конец те,
        // которые есть в избранном, но не в AvailableCurrencies
        var ordered = AvailableCurrencies
            .Where(c => favorites.Contains(c))
            .ToList();

        foreach (var f in favorites)
        {
            if (!ordered.Contains(f))
                ordered.Add(f);
        }

        return ordered.ToArray();
    }

    public static ObservableCollection<string> GetObservableCurrencies() 
        => new(AvailableCurrencies);
}