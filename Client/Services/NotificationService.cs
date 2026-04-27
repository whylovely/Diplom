using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Client.Models;
using Client.Views;
using System.Threading.Tasks;

namespace Client.Services
{
    /// <summary>
    /// Контракт для показа стандартных диалогов из ViewModel — без прямой ссылки на View.
    /// ViewModel вызывают эти методы вместо MessageBox, что позволяет тестировать их с моком.
    /// </summary>
    public interface INotificationService
    {
        Task ShowErrorAsync(string message, string title = "Ошибка");
        Task ShowWarningAsync(string message, string title = "Предупреждение");
        Task ShowInfoAsync(string message, string title = "Информация");
        Task<bool> ShowConfirmAsync(string message, string title = "Подтверждение");
    }

    /// <summary>
    /// Реальная реализация: показывает диалоги <c>MessageDialog</c> и <c>ConfirmDialog</c>
    /// в качестве модальных окон поверх <c>MainWindow</c>. В юнит-тестах подменяется на заглушку.
    /// </summary>
    public sealed class NotificationService : INotificationService
    {
        public Task ShowErrorAsync(string message, string title = "Ошибка")
            => ShowAsync(title, message, MessageLevel.Error);

        public Task ShowWarningAsync(string message, string title = "Предупреждение")
            => ShowAsync(title, message, MessageLevel.Warning);

        public Task ShowInfoAsync(string message, string title = "Информация")
            => ShowAsync(title, message, MessageLevel.Info);

        public async Task<bool> ShowConfirmAsync(string message, string title = "Подтверждение")
        {
            // Получаем ссылку на главное окно приложения для отображения модального диалога
            var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            var owner = lifetime.MainWindow;

            var dialog = new ConfirmDialog(title, message);
            var result = await dialog.ShowDialog<bool>(owner!);
            return result;
        }

        private async Task ShowAsync(string title, string message, MessageLevel level)
        {
            var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            var owner = lifetime.MainWindow;

            var dialog = new MessageDialog(title, message, level);
            await dialog.ShowDialog(owner!);
        }
    }
}