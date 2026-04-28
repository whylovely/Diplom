using Shared.Exchange;

namespace Server.Services;

// Интерфейс сервиса курсов валют
public interface IExchangeRateService
{
    //Возвращает все известные курсы к рублю
    Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct);

    // Конвертирует amount из from" в to
    Task<decimal?> ConvertAsync(string from, string to, decimal amount, CancellationToken ct);
}