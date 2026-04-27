using System.Net.Http.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shared.Exchange;

namespace Server.Services;

/// <summary>
/// Боевая реализация сервиса курсов валют. Тянет фиатные курсы из XML-фида ЦБ РФ
/// и крипто-курсы из CoinGecko. Двухуровневое кеширование:
/// <list type="bullet">
///   <item>CacheKey — свежий снепшот, живёт 1 час (свежие данные)</item>
///   <item>LastKnownKey — последний удачный, живёт 7 дней (fallback при недоступности API)</item>
/// </list>
/// При сетевой ошибке возвращаем последний известный — это лучше, чем валить весь endpoint.
/// </summary>
public sealed class CbrExchangeRateService : IExchangeRateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CbrExchangeRateService> _logger;

    private const string CacheKey = "ExchangeRates";
    private const string LastKnownKey = "ExchangeRates_LastKnown";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan LastKnownDuration = TimeSpan.FromDays(7);

    public CbrExchangeRateService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<CbrExchangeRateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out List<ExchangeRateDto>? cached) && cached is not null)
        {
            return cached;
        }

        // Кэш устарел
        List<ExchangeRateDto> rates;
        try
        {
            rates = await FetchRatesFromCbrAsync(ct);
            _logger.LogInformation("Fetched {Count} rates from CBR", rates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CBR API unavailable, falling back to last known rates");
            return GetLastKnownOrEmpty();
        }

        try
        {
            var cryptoUsdPrices = await FetchCryptoUsdPricesAsync(ct);
            var usdRate = rates.FirstOrDefault(r => r.Currency == "USD")?.Rate ?? 95m;

            if (cryptoUsdPrices.TryGetValue("USDT", out var usdtPrice))
                rates.Add(new ExchangeRateDto("USDT", usdtPrice * usdRate, DateTimeOffset.UtcNow));
            if (cryptoUsdPrices.TryGetValue("BTC", out var btcPrice))
                rates.Add(new ExchangeRateDto("BTC", btcPrice * usdRate, DateTimeOffset.UtcNow));
            if (cryptoUsdPrices.TryGetValue("ETH", out var ethPrice))
                rates.Add(new ExchangeRateDto("ETH", ethPrice * usdRate, DateTimeOffset.UtcNow));

            _logger.LogInformation("Fetched crypto prices from CoinGecko");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoinGecko unavailable, using hardcoded crypto fallback");
            var usdRate = rates.FirstOrDefault(r => r.Currency == "USD")?.Rate ?? 95m;
            rates.Add(new ExchangeRateDto("USDT", usdRate, DateTimeOffset.UtcNow));
            rates.Add(new ExchangeRateDto("BTC", 96000 * usdRate, DateTimeOffset.UtcNow));
            rates.Add(new ExchangeRateDto("ETH", 2700 * usdRate, DateTimeOffset.UtcNow));
            rates.Add(new ExchangeRateDto("SOL", 140 * usdRate, DateTimeOffset.UtcNow));
            rates.Add(new ExchangeRateDto("TON", 3.5m * usdRate, DateTimeOffset.UtcNow));
        }

        // Сохраняем в кэш (1 ч + 7 дней)
        _cache.Set(CacheKey, rates, CacheDuration);
        _cache.Set(LastKnownKey, rates, LastKnownDuration);

        return rates;
    }

    public async Task<decimal?> ConvertAsync(string from, string to, decimal amount, CancellationToken ct)
    {
        var rates = await GetRatesAsync(ct);

        if (!rates.Any(r => r.Currency == "RUB"))
            rates.Add(new ExchangeRateDto("RUB", 1m, DateTimeOffset.UtcNow));

        var fromRate = rates.FirstOrDefault(r => r.Currency.Equals(from, StringComparison.OrdinalIgnoreCase))?.Rate;
        var toRate = rates.FirstOrDefault(r => r.Currency.Equals(to, StringComparison.OrdinalIgnoreCase))?.Rate;

        if (fromRate is null || toRate is null || toRate == 0) return null;

        return (amount * fromRate.Value) / toRate.Value;
    }

    private List<ExchangeRateDto> GetLastKnownOrEmpty()
    {
        if (_cache.TryGetValue(LastKnownKey, out List<ExchangeRateDto>? lastKnown) && lastKnown is not null)
        {
            _logger.LogInformation("Serving {Count} last-known rates from fallback cache", lastKnown.Count);
            return lastKnown;
        }

        _logger.LogWarning("No last-known rates available, returning empty list");
        return new List<ExchangeRateDto>();
    }

    private async Task<Dictionary<string, decimal>> FetchCryptoUsdPricesAsync(CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FinanceTrackerApp/1.0");

        var url = "https://api.coingecko.com/api/v3/simple/price?ids=tether,bitcoin,ethereum,solana,the-open-network&vs_currencies=usd";
        var response = await client.GetFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>(url, ct);

        var result = new Dictionary<string, decimal>();
        if (response != null)
        {
            if (response.TryGetValue("tether",   out var usdt) && usdt.TryGetValue("usd", out var usdtPrice)) result["USDT"] = usdtPrice;
            if (response.TryGetValue("bitcoin",  out var btc)  && btc.TryGetValue("usd",  out var btcPrice))  result["BTC"]  = btcPrice;
            if (response.TryGetValue("ethereum", out var eth)  && eth.TryGetValue("usd",  out var ethPrice))  result["ETH"]  = ethPrice;
            if (response.TryGetValue("solana",   out var sol)  && sol.TryGetValue("usd",  out var solPrice))  result["SOL"]  = solPrice;
            if (response.TryGetValue("the-open-network", out var ton) && ton.TryGetValue("usd", out var tonPrice)) result["TON"] = tonPrice;
        }
        return result;
    }

    private async Task<List<ExchangeRateDto>> FetchRatesFromCbrAsync(CancellationToken ct)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetByteArrayAsync("http://www.cbr.ru/scripts/XML_daily.asp", ct);
        var xml = System.Text.Encoding.GetEncoding("windows-1251").GetString(response);

        var xdoc = XDocument.Parse(xml);
        var result = new List<ExchangeRateDto>();

        foreach (var element in xdoc.Descendants("Valute"))
        {
            var charCode      = element.Element("CharCode")?.Value;
            var valueString   = element.Element("Value")?.Value;
            var nominalString = element.Element("Nominal")?.Value;

            if (charCode != null && valueString != null && nominalString != null)
            {
                if (decimal.TryParse(valueString.Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var rate) &&
                    decimal.TryParse(nominalString, out var nominal))
                {
                    result.Add(new ExchangeRateDto(charCode, rate / nominal, DateTimeOffset.UtcNow));
                }
            }
        }

        return result;
    }
}