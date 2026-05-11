using System;
using System.Collections.Generic;
using System.Net;
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
// При 401 автоматически пытается обновить access token через refresh token и повторяет запрос.
// HttpClient создаётся заново при каждом вызове Build() — подхватывает актуальный ServerUrl.
public sealed class ApiService
{
    private readonly SettingsService _settings;
    private readonly AuthService _auth;

    // Флаг для защиты от рекурсивного рефреша
    private bool _isRefreshing;

    public ApiService(SettingsService settings, AuthService auth)
    {
        _settings = settings;
        _auth = auth;
    }

    // Создаёт HttpClient с актуальным BaseAddress и Bearer-токеном.
    // Таймаут 15 сек — достаточно для локального Docker-сервера.
    private HttpClient Build()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(_settings.ServerUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        var token = _settings.AuthToken;
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return http;
    }

    /// <summary>
    /// Выполняет GET-запрос с автоматическим обновлением токена при 401.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<HttpClient, Task<HttpResponseMessage>> request)
    {
        using var http = Build();
        var resp = await request(http);

        // Если 401 и у нас есть refresh token — пробуем обновить и повторить один раз
        if (resp.StatusCode == HttpStatusCode.Unauthorized && !_isRefreshing)
        {
            _isRefreshing = true;
            try
            {
                var refreshed = await _auth.TryRefreshAsync();
                if (refreshed)
                {
                    resp.Dispose();
                    using var http2 = Build(); // новый клиент с обновлённым access token
                    return await request(http2);
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        return resp;
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
        var resp = await SendWithRefreshAsync(h => h.GetAsync("api/accounts"));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<AccountDto>>();
    }

    public async Task<List<CategoryDto>?> GetCategoriesAsync()
    {
        var resp = await SendWithRefreshAsync(h => h.GetAsync("api/categories"));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<CategoryDto>>();
    }

    public async Task<List<TransactionDto>?> GetTransactionsAsync()
    {
        var resp = await SendWithRefreshAsync(h => h.GetAsync("api/transactions"));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<TransactionDto>>();
    }

    public async Task<List<ObligationDto>?> GetObligationsAsync()
    {
        var resp = await SendWithRefreshAsync(h => h.GetAsync("api/obligations"));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ObligationDto>>();
    }

    public async Task PushAllDataAsync(SyncPushRequest req)
    {
        var resp = await SendWithRefreshAsync(h => h.PostAsJsonAsync("api/sync/push", req));
        resp.EnsureSuccessStatusCode();
    }
}
