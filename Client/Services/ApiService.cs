using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.Accounts;
using Shared.Categories;
using Shared.Obligations;
using Shared.Sync;
using Shared.Transactions;

namespace Client.Services;

// Тонкая обёртка над HttpClient к серверному REST API.
// HttpClient создаётся заново при каждом вызове Build() — это гарантирует,
// что смена ServerUrl в настройках сразу подхватывается без перезапуска.
public sealed class ApiService
{
    private readonly SettingsService _settings;

    public ApiService(SettingsService settings)
    {
        _settings = settings;
    }

    // Создаёт HttpClient с актуальным BaseAddress и Bearer-токеном.
    // Таймаут 100 сек — запас для cold-старта на Render (бесплатный хостинг
    // «засыпает» и первый запрос может занять до 30 сек).
    private HttpClient Build()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(_settings.ServerUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(100)
        };

        var token = _settings.AuthToken;
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return http;
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            using var http = Build();
            var resp = await http.GetAsync("api/accounts");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<AccountDto>?> GetAccountsAsync()
    {
        using var http = Build();
        var resp = await http.GetAsync("api/accounts");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<AccountDto>>();
    }

    public async Task<List<CategoryDto>?> GetCategoriesAsync()
    {
        using var http = Build();
        var resp = await http.GetAsync("api/categories");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<CategoryDto>>();
    }

    public async Task<List<TransactionDto>?> GetTransactionsAsync()
    {
        using var http = Build();
        var resp = await http.GetAsync("api/transactions");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<TransactionDto>>();
    }

    public async Task<List<ObligationDto>?> GetObligationsAsync()
    {
        using var http = Build();
        var resp = await http.GetAsync("api/obligations");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ObligationDto>>();
    }

    public async Task PushAllDataAsync(SyncPushRequest req)
    {
        using var http = Build();
        var resp = await http.PostAsJsonAsync("api/sync/push", req);
        resp.EnsureSuccessStatusCode();
    }
}
