using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Client.Views;

/// <summary>
/// Диалог выбора при конфликте синхронизации.
/// Возвращает: "server" / "client" / null (отмена).
/// </summary>
public partial class SyncConflictDialog : Window
{
    public SyncConflictDialog() => InitializeComponent();

    public SyncConflictDialog(
        DateTimeOffset? clientDate,
        int clientTxCount,
        int serverTxCount)
    {
        InitializeComponent();

        ClientDateText.Text = clientDate.HasValue
            ? clientDate.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm")
            : "нет данных";
        ClientCountText.Text = $"операций: {clientTxCount}";

        ServerDateText.Text = "будет загружено";
        ServerCountText.Text = $"операций: {serverTxCount}";
    }

    private void OnTakeServer(object? sender, RoutedEventArgs e) => Close("server");
    private void OnPushServer(object? sender, RoutedEventArgs e) => Close("push");
    private void OnKeepClient(object? sender, RoutedEventArgs e) => Close("client");
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
