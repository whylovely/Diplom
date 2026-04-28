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

// Тонкая обёртка над HttpClient к серверному REST API
public sealed class ApiService
{
    private readonly HttpClient _http;
    private readonly SettingsService _settings;

    public ApiService(SettingsService settings)
    {
        _settings = settings;
        _http = new HttpClient
        {
            BaseAddress = new Uri(settings.ServerUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(100)
        };
    }

    private void SetAuth()
    {
        var token = _settings.AuthToken;
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            SetAuth();
            var resp = await _http.GetAsync("api/accounts");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<AccountDto>?> GetAccountsAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/accounts");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<AccountDto>>();
    }

    public async Task<List<CategoryDto>?> GetCategoriesAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/categories");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<CategoryDto>>();
    }

    public async Task<List<TransactionDto>?> GetTransactionsAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/transactions");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<TransactionDto>>();
    }

    public async Task<List<ObligationDto>?> GetObligationsAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/obligations");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ObligationDto>>();
    }

    public async Task PushAllDataAsync(SyncPushRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/sync/push", req);
        resp.EnsureSuccessStatusCode();
    }
}