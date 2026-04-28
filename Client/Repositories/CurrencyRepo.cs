using System;
using System.Collections.Generic;
using System.Linq;
using Client.Data;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Repositories;

// Хранит курсы валют относительно рубля (RateToBase = сколько RUB за 1 единицу валюты)
public sealed class CurrencyRatesRepository
{
    private readonly SqliteConFactory _factory;
    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    private readonly List<CurrencyRate> _currencyRates = new();
    public IReadOnlyList<CurrencyRate> All => _currencyRates;

    public CurrencyRatesRepository(SqliteConFactory f) => _factory = f;

    public void Load()
    {
        _currencyRates.Clear();

        using var conn = _factory.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM CurrencyRates";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _currencyRates.Add(new CurrencyRate
                {
                    CurrencyCode = r.GetString(r.GetOrdinal("CurrencyCode")),
                    RateToBase = (decimal)r.GetDouble(r.GetOrdinal("RateToBase"))
                });
            }
        }
    }

    // Реализован через рубль как «общий знаменатель»: A→B = (A→RUB) / (B→RUB).
    public decimal Get(string fromCurrency, string toCurrency = "RUB")
    {
        if (fromCurrency == toCurrency) return 1m;

        var fromRate = fromCurrency == "RUB" ? 1m : _currencyRates.FirstOrDefault(r => r.CurrencyCode == fromCurrency)?.RateToBase ?? 1m;
        var toRate = toCurrency == "RUB" ? 1m : _currencyRates.FirstOrDefault(r => r.CurrencyCode == toCurrency)?.RateToBase ?? 1m;

        if (toRate == 0) return 0;
        return fromRate / toRate;
    }

    public void Set(string code, decimal rate)
    {
        if (code == "RUB") return;

        var existing = _currencyRates.FirstOrDefault(r => r.CurrencyCode == code);
        using var conn = _factory.Open();

        if (existing != null)
        {
            existing.RateToBase = rate;
            SqliteConFactory.Exec(conn, "UPDATE CurrencyRates SET RateToBase = @Rate WHERE CurrencyCode = @Code",
                ("@Rate", (double)rate), ("@Code", code));
        }
        else
        {
            var newRate = new CurrencyRate { CurrencyCode = code, RateToBase = rate };
            _currencyRates.Add(newRate);
            SqliteConFactory.Exec(conn, "INSERT INTO CurrencyRates (CurrencyCode, RateToBase) VALUES (@Code, @Rate)",
                ("@Code", code), ("@Rate", (double)rate));
        }

        RaiseChanged();
    }
}