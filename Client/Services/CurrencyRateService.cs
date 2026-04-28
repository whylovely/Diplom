using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.Exchange;

namespace Client.Services;

/// <summary>
/// Подтягивает актуальные курсы валют с сервера приложения (GET /api/exchange/rates).
/// Сервер агрегирует курсы ЦБ РФ (фиат) и CoinGecko (крипто).
/// </summary>
public sealed class CurrencyRateService
{
    private readonly SettingsService _settings;

    public CurrencyRateService(SettingsService settings)
    {
        _settings = settings;
    }

    public async Task UpdateRatesAsync(IDataService data)
    {
        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri(_settings.ServerUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(120)
            };

            var token = _settings.AuthToken;
            if (!string.IsNullOrEmpty(token))
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var rates = await http.GetFromJsonAsync<List<ExchangeRateDto>>("api/exchange/rates");
            if (rates is null || rates.Count == 0) return;

            foreach (var r in rates)
            {
                if (!string.IsNullOrEmpty(r.Currency) && r.Currency != "RUB" && r.Rate > 0)
                {
                    data.SetCurrencyRate(r.Currency, Math.Round(r.Rate, 4));
                }
            }

            System.Diagnostics.Debug.WriteLine($"[CurrencyRateService] Обновлено {rates.Count} курсов с сервера");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CurrencyRateService] Ошибка загрузки курсов с сервера: {ex.Message}");
        }
    }
}