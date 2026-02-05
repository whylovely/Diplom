using System.Threading.Tasks;

namespace Client.Services
{
    public interface INotificationService
    {
        Task ShowErrorAsync(string message, string title = "Ошибка");
        Task ShowInfoAsync(string message, string title = "Информация");
    }
}