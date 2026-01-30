using Avalonia.Controls.ApplicationLifetimes;
using Client.Views;
using System.Threading.Tasks;

namespace Client.Services
{
    public sealed class NotificationService : INotificationService
    {
        public async Task ShowErrorAsync(string message, string title = "Ошибка") => await ShowAsync(title, message);
        public async Task ShowInfoAsync(string message, string title = "Информация") => await ShowAsync(title, message);

        private static async Task ShowAsync(string title, string message)
        {
            var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var owner = lifetime?.MainWindow;

            var dlg = new MessageDialog(title, message);

            if (owner != null)
                await dlg.ShowDialog(owner);
            else
                dlg.Show();
        }
    }
}