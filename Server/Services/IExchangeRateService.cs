using Shared.Exchange;

namespace Server.Services;

public interface IExchangeRateService
{
    Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct);
    Task<decimal?> ConvertAsync(string from, string to, decimal amount, CancellationToken ct);
}
