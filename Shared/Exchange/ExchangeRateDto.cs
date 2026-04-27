using System;

namespace Shared.Exchange;

/// <summary>
/// Курс валюты к рублю. Rate — сколько RUB за 1 единицу валюты, Date — момент котировки.
/// </summary>
public sealed record ExchangeRateDto(
    string Currency,
    decimal Rate,
    DateTimeOffset Date
);
