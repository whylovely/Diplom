using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.Auth;

namespace Client.Services
{
    /// <summary>
    /// Обёртка над endpoint'ами <c>/api/auth/login</c> и <c>/api/auth/register</c>.
    /// Методы возвращают tuple <c>(Ok, Error)</c> — UI показывает Error в красном поле формы.
    /// При успехе сохраняет JWT в <see cref="SettingsService"/>, после чего <c>ApiService</c>
    /// автоматически подставит его во все последующие запросы.
    /// </summary>
    public class AuthService
    {
        private readonly SettingsService _settings;
        private readonly HttpClient _http;

        public AuthService(SettingsService settings)
        {
            _settings = settings;
            _http = new HttpClient
            {
                BaseAddress = new Uri(settings.ServerUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(100)
            };
        }

        public bool IsLoggedIn => !string.IsNullOrEmpty(_settings.AuthToken);

        public async Task<(bool Ok, string? Error)> LoginAsync(string email, string password)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));

                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
                    _settings.AuthToken = body!.AccessToken;
                    return (true, null);
                }

                var error = await resp.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(error) ? $"Ошибка {(int)resp.StatusCode}" : error.Trim('"'));
            }
            catch (HttpRequestException)
            {
                return (false, "Не удалось подключиться к серверу");
            }
            catch (TaskCanceledException)
            {
                return (false, "Превышено время ожидания");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Error)> RegisterAsync(string email, string password)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync("api/auth/register", new RegisterRequest(email, password));

                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
                    _settings.AuthToken = body!.AccessToken;
                    return (true, null);
                }

                var error = await resp.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(error) ? $"Ошибка {(int)resp.StatusCode}" : error.Trim('"'));
            }
            catch (HttpRequestException)
            {
                return (false, "Не удалось подключиться к серверу");
            }
            catch (TaskCanceledException)
            {
                return (false, "Превышено время ожидания");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
