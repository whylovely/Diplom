using Client.Models;
using System;
using System.IO;
using System.Text.Json;

namespace Client.Services
{
    /// <summary>
    /// Хранит настройки приложения в JSON-файле <c>%AppData%/Diplom/user_settings.json</c>
    /// Настройки сохраняются автоматически после каждой записи в свойство
    /// </summary>
    public class SettingsService
    {
        private const string AppName = "Diplom";
        private const string FileName = "user_settings.json";
        private readonly string _filePath;

        public UserSettings Settings { get; private set; } = new();

        public event Action? SettingsChanged;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, AppName);
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, FileName);

            Load();
        }

        private void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    Settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
                catch
                {
                    Settings = new UserSettings();
                }
            }
            else
            {
                Settings = new UserSettings();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
                SettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public string BaseCurrency 
        {
            get => Settings.BaseCurrency;
            set
            {
                if (Settings.BaseCurrency != value)
                {
                    Settings.BaseCurrency = value;
                    Save();
                }
            }
        }

        public bool IsFirstRun => Settings.IsFirstRun;

        public void CompleteFirstRun()
        {
            Settings.IsFirstRun = false;
            Save();
        }

        public string? AuthToken
        {
            get => Settings.AuthToken;
            set
            {
                Settings.AuthToken = value;
                Save();
            }
        }

        public string ServerUrl
        {
            get => Settings.ServerUrl;
            set
            {
                if (Settings.ServerUrl != value)
                {
                    Settings.ServerUrl = value;
                    Save();
                }
            }
        }

        public DateTimeOffset? LastSyncedAt
        {
            get => Settings.LastSyncedAt;
            set
            {
                Settings.LastSyncedAt = value;
                Save();
            }
        }

        public void Logout()
        {
            Settings.AuthToken = null;
            Settings.LastSyncedAt = null;
            Save();
        }
    }
}