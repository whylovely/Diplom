using Shared.Exchange;

namespace Server.Services;

/// <summary>
/// Контракт сервиса курсов валют. Боевая реализация — <see cref="CbrExchangeRateService"/>
/// (ЦБ РФ для фиата + CoinGecko для крипто), в тестах подменяется на FakeExchangeRateService.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>Возвращает все известные курсы к рублю.</summary>
    Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct);

    /// <summary>
    /// Конвертирует <paramref name="amount"/> из <paramref name="from"/> в <paramref name="to"/>.
    /// Возвращает <c>null</c>, если хоть одна валюта неизвестна.
    /// </summary>
    Task<decimal?> ConvertAsync(string from, string to, decimal amount, CancellationToken ct);
}
