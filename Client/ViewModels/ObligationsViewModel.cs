using Client.Models;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Client.Views;

namespace Client.ViewModels;

public partial class ObligationsViewModel : ViewModelBase
{
    private readonly IDataService _data;
    private readonly INotificationService _notify;
    private readonly SettingsService _settings;
    private readonly Action<Obligation> _onPayDebt;

    public ObservableCollection<Obligation> Items { get; } = new();

    [ObservableProperty] private Obligation? _selectedItem;

    [ObservableProperty] private bool _showPaid;

    [ObservableProperty] private decimal _totalDebt;
    [ObservableProperty] private decimal _totalCredit;

    public string BaseCurrencyCode => _settings.BaseCurrency;

    public ObligationsViewModel(
        IDataService data, 
        INotificationService notify,
        SettingsService settings, 
        Action<Obligation> onPayDebt)
    {
        _data = data;
        _notify = notify;
        _settings = settings;
        _onPayDebt = onPayDebt;
        _data.DataChanged += Refresh;
        _settings.SettingsChanged += Refresh;
        Refresh();
    }

    partial void OnShowPaidChanged(bool value) => Refresh();

    private void Refresh()
    {
        Items.Clear();
        var all = _data.Obligations;

        var filtered = all.Where(o => ShowPaid || !o.IsPaid).OrderBy(o => o.DueDate).ToList();
        foreach (var item in filtered) Items.Add(item);

        TotalDebt = all.Where(o => o.Type == ObligationType.Debt && !o.IsPaid)
        .Sum(o => o.Amount * _data.GetRate(o.Currency, _settings.BaseCurrency));
        TotalCredit = all.Where(o => o.Type == ObligationType.Credit && !o.IsPaid)
        .Sum(o => o.Amount * _data.GetRate(o.Currency, _settings.BaseCurrency));
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var dialog = new AddObligationDialog();
        var result = await dialog.ShowDialog<Obligation?>(GetWindow());
        if (result != null)
        {
            await _data.AddObligationAsync(result);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedItem is null) return;
        
        var dialog = new AddObligationDialog(SelectedItem);
        var result = await dialog.ShowDialog<Obligation?>(GetWindow());
        if (result != null)
        {
            await _data.UpdateObligationAsync(result);
        }
    }

    private static Window GetWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow!;
        }
        throw new InvalidOperationException("Desktop lifetime is null");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedItem is null) return;

        var confirmed = await _notify.ShowConfirmAsync($"Удалить обязательство с {SelectedItem.Counterparty}?");
        if (confirmed)
        {
            await _data.DeleteObligationAsync(SelectedItem.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task MarkPaidAsync()
    {
        if (SelectedItem is null) return;
        
        await _data.MarkObligationPaidAsync(SelectedItem.Id, !SelectedItem.IsPaid);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void PayDebt()  // открытие окна для погашения
    {
        if (SelectedItem == null) return;
        _onPayDebt(SelectedItem);
    }

    private bool HasSelection => SelectedItem is not null;

    partial void OnSelectedItemChanged(Obligation? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        MarkPaidCommand.NotifyCanExecuteChanged();
        PayDebtCommand.NotifyCanExecuteChanged();
    }
}