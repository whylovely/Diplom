using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Shared.Exchange;

namespace Server.Services;

public sealed class CbrExchangeRateService : IExchangeRateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "ExchangeRates";

    public CbrExchangeRateService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<List<ExchangeRateDto>> GetRatesAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out List<ExchangeRateDto>? cachedRates) && cachedRates is not null)
        {
            return cachedRates;
        }

        var rates = await FetchRatesFromCbrAsync(ct);
        
        try 
        {
            var cryptoUsdPrices = await FetchCryptoUsdPricesAsync(ct);
            var usdRate = rates.FirstOrDefault(r => r.Currency == "USD")?.Rate ?? 95m; // Get USD-RUB rate from CBR data
            
            if (cryptoUsdPrices.TryGetValue("USDT", out var usdtUsdPrice))
            {
                rates.Add(new ExchangeRateDto("USDT", usdtUsdPrice * usdRate, DateTimeOffset.UtcNow));
            }
            if (cryptoUsdPrices.TryGetValue("BTC", out var btcUsdPrice))
            {
                rates.Add(new ExchangeRateDto("BTC", btcUsdPrice * usdRate, DateTimeOffset.UtcNow));
            }
            if (cryptoUsdPrices.TryGetValue("ETH", out var ethUsdPrice))
            {
                rates.Add(new ExchangeRateDto("ETH", ethUsdPrice * usdRate, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Error fetching crypto rates: {ex.Message}");
             // Fallback to approximate values if API fails
             var usdRate = rates.FirstOrDefault(r => r.Currency == "USD")?.Rate ?? 95m;
             rates.Add(new ExchangeRateDto("USDT", usdRate, DateTimeOffset.UtcNow));
             rates.Add(new ExchangeRateDto("BTC", 96000 * usdRate, DateTimeOffset.UtcNow));
             rates.Add(new ExchangeRateDto("ETH", 2700 * usdRate, DateTimeOffset.UtcNow));
        }

        _cache.Set(CacheKey, rates, TimeSpan.FromHours(1));
        return rates;
    }

    public async Task<decimal?> ConvertAsync(string from, string to, decimal amount, CancellationToken ct)
    {
        var rates = await GetRatesAsync(ct);
        
        // RUB is the base currency (Rate = 1)
        if (!rates.Any(r => r.Currency == "RUB"))
        {
            rates.Add(new ExchangeRateDto("RUB", 1m, DateTimeOffset.UtcNow));
        }

        var fromRate = rates.FirstOrDefault(r => r.Currency.Equals(from, StringComparison.OrdinalIgnoreCase))?.Rate;
        var toRate = rates.FirstOrDefault(r => r.Currency.Equals(to, StringComparison.OrdinalIgnoreCase))?.Rate;

        if (fromRate is null || toRate is null || toRate == 0) return null;

        // Formula: Amount * FromRate (to RUB) / ToRate (to Target)
        return (amount * fromRate.Value) / toRate.Value;
    }

    // New helper to fetch raw USD prices
    private async Task<Dictionary<string, decimal>> FetchCryptoUsdPricesAsync(CancellationToken ct)
    {
        // Using CoinGecko Public API
        // Note: Free tier has rate limits (approx 10-30 req/min)
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FinanceTrackerApp/1.0");
        
        var url = "https://api.coingecko.com/api/v3/simple/price?ids=tether,bitcoin,ethereum&vs_currencies=usd";
        var response = await client.GetFromJsonAsync<Dictionary<string, Dictionary<string, decimal>>>(url, ct);
        
        var result = new Dictionary<string, decimal>();
        if (response != null)
        {
            if (response.TryGetValue("tether", out var usdt) && usdt.TryGetValue("usd", out var usdtPrice)) result["USDT"] = usdtPrice;
            if (response.TryGetValue("bitcoin", out var btc) && btc.TryGetValue("usd", out var btcPrice)) result["BTC"] = btcPrice;
            if (response.TryGetValue("ethereum", out var eth) && eth.TryGetValue("usd", out var ethPrice)) result["ETH"] = ethPrice;
        }
        return result;
    }

    private async Task<List<ExchangeRateDto>> FetchRatesFromCbrAsync(CancellationToken ct)
    {
        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetByteArrayAsync("http://www.cbr.ru/scripts/XML_daily.asp", ct);
            var xml = System.Text.Encoding.GetEncoding("windows-1251").GetString(response);

            var xdoc = XDocument.Parse(xml);
            var result = new List<ExchangeRateDto>();

            foreach (var element in xdoc.Descendants("Valute"))
            {
                var charCode = element.Element("CharCode")?.Value;
                var valueString = element.Element("Value")?.Value;
                var nominalString = element.Element("Nominal")?.Value;

                if (charCode != null && valueString != null && nominalString != null)
                {
                    if (decimal.TryParse(valueString.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate) &&
                        decimal.TryParse(nominalString, out var nominal))
                    {
                        result.Add(new ExchangeRateDto(charCode, rate / nominal, DateTimeOffset.UtcNow));
                    }
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching rates: {ex.Message}");
            return new List<ExchangeRateDto>();
        }
    }
}
