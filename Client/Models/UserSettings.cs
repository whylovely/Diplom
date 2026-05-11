using System;
using System.Collections.Generic;

namespace Client.Models
{
    // Настройки приложения
    public class UserSettings
    {
        public string BaseCurrency { get; set; } = "RUB";
        public bool IsFirstRun { get; set; } = true;
        public string? AuthToken { get; set; }
        public string? RefreshToken { get; set; }
        public string ServerUrl { get; set; } = "http://localhost:5273";
        public List<string> FavoriteCurrencies { get; set; } = new();
        public DateTimeOffset? LastSyncedAt { get; set; }
    }
}