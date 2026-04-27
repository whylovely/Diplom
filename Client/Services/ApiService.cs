using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.Accounts;
using Shared.Categories;
using Shared.Obligations;
using Shared.Transactions;

namespace Client.Services;

/// <summary>
/// Тонкая обёртка над <see cref="HttpClient"/> к серверному REST API.
/// Подставляет JWT-токен из <see cref="SettingsService"/> в заголовок <c>Authorization</c>
/// перед каждым запросом. Не реализует политику повторов и не кеширует —
/// этим занимаются вызывающие сервисы (<c>SyncService</c>, <c>AuthService</c>).
///
/// Таймаут 100 секунд завышен для бесплатного хостинга на Render —
/// первый запрос «будит» инстанс из cold-старта, иногда занимает до 30 сек.
/// </summary>
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

    // Перед каждым запросом — обновляем токен. SettingsService может его поменять
    // (например, после перелогина), поэтому нельзя выставлять один раз в конструкторе.
    private void SetAuth()
    {
        var token = _settings.AuthToken;
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Простой ping сервера: сходит по защищённому endpoint и проверит, что вернулось 2xx.
    /// Используется для определения «онлайн ли сервер» перед попыткой синхронизации.
    /// </summary>
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

    public async Task<AccountDto?> CreateAccountAsync(CreateAccountRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/accounts", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<AccountDto>();
    }

    public async Task<List<CategoryDto>?> GetCategoriesAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/categories");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<CategoryDto>>();
    }

    public async Task<CategoryDto?> CreateCategoryAsync(CreateCategoryRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/categories", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CategoryDto>();
    }

    public async Task<List<TransactionDto>?> GetTransactionsAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/transactions");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<TransactionDto>>();
    }

    public async Task<TransactionDto?> CreateTransactionAsync(CreateTransactionRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/transactions", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TransactionDto>();
    }

    public async Task<List<ObligationDto>?> GetObligationsAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/obligations");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ObligationDto>>();
    }

    public async Task<ObligationDto?> CreateObligationAsync(CreateObligationRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/obligations", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ObligationDto>();
    }

    public async Task PushAllDataAsync(Shared.Sync.SyncPushRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/sync/push", req);
        resp.EnsureSuccessStatusCode();
    }
}