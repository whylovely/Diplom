using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shared.Auth;

namespace Client.Services
{
    public class AuthService
    {
        private readonly SettingsService _settings;

        public AuthService(SettingsService settings)
        {
            _settings = settings;
        }

        private HttpClient Build() => new HttpClient
        {
            BaseAddress = new Uri(_settings.ServerUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        public bool IsLoggedIn => !string.IsNullOrEmpty(_settings.AuthToken);

        public async Task<(bool Ok, string? Error)> LoginAsync(string email, string password)
        {
            try
            {
                using var http = Build();
                var resp = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));

                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
                    SaveTokens(body!);
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
                using var http = Build();
                var resp = await http.PostAsJsonAsync("api/auth/register", new RegisterRequest(email, password));

                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
                    SaveTokens(body!);
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

        public async Task<bool> TryRefreshAsync()
        {
            var refreshToken = _settings.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken)) return false;

            try
            {
                using var http = Build();
                var resp = await http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(refreshToken));

                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
                    SaveTokens(body!);
                    return true;
                }

                _settings.Logout();
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void SaveTokens(AuthResponse body)
        {
            _settings.AuthToken = body.AccessToken;
            _settings.RefreshToken = body.RefreshToken;
        }
    }
}