using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Client.ViewModels;
using Client.Views;
using System.Threading.Tasks;

namespace Client.Services
{
    public sealed class NotificationService : INotificationService
    {
        public async Task ShowErrorAsync(string message, string title = "Ошибка") => await ShowAsync(title, message);
        public async Task ShowInfoAsync(string message, string title = "Информация") => await ShowAsync(title, message);

        public async Task ShowAsync(string title, string message)
        {
            var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            var owner = lifetime.MainWindow;

            var dialog = new MessageDialog(title, message);
            dialog.DataContext = new MessageDialogViewModel(dialog, title, message);

            await dialog.ShowDialog(owner);
        }

    }
}