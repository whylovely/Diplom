using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Client.Models;
using Client.ViewModels;
using Client.Views;

namespace Client
{
    public partial class App : Application
    {
        public static Avalonia.Controls.Window? MainWindow { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Глобальные обработчики необработанных исключений
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var win = new MainWindow
                {
                    DataContext = new MainWindowViewModel()
                };
                desktop.MainWindow = win;
                MainWindow = win;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            var msg = ex?.Message ?? "Неизвестная ошибка";
            ShowGlobalError($"Необработанная ошибка:\n{msg}");
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved(); // предотвращаем завершение процесса
            var msg = e.Exception.InnerException?.Message ?? e.Exception.Message;
            ShowGlobalError($"Ошибка фоновой задачи:\n{msg}");
        }

        private static void ShowGlobalError(string message)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (MainWindow is null) return;
                    var dialog = new MessageDialog("Ошибка", message, MessageLevel.Error);
                    await dialog.ShowDialog(MainWindow);
                }
                catch
                {
                    // Если не удалось показать диалог — молча игнорируем
                }
            });
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}