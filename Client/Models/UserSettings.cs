using System;
using System.Collections.Generic;

namespace Client.Models
{
    /// <summary>
    /// Настройки приложения. Сериализуются в JSON-файл <c>user_settings.json</c>
    /// через <see cref="Client.Services.SettingsService"/>.
    /// IsFirstRun сбрасывается в false после прохождения мастера выбора базовой валюты.
    /// LastSyncedAt используется SyncOrchestrator чтобы решить «нужен ли push».
    /// </summary>
    public class UserSettings
    {
        public string BaseCurrency { get; set; } = "RUB";
        public bool IsFirstRun { get; set; } = true;
        public string? AuthToken { get; set; }
        public string ServerUrl { get; set; } = "https://diplom-odbj.onrender.com/";
        public List<string> FavoriteCurrencies { get; set; } = new();
        public DateTimeOffset? LastSyncedAt { get; set; }
    }
}
