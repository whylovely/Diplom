using System;

namespace Client.Models
{
    public class UserSettings
    {
        public string BaseCurrency { get; set; } = "RUB";
        public bool IsFirstRun { get; set; } = true;
        public string? AuthToken { get; set; }
        public string ServerUrl { get; set; } = "http://localhost:5273";
    }
}
